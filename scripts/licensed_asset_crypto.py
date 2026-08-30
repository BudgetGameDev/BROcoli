#!/usr/bin/env python3
"""Encrypt restricted third-party assets for the BROcoli Unity import pipeline."""

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path, PurePosixPath
from typing import Dict

from licensed_asset_archive import create_directory_archive, extract_directory_archive

KEY_NAME = "BROCOLI_LICENSED_ASSET_KEY"
ITERATIONS = 200_000
PROJECT_ROOT = Path(__file__).resolve().parent.parent
# Each game package owns its payloads under <owner>/Encrypted/Licensed and
# restores them under <owner>/**/Generated/Licensed, so unloading a game takes
# its restricted third-party assets with it. Roots are derived from the
# encrypted path rather than fixed, which is what lets a second game exist.
ENCRYPTED_SUFFIX = PurePosixPath("Encrypted/Licensed")
GENERATED_SEGMENTS = ("Generated", "Licensed")
OWNER_ROOTS = ("Assets", "LocalPackages")
PACKAGE_FORMAT_VERSION = 2


def load_key() -> str:
    key = os.environ.get(KEY_NAME, "").strip()
    env_path = PROJECT_ROOT / ".env"
    if not key and env_path.exists():
        for raw_line in env_path.read_text(encoding="utf-8").splitlines():
            line = raw_line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            name, value = line.split("=", 1)
            if name.strip() == KEY_NAME:
                key = value.strip().strip("'\"")
                break
    if len(key) < 32:
        raise RuntimeError(f"{KEY_NAME} must contain at least 32 characters")
    return key


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def run_openssl(mode: str, source: Path, destination: Path, key: str) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    environment[KEY_NAME] = key
    command = [
        "openssl",
        "enc",
        "-aes-256-cbc",
        "-pbkdf2",
        "-iter",
        str(ITERATIONS),
        "-md",
        "sha256",
        "-pass",
        f"env:{KEY_NAME}",
        "-in",
        str(source),
        "-out",
        str(destination),
    ]
    if mode == "decrypt":
        command.insert(2, "-d")
    subprocess.run(command, check=True, env=environment)


def normalized_project_path(value: str, label: str) -> PurePosixPath:
    normalized = value.replace("\\", "/").strip("/")
    path = PurePosixPath(normalized)
    if (
        not normalized
        or value.startswith(("/", "\\"))
        or any(part in ("", ".", "..") or ":" in part for part in path.parts)
    ):
        raise RuntimeError(f"{label} must be a safe project-relative path")
    return path


def require_path_under(path: PurePosixPath, root: PurePosixPath, label: str) -> None:
    if path == root or root not in path.parents:
        raise RuntimeError(f"{label} must stay under {root.as_posix()}/")


def owner_root(encrypted: PurePosixPath) -> PurePosixPath:
    """Directory that owns a payload: the tree holding its Encrypted/Licensed folder."""
    parent = encrypted.parent
    if parent.parts[-2:] != ENCRYPTED_SUFFIX.parts:
        raise RuntimeError(
            f"Encrypted output path must sit in a {ENCRYPTED_SUFFIX.as_posix()}/ folder"
        )
    owner = PurePosixPath(*parent.parts[:-2])
    if not owner.parts or owner.parts[0] not in OWNER_ROOTS:
        raise RuntimeError(f"Encrypted output path must live under one of {OWNER_ROOTS}")
    return owner


def resolve_encrypted_path(value: str) -> Path:
    relative = normalized_project_path(value, "Encrypted output path")
    owner_root(relative)
    if not relative.name.endswith(".enc"):
        raise RuntimeError("Encrypted output path must end in .enc")
    return PROJECT_ROOT.joinpath(*relative.parts)


def validate_generated_path(value: str, directory: bool, encrypted: str) -> str:
    relative = normalized_project_path(value, "Generated path")
    owner = owner_root(normalized_project_path(encrypted, "Encrypted output path"))
    require_path_under(relative, owner, "Generated path")
    parts = relative.parts
    if not any(parts[i : i + 2] == GENERATED_SEGMENTS for i in range(len(parts) - 1)):
        raise RuntimeError(
            f"Generated path must sit under a {'/'.join(GENERATED_SEGMENTS)}/ folder"
        )
    return relative.as_posix()


def package_root_guid(title: str, source_url: str, generated_path: str) -> str:
    identity = "\0".join((title, source_url, generated_path)).encode("utf-8")
    return hashlib.sha256(identity).hexdigest()[:32]


def package_metadata(args: argparse.Namespace, archive: Path) -> Dict[str, object]:
    required = {
        "--title": args.title,
        "--asset-version": args.asset_version,
        "--license-type": args.license_type,
        "--acquired-date": args.acquired_date,
    }
    missing = [flag for flag, value in required.items() if not value]
    if missing:
        raise RuntimeError("Directory packages require metadata options: " + ", ".join(missing))

    generated_path = validate_generated_path(
        args.generated_path, directory=True, encrypted=args.output
    )
    file_count, uncompressed_size = create_directory_archive(Path(args.input).resolve(), archive)
    return {
        "formatVersion": PACKAGE_FORMAT_VERSION,
        "payloadType": "directory",
        "archiveFormat": "zip",
        "generatedPath": generated_path,
        "rootGuid": package_root_guid(args.title, args.source_url, generated_path),
        "sha256": sha256(archive),
        "fileCount": file_count,
        "uncompressedSize": uncompressed_size,
        "title": args.title,
        "sourceUrl": args.source_url,
        "author": args.author,
        "license": args.license,
        "assetVersion": args.asset_version,
        "licenseType": args.license_type,
        "acquiredDate": args.acquired_date,
        "price": args.price or "",
    }


