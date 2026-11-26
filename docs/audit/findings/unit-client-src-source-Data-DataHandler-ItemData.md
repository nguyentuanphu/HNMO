# Unit Audit: `client/src/source/Data/DataHandler/ItemData`

- **Unit**: `client/src/source/Data/DataHandler/ItemData`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `19` (`4,363` lines) including `ItemBmdImport.cpp`, `ItemDataHandler.cpp`, `ItemComparisonMetadata.h`, `ItemJsonStorage.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Legacy `.bmd` stat requirement growth formula | `ItemBmdImport.cpp:342-360` | When requirement weight `!= 0`: `min(20 + weight * basis * percent / 100, MaxRequirement)`, where `percent = 4` for Energy vs `3` for Str/Agi/Vit/Cmd, and `basis = requirements.level` for `ITEM_GROUP_ETC` scrolls with level requirement (else drop `definition.level`) | Server `ItemDefinition` importer & Web `itemTooltip.ts` | Same |
| Item attribute serialization metadata | `ItemComparisonMetadata.h:20-34`, `ItemDataHandler.cpp` | Maps BMD fields (`Level`, `Defense`, `Durability`, etc.) and converts open-data `Group00..15` JSON back/forth with S6E3/CSV exports (`ItemDataExportS6E3`, `ItemDataExportAsCSV`) | `ServerImporter` & `ItemDataValidation` | Same |
| Stat profile derivation vs BMD weights | `ItemBmdImport.cpp:366-375+` | Selects explicit or rule-derived `ItemStatProfile` and determines whether item keeps weight-based requirements vs flat values | `Data/GameData/ItemData` | Same |
