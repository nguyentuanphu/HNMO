# Audit Findings — `server/src/GameLogic/PlugIns/MountChain`

- **Unit**: `server/src/GameLogic/PlugIns/MountChain` (9 files, 1,123 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `MountAccountState.cs`, `MountBagItem.cs`, `MountChainConfiguration.cs`, `MountChainEngine.cs`
  - `MountChainPlugIn.cs`, `MountChainStateWriter.cs`, `MountChainStore.cs`, `MountChainStoreData.cs`, `MountTierDefinition.cs`

## Rules & Behavior
- Custom progression chain (`IDisabledByDefault`, `Shift+U`): Uniria (`13:2`, 3 shards, 500k Zen) -> Dinorant (`13:3`, 6 shards, 1x Uniria, 1.5M) -> Fenrir (`13:37`, 12 shards, 1x Dinorant, 3M) -> Golden Fenrir (`13:37` lv4, 20 shards, 2x Fenrir, 6M) -> Royal Steed (`13:4`, 30 shards, 1x Golden Fenrir, 10M).
- Golden Boss kills award `ShardsPerGoldenBoss = 2` (`100%` default chance); salvage awards `2/4/8/12/16` shards; owed item queue delivers if bag was full at craft time.

## Findings & Observations
1. **Spec Mismatch — `MountChainPlugIn.cs:352-355` ("PvE-only" bonus applies to PvP too)**:
   - Docs/summary state tiers grant `"modest PvE-only bonuses"`, but `ApplyActiveBuffAsync` adds `tier.PveDefenseBonus` (`5..40`) via `Stats.DefenseFinal` (`AggregateType.AddFinal`), which affects PvP defense as well as PvE.
2. **Observation — `MountChainPlugIn.cs:96` & `176-187`**:
   - Golden Boss shard drops require `killer is Player` (summoned monster kills don't credit owner, unlike `GameEventPublisherPlugIn`).
   - `CompleteCraft` persists spent shards & unlock immediately to `HnmoStateStore`, while bag item removal & forged item addition wait for player persistence save.
