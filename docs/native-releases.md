# Native desktop releases

Native desktop players are an explicit, local release operation. They are not
part of `ci.sh`, `cd.sh`, the pre-push hook, or a GitHub Actions trigger. The
automatic build and deployment path remains WebGL-only.

## Prerequisites

Run the tooling on macOS because Unity can only produce the macOS player on a
Mac. Install the project editor version and its Windows and Linux Mono build
support modules:

```bash
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)"
unity install-modules --editor-version "$UNITY_VERSION" \
    --module windows-mono --module linux-mono --yes --accept-eula
```

The scripts also require `git`, `ditto`, `zip`, `tar`, and `shasum`. Publishing
requires an authenticated GitHub CLI (`gh auth login`). Close the Unity Editor
before starting a multi-target build so one batch Editor can switch platforms
safely.

## Build all native players

```bash
./scripts/native-builds.sh
```

Use `--development` only for local diagnostics. The build script produces and
packages:

- `BROcoli-windows-x86_64.zip`
- `BROcoli-macos-universal.zip`
- `BROcoli-linux-x86_64.tar.gz`
- `SHA256SUMS` and `build-info.txt`

Artifacts are written under `build/native/artifacts/`. The macOS build is a
universal Intel/Apple-silicon app. Windows and macOS retain the native HDR
configuration; Linux uses Vulkan with OpenGL Core as a fallback.

Individual player builds are also available in Unity under
`Tools > Build > Native`.

## Publish a GitHub Release

Commit the release contents, create and push a tag, then run:

```bash
git tag v1.2.3
git push origin v1.2.3
./scripts/native-release.sh v1.2.3
```

The publisher requires the tag to point at the current clean checkout. It
builds all three players, verifies that the packages came from that exact
commit, pass their recorded SHA-256 checksums, and are not development builds,
then creates the GitHub Release with generated notes. Useful options include
`--draft`, `--prerelease`, `--notes-file <path>`, and `--skip-build`.
