# Antigravity (Windows), about 1 hour, 2026-11-19 19:35 Sydney

Paste everything below the line into Antigravity.

---

You test HNMO on this Windows computer. You do not change code, except the one script named in step 2.
Stop when time runs out: an unfinished step is fine, a wrong "pass" is not. Your check3 report is merged (PR #89).

**1. Setup (5 min)**
`git fetch origin` and `git checkout -B antigravity-check4 origin/claude/hnmo-main-handover-54co2m`.

**2. Build the Season 9 pack (15 min)**
Run these in Git Bash. `<S9 Data>` is the owner's extracted Season 9 `Data` folder.
```
node client/web/tools/build-s9-pack.mjs "<S9 Data>" "client/src/bin/Data" "<scratch>/s9-pack" --dry-run
node client/web/tools/build-s9-pack.mjs "<S9 Data>" "client/src/bin/Data" "<scratch>/s9-pack"
node --test client/web/tests/build-s9-pack.test.mjs
```
Copy the summary table and the test result into the report. If the script crashes on a real file, you may fix
`client/web/tools/build-s9-pack.mjs` and add a test with made-up bytes to `client/web/tests/build-s9-pack.test.mjs`.

**3. Package with the pack (20 min)**
```
export HNMO_S9_DATA_DIR="<scratch>/s9-pack"
client/web/tools/package-hnmo.sh "<repo>/server" win-x64 "<scratch>/HNMO-pkg4"
```
Report the size of `HNMO-pkg4/Data_S9` (it was 1,217 MB with the whole Season 9 folder) and the build time.
Start `Start HNMO.bat` and check:
- `http://127.0.0.1:8080/data/seasons.json` gives `{"s6":true,"s9":true}`;
- `/data/Player/player.bmd` and `/data/s9/Player/player.bmd` have different sizes;
- `/data/s9/Local/Mix.bmd` is byte for byte the same as `/data/Local/Mix.bmd`.

**4. Play in a Season 9 world (20 min)**
On `http://127.0.0.1:8095/worlds`, create a Season 9 world with one free server id (as in check2), save, and restart.
Log in as `testgm` on that world's server. Walk Lorencia, then `/move devias` and `/move noria`. For each map:
- take a screenshot;
- list every `/data/s9/...` request that failed in **every** form (for example `.ktx2`, `.jpg` and `.tga` all
  404). A `.ktx2` 404 followed by a `.jpg` that loads is expected, not a failure;
- list any red console error.
Compare with your check1 list (Devias swords and shields, Noria `TileGrass02`, Lorencia `ston03`).

**Rules**
- Write `<scratch>` instead of your own folder paths.
- No third-party names in anything you write or commit. Never copy a game file into the repository.
- Do not push to `main`. Do not merge.
- For every result, give the command or the clicks, plus the output or a screenshot.

**Finish**
Write `docs/antigravity-report-2026-11-23.md`, with screenshots in `docs/screenshots/antigravity-2026-11-23/`.
Commit, push `antigravity-check4`, open a **draft** PR into `claude/hnmo-main-handover-54co2m`, and stop.
