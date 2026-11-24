# Audit Findings — `server/src/GameLogic/PlugIns/Talismans`

- **Unit**: `server/src/GameLogic/PlugIns/Talismans` (10 files, 873 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `StatConsumableDefinitionConfig.cs`, `StatConsumeDecision.cs`, `TalismanBagSummary.cs`, `TalismanCharacterRecord.cs`
  - `TalismanConfiguration.cs`, `TalismanDefinitionConfig.cs`, `TalismanEngine.cs`, `TalismanPlugIn.cs`
  - `TalismanStore.cs`, `TalismanStoreData.cs`

## Rules & Behavior
- Custom plug-in (`IDisabledByDefault`) granting passive buffs/bonuses while carrying configured items in main inventory slots (`>= 12, < FirstStoreItemSlotIndex`), e.g. `13:81` (+50 Def, +150 HP, +5% EXP) and `14:53` (+5% EXP, +10% Zen, +25 Dmg), plus capped stat-point consumables (`GlobalStatPointsCap = 5000`, default `14:91` +10 `levelUpPoints`).

## Findings & Observations
1. **Missing Max-Stat Cap Check — `TalismanPlugIn.cs:291-296`**:
   - When `TargetBucket` is `str`/`agi`/`vit`/`ene`/`cmd`, `player.Attributes[targetAttr] += decision.PointsGranted` does not clamp against `targetAttr.MaximumValue`. Points exceeding `MaximumValue` are capped/wasted on read while still counting toward `TotalExtraStatPoints`.
2. **State Desync on Crash — `TalismanPlugIn.cs:304-309`**:
   - `store.RecordGranted(...)` flushes to persistent `TalismanStore` immediately, whereas `item.Durability -= 1` and added stats/points rely on periodic player save (`ItemConsumeAction` does not call `SaveProgressAsync` here).
3. **Bag Stack Count vs Durability — `TalismanEngine.cs:47-59`**:
   - When `StackCopies` is enabled, stacks are counted by inventory slots (`matches.Count`), not `Durability`.
