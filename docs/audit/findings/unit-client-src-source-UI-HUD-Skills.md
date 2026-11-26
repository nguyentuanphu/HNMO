# Unit Audit: `client/src/source/UI/HUD/Skills`

- **Unit**: `client/src/source/UI/HUD/Skills`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `8` (`1,126` lines): `MasterSkillTreeLayout.*`, `SkillIconAtlas.*`, `SkillTooltip.*`, `SkillTooltipModel.*`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Elf buff & SD Recovery tooltip formulas | `SkillTooltipModel.cpp:356-409` | Heal: `Ene/5 + 5` (`* (1 + MasterBoost/100)`), Defense: `Ene/8 + 2` (`+Master/Str boosts`), Attack: `Ene/7 + 3` (`+Master/Str boosts`), Recover SD (`AT_SKILL_RECOVER`): `Ene/4 + Level` | Server Elf skill formulas & `CharacterManager` | Same |
| Plasma Storm Fenrir & Harmony skill tooltip weapon % | `SkillTooltipModel.cpp:199-201,417,463-519` | Applies Harmony weapon option (`Item.Level >= Jewel_Of_Harmony_OptionLevel`); checks `SkillAttribute[skillType].Level` & duration (`CanOnlyBeUsedForDSeconds`); BK Castle Siege / extension state | Server `SkillAttribute` & `SkillHitValidator` | Same |
| Master skill tree rank & grid layout (`10` ranks x `3` cols) | `MasterSkillTreeLayout.cpp:14-44` | Places Master skills across `3` trees x `10` ranks x `4` slots | Web `masterLevel.ts` | Same |
