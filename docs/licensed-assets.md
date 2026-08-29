# Licensed assets

BROcoli uses one repository-wide key for third-party assets whose licenses permit use
inside the game but prohibit exposing the stand-alone source files. The real key is
stored in the team password manager, in CI secrets, and in each developer's ignored
`.env` file as `BROCOLI_LICENSED_ASSET_KEY`.

The pipeline supports two encrypted payload formats:

- Format 1 restores one file under `Assets/Resources/Generated/Licensed/`. It remains
  supported for existing assets such as `theHand.fbx`.
- Format 2 restores a complete directory under `Assets/Generated/Licensed/`. It stores
  traditional Asset Store packages and other multi-file assets as an encrypted ZIP,
  preserving their directory structure, empty directories, and Unity `.meta` GUIDs.

## Rules

- Verify and record an asset's title, publisher or author, exact source URL, version,
  acquisition date, price, and exact license before downloading it.
- Prefer source-redistributable assets such as CC0 or CC BY when quality is comparable.
  Commit those normally with their required attribution and license notice rather than
  encrypting them merely for convenience.
- Never commit `.env`, a download archive, a decrypted restricted asset, or anything
  under `Assets/Resources/Generated/Licensed/` or `Assets/Generated/Licensed/`.
- Commit the encrypted `.enc` payload, its `.enc.json` metadata sidecar, and the Unity
  `.meta` files Unity creates for those two committed files.
- Encryption is source-control protection, not a substitute for a valid license. Every
  developer must still hold any license, entitlement, or seat required to access the
  decrypted files.
- Do not print the key in CI logs, commits, issues, pull requests, or documentation.
- Keep BROcoli-authored integration prefabs, scenes, materials, and configuration
  outside the generated licensed directories. A package update atomically replaces its
  complete generated directory.

## Choose the Unity package workflow

Use the action displayed for the asset in Unity's Package Manager:

- `Install` means the Asset Store item is a UPM package. Install the chosen version and
  commit `Packages/manifest.json` and `Packages/packages-lock.json`. The package source
  stays in Unity's package cache and is restored from its registry on other entitled
  machines, so it is neither committed nor passed through this encryption pipeline.
- `Download` or `Import` means it is a traditional Asset Store asset package. Import it
  once, stage the required imported files and all corresponding `.meta` files together,
  and encrypt that directory with the format-2 workflow below.

Do not independently import a traditional package on every development machine. Package
versions, selected contents, and generated GUIDs can drift. The encrypted payload is the
canonical imported copy for the repository.

## Sourcing workflow

1. Verify the exact listing, source format, price, license agreement, license type, seat
   requirements, dependencies, supported Unity versions, render pipelines, and target
   platforms. Never infer these from a search result.
2. Acquire the asset through the licensee's authenticated account.
3. For a traditional Asset Store package, import it into a temporary Unity project first
   when its contents or project-setting changes are unclear. Use Package Manager's
   package-content or imported-assets view to identify every imported file.
4. Import only the required runtime and editor files into BROcoli. Place them together
   under a package-specific directory such as
   `Assets/Generated/Licensed/ExampleWater/`, preserving their `.meta` files. Prefer
   moving files in Unity so references keep their GUIDs.
5. Verify the package from that generated location before encryption. Packages that
   depend on an exact top-level path such as `Assets/StreamingAssets`, `Assets/Gizmos`,
   or a hard-coded publisher directory need a tailored, reviewed integration; do not
   silently scatter decrypted files across the project.
6. Encrypt the package directory, invoke **BROcoli > Licensed Assets > Decrypt All**, and
   verify the reconstructed package in Play Mode and in a representative build.
7. Commit the encrypted payload, metadata, attribution records, and project-authored
   integration changes. Confirm that no plaintext package file is staged.

## Encrypt one restricted file

Put `BROCOLI_LICENSED_ASSET_KEY` in the local `.env` or process environment, then run:

```bash
python3 scripts/licensed_asset_crypto.py encrypt \
  --input /absolute/path/to/model.fbx \
  --output Assets/Encrypted/Licensed/model.fbx.enc \
  --generated-path Assets/Resources/Generated/Licensed/model.fbx \
  --source-url https://example.com/model \
  --author "Model Author" \
  --license "Exact license name"
```

This creates a backward-compatible format-1 payload.

## Encrypt an imported Asset Store package

Stage the imported package and all of its `.meta` files under its ignored generated
directory. Then run, substituting the values from the exact Asset Store listing:

