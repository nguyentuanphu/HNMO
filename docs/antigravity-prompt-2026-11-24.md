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
   Every report gives real numbers (units checked of 390, lines read of the total from `docs/audit/coverage.md`,
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

Order: the units "Changed since check", then the rule-heavy units (combat and damage, skills, items and options,
Chaos Machine mixes, drops, events and mini games, quests, monsters and AI, trade and shops, party, guild, PK, Gens,
Castle Siege), then the rest. Start over with `server/src/GameLogic (files 1)`: the VS Code findings for it are not
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

## Part 5 [Medium]: when the owner says so

The short tasks of the 24 Nov list: the other red browser tests (`pet-zen`, `sound`, `skill-effects` light,
`monster-looks` Season 9 models), the NPC dialogs of the sweep #328 (ObjectMessage for NPCs 371, 235, 406, 241, 229;
NpcWindowResponse kinds 0x07, 0x09, 0x11, 0x17, 0x18, 0x19, 0x25, 0x26; Golden Archer 236), and the small UI items
(vault and trade zen in an in-game dialog instead of `window.prompt`, Alt name toggle, Ultimate HUD minimize, bigger
admin shop icons).

After each part, tell the owner in Vietnamese: the PR, what was done, test results, the real audit numbers, what is left.
