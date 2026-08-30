# 3D model acquisition

Read [Asset acquisition](asset-acquisition.md) first. Its acquisition-first
rule, Unity Asset Store search, licensing checks, and recording requirements all
apply here; this guide only adds what is specific to this category.

After completing the Unity Asset Store search in
[Asset acquisition](asset-acquisition.md), follow this acquisition order
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

## Lighting and material verification

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

Read [licensed-assets.md](licensed-assets.md) before importing, replacing,
decrypting, or re-encrypting any licensed model. Never commit `.env` or anything
under a `Generated/Licensed/` folder.
