# Unit Audit: `client/src/source/Data/GameData/ItemData (files 3)`

- **Unit**: `client/src/source/Data/GameData/ItemData (files 3)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `32` (`4,389` lines) including `ItemStatsJson.cpp`, `ItemStatProfiles.cpp`, `ItemTooltipConditions.cpp`, `ItemTooltipLayouts.cpp`, `OptionRule.cpp`, `StatProfileRule.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Stat ceiling & requirement JSON bounds | `ItemStatsJson.cpp:40-82` | Enforces `MaxValue = 65535`, `MaxPerLevelAfterHalving = 255`, `MaxStrengthPerOptionLevel = 255`, `MaxDropLevelBonus = 1000`; parses requirement stats (`strength`, `dexterity`, `energy`, `vitality`, `leadership`), `LevelPerItemLevel`, `ExcellentLevelAdd`, and `StrengthPerOptionLevel` | Server `ItemDefinitions` & `common-server-and-character-rules-s6-s9.md` (`65535` stat ceiling) | Same |
| Stat profile inheritance & per-level growth terms | `ItemStatProfiles.cpp`, `ItemStatTerms.h`, `StatProfileRule.cpp` | Defines `Table`/`Values`/`PerLevel`, `ExcellentTerm` (`Times`, `Add`, `DropLevelDivisor`), `AncientTerm` (`AfterLevelBonus`), and requirement formulas (`Add + LevelWeight * Percent / Basis`) | `ItemBmdImport.cpp:342-360` & server `ItemStat` scaling | Same |
| Tooltip condition & layout evaluator | `ItemTooltipConditions.cpp:259`, `ItemTooltipLayouts.cpp`, `OptionRulesJson.cpp` | Evaluates tooltip visibility rules (`Kind::Class`, level/stat thresholds) and localized option texts | Client tooltip presentation | Same |
