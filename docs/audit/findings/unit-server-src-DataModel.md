# Audit Findings: `server/src/DataModel` (10 Units)

Checked on: `2026-11-24`

## Units Covered
1. `server/src/DataModel (files 1)`
2. `server/src/DataModel/Attributes`
3. `server/src/DataModel/Composition`
4. `server/src/DataModel/Configuration (files 1)`
5. `server/src/DataModel/Configuration/ItemCrafting`
6. `server/src/DataModel/Configuration/Items`
7. `server/src/DataModel/Configuration/Quests`
8. `server/src/DataModel/Entities`
9. `server/src/DataModel/Statistics`
10. `server/src/DataModel/Validation`

## Key Rules & Invariants Verified
- **Inventory & Storage Layout (`InventoryConstants.cs`)**:
  - 12 equippable slots (`0..11`: left/right hand, helm, armor, pants, gloves, boots, wings, pet, pendant, ring1, ring2).
  - Main inventory: 8 rows x 8 columns (`64` slots); up to `4` extensions x `4` rows (`128` slots); personal store: `4` rows (`32` slots) starting at `FirstStoreItemSlotIndex = 204` (S6E3 layout); warehouse (vault): `15` rows (`120` slots); temporary storage: `4` rows (`32` slots).
- **Item Rules & Durability (`ItemExtensions.cs`)**:
  - `IsWing`: Group 12 or 13, `Width >= 2`, `Height >= 2`, `MaximumItemLevel >= 11`, `Durability >= 200`, with valid `ItemSlots`.
  - `GetDarkRavenLeadershipRequirement(petLevel) = petLevel * 15 + 185`.
  - `MaximumStackSize`: Clamps `StackLimit?.Invoke(item) ?? definition.Durability` to `[1, 255]`.
  - `Durability()`: Rounds up (`Math.Ceiling`), matching original game wear-counting behavior so an item loses a visible durability point only after a full point of wear is accumulated (preventing immediate repair loops).
  - `CanLevelBeUpgraded`: Wearable item with slot `<= WingsSlot` (0..7) and `Level < MaximumItemLevel`.
- **Item Options, Stat Profiles & Price Modifiers (`Configuration/Items/*`, `Configuration/ItemCrafting/*`)**:
  - `ItemOptionTypes`: Fixed GUIDs for Excellent, Wing, Luck, Option, Harmony, AncientOption, AncientBonus, GuardianOption (380), SocketOption, SocketBonusOption, Blue/Black/Gold Fenrir, and DarkHorse.
  - `ItemPriceModifiers`: Persisted flags enum (`Skill`, `Luck`, `Option`, `WingOption`, `Excellent`, `All`).
  - `ItemStatProfile`: Named formula overrides falling back to `"default"` profile, aligned with client item stat file format.
  - `LevelBonus.GetAdditionalValueElement`: Scales `AdditionalValue` by `durabilityFactor`.
  - `SimpleCraftingSettings`: Models crafting fee, `NpcPriceDivisor` (Chaos Weapon & 1st Level Wings), `SuccessPercent`/`MaximumSuccessPercent`, item option success bonuses (Luck, Excellent, Ancient, Guardian, Socket), and result option chances.
- **Entities, Configuration, Quests, Attributes, Validation**:
  - `Item.Serial`: `Guid?` origin serial preserved across moves/copies to detect duplicates.
  - `QuestDefinition`: Models `Group`, `Number`, `StartingNumber`, `RefuseNumber`, `Repeatable`, `RequiredStartMoney`, level range, class filter, monster kill/item requirements, and rewards.
  - `PowerUpDefinition` & `LessThanOrEqualToAttribute`: Pure attribute-boost model and data-annotation validator.
- **Verdict**: No silent rule regressions found across all 10 `DataModel` units.
