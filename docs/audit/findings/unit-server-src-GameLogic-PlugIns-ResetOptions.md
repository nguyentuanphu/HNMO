# Unit Audit Findings — `server/src/GameLogic/PlugIns/ResetOptions`

- **Unit**: `server/src/GameLogic/PlugIns/ResetOptions` (21 files, 2,505 lines)
- **Checked on**: 2026-11-24

## Scope Covered

- `DelegatedResetOptions.cs`, `RelifeOptions.cs`, `ResetApplier.cs`, `ResetCharacterRecord.cs`, `ResetCoinCost.cs`, `ResetCoinHistory.cs`, `ResetEngine.cs`, `ResetItemCost.cs`, `ResetLogEntry.cs`, `ResetOptionsConfiguration.cs`, `ResetOptionsPlugIn.cs`, `ResetPlan.cs`, `ResetRankingEntry.cs`, `ResetStateWriter.cs`, `ResetStore.cs`, `ResetStoreData.cs`, `ResetSubject.cs`, `ResetTexts.cs`, `ResetTier.cs`, `ResetVipOptions.cs`, `WebsiteResetOptions.cs`

## Comparison Against Legacy Baseline

- Standard Season 6/9 configurations only include a simple reset-item table (`cat/index/reqReset`); HNMO's `ResetOptions` is an intentional custom progression suite (tiered reset requirements/rewards, VIP reductions, Relife/Grand Reset with permanent bonus points, offline Delegated Reset, monthly/relife rankings, and website integration).
- Legacy `Resets` behavior matches expectations: `Total` mode sums tier points across `0..resets-1` plus `relifes * PointsPerRelife` and VIP `ExtraPointsPercent`; `ZenTimesResetNumber` uses `long` arithmetic in `ResetEngine.PlanReset` (`line 281`) and `ResetOptionsPlugIn.TryPayAsync` checks `plan.Zen > int.MaxValue` (`line 389`).

## Findings & Bugs

1. **Off-by-one spawn coordinate on logout reset (`ResetOptionsPlugIn.cs:490-491`)**:
   - In `DoAsync`, when `configuration.LogOut && configuration.MoveHome`, the spawn position uses `(byte)Rand.NextInt(gate.X1, gate.X2)` and `(byte)Rand.NextInt(gate.Y1, gate.Y2)`. Because `IRandomizer.NextInt(min, max)` has an exclusive upper bound, `X2`/`Y2` are never chosen (unlike `gate.GetRandomPoint()` used in `DoAsync`'s non-logout branch or `WarpToAsync`).
2. **Stacked item consumption in `ResetApplier` (`ResetApplier.cs:35, 49`, `ResetOptionsPlugIn.cs:414-417`)**:
   - `Count` and `PickItems` count item instances (`items.Count(...)`, `Take(cost.Count)`) without accounting for `Durability` on stackable items, and `DestroyInventoryItemAsync` deletes the whole slot item. If a tier configures a stackable jewel/consumable cost, a stack of 10 counts as 1 piece and all 10 are destroyed.
3. **Other logic reviewed (OK)**:
   - `TryPayAsync` properly rolls back zen (`TryAddMoney`) if `TryTakeCoins` fails (`lines 399-407`) and records coin balance receipts (`ResetCoinHistory.Record`).
   - `ResetApplier.Apply` caps `LevelUpPoints` against `int.MaxValue`, keeps `keepBuild` stat allocations clamped to `MaximumValue - BaseValue`, and returns unallocated remainder to `LevelUpPoints`.
   - `ResetEngine` uses `MissionClock` (Sydney day/month rollover, 24 months retained) and `lock (Store.Lock)` cleanly.
