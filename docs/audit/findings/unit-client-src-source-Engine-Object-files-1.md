# Unit Audit: `client/src/source/Engine/Object (files 1)`

- **Unit**: `client/src/source/Engine/Object (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `24` (`3,002` lines) including `ItemTooltipValues.cpp`, `ItemTooltipText.cpp`, `ItemDropModel.cpp`, `ItemTooltipSnapshot.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `ItemTooltipValues.cpp` computes Harmony bonus damage/defense/requirements and Skill Book damage formulas)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Harmony strengthen gate & attack bonus clamp | `ItemTooltipValues.cpp:29-37,45-52,74-77` | Applies Harmony option only when `item.Level >= item.Jewel_Of_Harmony_OptionLevel`; adds min/max Harmony attack bonuses and clamps displayed `damageMin = min(damageMin + minBonus, damageMax + maxBonus)` | Server Harmony strengthening & tooltip display (`StrengthenCapability`) | Same |
| Skill book wizardry damage range | `ItemTooltipValues.cpp:58-70` | Computes Skill Book display damage from linked skill: `damageMin = skill.Damage`, `damageMax = skill.Damage + skill.Damage / 2` | `SkillAttribute` server/client skill damage table | Same |
| Harmony & socket requirement reductions | `ItemTooltipValues.cpp:82-100+` | Subtracts Harmony `SI_isNB` (`SI_force` / `SI_dex`) and socket stat reductions from displayed Strength/Agility requirements | Server `Equip` stat requirement checks | Same |
| Tooltip rendering & drop visual mapping | `ItemTooltipText.cpp`, `ItemDropModel.cpp`, remaining 20 model/helper files | Maps special drop visuals (Loch's Feather `level == 1` -> Crest of Monarch, Life Stone) and tooltip text pieces | Client visual rendering / formatting | No game rules |
