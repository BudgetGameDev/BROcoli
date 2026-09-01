# ambientCG glazed terracotta

Author: ambientCG (Lennart Demes)  
Source: https://ambientcg.com/view?id=GlazedTerracotta001  
Acquired: 2026-09-01  
Selected material: Glazed Terracotta 001  
Source format: 2K JPEG PBR maps (Color, NormalDX, Roughness, Displacement)  
License: Creative Commons CC0 1.0 Universal  

ambientCG publishes its assets under CC0, which permits commercial use,
modification and shipping the raw files inside a game. Attribution is not
required; the credits record one anyway.

BROcoli uses this set for dungeon pots. The set ships no metalness map, so
`scripts/prepare_stylized_pbr_textures.py` writes a black one and takes
smoothness from the roughness map; the normal map is applied at low strength
because glazed pottery is smooth.

Attribution: GlazedTerracotta001 from ambientCG.com, licensed under CC0 1.0 Universal
