# Agent instructions

## Unity Editor automation

Always open the Unity Editor in automated mode by using the `unity-open` command
available on the shell `PATH`. Do not launch the Unity Editor directly through Unity
Hub, an editor executable, or another command.

## Project structure

This repository hosts several games in one Unity project. Read
`docs/adding-a-game.md` before adding, removing, or restructuring a game.

- Every game is a local Unity package under `LocalPackages/`, mounted through
  `Packages/manifest.json` with a `file:` reference. Adding or removing that one
  line loads or unloads the game, its scenes, its resources, its tests, and its
  licensed assets.
- `com.budgetgamedev.hub` is the brand-neutral launcher. Games register by
  shipping a `GameDefinition` asset under `Resources/GameRegistry/`; the hub
  never references a game's code.
- `com.budgetgamedev.shared` holds reusable runtime services and must name no
  specific game. Inject game-specific values instead, as
  `GameAudioSettings.Configure` and `IPauseController` do.
- A game package declares its own Unity dependencies in its `package.json`, so
  they resolve transitively and disappear when the game is unloaded. Do not move
  a game-specific dependency into the project manifest.
- `Assets/` is for project-wide concerns only: render pipeline settings, TextMesh
  Pro, the WebGL template, and build/licensing editor tooling. Do not add game
  content there.
- Scene names and Resources paths are global across the whole build. Prefix
  scenes with the game id (`Brocoli_Dungeon`) and nest resources under
  `Resources/<GameId>/`.

## Verification gates

`dev` is ungated on purpose. Never run CI or a pre-push gate on it.

This is a hard requirement, not a default to be improved on:

- The pre-push hook runs `./ci.sh` only when a push updates `staging` or
  `production`. Do not widen it to `dev`, to other branches, or to a
  "fast subset" of gates on every push.
- Do not add a GitHub Actions workflow that runs quality checks, tests, or
  builds on `dev`, or on pull requests targeting it. The one hosted job is a
  manually dispatched Pages build kept as a fallback; deploys normally come from
  `./cd.sh` on the host, and nothing runs on push.
- Do not add commit hooks, watchers, or scheduled jobs that verify `dev`.

The cost of this is real and accepted: formatting, lint, and source-size
regressions accumulate on `dev` and surface together at promotion, where the
gate catches them. Fix them there. That backlog is not evidence the setup is
broken, and it is not a reason to gate `dev`.

Running `./ci.sh`, or any individual check, by hand at any time is fine. What is
forbidden is wiring one to run automatically on `dev`.

## Asset acquisition

### Acquisition-first rule

Prefer finding a suitable existing asset from the sources below over creating or
procedurally generating one yourself. Do not start by building a model in Blender/code
or synthesizing audio, shaders, effects, or other content in Unity merely because that
is faster for the agent. First make a reasonable search, inspect viable candidates, and
verify that their licenses, dependencies, and source formats work for this project.

Only fall back to making an asset procedurally in Unity/code after the applicable
preferred sources and acquisition workflows have been tried and no suitable, legally
compatible asset can be acquired. If the user explicitly asks for a procedurally
generated model, sound, or other asset, follow that request directly and skip the
acquisition-first requirement for that asset.

When searching for shaders, materials, VFX, particle effects, or other
rendering-dependent assets from any source, explicitly check render-pipeline support.
When suitable choices are otherwise comparable, prefer assets that support both URP
and HDRP out of the box, while still verifying that they work with the project's current
Unity version and active render pipeline. Treat dual URP/HDRP support as a strong
preference rather than a reason to reject an otherwise suitable asset when no compatible
dual-pipeline option is available.

Only acquire assets that are available at no monetary cost and whose EULA, license,
terms of use, and other applicable conditions are clear and compatible with the intended
use and this repository's asset workflow. Never accept or use assets with unclear or
incompatible terms, payments, subscriptions, paid dependencies or services, or other
material commitments. Reject them and continue searching for a free, compatible
alternative; do not ask the user to approve an exception.

When a free, compatible acquisition, download, import, or service workflow requires
accepting an EULA, license, terms of use, or similar agreement, accept it on the user's
behalf without asking for confirmation. The presence of an acceptance checkbox or other
legal-agreement step alone is not a reason to pause or ask the user.

### Unity Asset Store: search free assets first

