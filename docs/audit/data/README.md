# Audit data and raw probe outputs

This folder holds raw machine-readable probe outputs saved from the local audit runs (paths stripped).

## Contents

- [`probes/skillsweep/`](probes/skillsweep/) — 37 `rows.json` dumps from the local skill sweep and effect probes (`tools/local-checks/skillsweep.mjs` and `tools/local-checks/fxprobe.mjs`) across Season 6 and Season 9.
- [`probes/mapcheck/`](probes/mapcheck/) — JSON outputs from the local map, monster, and object checks (`tools/local-checks/mapcheck/`): `audit-s6.json`, `audit-s9.json`, `off-s6.json`, `off-s9.json`, `s9all.json`, `s9list.json`, `spawns.json`.

## Rerunning the probes and suite comparisons

- Set `LEGACY_S6_DIR` (the Season 6 suite root) or `LEGACY_S9_DIR` / `LEGACY_S9_MONSTERS_DIR` (the Season 9 suite root / monster folder) in your shell before running local comparison scripts.
- Skill sweep and effect probes: see [`tools/local-checks/README.md`](../../../tools/local-checks/README.md).
- Map and monster checks: see `tools/local-checks/mapcheck/` and [`docs/map-check-2026-11-20.md`](../../map-check-2026-11-20.md).
