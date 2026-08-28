#!/usr/bin/env python3
"""Encrypt restricted third-party assets for the BROcoli Unity import pipeline."""

import argparse
import hashlib
import json
import os
import shutil
import stat
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path, PurePosixPath
from typing import Dict, Tuple

KEY_NAME = "BROCOLI_LICENSED_ASSET_KEY"
ITERATIONS = 200_000
PROJECT_ROOT = Path(__file__).resolve().parent.parent
ENCRYPTED_ROOT = PurePosixPath("Assets/Encrypted/Licensed")
LEGACY_GENERATED_ROOT = PurePosixPath("Assets/Resources/Generated/Licensed")
PACKAGE_GENERATED_ROOT = PurePosixPath("Assets/Generated/Licensed")
PACKAGE_FORMAT_VERSION = 2
MAXIMUM_PACKAGE_FILES = 200_000
MAXIMUM_PACKAGE_BYTES = 20 * 1024 * 1024 * 1024


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


def resolve_encrypted_path(value: str) -> Path:
    relative = normalized_project_path(value, "Encrypted output path")
    require_path_under(relative, ENCRYPTED_ROOT, "Encrypted output path")
    if not relative.name.endswith(".enc"):
        raise RuntimeError("Encrypted output path must end in .enc")
    return PROJECT_ROOT.joinpath(*relative.parts)


def validate_generated_path(value: str, directory: bool) -> str:
    relative = normalized_project_path(value, "Generated path")
    root = PACKAGE_GENERATED_ROOT if directory else LEGACY_GENERATED_ROOT
    require_path_under(relative, root, "Generated path")
    return relative.as_posix()


def zip_info(relative_path: str, source_mode: int, directory: bool) -> zipfile.ZipInfo:
    name = relative_path.rstrip("/") + ("/" if directory else "")
    info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
    info.create_system = 3
    file_type = stat.S_IFDIR if directory else stat.S_IFREG
    permissions = stat.S_IMODE(source_mode) or (0o755 if directory else 0o644)
    info.external_attr = (file_type | permissions) << 16
    if directory:
        info.external_attr |= 0x10
        info.compress_type = zipfile.ZIP_STORED
    else:
        info.compress_type = zipfile.ZIP_DEFLATED
    return info


def create_directory_archive(source: Path, destination: Path) -> Tuple[int, int]:
    file_count = 0
    uncompressed_size = 0
    with zipfile.ZipFile(destination, "w", allowZip64=True) as archive:
        for root, directory_names, file_names in os.walk(source, followlinks=False):
            directory_names.sort()
            file_names.sort()
            root_path = Path(root)

            for name in directory_names:
                directory = root_path / name
                if directory.is_symlink():
                    raise RuntimeError(f"Package directories cannot contain symlinks: {directory}")
                relative = directory.relative_to(source).as_posix()
                archive.writestr(zip_info(relative, directory.stat().st_mode, True), b"")

            for name in file_names:
                path = root_path / name
                if path.is_symlink():
                    raise RuntimeError(f"Package directories cannot contain symlinks: {path}")
                if not path.is_file():
                    raise RuntimeError(f"Unsupported package entry: {path}")
                relative = path.relative_to(source).as_posix()
                if relative == ".brocoli-package.sha256":
                    raise RuntimeError(
                        "Package input contains the decryptor's reserved marker file"
                    )
                info = zip_info(relative, path.stat().st_mode, False)
                with path.open("rb") as input_file, archive.open(info, "w") as output_file:
                    shutil.copyfileobj(input_file, output_file, length=1024 * 1024)
                file_count += 1
                uncompressed_size += path.stat().st_size
    return file_count, uncompressed_size


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

    generated_path = validate_generated_path(args.generated_path, directory=True)
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
        "generatedPath": validate_generated_path(args.generated_path, directory=False),
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


def safe_archive_name(value: str) -> PurePosixPath:
    if "\\" in value or value.startswith("/") or "\0" in value:
        raise RuntimeError(f"Unsafe package archive entry: {value!r}")
    normalized = value.rstrip("/")
    path = PurePosixPath(normalized)
    if not normalized or any(part in ("", ".", "..") or ":" in part for part in path.parts):
        raise RuntimeError(f"Unsafe package archive entry: {value!r}")
    return path


def extract_directory_archive(
    archive_path: Path, output: Path, metadata: Dict[str, object]
) -> None:
    if output.exists():
        raise RuntimeError(f"Decrypt output already exists: {output}")
    staging = output.with_name(output.name + ".brocoli-staging")
    if staging.exists():
        raise RuntimeError(f"Decrypt staging path already exists: {staging}")
    staging.mkdir(parents=True)
    try:
        file_count = 0
        uncompressed_size = 0
        names = set()
        with zipfile.ZipFile(archive_path, "r") as archive:
            for entry in archive.infolist():
                relative = safe_archive_name(entry.filename)
                normalized_name = relative.as_posix()
                if normalized_name in names:
                    raise RuntimeError(f"Duplicate package archive entry: {normalized_name}")
                names.add(normalized_name)
                unix_type = (entry.external_attr >> 16) & 0o170000
                if unix_type == stat.S_IFLNK:
                    raise RuntimeError(f"Package archive contains a symlink: {normalized_name}")
                if unix_type not in (0, stat.S_IFDIR, stat.S_IFREG):
                    raise RuntimeError(
                        f"Package archive contains an unsupported entry: {normalized_name}"
                    )
                if entry.is_dir() and unix_type == stat.S_IFREG:
                    raise RuntimeError(
                        f"Package archive has an invalid directory: {normalized_name}"
                    )
                if not entry.is_dir() and unix_type == stat.S_IFDIR:
                    raise RuntimeError(f"Package archive has an invalid file: {normalized_name}")
                if entry.is_dir():
                    continue
                file_count += 1
                uncompressed_size += entry.file_size
                if file_count > MAXIMUM_PACKAGE_FILES or uncompressed_size > MAXIMUM_PACKAGE_BYTES:
                    raise RuntimeError("Package archive exceeds extraction safety limits")

            if file_count != metadata.get("fileCount"):
                raise RuntimeError("Package archive file count does not match metadata")
            if uncompressed_size != metadata.get("uncompressedSize"):
                raise RuntimeError("Package archive size does not match metadata")

            for entry in archive.infolist():
                relative = safe_archive_name(entry.filename)
                destination = staging.joinpath(*relative.parts)
                if entry.is_dir():
                    destination.mkdir(parents=True, exist_ok=True)
                    continue
                destination.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(entry, "r") as source, destination.open("xb") as target:
                    shutil.copyfileobj(source, target, length=1024 * 1024)
        os.replace(staging, output)
    finally:
        if staging.exists():
            shutil.rmtree(staging)


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