Before using the category-specific sources below, search the
[Unity Asset Store](https://assetstore.unity.com/) and filter the results to assets that
are free at the time of acquisition. Prefer it especially for Unity-native content such
as shaders, water, fire, weather and particle effects, VFX, materials, rendering tools,
editor extensions, controllers, frameworks, and other packages whose Unity integration
would otherwise need to be recreated. For shaders, VFX, materials, and rendering tools,
actively look for packages that support both URP and HDRP and prefer them when possible,
while requiring compatibility with the project's currently active pipeline. Also
consider the Asset Store first for 2D and 3D art, animation, audio, UI, environments, and
any other category where a suitable free package may exist.

Make a reasonable search across relevant terms and categories, inspect promising
candidates, and prefer assets that are maintained, documented, compatible with the
project's Unity editor version, active render pipeline, target platforms, visual style,
and runtime budget, and do not require paid dependencies or services. Import only the
files and samples the project actually needs and verify the asset in Play Mode through
the real gameplay camera and target platform settings.

When evaluating an Asset Store package, use the action shown by Unity's Package Manager
to choose the repository workflow:

- If Package Manager offers `Install`, treat it as a UPM package. Install the selected
  version and commit both `Packages/manifest.json` and `Packages/packages-lock.json`.
  Do not commit or encrypt Unity's package cache; another licensed machine restores the
  dependency from the package registry after pulling those two files.
- If Package Manager offers `Download` or `Import`, treat it as a traditional Asset
  Store asset package. Use the encrypted licensed-asset workflow below for the imported
  files and their Unity `.meta` files. Do not rely on each developer independently
  selecting and importing package contents, because that can produce version and GUID
  drift.

An Asset Store price of `Free` does not mean the asset is public domain or freely
redistributable. Before downloading or importing an asset, inspect its exact store page
and linked terms and verify all of the following:

- Its current price is free; never acquire a paid asset, subscription, paid dependency,
  or paid service.
- Its license permits the game's intended commercial use, modification when needed,
  and embedding and distribution in the built game.
- Whether it uses the Standard Unity Asset Store EULA, is a Restricted Asset, or has a
  separate provider license. Read and comply with any provider-specific terms; reject
  assets whose terms are unclear or incompatible.
- Its license type and seat requirements work for this repository and team. In
  particular, Extension Assets generally require a license for each user or seat that
  works with them.
- Its source files can be handled by this repository's distribution model. The
  [Unity Asset Store EULA](https://unity.com/legal/as-terms) generally permits eligible
  assets to be embedded in a substantial game, but does not grant permission to
  redistribute the stand-alone asset or plaintext source package. Store traditional
  `Download`/`Import` Asset Store packages and their imported source files through the
  repository's existing encrypted licensed-asset pipeline and shared key, as is done for
  other restricted third-party assets. Commit only encrypted payloads and their metadata
  sidecars; never commit the download archive, plaintext source files, `.env`, or
  generated decrypted files. For a multi-file package, preserve its folder structure
  and Unity `.meta` files in a single encrypted package payload. Store it under the
  owning game package's `Encrypted/Licensed/` folder and restore it only under that
  same package's `Generated/Licensed/`, so unloading the game removes its restricted
  assets too; keep all integration scenes, prefabs, and configuration outside that
  ignored generated tree so source package updates cannot overwrite project-authored
  work.

Encryption prevents the repository from exposing a usable stand-alone source package;
it does not create license rights or override the Asset Store EULA or provider terms.
Every collaborator must still obtain any license, entitlement, or seat required for
their use. If a license prohibits the intended commercial use, modification, embedding,
encrypted storage workflow, or team access—or is unclear—do not use the asset. Read
`docs/licensed-assets.md` before importing, encrypting, replacing, decrypting, or
re-encrypting any Asset Store package or other restricted third-party asset.

For every acquired Unity Asset Store asset, record its title, publisher, exact store-page
URL, asset version, acquired format or package, acquisition date, free price, exact EULA
or provider license, license type and seat requirements, required attribution, and any
external dependencies. Preserve required license and attribution files. Do not rely on
the store's general reputation or on an asset being free as a substitute for checking
the exact listing and terms.

### 3D models: Unity Asset Store, then Sketchfab, Openverse, and SAM 3D

After completing the Unity Asset Store search above, follow this acquisition order
before creating a 3D model yourself:

1. Search [Sketchfab](https://sketchfab.com/) for a suitable downloadable model. Prefer
   one that fits the requested art direction, animation needs, and runtime polygon
   budget, even when it needs reasonable Blender conversion or optimization.
2. If Sketchfab has no suitable legally compatible model, search
   [Openverse](https://openverse.org/) for a suitable 2D image whose license permits the
   required use and adaptation.
3. Upload the acquired image to Meta's
   [SAM 3D editor](https://aidemos.meta.com/segment-anything/editor/convert-image-to-3d).
   In the editor, select, mask, or annotate the specific object needed for the game,
   generate it as a 3D asset, and download the resulting GLB file. Do not upload an
   image when its license or applicable service terms do not permit this processing.
4. Import the GLB into Blender. Inspect and clean the mesh, remove conversion artifacts
   and unwanted geometry, reduce it to an appropriate runtime polygon budget, repair
   materials and normals, and set sensible scale, orientation, origin, and pivot. Export
   it in a format supported by the Unity project while retaining the source GLB and
   required attribution records when licensing permits.
5. Import the prepared model into Unity, configure its materials and import settings,
   create or update the appropriate prefab, place it in the requested Unity scene, and
   verify its scale, orientation, lighting, collisions, and runtime appearance.

#### Lighting and material verification

An imported model is not complete until it is lit consistently with the existing game.
Before choosing shader or renderer settings, inspect the Broccoli player and coronavirus
enemy in the target gameplay scene and use their runtime appearance as the reference.
Do not switch an asset to an Unlit shader merely to preserve its source colors or make it
more visible.

- Use the shader family for the project's active render pipeline. In this project, prefer
  `Universal Render Pipeline/Lit` (or the same Lit variant used by the nearby reference
  model) for opaque 3D meshes.
- Preserve and validate normals and tangents during Blender cleanup and Unity import.
  Recalculate them only when the source data is missing or visibly incorrect; check for
  inverted faces, faceted gradients, and broken normal-map response.
- Match material response to the object and the established scene style. As a baseline,
  non-metal objects should use metallic `0`, and smoothness should match the Broccoli and
  coronavirus materials (currently approximately `0.5`) unless the object's surface
  clearly requires a different value.
- Enable shadow casting and shadow receiving when comparable scene models use them.
  Confirm that the renderer's layer is included in the relevant light and camera culling
  masks, and do not add an asset-specific light to compensate for incorrect materials,
  normals, layers, or exposure.
- Verify the result in Play Mode through the actual gameplay camera and scene lights, not
  only in Blender, the model importer, or Unity's Scene view. Inspect all gameplay-facing
  orientations and relevant animation poses for readable volume, grounded shadows, and
  consistent brightness beside the player and enemies.

If the imported model still looks flat, overly dark, or self-illuminated, diagnose its
shader, normals, material values, renderer shadow settings, layer, and scene-light setup
before changing its geometry or accepting the asset.

Only fall back to procedural model generation after the Unity Asset Store and Sketchfab
searches and the Openverse-to-SAM-3D workflow fail to produce a suitable, legally
compatible result.

Before adding a model, verify and record its title, author, exact model-page URL,
downloadable formats, and exact license. For an image-derived model, record the source
image's title, creator, exact Openverse and original-work URLs, exact license, and the
fact that SAM 3D was used for conversion. Prefer licenses that allow source
redistribution and adaptation, such as CC0 or CC BY; commit those assets normally with
their required attribution.

If a license permits use in the game but prohibits redistribution of the stand-alone
source model, use the repository's existing encrypted licensed-asset pipeline and key.
Encryption does not make an otherwise prohibited use legal. If the license forbids the
game's intended commercial use, modification, or embedding—or is unclear—do not use the
model; find another free, compatible model or use the allowed procedural fallback.

Read `docs/licensed-assets.md` before importing, replacing, decrypting, or re-encrypting
any licensed model. Never commit `.env` or anything under a `Generated/Licensed/` folder.

### Other game assets: Unity Asset Store, then Kenney

For general-purpose game assets, search the Unity Asset Store first as described above,
then search the [Kenney asset catalog](https://kenney.nl/assets) before creating
replacements yourself. This includes sprites, UI elements, icons, fonts, tiles,
textures, environment kits, props, particles, and other reusable 2D or 3D game content.
Make a reasonable search across relevant asset packs, inspect promising candidates, and
prefer assets that match the project's established visual style, required dimensions,
render pipeline, runtime budget, and target platforms.

When a suitable asset exists, download it and use it in the project: import only the
needed runtime files and required license or attribution material, configure the Unity
import settings, integrate it into the appropriate prefab or scene, and verify it in
context. Do not stop after presenting a list of candidates unless the user asked for
recommendations or a choice is genuinely required.

For every acquired Kenney asset, record the pack title, exact pack-page URL, creator,
downloaded format, and exact license. Verify the license for the specific pack instead
of assuming that every asset on the site has identical terms. Only fall back to another
source or procedural creation after a reasonable Kenney search finds no suitable,
legally compatible asset.

### Audio and SFX: Unity Asset Store, then Freesound

After completing the Unity Asset Store search above, search
[Freesound](https://freesound.org/) before synthesizing audio yourself, especially for
these categories:

- Footsteps, impacts, weapons, doors, and UI sounds.
- Forests, rain, cities, machinery, and other ambience.
- Creature and vocal sounds.
- Loops, drones, and experimental audio.

For every acquired sound, verify the license on the exact sound page and record the
title, creator, source URL, and attribution requirements. Prefer legally compatible CC0
or CC BY audio. Do not use encryption or transformation as a workaround for a license
that prohibits the game's intended commercial use, modification, or distribution.

If reasonable Unity Asset Store and Freesound searches produce no suitable compatible
asset, procedural generation in Unity/code becomes the fallback. Keep procedural audio
reproducible and document why acquisition was not suitable when the reason is not
obvious from the task.
