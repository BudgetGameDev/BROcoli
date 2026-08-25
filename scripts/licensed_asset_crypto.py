#!/usr/bin/env python3
"""Encrypt restricted third-party assets for the BROcoli Unity import pipeline."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

KEY_NAME = "BROCOLI_LICENSED_ASSET_KEY"
ITERATIONS = 200_000
PROJECT_ROOT = Path(__file__).resolve().parent.parent


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
        "openssl", "enc", "-aes-256-cbc", "-pbkdf2", "-iter", str(ITERATIONS),
        "-md", "sha256", "-pass", f"env:{KEY_NAME}", "-in", str(source),
        "-out", str(destination),
    ]
    if mode == "decrypt":
        command.insert(2, "-d")
    subprocess.run(command, check=True, env=environment)


def encrypt(args: argparse.Namespace) -> None:
    source = Path(args.input).resolve()
    output = (PROJECT_ROOT / args.output).resolve()
    if not source.is_file():
        raise FileNotFoundError(source)
    run_openssl("encrypt", source, output, load_key())
    metadata = {
        "formatVersion": 1,
        "generatedPath": args.generated_path,
        "sha256": sha256(source),
        "sourceUrl": args.source_url,
        "author": args.author,
        "license": args.license,
    }
    Path(str(output) + ".json").write_text(
        json.dumps(metadata, indent=2) + "\n", encoding="utf-8"
    )
    print(f"Encrypted {source.name} -> {output.relative_to(PROJECT_ROOT)}")


def decrypt(args: argparse.Namespace) -> None:
    source = (PROJECT_ROOT / args.input).resolve()
    output = Path(args.output).resolve()
    metadata = json.loads(Path(str(source) + ".json").read_text(encoding="utf-8"))
    run_openssl("decrypt", source, output, load_key())
    if sha256(output) != metadata["sha256"]:
        output.unlink(missing_ok=True)
        raise RuntimeError("Decrypted asset hash does not match metadata")
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
    decrypt_parser = commands.add_parser("decrypt")
    decrypt_parser.add_argument("--input", required=True)
    decrypt_parser.add_argument("--output", required=True)
    return parser.parse_args()


def main() -> int:
    try:
        args = parse_args()
        encrypt(args) if args.command == "encrypt" else decrypt(args)
        return 0
    except (OSError, RuntimeError, subprocess.CalledProcessError, json.JSONDecodeError) as error:
        print(f"licensed asset error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
