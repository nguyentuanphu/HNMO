# HNMO build protection (owner notes — not shipped)

This file explains the ownership watermarks and the tamper-evidence built into a packaged
HNMO folder, so you can recognise your own builds and spot a rebranded copy. It lives in the
repo and is **not** copied into the shipped folder by `package-hnmo.sh`.

Project id: `1abdb72e-f72e-457c-93d5-3141ad991109` (also `PROJECT_ID.txt`).
Build stamp shape: `HNMO-<projectId>-<yyyyMMdd>`.

## 1. Where the watermarks are

Full location list (incl. the hidden markers) is in `WATERMARKS.private.md`. Summary:

- **Server binaries** — every `HNMO.*.dll` carries `HNMO.ProjectId` / `HNMO.BuildStamp` assembly
  metadata (bundled into the single-file `server/HNMO.Startup` in a shipped build). Also
  `HNMO.Integrity.HnmoBuildStamp` as `const`s.
- **Web JS** — the stamp is compiled into `web/wwwroot/js/main.min.js`; it is also logged to
  the browser console and set on `window.__hnmoBuild`.
- **Hidden dot-file** — `web/wwwroot/.hnmo-build`.
- **Trailing comment** — appended after the end of `web/wwwroot/index.html`.

## 2. Checking a suspect folder for the project id

```bash
# the id baked into the server binaries (single-file exe in a shipped build)
strings suspect/server/HNMO.Startup | grep -i '1abdb72e-f72e-457c-93d5-3141ad991109'
# the id baked into the web bundle
grep -o 'HNMO-1abdb72e[^"]*' suspect/web/wwwroot/js/main.min.js
grep -o '1abdb72e-f72e-457c-93d5-3141ad991109' suspect/web/wwwroot/js/main.min.js
# the hidden markers
cat suspect/web/wwwroot/.hnmo-build
tail -c 120 suspect/web/wwwroot/index.html
```

In the running game, open the browser console and read `window.__hnmoBuild`.

If any of these still show `1abdb72e-…`, the folder is a build of this project, however it is
labelled.

## 3. How the integrity check works

At package time `package-hnmo.sh`:

1. Hashes (SHA-256) the key files: the server executable `server/HNMO.Startup[.exe]` (or the
   loose `HNMO.*.dll` of a non-single-file build), the launcher (`web/HNMO.Web[.exe]`) and the
   web bundle (`web/wwwroot/js/main.min.js`).
2. Writes them to `server/HNMO.integrity.json` (with the project id and build stamp).
3. Signs that manifest with the **private Ed25519 key** and writes the signature (base64) to
   `server/HNMO.integrity.sig`.

At startup the server (`HNMO.Integrity.IntegrityVerifier`, called early from `Program.cs`):

1. Loads the **public key** embedded in the server assembly.
2. Verifies the signature over `HNMO.integrity.json`.
3. Re-hashes every listed file and compares.

Outcomes:

| Situation | Result | Server behaviour |
|-----------|--------|------------------|
| Valid signature, all files match | `Ok` | starts, logs a confirmation |
| No manifest / no signature | `Unsigned` | starts, logs a warning (dev build) |
| Signature present but invalid | `SignatureInvalid` | **refuses to start** |
| Signature valid, a file changed | `FileMismatch` | warns and starts; refuses if `Integrity:OnMismatch` = `refuse` |

`Integrity:OnMismatch` is set in `server/appsettings.json` (default `warn`, so a legit
unsigned/dev build still runs). A tampered **signed** build can be made to refuse by setting it
to `refuse`.

## 4. The private signing key

- The key is **not** in the repo. Keep the one generated for you at the path reported by the
  packaging task in a safe place (a password manager / offline backup). If you lose it you can
  generate a new keypair, but then old signed builds can no longer be matched to a new one, and
  you must replace the embedded public key (`client/web/tools/hnmo/integrity-pub.pem`) and
  rebuild.
- Regenerate (if ever needed):
  ```bash
  openssl genpkey -algorithm ed25519 -out hnmo-signing.key
  openssl pkey -in hnmo-signing.key -pubout -out client/web/tools/hnmo/integrity-pub.pem
  ```
  Commit only the `integrity-pub.pem`; never the `.key`.

## 5. Signing release builds in GitHub Actions

Store the private key as an encrypted repository secret and feed it to the packaging step:

1. Repo → Settings → Secrets and variables → Actions → New repository secret.
   Name `HNMO_SIGNING_KEY`, value = the full PEM contents of `hnmo-signing.key`.
2. In the HNMO package workflow, pass it to the script:
   ```yaml
   - name: Package HNMO
     env:
       HNMO_SIGNING_KEY: ${{ secrets.HNMO_SIGNING_KEY }}
     run: client/web/tools/package-hnmo.sh "$PWD/server" linux-x64 dist/HNMO
   ```
   (Or write it to a file and set `HNMO_SIGNING_KEY_FILE` to its path.)

With no secret set the workflow still builds, but produces an **unsigned** folder and the script
prints a clear warning.

## 6. Honest limits

- This deters **casual** rebranding and **proves ownership** of a build; it is not DRM.
- A determined person with the binaries can patch the integrity check out of the server and strip
  the visible markers. They are far less likely to find and remove every hidden marker, and the
  signed manifest means any file they change is detectable by anyone who runs the stock server.
- The embedded public key lets anyone verify a folder is a genuine, unmodified build; only you,
  holding the private key, can produce a new signed build.