def file_metadata(args: argparse.Namespace, source: Path) -> Dict[str, object]:
    metadata = {
        "formatVersion": 1,
        "generatedPath": validate_generated_path(
            args.generated_path, directory=False, encrypted=args.output
        ),
        "sha256": sha256(source),
        "sourceUrl": args.source_url,
        "author": args.author,
        "license": args.license,
    }
    optional_fields = {
        "title": args.title,
        "assetVersion": args.asset_version,
        "licenseType": args.license_type,
        "acquiredDate": args.acquired_date,
        "price": args.price,
    }
    metadata.update({name: value for name, value in optional_fields.items() if value})
    return metadata


def encrypt_payload(payload: Path, output: Path, metadata: Dict[str, object]) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    sidecar = Path(str(output) + ".json")
    encrypted_handle, encrypted_name = tempfile.mkstemp(prefix=output.name + ".", dir=output.parent)
    metadata_handle, metadata_name = tempfile.mkstemp(prefix=sidecar.name + ".", dir=sidecar.parent)
    os.close(encrypted_handle)
    os.close(metadata_handle)
    encrypted_temp = Path(encrypted_name)
    metadata_temp = Path(metadata_name)
    try:
        run_openssl("encrypt", payload, encrypted_temp, load_key())
        metadata_temp.write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
        os.replace(encrypted_temp, output)
        os.replace(metadata_temp, sidecar)
    finally:
        encrypted_temp.unlink(missing_ok=True)
        metadata_temp.unlink(missing_ok=True)


def encrypt(args: argparse.Namespace) -> None:
    source = Path(args.input).resolve()
    output = resolve_encrypted_path(args.output)
    if not source.exists():
        raise FileNotFoundError(source)

    if source.is_dir():
        with tempfile.TemporaryDirectory(prefix="brocoli-licensed-package-") as temporary:
            archive = Path(temporary) / "package.zip"
            metadata = package_metadata(args, archive)
            encrypt_payload(archive, output, metadata)
    elif source.is_file():
        encrypt_payload(source, output, file_metadata(args, source))
    else:
        raise RuntimeError(f"Input must be a regular file or directory: {source}")
    print(f"Encrypted {source.name} -> {output.relative_to(PROJECT_ROOT)}")


def decrypt(args: argparse.Namespace) -> None:
    source = resolve_encrypted_path(args.input)
    sidecar = Path(str(source) + ".json")
    metadata = json.loads(sidecar.read_text(encoding="utf-8"))
    output = Path(args.output).resolve()
    if output.exists():
        raise RuntimeError(f"Decrypt output already exists: {output}")

    with tempfile.TemporaryDirectory(prefix="brocoli-licensed-decrypt-") as temporary:
        payload = Path(temporary) / "payload"
        run_openssl("decrypt", source, payload, load_key())
        if sha256(payload) != metadata["sha256"]:
            raise RuntimeError("Decrypted asset hash does not match metadata")
        if metadata.get("formatVersion") == PACKAGE_FORMAT_VERSION:
            if metadata.get("payloadType") != "directory" or metadata.get("archiveFormat") != "zip":
                raise RuntimeError("Unsupported licensed package payload")
            extract_directory_archive(payload, output, metadata)
        elif metadata.get("formatVersion") == 1:
            output.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(payload, output)
        else:
            raise RuntimeError("Unsupported licensed asset metadata format")
    print(f"Decrypted {source.name} -> {output}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    encrypt_parser = commands.add_parser("encrypt")
    encrypt_parser.add_argument("--input", required=True)
    encrypt_parser.add_argument("--output", required=True)
    encrypt_parser.add_argument("--generated-path", required=True)
    encrypt_parser.add_argument("--source-url", required=True)
    encrypt_parser.add_argument("--author", required=True)
    encrypt_parser.add_argument("--license", required=True)
    encrypt_parser.add_argument("--title")
    encrypt_parser.add_argument("--asset-version")
    encrypt_parser.add_argument("--license-type")
    encrypt_parser.add_argument("--acquired-date")
    encrypt_parser.add_argument("--price")
    decrypt_parser = commands.add_parser("decrypt")
    decrypt_parser.add_argument("--input", required=True)
    decrypt_parser.add_argument("--output", required=True)
    return parser.parse_args()


def main() -> int:
    try:
        args = parse_args()
        encrypt(args) if args.command == "encrypt" else decrypt(args)
        return 0
    except (
        OSError,
        RuntimeError,
        subprocess.CalledProcessError,
        json.JSONDecodeError,
        zipfile.BadZipFile,
    ) as error:
        print(f"licensed asset error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
