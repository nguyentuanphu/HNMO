# Unit Audit: `client/src/source/Data/GameData/ItemData (files 1)`

- **Unit**: `client/src/source/Data/GameData/ItemData (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `21` (`5,417` lines) including `ItemAttributeConversion.cpp`, `ItemDataValidation.cpp`, `ItemDatabase.cpp`, `ItemDefinition.h`, `ItemFieldDefs.h`, `ItemGroundLabels.cpp`, `ItemExchangeFile.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Description & tooltip level ranges | `ItemDefinition.h:124-135,153-162` | `minLevel = 0`, `maxLevel = ItemLevelCount - 1` (`15`, supporting `+0..+15`); supports per-level placeholder tables (`Fruits` stat names by `+level`) & conditional tooltips (`afterOptions`) | Server `ItemLevelCount = 16` (`0..15`) | Same |
| Item data invariant validation rules | `ItemDataValidation.cpp:247,383+` | Validates stat/requirement consistency against `ItemStatProfile` (e.g., item with `damageMin/Max > 0` must carry a damage profile; `requirements.level` vs `requirementsByLevel`) | Server item data validation | Same |
| Legacy field bindings & equipment slots | `ItemFieldDefs.h:11-23`, `ItemDefinition.h:33` | Maps item slots (`Pet = 8`, etc.) and `ITEM_ATTRIBUTE` fields (`Level`, `m_wSkillIndex`, `Defense`, `Durability`) | Server `InventoryConstants` & `ItemDefinition` | Same |
| Ground drop styling & item exchange definitions | `ItemGroundLabels.cpp`, `ItemExchangeFile.cpp` | Parses ground item label display rules (`GroundLabels.json`) and item exchange catalogs | Web `groundItems.ts:66` | Same |
