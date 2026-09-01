# Dungeon reflection environment

Author: Andreas Mischok (Poly Haven)  
Source: https://polyhaven.com/a/drachenfels_cellar  
Acquired: 2026-09-01  
Selected asset: Drachenfels Cellar HDRI, 1K  
License: Creative Commons CC0 1.0 Universal  

Poly Haven publishes its HDRIs under CC0: commercial use, modification and
shipping the files inside a game are all permitted, and attribution is optional.
The credits record one anyway.

`DungeonReflection.png` is derived from the 1K HDRI: scaled to 0.6, clamped, and
resized to 1024x512, then imported as a cubemap. The raw HDRI is not kept. Its
peak radiance is around 139, and a value that large survives neither BC6H
compression nor URP's reflection sampling: the scene renders white with colour
speckles because the probe returns infinities. A clamped low-dynamic-range map
gives dungeon steel something dim and stone-coloured to reflect instead.

Attribution: Drachenfels Cellar by Andreas Mischok from Poly Haven, CC0
