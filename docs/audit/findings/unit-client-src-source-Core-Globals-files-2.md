# Unit Audit: `client/src/source/Core/Globals (files 2)`

- **Unit**: `client/src/source/Core/Globals (files 2)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `3` (`5,977` lines): `_enum.h` (`5,257L`), `_struct.h` (`677L`), `_types.h` (`43L`)
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Experience update packet result enum | `_enum.h:51-59` | `ExperienceType`: `Undefined=0`, `Normal=1`, `Master=2`, `MaxLevelReached=0x10`, `MaxMasterLevelReached=0x20`, `MonsterLevelTooLowForMasterExperience=0x21` | `common-server-and-character-rules-s6-s9.md` (`MonsterMinLevelForMLExp = 95`) & Server `AddExperiencePlugIn` | Same |
| Item option bitmask flags | `_enum.h:61-74` | `HasOption=0x01`, `HasLuck=0x02`, `HasSkill=0x04`, `HasExcellent=0x08`, `HasAncient=0x10`, `HasHarmony=0x20`, `HasGuardian=0x40`, `HasSockets=0x80` | Web `inventory.ts:37` & Server extended item serialization | Same |
| Global Skill, Item, Monster & Map enum IDs | `_enum.h:106-5256`, `_struct.h:390` | Master skills (`AT_SKILL_EXPANSION_OF_WIZARDRY_STR = 380`), Stone Statue (`MONSTER_STONE_STATUE = 380`), 64-bit `uint64_t Experience`, world indices (`WD_2DEVIAS`, `WD_6STADIUM`, `WD_7ATLANSE`, `WD_67DOPPLEGANGER3`), and music asset constants | Server definitions (`ItemDefinitions`, `SkillDefinitions`, `MapDefinitions`) | Same |
