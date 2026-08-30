# Audio and SFX acquisition

Read [Asset acquisition](asset-acquisition.md) first. Its acquisition-first
rule, Unity Asset Store search, licensing checks, and recording requirements all
apply here; this guide only adds what is specific to this category.

After completing the Unity Asset Store search in
[Asset acquisition](asset-acquisition.md), search
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
