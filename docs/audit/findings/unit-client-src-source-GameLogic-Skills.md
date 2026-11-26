# Unit Audit: `client/src/source/GameLogic/Skills`

- **Unit**: `client/src/source/GameLogic/Skills`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `7` (`1,225` lines) including `SkillManager.cpp`, `SummonSystem.cpp`, `SkillHotkeyConfiguration.h`, `SkillEffectMgr.h`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Skill Energy requirement formula (Normal vs Knight vs Summon) | `SkillManager.cpp:154-191` | If `p->Energy == 0` -> `0`; if `p->Level == 0` -> direct `p->Energy` (e.g. Elf Summon Goblin `30`, Soldier `300`); else `BASE + (Energy * Level * SCALE / 100)` with Knight base (`ENERGY_REQ_BASE_KNIGHT`) vs default `20`, and Summoner Explosion/Requiem special scale (`ENERGY_REQ_SCALE_SUMMON_PERCENT`) | Server `SkillAttribute` energy requirement formulas & `skillTooltip.ts` | Same |
| Skill Mana / AG / Charisma / Damage lookup | `SkillManager.cpp:91-95,140-152,193-210` | Extracts `Mana`, `AbilityGuage` (AG), `Charisma`, `Damage`, and learned slot checks (`113`, `123`) | Server `SkillDefinition` | Same |
| Skill hotkey layout config & pet summons | `SkillHotkeyConfiguration.h:15`, `SummonSystem.cpp:49` | Maps `20` key configuration slots & active helper summons | Server `KeyConfiguration` | Same |
