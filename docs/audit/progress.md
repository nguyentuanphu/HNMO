# HNMO Full Season 6 / Season 9 Audit Progress

**Branch:** `audit-full-2026-11-23` from `origin/claude/hnmo-main-handover-54co2m`
**Started:** 2026-11-23 14:23:57 (Australia/Sydney)
**Last updated:** 2026-11-23 14:23:57 (Australia/Sydney)

## Folder Paths (environment variables only)

- `LEGACY_S6_DIR` = D:\WorkSpace\MuOnline\IGCN\IGCN x6\Evi.Network.IGC.Server Suite.x6.0.0.1
- `LEGACY_S9_DIR` = D:\WorkSpace\MuOnline\IGCN\IGCN x9\Evi.Network.IGC.Server Suite.x9
- `LEGACY_S6_CLIENT_DIR` = D:\WorkSpace\MuOnline\IGCN\IGCN x6\IGC.Evi.Network x6.3 6.0.0.1 Full Client
- `LEGACY_S9_CLIENT_DIR` = D:\WorkSpace\MuOnline\IGCN\IGCN x9\IGC.Evi.Network x9 Full Client

## Totals (to be filled after inventory)

- Season 6 suite: 463 files total, 259 with rules, 28 already parsed (parsers cover: items, skills, monsters, spawns, gates/warps, shops, mixes, socket options, item tables, exp/events)
- Season 9 suite: 508 files total, 274 with rules, 28 already parsed (same parsers)
- Season 6 client: 14121 files total, 15 with rules, 0 already parsed
- Season 9 client: 18990 files total, 18 with rules, 0 already parsed
- HNMO code units: 390 total, 19 checked, 14 changed since check, 350 not checked

---

## Step 1: Inventory

### Season 6 Server Suite
- [x] List every file in LEGACY_S6_DIR → `docs/audit/inventory/s6-suite.csv` (commit: )
- [x] Mark 28 kinds from existing parsers
- [x] Write totals to progress.md

### Season 9 Server Suite
- [x] List every file in LEGACY_S9_DIR → `docs/audit/inventory/s9-suite.csv` (commit: )
- [x] Mark 28 kinds from existing parsers
- [x] Write totals to progress.md

### Season 6 Client
- [x] List every file in LEGACY_S6_CLIENT_DIR → `docs/audit/inventory/s6-client.csv` (commit: )
- [x] Mark 28 kinds from existing parsers
- [x] Write totals to progress.md

### Season 9 Client
- [x] List every file in LEGACY_S9_CLIENT_DIR → `docs/audit/inventory/s9-client.csv` (commit: )
- [x] Mark 28 kinds from existing parsers
- [x] Write totals to progress.md

---

## Step 2: HNMO's Own Data (Season 6 and Season 9)

- [ ] Dump HNMO data normally (Season 6) using `tools/audit/ZzDataDump.cs.txt`
- [ ] Dump HNMO data with `HNMO_SEASON=s9` into `docs/audit/data/hnmo-s9/`
- [ ] Verify Season 9 dump has Season 9 layer (281 items, 111 monsters, Season 9 maps)
- [ ] Explain 281 vs 279 Season 9-only items discrepancy

---

## Step 3: Data Tables - Suite vs HNMO

- [ ] Run `diff-all.mjs` for Season 6 (suite S6 vs HNMO S6) → `docs/audit/data/diff-*.txt`
- [ ] Run `diff-all.mjs` for Season 9 (suite S9 vs HNMO S9) → `docs/audit/data/s9/diff-*.txt`
- [ ] For each unparsed rule file: write parser, dump HNMO data, diff, mark "read"
  - Priority: events, quests, drops/reward bags, master skill tree, Castle Siege, Gens, PK/party/guild limits
  - Each diff row: key, S6 value, S9 value, HNMO value, status (same/differs/missing in HNMO/HNMO only)

---

## Step 4: HNMO Code Units (unit by unit)

Order: "Changed since check" first, then rule-heavy "Not checked" (combat, skills, items, mixes, events/mini-games, quests, monsters/AI, drops, trade/shops, party, guild, PK, Gens, Castle Siege), then all others.

For each unit:
- [ ] Read every file in full
- [ ] For every rule: write what HNMO does vs Season 6/9 suites (file name + value)
- [ ] Record differences/bugs in `docs/audit/findings/unit-<unit id>.md`
- [ ] Update `coverage.json`: status, checkedAt, references, evidence
- [ ] Run coverage script again

---

## Step 5: Report

After every step and at end:
- [ ] Write `docs/audit/report-<date>.md`
- [ ] Report real numbers (files read/total, units checked/total, lines read/total)
- [ ] Biggest differences found
- [ ] Questions for owner
- [ ] What remains