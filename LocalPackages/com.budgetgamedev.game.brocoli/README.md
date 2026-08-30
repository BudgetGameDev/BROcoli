# BROcoli

A top-down roguelite: a broccoli sprays its way through a procedurally generated
dungeon of coronaviruses.

Registers itself with the Budget GameDev Game Hub through
`Resources/GameRegistry/brocoli.asset`. Removing this package from
`Packages/manifest.json` removes the game, its scenes, its resources and its
licensed third-party VFX from the project in one step.

## Layout

- `Runtime/` — gameplay, dungeon generation, spray weapon, enemies, UI, autoplay.
- `Editor/` — spray setup tooling.
- `Tests/Editor/` — dungeon geometry, occlusion and wall-visibility suites.
- `Scenes/` — `Brocoli_MainMenu`, `Brocoli_Dungeon`. Scene names are game-prefixed
  because Unity loads scenes by name across the whole build.
- `Resources/Brocoli/` — audio, sprites, shaders and prefabs, namespaced so a
  second game's resources cannot collide with them.
- `Encrypted/Licensed/` — encrypted Asset Store payloads; see
  `docs/licensed-assets.md` in the repository root.

## Licensed third-party assets

The VFX packages this game uses are Unity Asset Store **Extension Assets** under
the Standard Unity Asset Store EULA. They are stored encrypted and restored to
`Generated/Licensed/`, which is git-ignored. They are dependencies of this game
in the practical sense — they arrive and leave with the package — but they are
not UPM `dependencies`, and they may not be redistributed as part of this
package. See "Publishing" below.

## Publishing

The folder layout is a standard UPM package. Before publishing anywhere, the
licensed Extension Assets must be removed or replaced: the Asset Store EULA
permits embedding them in a game, not redistributing them inside another package.
