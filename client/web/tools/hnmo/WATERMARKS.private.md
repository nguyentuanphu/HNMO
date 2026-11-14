# HNMO watermark locations (PRIVATE — do not ship, do not publish)

This note records where the hidden ownership markers live in a packaged HNMO folder,
so the owner knows where to look when checking a suspect copy. It is kept in the repo
and is **not** copied into the shipped folder by `package-hnmo.sh`.

Project id: `1abdb72e-f72e-457c-93d5-3141ad991109` (also in `PROJECT_ID.txt`).
Build stamp shape: `HNMO-<projectId>-<yyyyMMdd>`.

## Where the stamp is embedded

1. **Server binaries (compiled in).** Every `HNMO.*.dll` carries two assembly metadata
   attributes, `HNMO.ProjectId` and `HNMO.BuildStamp` (from `server/src/SharedAssemblyInfo.cs`).
   `HNMO.Integrity.HnmoBuildStamp` also holds them as `const` strings. In the shipped (single-file)
   build the DLLs are bundled into `server/HNMO.Startup` (or `server/HNMO.Startup.exe`), so read
   the stamp from the executable:
   `strings server/HNMO.Startup | grep -i 'HNMO-1abdb72e'`
   or load a DLL and read `AssemblyMetadataAttribute`.

2. **Web client (compiled into the JS).** `client/web/src/build.ts` defines the stamp;
   it is logged to the console on load and set on `window.__hnmoBuild`. It appears in the
   built `public/js/main.js` and in the packaged, minified `web/wwwroot/js/main.min.js`.
   Read: `grep -o 'HNMO-1abdb72e[^"]*' web/wwwroot/js/main.min.js`.

3. **Hidden dot-file (written at package time).** `web/wwwroot/.hnmo-build` — a single
   line with the packaging stamp. A dot-file is easy to miss in a file listing.

4. **Trailing comment in index.html (written at package time).** A newline plus an HTML
   comment `<!-- HNMO-<projectId>-<date> -->` is appended after the end of
   `web/wwwroot/index.html`. It is not rendered, so it stays out of sight, but it travels
   with the page. Read: `tail -c 120 web/wwwroot/index.html`.

The private signing key is **not** in the repo. See PROTECTION.md for the integrity
signature and where the key is kept.
