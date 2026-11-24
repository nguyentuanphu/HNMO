# Unit Audit: `client/src/source/GameLogic/Combat`

- **Unit**: `client/src/source/GameLogic/Combat`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `16` (`4,587` lines) including `SkillExecution.cpp`, `ClassAttack.cpp`, `SkillCast.cpp`, `DuelMgr.cpp`, `CriticalDamageRate.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Critical damage rate breakdown (Character window) | `CriticalDamageRate.cpp:60-98` | Combines worn Luck (`+5%`/item), Ancient Set `AT_SET_OPTION_IMPROVE_CRITICAL_DAMAGE_PERCENT`, Socket bonus `m_StatusBonus.m_iCriticalDamageRateBonus`, Master Skill passive buff (`round(value)`), and Scroll of Battle (`14:97`) `+20` value when `eBuff_Scroll_Battle` is active | Server `Stats` `CriticalDamageChance` & `UI/Character/CriticalDamageRateLine.cpp` | Same |
| Auto-attack persistence filter by class & skill | `SkillExecution.cpp:250-314` | Keeps `Attacking = 2` outside `WD_6STADIUM` and Chaos Castle; resets `Attacking = -1` for non-bow Elf skills, BK `Swell Life`, DL `Critical Damage`/`Party Teleport`, DW `Nova`, Stun/Buff skills (`AT_SKILL_STUN..REMOVAL_BUFF`), Summoner buffs/debuffs (`Berserker`, `Sleep`, `Blind`, `Weakness`, `Enervation`), and RF party buffs | Web `skillCast.ts` (`keepAutoAttack`) | Same |
| Class attack dispatch & Duel target eligibility | `ClassAttack.cpp:77-248`, `SkillCast.cpp:128-208`, `DuelMgr.cpp:125` | Dispatches class skill Mana/AG queries (`GetSkillInformation`), bow arrow generation, Summoner summons duel exclusion check, and Stadium/CC/SafeZone attack guards | Server `SkillHitValidator` & Web `duel.ts` | Same |
