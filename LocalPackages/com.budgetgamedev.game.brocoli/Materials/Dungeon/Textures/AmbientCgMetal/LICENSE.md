# ambientCG metal

Author: ambientCG (Lennart Demes)  
Source: https://ambientcg.com/view?id=Metal063  
Acquired: 2026-09-01  
Selected material: Metal 063 (aged, oxidised steel)  
Source format: 2K JPEG PBR maps (Color, NormalDX, Roughness, Metalness, Displacement)  
License: Creative Commons CC0 1.0 Universal  

ambientCG publishes its assets under CC0: "You can copy, modify, distribute and
perform the assets, even for commercial purposes, all without asking permission",
and "You can include the raw files in your project, for example a video game."
Attribution is not required; the credits record one anyway.

BROcoli uses this set for the steel bands, blades, spikes and fittings on dungeon
props, and for the surface detail under the gold tint on coins, keys and the
golden chest. `scripts/prepare_stylized_pbr_textures.py` combines the metalness
and roughness maps into a Unity metallic/smoothness texture and scales metalness
to 0.6. A fully metallic prop shows only what it reflects, and a dungeon lit by
torches gives it very little, so the part-metal keeps some albedo under the
torchlight while the environment cubemap supplies the shine. The DirectX normal
map is unchanged and the displacement map is not used.

Attribution: Metal063 from ambientCG.com, licensed under CC0 1.0 Universal
