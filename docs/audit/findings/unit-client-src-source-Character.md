# Unit Audit: `client/src/source/Character`

- **Unit**: `client/src/source/Character`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `13` (`2,955` lines): `CharacterManager.*`, `CharInfoBalloon.*`, `CharInfoBalloonMng.*`, `CharMakeWin.*`, `CharSelMainWin.*`, `ClassStep.h`, `CSParts.*`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Equipped bow & arrow / crossbow & bolt slot check | `CharacterManager.cpp:312-342` | `GetEquipedBowType_Skill` requires Bow in `EQUIPMENT_WEAPON_LEFT` + `ITEM_ARROWS` in right hand, OR Crossbow in `EQUIPMENT_WEAPON_RIGHT` + `ITEM_BOLT` in left hand | Server `InventoryConstants` bow/crossbow dual-slot equip rules & Web `skillCast.ts:275` | Same |
| Wizardry skill damage formula | `CharacterManager.cpp:344-377` | For Summoner explosion/requiem (`223..224`): raw `MagicDamageMin..Max`. Other skills: `min = MagicDmgMin + SkillDmg`, `max = MagicDmgMax + SkillDmg + SkillDmg/2`, scaled by `1 + AT_SET_OPTION_IMPROVE_MAGIC_POWER/100`, plus `Mastery` & `AT_SET_OPTION_IMPROVE_SKILL_ATTACK` | Web `characterCalc.ts:482-488` & `ItemTooltipValues.cpp:58-70` | Same |
| Curse skill damage formula | `CharacterManager.cpp:379-395` | Summoner only: `AT_SKILL_SUMMON_EXPLOSION..AT_SKILL_SUMMON_REQUIEM` adds `SkillAttribute[iType].Damage` and `+ Damage/2` to `CurseDamageMin..Max` | Web `characterCalc.ts:491-496` | Same |
| Character class creation packet encoding | `CharMakeWin.cpp:443` | Encodes selected class into `CharacterClassNumber` (`Class << 2`) before sending `CreateCharacter` | Web `net/session.ts:244` (`classIndex * 4`) & Server `CharacterClass` | Same |
| Master class & step helpers | `CharacterManager.cpp:210`, `ClassStep.h` | Checks Master progression (`IsMasterLevel`) and step enumeration | `Server` `CharacterClass` | Same |
| Character select / balloon / cosmetic part meshes | `CharSelMainWin.cpp`, `CharInfoBalloon*.cpp`, `CSParts.cpp` | Scene portrait/selection UI and floating info balloons/parts | Presentation only | No game rules |
