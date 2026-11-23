# Prompt for the local VS Code agent: full Season 6 / Season 9 audit (23 Nov 2026)

Paste everything below the line into the VS Code agent chat, in the HNMO checkout on the owner's PC.
If the agent stops, paste only the last section ("If you stopped") again.

---

You are the local audit agent for HNMO, on the owner's Windows PC, in VS Code. Answer the owner in Vietnamese;
write files, commits and PRs in English. Times in Sydney time (Australia/Sydney).

## The goal

The owner's decision (23 Nov): audit **everything**, in priority order ("Hết, theo thứ tự ưu tiên"). That means:

1. every file of the Season 6 server suite, the Season 9 server suite and both game clients (the folders the owner
   points you to) that holds game data or game rules;
2. every code unit of HNMO (`docs/audit/coverage.json`, 389 units today: 20 checked, 13 changed since their check,
   349 not checked; about 3 % of lines done);
3. compare them: for each rule or value, does HNMO do the same as Season 6, and in Season 9 mode the same as
   Season 9? Record every difference.

This is an **audit**: read, compare, record. Do not change game code. Fixes come later as separate PRs, after the
owner decides.

## Folders

Ask the owner once, at the start, for the four folders and write them in `docs/audit/progress.md` (only as
`LEGACY_S6_DIR`, `LEGACY_S9_DIR`, `LEGACY_S6_CLIENT_DIR`, `LEGACY_S9_CLIENT_DIR`; never write the real paths into a
committed file). Set them as environment variables for the scripts. The HNMO repository is the VS Code workspace.

## Rules you must keep

1. **Never stop before the task is finished.** Work in batches; after each batch, save, commit and go straight on to
   the next batch without asking. Stop only when every item in `docs/audit/progress.md` is done, or when something
   blocks you (a missing folder, a failing build you can't fix). Then write what blocks you and keep going with the
   items that are not blocked.
2. **Save as you go.** `docs/audit/progress.md` is the single checklist. Update it after every batch (at most 20
   files or one code unit per batch). Commit after every batch. If you are restarted, you continue from it; nothing
   may live only in the chat.
3. **Never say something is read, checked or done unless you read it in full in this run.** Every report gives the
   real numbers: files read of total, lines read of total, units checked of total. A file you only skimmed or
   searched is "not read".
4. **No third-party names** in any committed file, commit or PR: say "the Season 6 suite", "the Season 9 suite",
   "the Season 6 client", "the Season 9 client". **Never copy suite or client files into the repository**: write
   only values, rule descriptions and file names.
5. Owner questions do not stop the work: write each in `docs/audit/findings/questions-for-owner.md` (the rule, what
   Season 6 does, what Season 9 does, what HNMO does, your recommendation) and go on.
6. Builds and tests on this PC go through `powershell -File D:\WorkSpace\meshy\tools\heavy.ps1 <command>` (one heavy
   job at a time). In a fresh checkout build `server/src/Persistence/SourceGenerator` (Debug) first.
7. Work on a branch `audit-full-2026-11-23` from a fresh `origin/claude/hnmo-main-handover-54co2m`; push after every
   few batches; open one DRAFT PR into `claude/hnmo-main-handover-54co2m` at the start and keep pushing to it.
   Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Steps (in this order)

### Step 1: inventory (do this fully before anything else)

1. List **every file** in the four folders (recursive): path relative to its folder, size, type. Save it as
   `docs/audit/inventory/s6-suite.csv`, `s9-suite.csv`, `s6-client.csv`, `s9-client.csv` (columns: `path, bytes,
   kind, has_rules, status`). `kind` = data table, rule config, script, binary data, art/sound, program, other.
   `has_rules` = yes for anything that holds game values or rules (items, skills, monsters, spawns, maps, gates,
   shops, mixes, sockets, sets, options, EXP, events, quests, Castle Siege, Gens, drops and reward bags, master
   skills, PK, party, guild, prices, cash shop, VIP, limits, client data tables). `status` = not read.
2. Mark the 28 kinds the existing parsers read (`tools/audit/suite-parsers/suite-files.mjs`).
3. Write the totals in `progress.md`: files per folder, files with rules, files already parsed. Show the owner this
   summary, then go on.

### Step 2: HNMO's own data, Season 6 and Season 9

1. Dump HNMO's data with `tools/audit/ZzDataDump.cs.txt` (how: `docs/audit/data/README.md`) twice: normally
   (Season 6) and with `HNMO_SEASON=s9` into `docs/audit/data/hnmo-s9/`.
2. Check that the Season 9 dump really has the Season 9 layer (`server/src/Persistence/Initialization/Updates/
   SeasonNine/`: Season 9-only items, the 111 monsters, the Season 9 maps). If it has the same counts as Season 6,
   the dump does not run the update plug-ins: extend the dump test so it applies them, and say so.
3. Explain why the Season 9 plan says 281 Season 9-only items and the code adds 279.

### Step 3: data tables, suite against HNMO

1. Rerun `node tools/audit/suite-parsers/diff-all.mjs` for Season 6 (suite S6 against HNMO S6) and add the run for
   Season 9 (suite S9 against HNMO S9), writing `docs/audit/data/diff-*.txt` and `docs/audit/data/s9/diff-*.txt`.
2. For every rule file of Step 1 that no parser reads yet: write a parser (in `tools/audit/suite-parsers/`), dump
   HNMO's matching data, diff, and mark the file "read" in the inventory. Priority: events (all of them), quests,
   drops and reward bags, master skill tree, Castle Siege, Gens, PK / party / guild limits, then the rest.
3. Each diff row: the key, the value in Season 6, in Season 9, in HNMO; mark "same", "differs", "missing in HNMO",
   "HNMO only".

### Step 4: HNMO code, unit by unit

1. Run `node tools/audit/coverage.mjs`. Take the units in this order: "Changed since check" first, then the
   rule-heavy "Not checked" units (combat, skills, items, mixes, events and mini games, quests, monsters and AI,
   drops, trade and shops, party, guild, PK, Gens, Castle Siege), then all other units.
2. For each unit: read **every file in full**; for every rule write what HNMO does and what the Season 6 / Season 9
   suites do (file name and the value, not the file's text); record differences and bugs in
   `docs/audit/findings/unit-<unit id>.md`.
3. Update the unit's row in `docs/audit/coverage.json` (`status: "Checked"`, `checkedAt` = the commit,
   `references` = the suite files you compared, `evidence` = the findings file) and run the script again. Units
   without game rules: `status: "No game rules"`, with one line why.

### Step 5: report

After every step, and at the end, write `docs/audit/report-<date>.md` and tell the owner (in Vietnamese):
the real numbers (files read of total per folder, units checked of total, lines read of total), the biggest
differences found, the questions for the owner, and what is left.

## `docs/audit/progress.md` (create it first)

A checklist with one line per item: Step 1 per folder; Step 2 items; Step 3 one line per rule file; Step 4 one line
per unit. Each line: `- [ ]` or `- [x]`, the item, and when done the commit and the findings file. At the top: the
totals and the time of the last update (Sydney).

## If you stopped (paste this again to resume)

Read `docs/audit/progress.md`. Do not start over and do not ask whether to continue: take the first item that is not
`[x]`, finish it, save, commit, and go on until every item is `[x]`. Report the real numbers only when a step ends.
