# Unit Audit: `client/src/source/UI/HUD (files 3)`

- **Unit**: `client/src/source/UI/HUD (files 3)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `8` (`2,324` lines) including `SkillList.cpp`, `SkillList.h`
- **Status**: `Checked` (reclassified from `"No game rules"` — `SkillList.cpp` enforces skill usability checks on hotkeys/slot selection)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Dark Horse mounted skills & durability guard | `SkillList.cpp:565-584` | Blocks `Earthshake` (normal/str/mastery), `Rider`, and Dark Spirit Pet Commands unless Dark Horse is ridden / alive (`Equipment[EQUIPMENT_HELPER].Durability > 0`) | Server Pet / Earthshake validation (`Durability > 0`) | Same |
| Rage Fighter glove, mount & ring restrictions | `SkillList.cpp:586-602` | Delegates to `g_CMonkSystem`: `IsSwordformGlovesUseSkill`, `IsRideNotUseSkill(skill, Helper.Type)`, and `IsChangeringNotUseSkill` for `CLASS_RAGEFIGHTER` | Server RF weapon/mount/ring skill rules | Same |
| Equipped weapon / bow / 1H melee checks | `SkillList.cpp:604-617` | Enforces `IsNonWeaponSkillOrIsSkillEquipped`, `Multi-Shot` requires `BOWTYPE_NONE != GetEquipedBowType_Skill()`, and `Flame Strike` requires `HasOneHandedOrMeleeWeapon()` | Server `SkillAttribute` & weapon equip validators | Same |
| Skill bar icon resolution & rendering | `SkillList.cpp:623-640+` & other files | Skill icon sprite coordinates, tooltip triggers | UI presentation | No game rules |
