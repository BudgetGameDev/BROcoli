# Agent instructions

## Third-party 3D assets

When the game needs a 3D model, search Sketchfab first and prefer a downloadable model
that fits the art direction, animation needs, and runtime polygon budget. Before adding
it, verify and record the model title, author, source URL, and exact license from the
model page. Prefer licenses that allow source redistribution, such as CC0 or CC BY;
commit those models normally with their required attribution.

If a license permits use in the game but prohibits redistribution of the stand-alone
source model, use the repository's existing encrypted licensed-asset pipeline and key.
Encryption does not make an otherwise prohibited use legal. If the license forbids the
game's intended commercial use, modification, or embedding—or is unclear—do not use the
model; find another model or ask the user.

Read `docs/licensed-assets.md` before importing, replacing, decrypting, or re-encrypting
any licensed model. Never commit `.env` or anything under
`Assets/Resources/Generated/Licensed/`.
