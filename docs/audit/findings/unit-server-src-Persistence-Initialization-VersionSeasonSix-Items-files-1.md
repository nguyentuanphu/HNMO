# Unit Audit: `server/src/Persistence/Initialization/VersionSeasonSix/Items (files 1)`

- **Checked at**: `49a5970d` (2026-11-24)
- **Status**: `Checked`
- **Reference saved**: [item-options-380-harmony-cashshop.md](../references/item-options-380-harmony-cashshop.md)
- **Files read in full (19)**:
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/AncientSets.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/Armors.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/BoxOfLuck.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/BuffItems.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/CashShopItems.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/CraftingTalismans.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/EventTicketItems.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/GensRewardItems.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/GoblinBoxItems.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/GuardianOptions.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/HarmonyOptions.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/ItemRules.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/Jewelery.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/Jewels.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/LuckyItems.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/Misc.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/MossGambleItemCatalog.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/Orbs.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/PackedJewels.cs`

## Discrepancies & Findings
1. **Kantata Ancient Set Excellent Damage Chance = `1000%` (`AncientSets.cs:199`) — Bug**:
   - Line 199 sets `(Stats.ExcellentDamageChance, 10.0f, AggregateType.AddRaw)`. Everywhere else in `AncientSets.cs`, percentage chances use `0..1` fractions (`0.05f`, `0.10f`, `0.15f`), and S6/S9 `the suite's ancient set option file` both specify `FullOptIdx2="17" FullOptVal2="10"` (`10%`). With `10.0f`, wearing full Kantata guarantees 100% Excellent hits.
2. **Blue Chocolate Box & Deep Blue Candy Box Normal Item/Jewel Pools Dropped (`BoxOfLuck.cs:924-1052`) — Bug**:
   - In `CreateBlueChocolateBox()` (`14:34`), `blueBox` (`SourceItemLevel = 0`, `Chance = 0.4`, lines 927–957) and `deepBlueBox` (`SourceItemLevel = 1`, `Chance = 0.4`, lines 990–1020) are constructed and populated, but **`box.DropItems.Add(blueBox)` and `box.DropItems.Add(deepBlueBox)` are never called**.
   - As a result, dropping `14:34` (+0 or +1) only registers `blueBoxExc` / `deepBlueBoxExc` (`0.1` chance) and the money fallback (`1.0` chance), never dropping any of the normal items or jewels (`12:15`, `14:13..16,22,42`) defined in `blueBox` / `deepBlueBox`.
3. **Guardian Lv380 Boots Option Max SD `+200` vs `+700` (`GuardianOptions.cs:106`) — Bug**:
   - Line 106 configures Boots (`GuardianOptionBoots`) with `Stats.MaximumShield, 200`, whereas both S6 (`the suite's item option manager file`) and S9 (`the suite's 380 item option file`) define `Option1="6" Value1="700"` (`Max SD increase +700`).
4. **Seals missing `+150%` Drop Rate (`BuffItems.cs:106, 109, 111`) — Rule Difference**:
   - S6/S9 `the suite's item option manager file` gives `Seal of Wealth` (`13:44`), `Master Seal of Wealth` (`13:94`), and `Seal of Divinity` (`13:63`) `Option = 7, Value = 150` (`Drop Rate Increase +150%`), which is absent in `BuffItems.cs` (`ExperienceRate`/`MasterExperienceRate` and `ManaRecoveryMultiplier` only).
5. **Item Rules missing `13:71..75` & `14:153` (`ItemRules.cs`, `Misc.cs`, `MossGambleItemCatalog.cs`) — Rule Difference**:
   - S9 suite item list blocks `Trade`, `Drop`, `Store`, `PersonalStore`, `SellToNPC`, `Repair` on Moss gamble vouchers (`13:71..75`) and blocks `Trade`, `Drop`, `Store`, `PersonalStore`, `Repair` on `Stardust` (`14:153`, `Overlap="20"`), while `ItemRules.cs` has no entries for either yet (`Misc.cs` also sets `Durability = 1` for Stardust).
6. **Cash Shop Jewelry (`Jewelery.cs:73-137`)**:
   - `13:109..115` intentionally match client tooltip stats rather than legacy suite `the suite's item option manager file` values (`+3%` vs `+8%` HP rec, etc.).
7. **Other verified items in unit**:
   - `Armors.cs`, `CashShopItems.cs`, `CraftingTalismans.cs`, `EventTicketItems.cs`, `GensRewardItems.cs`, `GoblinBoxItems.cs`, `HarmonyOptions.cs` (level 0..13 match S9 `the suite's Harmony item option file`; +14/+15 handled via `HarmonyLevels14And15UpdatePlugIn`), `Jewels.cs`, `LuckyItems.cs` (`Lucky Atlans` skips Helm 71, `Lucky Storm Jahad` skips Gloves 72, matching `ItemRules.cs`), `Orbs.cs`, `PackedJewels.cs` are internally consistent.

## Re-check 2026-11-27 (changes 49a5970d..a108e846)

Commits read (every changed file read in full):

- 1fc60d72 fix(init): fix Kantata exc chance, Blue/DeepBlue boxes, Guardian boots SD, GM DL slots, and Hanzo store

Evidence:

- AncientSets Kantata: ExcellentDamageChance 0.10 (was 10.0 = every hit excellent: AttackableExtensions uses Rand.NextRandomBool(chance)) - fixed for new databases; existing ones by AuditDataFixesUpdatePlugInSeason6 (any set chance >= 1 divided by 100).
- GuardianOptions boots: MaximumShield 700 (was 200) - fixed; existing databases by the same update. Value: needs suite check.
- BoxOfLuck: the Blue Chocolate / Deep Blue Candy normal-item groups are now added to their boxes (they were created but never attached, so the boxes only gave Zen/excellent items) and their descriptions localized - fixed for new databases. OPEN: existing databases need an update that rebuilds and attaches these groups.
