# Local Claude, overnight (2026-11-19 22:30 Sydney, owner asleep, nobody to ask)

Paste everything below the line.

---

You work on HNMO (github.com/nguyentuanphu/HNMO) on the owner's Windows PC, overnight. The owner is asleep: nobody
answers questions. Main Claude (cloud) reviews and merges your work into `claude/hnmo-main-handover-54co2m`.

## Rules (always)

- Read `CLAUDE.md` first. Sydney time. HNMO name only: no third-party game, publisher, vendor or project names in
  code, commits, docs, PR texts or file names. Write `<scratch>` instead of folder paths with the owner's user name.
- Never copy Season 9 or other game files into the repository; report names, counts and sizes only.
- Never weaken, skip or delete a test to get green. Never push to `main`, never merge, never force-push.
- One draft PR per job, into `claude/hnmo-main-handover-54co2m`. Every claim in a PR says what you ran and its
  result. If you are stuck on a job for more than about 90 minutes, write down what you found in the PR and move on.
- Before each job: `git fetch origin` and branch from the newest `origin/claude/hnmo-main-handover-54co2m`
  (commit `1eee2222` or later; it already has #90, #92 and #93).
- Test setup for everything below: a demo server and the development web client built from that same commit
  (as `.github/workflows/web-e2e.yml` does: `dotnet run` of `server/src/Startup` with
  `-demo -autostart -resolveIP:loopback` and `ASPNETCORE_URLS=http://127.0.0.1:8095`, the bridge on 8081, then
  `npx playwright test <files> --workers=1 --trace off`). Not the packaged folder: many tests import single modules
  that a package doesn't ship. Restart the demo server between jobs (tests share accounts).

## Job 1: movement and NPC talk (branch `movement-npc-talk`), the release blocker

These fail every time on the branch tip against a fresh demo server:
1. `tests/e2e/gens.spec.ts` and `tests/e2e/social.spec.ts`: talking to an NPC (`online.talkTo`) does not walk the
   hero next to it, so the dialogue never opens. In gens, test7 stays at (146, 142), 5 cells from the steward at
   (141, 143).
2. `tests/e2e/command-window.spec.ts`: after the server corrects a walk (#93's resync, logged "walked while changing
   maps; putting the client back to ..."), the player's next walk from the corrected cell shows on its own screen
   but never reaches the other player; the server logs nothing about it at Information level. Turn on Debug logging
   for `HNMO.GameLogic.Player` to see which early return or check drops it.
Find the cause in the server or the web client (`client/web/src/game/online.ts`, `server/src/GameLogic/PlayerMovement.cs`),
fix it, add a server unit test or a browser test for it, and run `gens`, `social`, `command-window`, `trade`,
`quests`, `inventory` five times each in a row against one demo server: all must pass, tests unchanged.

## Job 2: black rectangles over Lorencia's buildings (branch `indoor-fade-check`)

The gens failure screenshot (hero at Lorencia 146,142) shows large black rectangles where building parts should be.
Check whether #92's roof fade (`updateIndoorFade`, `client/web/src/world.ts`) causes it (compare with #92 reverted,
in the same spot), fix it, screenshots before and after in the PR.

## Job 3: the Season 9 run (branch `s9-run-2026-11-20`, report only unless a fix is small)

Same setup, plus the bridge's Season 9 data pointed at the owner's Season 9 pack (built with
`client/web/tools/build-s9-pack.mjs ... --base-allowlist ...`), and a Season 9 world on the admin page `/worlds`
whose server the tests log in to. Run:
monster-looks, movement, click-walk, combat, skills, skill-cast, skill-effects, item-glow, ground, pets, mount-chain,
pet-zen, minimap, gates, move, teleport-npc, shop, sound, opus, visual-check, season, rage-fighter, new-skills,
character, inventory, items-use, events-field, world-bosses-live.
Write `docs/s9-run-2026-11-20.md`: per test passed / failed, for each failure the error lines and a screenshot
(`docs/screenshots/s9-run-2026-11-20/`, named after the test), and whether it also fails in a Season 6 world (run the
failing ones once more without the Season 9 world). A failure that also fails in Season 6 is not a Season 9 problem.

## Job 4 (if time is left): `tests/e2e/events.spec.ts`

The Blood Castle countdown (`.rml-hud_status .countdown-text`) stays empty. Find out why (server event state, the
packet, or the client's countdown), fix it or write down what you found in a draft PR.

## When you stop

Leave every job as a draft PR (finished or not) with what passed, what failed, and what is left. Do not start
anything not listed here.

## Added 22:30 Sydney: Antigravity is out; these are yours too (after jobs 1-4)

### Job 5: the Season 9 package check on Windows (branch `s9-package-check`)
From `docs/prompts/antigravity-2026-11-20.md`: build the Season 9 pack (`build-s9-pack.mjs` with
`--base-allowlist`), package with `HNMO_S9_DATA_DIR` pointed at it, start it, check `/data/seasons.json`,
`/data/s9/Player/player.bmd` differs from `/data/Player/player.bmd`, `/data/s9/Local/Mix.bmd` equals the base one,
then create a Season 9 world on `/worlds`, log in on its server and walk Lorencia, Devias, Noria: screenshots and
every `/data/s9/` request that fails in every form. Report in `docs/s9-package-2026-11-20.md`.

### Job 6: what Season 9's new classes need (branch `s9-classes-files`, report only)
Season 9's character classes the web game can't draw yet must be made to work from the Season 9 files (plan:
`docs/plans/seasons-2026-11-19.md`, step 6). List for each new class which files it uses: body and armour sets,
weapons, its animation set, its skill effect models and textures, sounds (names, folders, counts, sizes; never the
files). Check the pack builder takes them all. Report in `docs/s9-classes-files-2026-11-20.md`.

Main Claude works in parallel on the seasons plan steps 1-4 (server rules per season, the two-button admin panel,
files per season, the start screen) on its own branches; do not change those areas.

## Job 7 (added 23:15): models for 39 items from the Season 9 data

The owner chose: take the missing item models from the Season 9 pack. These items have no 3D model in the base data
(see `node client/web/tools/make-item-icons.mjs`, which lists them): 5:32, 7:53, 8:53, 9:53, 10:53, 11:53, 12:20,
12:26, 12:73, 12:74, 13:95, 13:96, 13:99, 13:117-120, 13:128-134, 14:27, 14:55-57, 14:125, 14:151-159, 15:25.
1. In the local Season 9 data, find each item's model and textures (`Item/` .bmd, .ozj/.ozt). Report names and sizes
   only; never commit Season 9 files.
2. Make sure `build-s9-pack` takes them, and say which items still have nothing.
3. Draft PR into `claude/hnmo-main-handover-54co2m` with the report (and any pack builder change). Main Claude makes
   the web game read them for every season and draws the pictures.

## Job 8 (added 23:40): Season 9 as its own program, on the owner's computer

PR #97 (branch `seasons-databases`) runs Season 9 as a second server program with its own database (`hnmo_s9`).
Accounts and coins are shared with Season 6.3. PR #98 (`seasons-admin`) adds the admin panel's big season buttons.
Check them in a packaged folder:
1. Package `seasons-admin` (it contains #97). In the admin panel (8095), Host settings: switch on "Season 9", save,
   restart HNMO. The log must show "Season 9 is starting (connect server 44506, admin panel http://127.0.0.1:8096)".
2. The game's start screen shows the Season 6.3 and Season 9 columns. Then:
   - play a Season 9 server with a Season 6.3 account;
   - "Play" from the website also works for a Season 9 server;
   - coins bought on the website show in Season 9.
3. In Season 9's admin panel (8096), use Import server on the local Season 9 server files, so `hnmo_s9` gets
   Season 9's shops, spawns, drops and rates. Report the preview's counts and warnings (names only; no vendor files
   in git).
4. Draft PR with the report (`docs/local-s9-program-<date>.md`) into `claude/hnmo-main-handover-54co2m`.

## Note (00:15): Job 4 is taken by main Claude

Main Claude (cloud) works on Job 4 (the events countdown) now. Skip it. If you already started it, push what you
have to a draft PR and say so in it.

## Job 9 (added 03:45, 20 Nov): draw Stardust (14:153) from the Season 9 pack

Job 7's report (`docs/s9-item-models-2026-11-20.md`) found one item whose model is only in the Season 9 data:
Stardust, 14:153, candidate `Item/SpiritDust.bmd` (version 0x0e) with `Item/Texture/SpiritDust.OZJ` and
`SpiritDustAlpha.OZT`. Main Claude's cloud copy of the Season 9 data has no `Item/` folder, so it can't draw it.
1. With the local Season 9 data, draw the candidate with the web game's item renderer (as
   `client/web/tools/make-item-icons.mjs` does) and look at it: is it a star dust (a small glowing powder or pouch)?
2. If yes: add the model entry for 14:153 to `client/src/bin/Data/Items/Models/Group14_Potion.json`
   (file `Data/Item/SpiritDust.bmd`, texture folder `Item/Texture`). Then let the bridge serve those three files from
   the Season 9 pack when the base data has none: `SeasonData` in `client/web/bridge` already does this for the
   Season 9-only map folders (`IsSeasonNineOnlyMapFile`). Add a tested allowlist of exactly these three files.
   Make the item picture (`make-item-icons.mjs`) and check the item in the game's bag (`/item 14 153` with `testgm`).
3. If no: say so in the report; nothing changes.
4. Draft PR into `claude/hnmo-main-handover-54co2m`, with a screenshot.
