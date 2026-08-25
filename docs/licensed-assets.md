# Licensed 3D assets

BROcoli uses one repository-wide key for third-party models whose licenses permit use
inside the game but prohibit redistribution as stand-alone source files. The real key is
stored in the team password manager and in each developer's ignored `.env` file as
`BROCOLI_LICENSED_ASSET_KEY`.

## Rules

- Verify and record the model's author, source URL, and license before downloading.
- Never commit `.env`, a decrypted restricted model, or a download archive.
- Do not encrypt CC0/CC BY assets merely for convenience; commit those normally with
  their required attribution and license notice.
- Encryption is source-control protection, not a substitute for acquiring a valid
  license. Download restricted assets through the licensee's authenticated account.
- Do not print the key in CI logs, commits, issues, pull requests, or documentation.

## Encrypting a new restricted model

1. Put `BROCOLI_LICENSED_ASSET_KEY` in the local `.env` or process environment.
2. Run:

   ```bash
   python3 scripts/licensed_asset_crypto.py encrypt \
     --input /absolute/path/to/model.fbx \
     --output Assets/Encrypted/Licensed/model.fbx.enc \
     --generated-path Assets/Resources/Generated/Licensed/model.fbx \
     --source-url https://example.com/model \
     --author "Model Author" \
     --license "Exact license name"
   ```

3. Commit the `.enc` file and its `.json` metadata sidecar. The Unity editor decrypts
   them into the ignored generated directory. Builds fail early when the key is missing.

The tool uses OpenSSL AES-256-CBC with PBKDF2-HMAC-SHA256 and 200,000 iterations. The
metadata stores a SHA-256 digest so the editor rejects a wrong key or modified payload.

## Current restricted assets

- `theHand.fbx.enc` — “the Hand” by sayjing, Sketchfab Free Standard, acquired through
  the authenticated licensee account on 2026-08-25.
  Source: https://sketchfab.com/3d-models/the-hand-a0bd25ee25544603a8455121aa1242ec
