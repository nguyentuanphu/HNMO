# Local checks

Probe scripts the local sessions used on the owner's PC to check the game in a browser and the server code. They are
kept here so a cloud session can read and reuse them. Most of them need a running demo game server and web bridge,
which only the owner's PC has.

Environment:
- `HNMO_WORK`: the folder for results, default `C:/hnmo-work`.
- `LEGACY_S9_MONSTERS_DIR`: for `mapcheck/s9list.mjs`, the Season 9 server suite's monster folder. Legacy server files
  are not in the repository.
- Playwright from `client/web`; the bridge URL and the test accounts (`testgm`, `testgm2`, `test3`, password the same
  as the name, demo server only) are set at the top of each script.

| Script | What it does |
|---|---|
| `unused-settings.mjs <server/src>` | Lists settings (public properties of configuration classes) that no game code reads: a setting that does nothing when changed. Some are read through C# pattern matching (`{ Flag: true }`), which this text scan misses, so check each by hand. |
| `skillsweep.mjs <account> <charIndex> <label>` | Logs in, goes to a monster field, casts every skill of the character at a monster, and records the damage and the frame times (p95, max) to `skillsweep/<label>/rows.json`. Its effect counts are low: it subtracted the effects that were already there before the cast. |
| `fxprobe.mjs` | The corrected effect counter: counts each effect a cast creates. Use it for effect checks. |
| `sweepsum.js <labels…>` | Summarises `rows.json` files per class. |
| `s9world.mjs` | Sets the demo world to Season 9 on the admin page (`/worlds`). Reads the admin password from `$HNMO_WORK/demo-admin-pass.txt`. |
| `s9check.mjs`, `pathprobe.mjs` | Small login and walk probes. |
| `mapcheck/` | The map-by-map check of monsters, NPCs and objects (`mapcheck.mjs` in game, `offline.mjs` without a server, `spawns.mjs`, `report.mjs`, `summarize.mjs`); see `docs/map-check-2026-11-20.md`. |
