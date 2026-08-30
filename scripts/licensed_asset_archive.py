#!/usr/bin/env python3
"""Build and extract the zip payloads used by the BROcoli licensed asset pipeline."""

import json
import os
import shutil
import stat
import zipfile
from pathlib import Path, PurePosixPath

MAXIMUM_PACKAGE_FILES = 200_000
MAXIMUM_PACKAGE_BYTES = 20 * 1024 * 1024 * 1024


def preserved_root_guid(sidecar: Path, generated_path: str, fallback: str) -> str:
    if not sidecar.exists():
        return fallback
    existing = json.loads(sidecar.read_text(encoding="utf-8"))
    root_guid = existing.get("rootGuid")
    if (
        existing.get("formatVersion") == 2
        and existing.get("generatedPath") == generated_path
        and isinstance(root_guid, str)
        and len(root_guid) == 32
    ):
        return root_guid
    return fallback


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


def create_directory_archive(source: Path, destination: Path) -> tuple[int, int]:
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


def safe_archive_name(value: str) -> PurePosixPath:
    if "\\" in value or value.startswith("/") or "\0" in value:
        raise RuntimeError(f"Unsafe package archive entry: {value!r}")
    normalized = value.rstrip("/")
    path = PurePosixPath(normalized)
    if not normalized or any(part in ("", ".", "..") or ":" in part for part in path.parts):
        raise RuntimeError(f"Unsafe package archive entry: {value!r}")
    return path


def extract_directory_archive(
    archive_path: Path, output: Path, metadata: dict[str, object]
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
