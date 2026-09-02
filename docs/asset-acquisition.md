# Asset acquisition

Read this before creating or generating any game asset: model, texture, sprite,
sound, music, shader, VFX, material, particle effect, font, UI element, or
environment kit.

## What this guide covers

This guide is about game content: the things that ship inside a game package
under `LocalPackages/` and reach the player through the game itself. It is in
scope when the thing you need is any of:

- 2D or 3D art -- models, textures, sprites, tiles, environment kits, props,
  fonts, and in-game UI art.
- Audio -- sound effects, ambience, voice, and music.
- Rendering and effects -- shaders, materials, VFX, and particle effects.
- Animation -- clips, rigs, and animator controllers.
- A third-party asset pack, Unity package, or gameplay system that would be
  acquired rather than written: a character controller, inventory, dialogue,
  save, or behaviour framework, and similar ready-made building blocks.

## What this guide does not cover

Everything outside the shipped game content is out of scope, even when the
artifact is a picture. None of it needs an acquisition-first search, a category
guide, or an acquisition record:

- The web layer around the build -- the WebGL template's `index.html`, CSS,
  JavaScript, service worker, and manifests, together with any SVG or CSS icon,
  favicon, PWA icon, or install-prompt graphic that lives there. Write those by
  hand, or take them from a permissively licensed library and vendor the parts
  you use into the template. Vendor rather than link: the template is
  offline-first, so a CDN dependency fails exactly when the service worker is
  serving the game from cache. Record the source, version, and license in the
  vendored file's header and in the template's `THIRD-PARTY-LICENSES.txt`, which
  ships with the player.
- Repository and documentation artifacts -- diagrams, screenshots, README
  images, and badges.
- Tooling and CI -- build, editor, and automation scripts, and anything they
  generate.
- This game's own gameplay code. Writing a C# script for a mechanic is ordinary
  programming, not an acquisition; the rule above applies only when a ready-made
  package would fill the need instead of writing one.

Out of scope means this guide's search and recording steps do not apply. It is
not permission to copy someone else's work: third-party code, art, or fonts
pulled into the web layer, the docs, or the tooling still need a compatible
license, and their attribution still belongs in the usual places.

## Category guides

The rules below apply to every in-scope asset category. When you know which
category you need, read the matching guide as well:

| Need | Guide |
| --- | --- |
| 3D models | [3D models](asset-acquisition-3d-models.md) |
| Sprites, UI, icons, fonts, tiles, textures, kits, props | [2D art and kits](asset-acquisition-2d-and-kits.md) |
| Audio, SFX, ambience, music | [Audio and SFX](asset-acquisition-audio.md) |

## Acquisition-first rule

Prefer finding a suitable existing asset from the sources in this guide and the
category guide for what you need, over creating or
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

## Unity Asset Store: search free assets first

Before using the category-specific sources in the linked guides, search the
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
[licensed-assets.md](licensed-assets.md) before importing, encrypting, replacing,
decrypting, or re-encrypting any Asset Store package or other restricted
third-party asset.

For every acquired Unity Asset Store asset, record its title, publisher, exact store-page
URL, asset version, acquired format or package, acquisition date, free price, exact EULA
or provider license, license type and seat requirements, required attribution, and any
external dependencies. Preserve required license and attribution files. Do not rely on
the store's general reputation or on an asset being free as a substitute for checking
the exact listing and terms.
