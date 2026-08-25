# Agent instructions

## Asset acquisition

### Acquisition-first rule

Prefer finding a suitable existing asset from the sources below over creating or
procedurally generating one yourself. Do not start by building a model in Blender/code
or synthesizing audio in Unity merely because that is faster for the agent. First make a
reasonable search, inspect viable candidates, and verify that their licenses and source
formats work for this project.

Only fall back to making an asset procedurally in Unity/code after the preferred source
has been searched and no suitable, legally compatible asset can be acquired. If the user
explicitly asks for a procedurally generated model, sound, or other asset, follow that
request directly and skip the acquisition-first requirement for that asset.

### 3D models: search Sketchfab first

Search [Sketchfab](https://sketchfab.com/) before creating a 3D model yourself. Prefer a
downloadable model that fits the requested art direction, animation needs, and runtime
polygon budget, even when it needs reasonable Blender conversion or optimization.

Before adding a model, verify and record its title, author, exact model-page URL,
downloadable formats, and exact license. Prefer licenses that allow source
redistribution, such as CC0 or CC BY; commit those models normally with their required
attribution.

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
