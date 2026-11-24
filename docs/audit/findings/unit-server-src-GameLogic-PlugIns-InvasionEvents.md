# Unit Audit Findings — `server/src/GameLogic/PlugIns/InvasionEvents`

- **Unit**: `server/src/GameLogic/PlugIns/InvasionEvents` (16 files, 1,664 lines)
- **Checked on**: 2026-11-24
- **Reference**: [invasions-and-s6-events-s6-s9.md](file:///D:/WorkSpace/HNMO-ag/docs/audit/references/invasions-and-s6-events-s6-s9.md)

## Scope Covered

- `BaseInvasionPlugIn.cs`, `FindTheGmConfiguration.cs`, `FindTheGmInvasionPlugIn.cs`, `GoldenInvasionPlugIn.cs`, `InvasionConfigurationDefaults.cs`, `InvasionGameServerState.cs`, `InvasionMaps.cs`, `InvasionMonsters.cs`, `InvasionSpawnConfiguration.cs`, `PeriodicInvasionConfiguration.cs`, `RabbitInvasionPlugIn.cs`, `RedDragonInvasionPlugIn.cs`, `SimpleInvasionPlugIn.cs`, `SnowmanInvasionPlugIn.cs`, `SpawnMapStrategy.cs`, `WhiteWizardInvasionPlugIn.cs`

## Findings & Bugs

1. **Critical: Subordinate/Boss mobs with empty `MapIds` (`[]`) NEVER spawn (`InvasionConfigurationDefaults.cs:68-69, 87, 105`, `BaseInvasionPlugIn.cs:519-534, 384-406, 290-316`)**:
   - In `InvasionConfigurationDefaults`, three `ForceSingleMap = true` invasions rely on leaving secondary entries with empty `MapIds = []`:
     - `WhiteWizard`: `DestructiveOgreSoldier` (`136`, x15) and `DestructiveOgreArcher` (`137`, x10) have `MapIds = []`.
     - `Rabbits`: `HideousRabbit` (`420`, x10, `announceDeath: true`, and `AnnouncedMonsterId`!) has `MapIds = []`.
     - `Snowmen`: `CursedSanta` (`476`, x5, `announceDeath: true`, and `AnnouncedMonsterId`!) has `MapIds = []`.
   - However, `BaseInvasionPlugIn.GetActiveMobs` filters `.Where(mob => mob.MapIds.Count > 0)` at `line 532`.
   - As a result:
     1. None of `DestructiveOgreSoldier`, `DestructiveOgreArcher`, `HideousRabbit`, or `CursedSanta` are ever spawned in `SpawnMobsOnMapsAsync`!
     2. In `SelectSingleMap` (`lines 386-389`), `AnnouncedMonsterId` (`HideousRabbit` / `CursedSanta`) is absent from `mobs`, forcing fallback to `mobs[0]`, and `InvasionSpawnConfiguration.MapIds` also fails its `[MinLength(1)]` data annotation if validated in UI.
2. **White Wizard archer count & S6 Golden Invasion differences (`InvasionConfigurationDefaults.cs:17-36, 65-71`)**:
   - Legacy `the suite's White Wizard invasion config file` spawns `15` `DestructiveOgreArcher` (`137`), whereas HNMO `WhiteWizard` configures `10`.
   - Legacy S6/S9 Golden Invasion includes Golden `493..502` (Dungeon, LostTower, Aida, Icarus, Kanturu, Raklion, Swamp, Elbeland) and spawns 1–2 per monster (Golden Dragon on LostTower `4` as well as `0, 2, 3`); HNMO's default spawns only classic Golden `43, 53, 54, 78, 79, 80, 81, 82, 83` at higher counts (`10..20`).
3. **`BaseInvasionPlugIn` / `FindTheGmInvasionPlugIn` / `SnowmanInvasionPlugIn` (OK)**:
   - `FindTheGmInvasionPlugIn` properly guards concurrent winner claims with `lock (run.Gate)`, records unique `runId:rank:accountKey` coin grant references via `MissionCoinGranter`, and disposes NPCs cleanly on finish or timeout.
   - `SnowmanInvasionPlugIn.ResolveMonsterDefinition` clones NPC `476`/`477` into `NpcObjectKind.Monster` with combat attributes if DB definitions lack monster stats.
