# Unit Audit: `client/src/source/Data/GameData/ItemData (files 2)`

- **Unit**: `client/src/source/Data/GameData/ItemData (files 2)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `37` (`5,893` lines) including `ItemOptionEffects.cpp`, `ItemOptionDatabase.cpp`, `ItemOptionsJson.cpp`, `ItemOptionValidation.cpp`, `ItemModelGlowJson.cpp`, `ItemRuleConditions.cpp`, `ItemJsonFormat.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Item option effect slot & bonus mapping | `ItemOptionEffects.cpp:25-45+` | Maps 24 JSON option types (`Luck -> AT_LUCK` +%, `Option`: `physicalBaseDmg`/`baseDamageBonus` -> `AT_IMPROVE_DAMAGE`, `wizardryBaseDmg`, `curseBaseDmg`, `defenseRatePvm`, `defenseBase`, `healthRecoveryMultiplier`, `maximumMana`, `maximumAbility`; `Excellent`: `maximumHealth/Mana`, `armorDamageDecrease`, `damageReflection`, `defenseRatePvm`, `moneyAmountRate`, `excellentDamageChance`) to client stat slots and client-computed display flags | Server `IncreasableItemOption` definitions (`DataModel/Configuration`) | Same |
| Option data validation & rule conditions | `ItemOptionValidation.cpp`, `ItemRuleConditions.cpp` | Validates option IDs, value scales, and conditional visibility/effect rules | `ItemData (files 1)` validation | Same |
| Model display, glow JSON & item name forms | `ItemModelGlowJson.cpp`, `ItemModelDisplayJson.cpp`, `ItemNameCases.cpp`, `ItemNameForms.cpp` | JSON schemas for glow intensities (`ItemModelGlowJson.h:31`), localization declensions, and asset textures | Client presentation only | No game rules |
