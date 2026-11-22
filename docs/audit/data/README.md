# Audit data, table diffs, and raw probe outputs

This folder holds HNMO's dumped configuration tables, table-by-table differences against the Season 6 and Season 9 server suites, and raw machine-readable probe outputs (all third-party names and local paths stripped).

## Contents

- [`hnmo/`](hnmo/) — HNMO's initialized Season 6 configuration dumped to CSV (`items.csv`, `item_options.csv`, `socket_options.csv`, `ancient_sets.csv`, `skills.csv`, `monsters.csv`, `spawns.csv`, `maps.csv`, `enter_gates.csv`, `exit_gates.csv`, `warps.csv`, `shops.csv`, `mixes.csv`, `events.csv`, `exp_and_globals.csv`).
- `diff-*.txt` — Table-by-table difference reports produced by comparing `hnmo/*.csv` with the Season 6 and Season 9 server suites (`diff-items.txt`, `diff-item-tables.txt`, `diff-skills.txt`, `diff-monsters.txt`, `diff-spawns.txt`, `diff-gates.txt`, `diff-shops.txt`, `diff-mixes.txt`, `diff-socket-options.txt`, `diff-exp-events.txt`).
- [`probes/skillsweep/`](probes/skillsweep/) — 37 `rows.json` dumps from the local skill sweep and effect probes (`tools/local-checks/skillsweep.mjs` and `tools/local-checks/fxprobe.mjs`) across Season 6 and Season 9.
- [`probes/mapcheck/`](probes/mapcheck/) — JSON outputs from the local map, monster, and object checks (`tools/local-checks/mapcheck/`): `audit-s6.json`, `audit-s9.json`, `off-s6.json`, `off-s9.json`, `s9all.json`, `s9list.json`, `spawns.json`.

## How to rerun the HNMO dump (`ZzDataDump`)

1. Copy [`tools/audit/ZzDataDump.cs.txt`](../../../tools/audit/ZzDataDump.cs.txt) to `server/tests/HNMO.Persistence.Initialization.Tests/ZzDataDump.cs`.
2. Run the explicit NUnit test from the repository root:
   ```bash
   dotnet test server/tests/HNMO.Persistence.Initialization.Tests/HNMO.Persistence.Initialization.Tests.csproj --filter ZzDataDump
   ```
   Optionally set `HNMO_DUMP_OUT` to write CSVs to a custom folder. Delete the temporary `ZzDataDump.cs` copy when done.

## How to rerun the suite diffs (`suite-parsers`)

1. Set `LEGACY_S6_DIR` (the Season 6 suite root) and `LEGACY_S9_DIR` (the Season 9 suite root) in your shell.
2. Run the table parser and diff runner:
   ```bash
   node tools/audit/suite-parsers/diff-all.mjs
   ```
   This regenerates all `docs/audit/data/diff-*.txt` files using `docs/audit/data/hnmo/*.csv` and both suite directories without copying any suite files into the repository.

## Rerunning probes

- Set `LEGACY_S6_DIR` / `LEGACY_S9_DIR` / `LEGACY_S9_MONSTERS_DIR` in your shell before running local comparison scripts.
- Skill sweep and effect probes: see [`tools/local-checks/README.md`](../../../tools/local-checks/README.md).
- Map and monster checks: see `tools/local-checks/mapcheck/` and [`docs/map-check-2026-11-20.md`](../../map-check-2026-11-20.md).
