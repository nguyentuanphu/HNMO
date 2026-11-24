# Prompt for Antigravity (24 Nov 2026): red tests, then the audit

Paste everything below the line into Antigravity on the owner's PC. It replaces the earlier version of this file.

---

You are Antigravity, working on HNMO (repo nguyentuanphu/HNMO) on the owner's Windows PC. Answer the owner in
Vietnamese; code, commits and PRs in English; times in Sydney time. The VS Code agent has stopped; you take over its
audit. ChatGPT works on the website (`client/web/src/website*.ts`, `client/web/public/home.html`, `account*`, the
`codex/website-*` branches): do not touch those.

Read first, from `origin/claude/hnmo-main-handover-54co2m`: `docs/owner-decisions.md`,
`docs/vscode-audit-prompt-2026-11-23.md` (the audit task, its steps and rules), `docs/audit/review-audit-full-2026-11-23.md`
and `docs/audit/review-audit-full-2026-11-24.md` (main's reviews of the VS Code work: what to keep, what is wrong).

## Thinking level

Each part below says **[Medium]** or **[High]**. Switch your model to that level before starting the part, and tell
the owner when you switch.

## Rules

1. One DRAFT PR per area into `claude/hnmo-main-handover-54co2m`, from a fresh base. Each fix has a test that fails
   before and passes after. Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
2. Run only the tests of what you touched; GitHub runs everything on the PR. Builds through
   `powershell -File D:\WorkSpace\meshy\tools\heavy.ps1 <command>` if it exists; otherwise one at a time.
3. **Never write "done", "all", "checked" or "100 %" for something you did not finish in full in this session.**
   Every report gives real numbers (units checked of 400, lines read of the total from `docs/audit/coverage.md`,
   suite files read of the total). If unsure, write "not checked".
4. **Never commit a local path or a third-party name** (the original game, its publisher, the suites' or clients'
   vendor names) in any file, commit message or PR text; say "the Season 6 suite", "the Season 9 client". Before every
   push run `git grep -n -i -E "<the vendor names>|D:\\\\"` on your branch and fix anything it finds. Never copy suite or
   client files into the repository.
5. Do not stop between parts to ask whether to go on. Owner questions go in
   `docs/audit/findings/questions-for-owner.md` and you continue. Save, commit and push after every unit; if you stop,
   continue from the first unchecked item in `docs/audit/progress.md`.

## Part 1 [Medium]: the two browser tests broken by your merged PRs

Check the latest "Web game tests" run on `claude/hnmo-main-handover-54co2m` (main started one on 24 Nov). Fix
`client/web/tests/e2e/bottom-hud-bar.spec.ts:25` (from #322) and `client/web/tests/e2e/quest-window-buttons.spec.ts:26`
(from #325): find whether the game or the test is wrong, fix the game when it is the game. One PR.

## Part 2 [Medium]: a clean audit branch

1. Create a new branch `audit-2026-11-24` from a fresh `origin/claude/hnmo-main-handover-54co2m` (do not branch from
   `audit-full-2026-11-23`: its history holds the owner's paths and vendor names).
2. Bring over only what main's reviews say to keep, re-made clean: the fixed `tools/audit/ZzDataDump.cs.txt`
   (attribute names as in `Stats.cs`; also dump Wind and Earth resistance), the Season 6 and Season 9 HNMO dumps,
   the inventory of the four folders (paths relative to each folder, rows with rules only, vendor names replaced by
   `<vendor>`), `progress.md` with variable names only.
3. Fix the suite diff runner: the last Season 6 run read 0 items and 0 monsters from the Season 6 suite
   (`diff-items.txt` / `diff-monsters.txt`: `S6=0`). Find why, rerun Season 6 and Season 9, and check every diff
   header shows non-zero counts on all three sides.
4. Settle the Season 9 items with item keys: the dumps give 848 (Season 6) and 1,112 (Season 9) items, 264
   Season 9-only; compare that list with `SeasonNineOnlyItemsCatalog.cs` (shields and wings use item numbers, not
   groups) and with the suite's Season 9-only items; list each key that differs.
5. Open one DRAFT PR from `audit-2026-11-24`. Close #330 with a comment pointing to the new PR, and tell the owner to
   delete the branch `audit-full-2026-11-23` on GitHub (it still holds the paths and names).

## Part 3 [High]: the code audit, unit by unit (Step 4)

`docs/audit/coverage.json` has 400 units since 24 Nov (the owner added AttributeSystem, GuildServer, FriendServer,
PlugIns, Interfaces and the web bridge). Order: the units "Changed since check", then the rule-heavy server and data
units (combat and damage, the attribute system, skills, items and options, Chaos Machine mixes, drops, events and
mini games, quests, monsters and AI, trade and shops, party, guild, PK, Gens, Castle Siege), then the other server
units, then the web client. **The desktop client comes last, its rule parts only** (owner, 24 Nov: stats, skills,
items, trade, inventory ...); mark its drawing, sound and network units "No game rules" with one line why. Start over with `server/src/GameLogic (files 1)`: the VS Code findings for it are not
valid (they call `AttackableExtensions.cs`, the damage formulas, "no rules").

For each unit:
- read every file in full;
- for every rule or number: what HNMO does (file and line), what the Season 6 suite and the Season 9 suite do (file
  name and value), and same / differs / missing; formulas written out, units (%, per mille, folded values) stated;
- `docs/audit/findings/unit-<unit id>.md` with that table, real bugs marked, owner questions moved to
  `questions-for-owner.md`;
- the unit's row in `docs/audit/coverage.json` (status, checkedAt = commit, references, evidence), then
  `node tools/audit/coverage.mjs`; commit and push.

## Part 4 [High]: the suite rule files nobody reads yet (Step 3.2)

From the inventory, every Season 6 / Season 9 suite file with rules that `tools/audit/suite-parsers` does not read:
quests, all events and invasions, reward bags, master skill tree, Castle Siege, Gens, PK / party / guild limits, then
the rest. For each: a parser, HNMO's matching values, a diff, the file marked read in the inventory.

Also the Season 6 and Season 9 **clients' encoded data tables** (owner, 24 Nov): the files in their data folders that
hold items, skills, quests, gates, maps, NPC texts and similar tables (not only the .xml / .ini files). Mark them
"has rules" in the client inventory, decode them with the tools in `client/tools` or `client/web/tools` where one
exists (write one where not), and compare their values with HNMO's data and with the suites. Never commit the
decoded files; only values and differences.

## Part 5 [Medium]: when the owner says so

The short tasks of the 24 Nov list: the other red browser tests (`pet-zen`, `sound`, `skill-effects` light,
`monster-looks` Season 9 models), the NPC dialogs of the sweep #328 (ObjectMessage for NPCs 371, 235, 406, 241, 229;
NpcWindowResponse kinds 0x07, 0x09, 0x11, 0x17, 0x18, 0x19, 0x25, 0x26; Golden Archer 236), and the small UI items
(vault and trade zen in an in-game dialog instead of `window.prompt`, Alt name toggle, Ultimate HUD minimize, bigger
admin shop icons).

## Part 6 [Medium]: one auto play window (owner, 24 Nov)

The owner: "Gộp 3 thành một autoplay", "Gom vào cửa sổ 1, thêm tab", and Antigravity does it. Today there are three
windows: the desktop helper settings window (`client/web/src/ui/autoPlayWindows.ts`, RML `mu_helper_config`, Z), the
HNMO extras (`autoPlayExtrasPanel.ts`, `hnmo_auto_play_extras.rml`, Shift+Z) and Ultimate Autoplay
(`ultimateAutoPlayWindow.ts`, an HTML panel, Alt+Z, with the small HUD bar). Wiring in `client/web/src/main.ts`.
- Window 1 keeps its three desktop tabs exactly; add two tabs, "Extras" / "Tuỳ chọn thêm" and "Ultimate". Do not edit
  the shared desktop files in `client/src/bin/Data/Interface/RmlUi/` (the C++ client reads them): add the tabs from
  the web side.
- An HNMO tab shows its content in the window body, fitted to the window's width (Ultimate re-laid out in one column;
  its survival / combat / stats sections as a small switcher). Free/VIP locks stay.
- Save saves the desktop settings and the extras / Ultimate settings. Z opens the window; Shift+Z opens it on Extras,
  Alt+Z on Ultimate; the HUD bar's open button opens Ultimate. The old two windows no longer open on their own. The
  small Ultimate HUD bar stays.
- EN / VI texts; update the browser specs that used the old windows without weakening them; add a spec for the five
  tabs, the shortcuts and Save; screenshots of the five tabs in the PR.

## Part 7 [High]: finish the audit for real (main's review, 24 Nov afternoon)

Main checked `audit-2026-11-24` at `20de6828`. Good: the 217 stamped units are back to "Not checked", the vendor file
prefix is gone, the numbers are honest (127 units checked, about 213,000 lines; 209 units, about 563,000 lines, left).
Part 4 is **not** done: most outputs only count things and compare Season 6 with Season 9, never with HNMO, and several
parsers read nothing:
- `diff-castle-siege.txt`: the schedule is all zeros (`cycleDays` [0,...], `stateCount` 0): the parser did not read it.
- `diff-gens.txt`: Season 6 `{}`, Season 9 all zeros: nothing read.
- `diff-quests.txt`: "QuestInfo differences" empty; only counts.
- `diff-master-skills.txt`: Season 6 = 0 (the .dat was not read).
- `diff-events-all.txt`: only which file exists in which season; no values.
- `diff-itembags.txt`: only item counts per bag; not the items, levels, options and rates.
- `diff-suite-rules.txt`: copies database names and default connection settings of the suites (one is a third-party
  name): remove connection, database and password settings from all diffs; they are not game rules.

Do, in this order, one commit per file or unit:
1. Fix each parser above so it reads the real values (check one value by hand against the file and write that check in
   the findings). For every rule file: Season 6 value, Season 9 value **and HNMO's value** (from the HNMO dumps of both
   seasons or HNMO's code, with file:line), marked same / differs / missing in HNMO. Events: entry levels, times,
   durations, player limits, rewards. Item bags: each item, level, option and rate against HNMO's drop groups and boxes.
   Quests: each quest's requirements and rewards. Castle Siege and Gens: schedule, ranks, rewards, limits. Master skill
   tree: each skill's rank, max level, requirements and value per level.
2. The clients' tables (`diff-client-tables.txt`): the same, against HNMO's data and the web client's data files.
3. Part 3, the 209 units left: game data first, then GameServer, DataModel, the web client, then the desktop client's
   rule parts. One findings file per unit with file:line, the suite file's role and value, same / differs.
4. After each step, report from `docs/audit/coverage.md` and the diff headers: units checked of 400, lines read of the
   total, rule files read of the total; never "complete" for a part with an empty or all-zero section.

## Part 8: the other big items (after Parts 7 and 6; the owner, 24 Nov: "còn việc nào lớn nhờ antigravity làm luôn")

For each item, first check on the latest branch whether it is already done (some may be); if so, show where and skip
it. Otherwise one draft PR per item, with tests, EN/VI texts and screenshots.
1. [Medium] One quest window (owner's 22 Nov feedback: two quest windows open for the same thing;
   `client/web/src/ui/questWindows.ts`): one window, as the desktop client's, keeping every function.
2. [Medium] Launchers (owner's 22 Nov feedback): one Windows start file instead of two `.bat` (a setting or an argument
   for the visible console), a macOS start file, the Linux script checked; "silent" mode really stops when asked
   (`client/web/tools/hnmo/`, `client/web/tools/package-hnmo.sh`). Test on Windows; say what could not be tested on macOS.
3. [Medium] Seasons in the admin panel: Season 6 / Season 9 / Arena enabling clear and on one settings page, every
   text in EN / VI / DE / zh-CN.
4. [Medium] Chat, whisper and messenger windows resizable (owner's 22 Nov feedback), size kept per browser.
5. [High] Monsters drawn with missing body parts (owner's 22 Nov feedback) and skill effects that lag: find which
   models and effects, compare with the desktop client's drawing, fix; include the five Season 9 monsters without a
   model (580, 581, 584, 643, 689) and the Arena map (6) layout.
6. [High] HUD reference sweep: the web game's HUD against the Season 6 and Season 9 clients (positions, buttons,
   gauges, menus), a findings list, then the fixes.
7. [Medium] Season 9 checks in a Season 9 world: the browser suites, the skill sweeps of every class, a play test
   (Arena walk after a warp and after a respawn).
8. [Medium] Desktop client: build it, run its tests, check its data against the rules changed since 21 Nov (rank-2
   class items, Horn of Fenrir from second class, level 380 items, second wing options, harmony +14/+15, the Eternal Wing
   set, jewel prices).

## Part 9: after main's review of #348, #359, #362-#364 (24 Nov evening)

Merged by main: #348 (audit), #362 (one auto play window), #363 (one quest window), #364 (launchers). Not merged:
#359 (it repeats #347, already merged, on an old base and conflicts in four files).

1. [Medium] #359: on a fresh branch from the latest `claude/hnmo-main-handover-54co2m`, keep only what #347 did not
   already do (the ground items' Alt toggle, the trade window's in-game zen prompt, anything else new), one PR; close #359.
2. [High] Audit gaps main found in #348:
   - 141 units were moved to "No game rules" to reach 0 "Not checked". Some hold rules: `client/web/src (files 2)`
     (it contains `main.ts`, with party, guild, trade and chat checks), desktop `GameLogic/Buffs` and the other desktop
     GameLogic units. Re-check every unit moved to "No game rules" on 24 Nov: keep it only with a one-line reason that
     names what the files do; otherwise audit it.
   - 11 server units are "Checked" without their own findings file (Bots files 1 and 3, GuildWar, ItemDefinitions,
     ItemFormulas, MiniGames/Doppelganger, MiniGames/ImperialGuardian, PlayerActions/Chat, PlugIns/Chat,
     PlugIns/DuplicateItems, Raklion): write them.
   - The desktop and web client findings are about ten lines for a thousand lines of code: compare each rule the files
     hold (with file:line), not only a summary.
3. [High] From findings to fixes: write `docs/audit/findings/differences-summary.md`, every "differs" and "missing in
   HNMO" from all diff files and unit findings, grouped (master skills: 187 missing, 34 differ; Castle Siege 7 vs 14
   days and friendly fire; episode quests 247 vs 456; event reward bags; and so on), each with a recommendation: a bug to
   fix, a choice for the owner (into `questions-for-owner.md`), or HNMO's own rule to keep. Then one draft PR per area
   for the clear bugs, each with a test that fails before; start with the three in `unit-server-src-GameLogic-files-1.md`
   (Summoner's Fenrir skill damage, the spawn gate random point, the dropped item stack check).
4. Part 8 items 3 to 8 (seasons page, resizable chat, models and effects, HUD sweep, Season 9 checks, desktop client).

After each part, tell the owner in Vietnamese: the PR, what was done, test results, the real audit numbers, what is left.
