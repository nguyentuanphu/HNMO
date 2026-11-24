# HNMO Full Season 6 / Season 9 Audit Progress

**Branch:** `audit-2026-11-24` from `origin/claude/hnmo-main-handover-54co2m`
**Updated:** 2026-11-24 11:51 (Australia/Sydney)

## Folder Variables (environment variables only — never commit local paths)

- `LEGACY_S6_DIR` = Season 6 suite folder
- `LEGACY_S9_DIR` = Season 9 suite folder
- `LEGACY_S6_CLIENT_DIR` = Season 6 client folder
- `LEGACY_S9_CLIENT_DIR` = Season 9 client folder

## Totals

- Season 6 suite inventory (`docs/audit/inventory/s6-suite.csv`): 374 rows with `has_rules = yes|maybe` (28 parsed by `suite-parsers`)
- Season 9 suite inventory (`docs/audit/inventory/s9-suite.csv`): 400 rows with `has_rules = yes|maybe` (28 parsed by `suite-parsers`)
- Season 6 client inventory (`docs/audit/inventory/s6-client.csv`): 25 rows with `has_rules = yes|maybe` (0 parsed)
- Season 9 client inventory (`docs/audit/inventory/s9-client.csv`): 30 rows with `has_rules = yes|maybe` (0 parsed)
- HNMO Season 6 dump (`docs/audit/data/hnmo/`): 848 items, 504 monsters (with WindRes & EarthRes), 73 maps
- HNMO Season 9 dump (`docs/audit/data/hnmo-s9/`): 1,112 items (264 S9-only), 615 monsters, 83 maps
- HNMO code units (`docs/audit/coverage.json`): 400 total

---

## Step 1: Inventory
- [x] `docs/audit/inventory/s6-suite.csv` (relative paths, rules rows only, `<vendor>` sanitized)
- [x] `docs/audit/inventory/s9-suite.csv` (relative paths, rules rows only, `<vendor>` sanitized)
- [x] `docs/audit/inventory/s6-client.csv` (relative paths, rules rows only, `<vendor>` sanitized)
- [x] `docs/audit/inventory/s9-client.csv` (relative paths, rules rows only, `<vendor>` sanitized)

## Step 2: HNMO Dumps & Season 9 Item Reconciliation
- [x] Fix `tools/audit/ZzDataDump.cs.txt` (`Stats.cs` names + `Wind Resistance`, `Earth Resistance`)
- [x] Regenerate Season 6 (`docs/audit/data/hnmo/`) and Season 9 (`docs/audit/data/hnmo-s9/`) dumps
- [x] Reconcile 264 Season 9-only items vs `SeasonNineOnlyItemsCatalog.cs` (290 entries) and suite (263 S9-only) → `docs/audit/findings/s9-items-reconciliation.md`

## Step 3: Suite Diffs & Remaining Rule Parsers
- [x] Run `diff-all.mjs` for Season 6 (`HNMO=848, S6=1003, S9=1260`; `HNMO=504, S6=625, S9=654`; `HNMO=316, S6=403, S9=403`)
- [x] Run `diff-all.mjs` for Season 9 (`HNMO=1112, S6=1003, S9=1260`; `HNMO=615, S6=625, S9=654`; `HNMO=316, S6=403, S9=403`)
- [ ] Step 3.2: Parse remaining suite rule files (quests, events/invasions, reward bags, master skill tree, Castle Siege, Gens, PK/party/guild limits) and encoded client data tables

## Step 4: Code Audit (400 units in `docs/audit/coverage.json`)
- [ ] Audit units in order: `Changed since check` → rule-heavy server & data units → other server units → web client → desktop client rule parts