```bash
python3 scripts/licensed_asset_crypto.py encrypt \
  --input Assets/Generated/Licensed/ExampleWater \
  --output Assets/Encrypted/Licensed/example-water.zip.enc \
  --generated-path Assets/Generated/Licensed/ExampleWater \
  --title "Example Water" \
  --author "Example Publisher" \
  --source-url https://assetstore.unity.com/packages/example \
  --asset-version "1.2.3" \
  --price "Free" \
  --license "Standard Unity Asset Store EULA" \
  --license-type "Extension Asset" \
  --acquired-date "2026-08-28"
```

Directory inputs require the title, asset version, license type, and acquisition date.
The command creates one encrypted ZIP payload and a version-2 JSON sidecar containing a
payload hash, file count, uncompressed size, stable package-folder GUID, and acquisition
metadata.

On editor startup and before a build, `LicensedAssetDecryptor`:

1. Decrypts the payload to a temporary file under `Library/`.
2. Verifies the SHA-256 digest before reading it.
3. Rejects absolute paths, parent traversal, links, duplicate entries, unsupported file
   types, and archives whose file count or expanded size differs from their metadata.
4. Extracts to a staging directory and atomically replaces the package's previous
   generated directory, which also removes files dropped by package updates.
5. Restores a deterministic root-folder `.meta` file and refreshes Unity's AssetDatabase.

## Set up another development machine

1. Pull the repository.
2. Retrieve the repository key from the team password manager and put it in `.env`.
3. Ensure the developer holds the required Asset Store entitlement or seat.
4. Open the Unity project. The editor automatically reconstructs all encrypted payloads.
5. If needed, invoke **BROcoli > Licensed Assets > Decrypt All**. Builds fail early when
   encrypted assets exist but the key is unavailable.

For UPM packages, the other machine instead signs into an entitled Unity account, pulls
the committed package manifests, and lets Package Manager restore the exact dependency.

## Updating or removing a package

To update a traditional package, import and validate the selected new version on one
machine, replace the contents of its generated directory, and rerun the same encryption
command with updated acquisition metadata. The encrypted payload and JSON sidecar are
the reviewable version change; other machines receive it through Git.

To remove a package, first remove all project references and integration content, then
remove its encrypted payload and sidecar. Each developer may delete the corresponding
ignored generated directory locally. Do not delete another package's generated path.

## Current restricted assets

- `theHand.fbx.enc` — “the Hand” by sayjing, Sketchfab Free Standard, acquired through
  the authenticated licensee account on 2026-08-25.
  Source: https://sketchfab.com/3d-models/the-hand-a0bd25ee25544603a8455121aa1242ec
- `fog-particles.zip.enc` — “Fog Particles” version 1.0.0 by Game Seed Assets,
  acquired free on 2026-08-29 under the Standard Unity Asset Store EULA as an
  Extension Asset. The payload restores to `Assets/Generated/Licensed/FogParticles/`
  and retains only the bluish fog prefab and its material/texture dependencies. It is
  instantiated under `DungeonManager` as the dungeon's general atmospheric fog.
  Source: https://assetstore.unity.com/packages/vfx/particles/fog-particles-351840
- `free-fire-vfx-urp.zip.enc` — “Free Fire VFX - URP” version 1.0.2023.1 by
  Vefects, acquired free on 2026-08-29 under the Standard Unity Asset Store EULA as
  an Extension Asset. The payload restores to
  `Assets/Generated/Licensed/FreeFireVFXURP/` and contains the selected small fire
  prefab plus its runtime dependencies; unused audio, light, heat-haze, demo, and
  alternate-effect content was removed. `DungeonTorch.prefab` uses the selected fire
  effect for every generated torch.
  Source: https://assetstore.unity.com/packages/vfx/particles/fire-explosions/free-fire-vfx-urp-266226
- `urp-stylized-water-shader.zip.enc` — “URP Stylized Water Shader - Proto Series”
  version 1.0 by BitGem, acquired free on 2026-08-29 under the Standard Unity Asset
  Store EULA as an Extension Asset. The payload restores to
  `Assets/Generated/Licensed/StylisedWaterShader/` and retains the water Shader Graph
  and normal texture. A BROcoli-authored integration material applies it to the
  shallow volume mesh used by `DungeonWater.prefab`.
  Source: https://assetstore.unity.com/packages/vfx/shaders/urp-stylized-water-shader-proto-series-187485
- `stylized-water-effect-pack.zip.enc` — “Stylized Water Effect Pack” version 1.0
  by Namu, acquired free on 2026-08-29 under the Standard Unity Asset Store EULA as
  an Extension Asset. The payload restores to
  `Assets/Generated/Licensed/StylizedWaterEffectPack/` and retains only the particle
  Shader Graph and its texture/subgraph dependencies. The existing gameplay spray
  keeps its range, collision, and burst logic while its core particles use this
  acquired water-effect shader through a BROcoli-authored Resources material.
  Source: https://assetstore.unity.com/packages/vfx/particles/stylized-water-effect-pack-270114
