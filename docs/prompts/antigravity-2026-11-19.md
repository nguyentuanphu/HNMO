# Prompt for Antigravity (Windows), 2026-11-19

Paste everything below into Antigravity once PR #85 is merged into `main`.

---

You are working on HNMO (github.com/nguyentuanphu/HNMO) on the owner's Windows computer. Read `CLAUDE.md`,
`docs/HANDOVER-2026-11-19.md`, `docs/WORKBOARD.md` and `docs/audit-2026-11-19.md` first. All times are Sydney
time. Work on a new branch `antigravity-win-2026-11-19` from the newest `main`; never push to `main`.
Rules that matter here: the name is HNMO only; never write a vendor's name, the original game's name or a
local disk path that contains one into any file, commit message or report; no vendor files in git; no fixed
sleeps in tests; never disable or weaken a test.

Do these four things, in this order, and write the results to `docs/playtest-2026-11-20.md`:

1. **Package and start.** `git pull`, then build the Windows folder into a clean `HNMO-pkg`
   (`client/web/tools/package-hnmo.sh "<repo>/server" win-x64 "<HNMO-pkg>"` from Git Bash, as before). Start it
   with `Start HNMO.bat`. Report: build time, folder size, time until the game (8080) and the admin panel
   (8095) answer.

2. **Windows playtests.** Run the packaged smoke set (`--workers=1 --trace off`), then the full non-smoke
   sweep listed in the workboard's Wave 3. For every failure give the test name, the error, and whether it
   also fails on a second run.

3. **Check the audit fixes by hand in the real game** (two browsers, accounts `test1` and `test2`):
   - Open a trade between the two characters. While it is open, try the Jewel Bank, the Item Market, Mount
     Chain salvage and a chat command such as `/code`. Expected: nothing happens for the windows, and the
     chat command answers "Close the trade or crafting window before using a command." Cancel the trade:
     both bags are as before.
   - Open `http://localhost:8080/control` on the host: it works. Open it from another device on the network,
     and through any tunnel or proxy if one is set up: it must answer 403.
   - On `/worlds` in the admin panel: create a second world with Season 9 and one server id, save, restart
     the server. The world and its season are still there (`save/hnmo-worlds.json`). The website's home page
     shows both worlds with `S6.3` / `S9` badges and a season choice; the game's server list shows the badge.
     Set the new world's EXP to 3 and save: the first world's rate on `/servers` is unchanged.

4. **The Season 9 pack with real files.** Package again with `HNMO_S9_DATA_DIR` set to the extracted Season 9
   `Data` folder. Expected: the files are in `HNMO-pkg/Data_S9`, nothing of them in `HNMO-pkg/Data`.
   Then check, with the browser's network tab:
   - `http://localhost:8080/data/seasons.json` says `"s9": true`.
   - A model that exists in both seasons (for example `Player/player.bmd`): `/data/Player/player.bmd` and
     `/data/s9/Player/player.bmd` have different sizes; `/data/s9/Local/...` and `/data/s9/Interface/...`
     give the same bytes as without `s9/`.
   - Log in to a Season 6 world: no request goes to `/data/s9/`. Log in to the Season 9 world: models, maps
     and sounds are asked under `/data/s9/`, and the game still enters Lorencia without errors in the console.
   - Report which Season 9 files the web game could not read (name, error), without copying any file.

Finish with: what passed, what failed, what you could not check and why. Commit only the report and
screenshots (no data files), push the branch, open a draft pull request.
