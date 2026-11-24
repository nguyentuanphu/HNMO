# Audit Findings: `VersionSeasonSix (files 2)` & `Updates/SeasonNine`

- **Units**:
  - `server/src/Persistence/Initialization/VersionSeasonSix (files 2)` (`Quests.cs`, `SkillsInitializer.cs`)
  - `server/src/Persistence/Initialization/Updates/SeasonNine` (10 files)
- **Checked on**: 2026-11-24
- **Status**: `Checked`

## Summary

1. **`VersionSeasonSix/Quests.cs`**:
   - Initializes legacy quests (level 150 Scroll of Emperor & Treasures, level 220 Hero Status & Secret of the Dark Stone, level 380 Evidence of Strength, level 400 Barracks & Darkness Zone) and Season 6 Quest System quests (Group 18/19).
2. **`VersionSeasonSix/SkillsInitializer.cs`**:
   - Initializes all base character/monster/boss skills and Master Skill Tree definitions (Common, DK, DW, ELF, SUM, MG, DL, RF) with their level formulas, target attributes, and replaced-skill inheritance.
3. **`Updates/SeasonNine/`**:
   - `SeasonNineUpdatePlugInBase`: Gates all Season 9 updates on `HnmoSeasonInstance.IsSeasonNine` (`HNMO_SEASON=s9`), leaving Season 6 data untouched when inactive.
   - `SeasonNineSharedItemsUpdatePlugIn`: Adjusts Cape of Fighter drop level (`180 -> 150`), Cape of Lord required level (`180 -> 150`), and basic potion stack durability (`3 -> 255`), preserving operator-customized values.
   - `SeasonNineSocketOptionsUpdatePlugIn`: Adjusts Water level 5 shield protection (`20% -> 30%`), Wind max AG (`25..50 -> 50..130`), and Wind AG recovery (`3..15 -> 5..13`), preserving operator-customized values.
   - `SeasonNineOnlyItemsUpdatePlugIn`: Adds 279 Season 9-only items, 13 Combination Notes (`14-191..203`), and links socket weapons to seed sphere craftings (`44`, `45`).
   - `SeasonNineMonstersUpdatePlugIn`: Adds Season 9 monsters/NPCs and updates Dark Elf (Trainee Soldier) respawn delay (`10s -> 10800s`) when running in Season 9.
   - `SeasonNineMapsAndGatesUpdatePlugIn` & `SeasonNineJinShopsUpdatePlugIn`: Adds Season 9 maps/gates/spawns and Jin merchants (`583` on Acheron, `604` on Acheron Arca War).
