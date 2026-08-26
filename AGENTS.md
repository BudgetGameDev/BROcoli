# Agent instructions

## Asset acquisition

### Acquisition-first rule

Prefer finding a suitable existing asset from the sources below over creating or
procedurally generating one yourself. Do not start by building a model in Blender/code
or synthesizing audio in Unity merely because that is faster for the agent. First make a
reasonable search, inspect viable candidates, and verify that their licenses and source
formats work for this project.

Only fall back to making an asset procedurally in Unity/code after the applicable
preferred sources and acquisition workflows have been tried and no suitable, legally
compatible asset can be acquired. If the user explicitly asks for a procedurally
generated model, sound, or other asset, follow that request directly and skip the
acquisition-first requirement for that asset.

### 3D models: Sketchfab, then Openverse and SAM 3D

Follow this acquisition order before creating a 3D model yourself:

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

Only fall back to procedural model generation after both the Sketchfab search and the
Openverse-to-SAM-3D workflow fail to produce a suitable, legally compatible result.

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
model; find another model or ask the user.

Read `docs/licensed-assets.md` before importing, replacing, decrypting, or re-encrypting
any licensed model. Never commit `.env` or anything under
`Assets/Resources/Generated/Licensed/`.

### Audio and SFX: search Freesound first

Search [Freesound](https://freesound.org/) before synthesizing audio yourself, especially
for these categories:

- Footsteps, impacts, weapons, doors, and UI sounds.
- Forests, rain, cities, machinery, and other ambience.
- Creature and vocal sounds.
- Loops, drones, and experimental audio.

For every acquired sound, verify the license on the exact sound page and record the
title, creator, source URL, and attribution requirements. Prefer legally compatible CC0
or CC BY audio. Do not use encryption or transformation as a workaround for a license
that prohibits the game's intended commercial use, modification, or distribution.

If a reasonable Freesound search produces no suitable compatible asset, procedural
generation in Unity/code becomes the fallback. Keep procedural audio reproducible and
document why acquisition was not suitable when the reason is not obvious from the task.
