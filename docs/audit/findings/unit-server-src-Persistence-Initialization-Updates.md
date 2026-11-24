# Audit Findings: `server/src/Persistence/Initialization/Updates (files 1..5)`

- **Units**: `server/src/Persistence/Initialization/Updates (files 1)` through `(files 5)` (284 files total)
- **Checked on**: 2026-11-24
- **Status**: `Checked`

## Summary

Audited the update orchestration infrastructure (`DataUpdateService`, `NewDatabaseUpdates`, `UpdatePlugInBase`) and the HNMO-specific and suite-aligned configuration update plug-ins across `Updates (files 1..5)`.

## Key Verifications

1. **Orchestration (`DataUpdateService`, `NewDatabaseUpdates`, `UpdatePlugInBase`)**:
   - Topological dependency ordering (`OrderByDependencies`) detects missing and circular dependencies (`MissingUpdateDependencyException`, `CircularUpdateDependencyException`).
   - `NewDatabaseUpdates.InstallAsync` marks already-initialized updates as installed and runs mandatory `RunsOnNewDatabase` updates in dependency order.
2. **HNMO Custom Updates**:
   - `AddHnmoSkillsUpdatePlugIn` / `StrongerHnmoSkillsUpdatePlugIn`: Adds 28 HNMO skills (700-727) and Elf Soldier trial quests, and applies the +20% / 200-kill / 5-Chaos tuning.
   - `AddArenaHeraldUpdatePlugIn`, `ArenaGuardShopUpdatePlugIn`, `ArenaTitlesUpdatePlugIn`: Adds Arena Herald NPCs, Arena Guard (239) shop, and Arena rating titles while preserving existing custom tiers.
   - `HappyHourScheduleUpdatePlugIn`, `VaultPagesDefaultsUpdatePlugIn`, `PkClearCostUpdatePlugIn`, `JewelNpcPricesUpdatePlugIn`, `GroundDropDurationUpdatePlugIn`, `GuildCreateLevelUpdatePlugIn`, `GuildMembersCapUpdatePlugIn`, `ZenLossBagOnlyUpdatePlugIn`, `ExperienceLossRowsUpdatePlugIn`: All guard against overwriting host-customized values by matching exact previous defaults and stripping `$id`/`$ref` markers from admin-saved JSON.
3. **Suite-Aligned Updates**:
   - `MonsterStatsFromSuitesUpdatePlugIn`, `MonsterResistancesFromSuitesUpdatePlugIn`, `MonsterRespawnFromSuitesUpdatePlugIn`, `MonsterSpeedsAndRangesFromSuitesUpdatePlugIn`, `KundunFromSuitesUpdatePlugIn`, `FieldSpawnCountsFromSuitesUpdatePlugIn`, `Season6ShopListsUpdatePlugIn`: Align monster stats, resistances (migrating legacy Water resistance to Lightning), respawn delays, spawn counts, and NPC shops to the Season 6/9 server suites without overwriting customized entries.
