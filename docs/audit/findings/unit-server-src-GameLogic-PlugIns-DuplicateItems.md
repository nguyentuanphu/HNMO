# Audit Findings: `server/src/GameLogic/PlugIns/DuplicateItems`

**Status**: Checked (all 7 files read in full, 462 lines)
**References**: Owner decision (2026-11-20), [`ledger-combat-items-events-2026-11-22.md`](ledger-combat-items-events-2026-11-22.md)

## Scope Covered
- `DuplicateItemCase.cs` (32 lines)
- `DuplicateItemCaseEntry.cs` (27 lines)
- `DuplicateItemsConfiguration.cs` (25 lines)
- `DuplicateItemsData.cs` (18 lines)
- `DuplicateItemsEngine.cs` (90 lines)
- `DuplicateItemsPlugIn.cs` (197 lines)
- `DuplicateItemsStore.cs` (66 lines)

## Rule & Design Comparison

| File & Line | Rule / Behavior | Suite / Decision Reference | Verdict |
|---|---|---|---|
| `DuplicateItemsConfiguration.cs:16-24` | `ScanIntervalMinutes` clamped to `[5..1440]` (default `60`); `FreezeDuplicates = true`; persists in `HnmoStateStore` under section `"duplicateItems"`. | HNMO custom anti-dupe subsystem (owner decision 2026-11-20; S6/S9 suites rely on sequential item serial numbers in SQL/ItemSerial without runtime freeze cases). | HNMO custom |
| `DuplicateItemsPlugIn.cs:168-196` | On `PlayerState.EnteredWorld`, `GiveSerials` assigns a UUID `Serial` (`item.EnsureSerial()`) to any inventory or vault item missing one. | Ensures all legacy or newly created items entering play are serialized before persistence. | HNMO custom |
| `DuplicateItemsPlugIn.cs:84-113,136-158` | Periodic task acquires static `_scanning` CAS lock, assigns missing DB serials, queries duplicate serials (`FindDuplicates()`), records/updates open cases via `DuplicateItemsEngine.Record`, and refreshes `_frozen`. | Prevents concurrent multi-thread scans; clamps scan period with `Math.Max(5, cfg.ScanIntervalMinutes)`. | HNMO custom |
| `DuplicateItemsPlugIn.cs:43-64` | `IsFrozen` / `RefuseFrozenAsync` blocks frozen serials (open cases when `_freeze == true`) from trade, sale, drop, vault, store, market, escrow, and mix, with EN/VI warning message. | Admin resolves open cases on the Duplicate Items admin page via `DuplicateItemsEngine.Resolve`. | HNMO custom |
| `DuplicateItemsEngine.cs:22-58,76-89` | Groups duplicates by `Serial`, skips singletons (`entries.Count < 2`), preserves resolved cases so admin decisions stay final, and updates open case entries on re-scan. | Idempotent across scans (`Resolve` clears freeze immediately upon admin decision). | Sound |
| `DuplicateItemsStore.cs:28-65` | Wraps JSON state under `this._store.SyncRoot` lock and falls back to an isolated temp path when `filePath` is blank (tests). | Thread-safe atomic read/mutate. | Sound |

## Bugs / Open Issues
- None critical. Note (`DuplicateItemsPlugIn.cs:27-30`): `_frozen` / `_freeze` / `_scanning` / `_nextScanUtc` are `static` per process, whereas `FreezeDuplicates` is configured per game server context; in multi-game-server processes with mixed settings, the most recently refreshing plugin's config wins.
