# Long task for Antigravity (Windows), 2026-11-19 15:30 Sydney

Paste everything below the line into Antigravity.

---

You are working on HNMO (github.com/nguyentuanphu/HNMO) on the owner's Windows computer. Main Claude (cloud)
re-audited your branch `antigravity-s6-work` and continued from it. **Do not work on `antigravity-s6-work`
any more.** Everything, your importer commit `81b5f39b` included, is on `claude/hnmo-main-handover-54co2m`
(PR #85), which is about to be merged into `main`.

## Start

1. `git fetch origin`. If PR #85 is merged, branch from `origin/main`; otherwise from
   `origin/claude/hnmo-main-handover-54co2m`. Name the branch `antigravity-wave3`.
2. Read, in this order: `CLAUDE.md`, `docs/HANDOVER-2026-11-19.md`, `docs/audit-2026-11-19.md` (all of it:
   it lists what went wrong in the helpers' work and why), `docs/WORKBOARD.md`,
   `docs/s9-asset-spec-2026-11-19.md` (section 3 changed), `docs/s6-s9-server-spec-2026-11-19.md`.

## Rules (these were broken before; they are checked on every commit)

- **No third-party names anywhere**: not the server package's vendor, not the original game or its publisher,
  not a local disk path that contains one. This includes **commit messages**, branch names, test names, file
  names in tests, docs and screenshots. Your commit `81b5f39b` named the vendor in its message, in the spec
  and in a test; it was taken into the main line as a new, cleaned commit. Say "the owner's Season 6 / Season 9
  package". Before every commit run
  `git diff --cached | grep -inE "<vendor>|<publisher>|<original game>"` with the real words and fix any hit.
- No third-party files, texts, configs or scripts in git. Decoded Season 9 data stays outside the repository.
- Never push to `main`, never merge, never force-push. Never rewrite history that is already pushed.
- HNMO only; web only (no desktop client work); EN + VI for every visible text, in natural Vietnamese with
  accents; Sydney time.
- C# warnings are errors on new code; `.resx` files are append-only; strict TypeScript; never edit
  `client/web/public/js` by hand; no fixed sleeps in tests; **never skip, delete or weaken a test** to get green.
- Report only what you ran. For every "passes" give the command and its last lines of output. If you could
  not run something, say so.
- **Stay out of these areas** (main Claude is working in them; changes there will conflict):
  `server/src/Persistence/Worlds`, `server/src/Persistence/Initialization`, `server/src/GameLogic/PlugIns`
  (except the warning fixes of Part C, which must not change behaviour), `client/web/bridge/Program.cs`,
  `GameFiles.cs`, `SeasonData.cs`, `HostOnly.cs`, `client/web/src/dataRoot.ts`, `bmd.ts`, `main.ts`,
  `website.ts`, and anything for Season 9 game systems on the server (elemental, pentagram, muun, master
  tree, events). Do not start Waves 2 or 4B-4D.
- Commit small, push after each part, tick the workboard only for what is done and proven.

Do the parts in order. Each one ends with a section in `docs/antigravity-report-2026-11-20.md`.

## Part A: Windows package and playtest of the audited build (about half a day)

1. Build a clean Windows folder: `client/web/tools/package-hnmo.sh "<repo>/server" win-x64 "<HNMO-pkg>"` from
   Git Bash. Start it with `Start HNMO.bat`. Report build time, folder size, and the time until the game
   (8080) and the admin panel (8095) answer.
2. Run the packaged smoke set, then the full non-smoke sweep of the workboard's Wave 3, with
   `--workers=1 --trace off`. Add `cash-shop.spec` to the packaged smoke set for good: the cash shop could not
   open in any packaged folder of your branch (its script files were missing from the data pack) and the
   smoke set did not notice. Tests that import `/js/...` modules or start `dotnet` only work against the
   developer web client, not the package: list which ones those are instead of counting them as failures.
3. Check these by hand in the real game, two browsers, accounts `test1` and `test2`:
   - Open a trade between the two characters. While it is open, use the Jewel Bank, the Item Market, Mount
     Chain salvage and a chat command such as `/code`. Expected: the windows do nothing, the command answers
     "Close the trade or crafting window before using a command." Cancel the trade: both bags are as before.
     Do the same with the Chaos Goblin's window open.
   - `http://localhost:8080/control` on the host works. From another device on the network, and through any
     tunnel or proxy if one is set up, it answers 403.
   - Admin panel `/worlds`: create a second world, Season 9, one free server id; save; restart the server.
     The world and its season are still there (`save/hnmo-worlds.json`). The home page shows both worlds with
     `S6.3` / `S9` badges and a season choice; the game's server list shows the badge. Set the new world's
     EXP to 3 and save: the first world's rate on `/servers` has not changed.
   - Press X in a safe zone: the cash shop opens and lists its packages.
   - Try to list an ancient or socket item on the market: it is refused with a notice in EN and in VI.
   Screenshot each result into `docs/screenshots/antigravity-2026-11-20/`.

## Part B: the Season 9 pack with real files (about a day)

Nobody could test this yet: the real files exist only on this computer.

1. Package again with `HNMO_S9_DATA_DIR` set to the extracted Season 9 `Data` folder. Expected: the files
   are in `HNMO-pkg/Data_S9`, none of them in `HNMO-pkg/Data`. Report the size of `Data_S9`.
2. With the browser's network tab:
   - `/data/seasons.json` says `"s9": true`.
   - A model that exists in both seasons (for example `Player/player.bmd`): `/data/...` and `/data/s9/...`
     give different sizes. `/data/s9/Local/...` and `/data/s9/Interface/...` give the same bytes as without
     `s9/` (those folders never come from the pack).
   - In a Season 6 world no request goes to `/data/s9/`. In the Season 9 world, models, maps and sounds are
     asked under `/data/s9/`, and the game enters Lorencia with no error in the console.
3. Walk through Lorencia, Devias, Noria, Lost Tower and Atlans in the Season 9 world. Every Season 6 map now
   draws with the pack's files. List what looks wrong or fails to load (file name, console error, a
   screenshot), **without copying any file into the repository**.
4. Write a script `client/web/tools/build-s9-pack.mjs` (HNMO's own code) that prepares a pack folder from
   an extracted Season 9 `Data` folder: only the folders `bridge/SeasonData.cs` serves (`WorldN`, `ObjectN`,
   `Monster`, `Item`, `Skill`, `Effect`, `Player`, `NPC`, `Sound`, `Music`), skipping every file that is
   byte-identical to the base game's file of the same path (so the pack holds only what differs), with a
   summary of counts and sizes per folder. Unit-test it with small made-up files. Report the real numbers.
5. Add the Season 9 maps to the developer map viewer only (not to the game's map list): load each of the
   pack's extra `WorldN` folders (see the asset spec, section 2) in the viewer and report per map: terrain,
   objects, textures: loaded or not, with the error. Fix decoder bugs you find in `formats.ts` /
   `modulusCipher.ts`, each with a unit test built from made-up bytes.

## Part C: analyzer cleanup, Wave 3 (two to three days; the long one)

A full `dotnet build server/src/HNMO.sln -c Release` gives about 870 warnings. Bring it to zero **without
changing behaviour**.

1. First the nine that are errors under `-warnaserror` (`HNMO.Interfaces`, `HNMO.Network`,
   `client/web/bridge/VisitorLimits.cs`).
2. Then project by project, smallest first. One commit per project, message
   "Warnings: <project> (<count> fixed)". Allowed fixes: using order, member order, missing XML comments
   (write a real sentence about what the member is for, never a repeat of its name), spacing, file
   headers, unused usings and variables, nullability that is provably right. **Not allowed**: changing
   logic, signatures used by other projects, public names, serialized names, test expectations; adding
   `#pragma warning disable`, `<NoWarn>` or `[SuppressMessage]` (if one truly needs it, leave the warning and
   list it in the report with the reason).
3. After each project: build, and run that project's tests. At the end run the whole solution's tests
   (about 12 minutes; the 18 skipped ones need a real database and stay skipped) and `HNMO.Web.Tests` five
   times in a row. Report the warning count before and after per project, and the final test lines.
4. If a warning points at a real bug (an unawaited task, a comparison that is always false, a value that
   is never used but should be), do not "fix" it silently: list it in the report with file and line, and
   leave it for main Claude.

## Part D: Vietnamese texts (about a day)

`client/web/tools/lang/vi-overrides.json` (and `public/data/lang/vi.json`, which is built from it: read
`tools/lang/build-vi.py` first and change the source, then rebuild) has about 175 game texts without
accents ("Co the thay doi ten nhan vat") and a few joke texts ("dzoooooo!!!"). Rewrite them in natural,
accented Vietnamese that says what the English key says. Keep every placeholder (`%d`, `%s`, `{0}`, `\n`,
`#`) exactly; `tools/lang/check-vi.py` must pass. Do not change keys. Then look through the rest of the
file for texts that name the original game or its website and list them in the report (do not change the
English keys: that is the owner's decision).

## Part E: fixed waits in the browser tests (as far as time allows)

`client/web/tests/e2e` uses `waitForTimeout` in many files (`events-instance`, `text-fit`,
`world-bosses-live`, `item-glow`, `skill-effects`, `quests`, `gens`, `events-field`, ...). Replace each with
a wait for the state the test needs (`expect.poll`, `toPass`, a locator), one file per commit. A test must
pass three times in a row on the developer web client before and after your change. If a wait cannot be
replaced, leave it and say why in a comment. Never loosen an assertion.

## When you finish a part

Push, update `docs/antigravity-report-2026-11-20.md` (what passed, what failed, what you could not check
and why, commands and their last output lines) and `docs/WORKBOARD.md`. After Part E, or when you are
blocked, open a draft pull request from `antigravity-wave3` to `main` and stop. Do not merge it.
