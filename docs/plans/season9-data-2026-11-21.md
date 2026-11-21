# Season 9 Data Set Plan (2026-11-21)

## Goal

Give HNMO's Season 9 server (`HNMO_SEASON=s9`) its own Season 9 configuration data (items, socket options, monsters, maps, gates, and spawns) matching the Season 9 server suite, without duplicating initialization keys or altering Season 6 data.

## Architecture (Main's Design)

1. **Keep the `"season6"` initialization key** (`VersionSeasonSix.DataInitialization.Id`).
   - All existing Season 6 updates and existing Season 9 databases are already keyed to `"season6"`.
2. **Season 9-only mandatory update plug-ins** in `server/src/Persistence/Initialization/Updates/SeasonNine/`:
   - Add a shared base class `SeasonNineUpdatePlugInBase : UpdatePlugInBase`:
     - `DataInitializationKey => VersionSeasonSix.DataInitialization.Id`
     - `IsMandatory => true`
     - `RunsOnNewDatabase => HnmoSeasonInstance.IsSeasonNine`
   - When `!HnmoSeasonInstance.IsSeasonNine` (Season 6):
     - Fresh Season 6 database initialization marks the plug-in installed without executing it (`NewDatabaseUpdates.InstallAsync` checks `!update.RunsOnNewDatabase`).
     - Existing Season 6 databases mark the plug-in installed via a no-op guard (`if (!HnmoSeasonInstance.IsSeasonNine) return;`) in `ApplyAsync` without touching Season 6 data.
   - When `HnmoSeasonInstance.IsSeasonNine` (`HNMO_SEASON=s9`):
     - Fresh Season 9 database initialization executes every plug-in in dependency/`CreatedAt` order.
     - Existing Season 9 databases execute every pending mandatory plug-in on startup, preserving any admin-customized values by only updating fields still at HNMO's previous Season 6 value.
3. **Localization & Admin Panel compatibility**:
   - Every new plug-in caption, item name/group, option, monster, map, and warp caption comes from `.resx` resources (`en`, `vi`, `de`, `zh-CN`), verified by `AllCaptionsCanBeTranslatedAsync`.
   - Every added or updated entity remains editable in the Admin Panel under its normal configuration pages (`System -> Updates`, `Configuration -> Items / Item Options / Monsters / Maps / Gates / Warps`).
4. **Web Client & Map Numbers** (`/data/s9/`):
   - Assets for Season 9 load through `dataUrl()` (`client/web/src/dataRoot.ts`) under `/data/s9/`.
   - Season 9-only map folders (`World/Object 83, 92, 94, 95, 96, 99, 101, 103`) are routed by `SeasonData.IsSeasonNineOnlyMapFile` (`client/web/bridge/SeasonData.cs`) using the client `worldNumber = mapNumber + 1` convention (`82 -> 83`, `91 -> 92`, `93 -> 94`, `94 -> 95`, `95 -> 96`, `98 -> 99`, `100 -> 101`, `102 -> 103`).
   - Server map numbers, `MAP_NAMES` / `SHARED_FOLDERS` in `client/web/src/world.ts`, and `client/web/public/data/Monsters/Season9.json` must match the server definitions added in PRs 4.4 and 4.5.

## Ordered Draft PRs (into `claude/hnmo-main-handover-54co2m`)

Values are ported directly from the Season 9 server suite tables; suite files are never copied into the repository.

| Order | PR Scope | Summary of Data Changes | Dependencies / Notes |
|---|---|---|---|
| **4.1** | **Shared items (210 differing items)** | Add `SeasonNineUpdatePlugInBase` and update the 210 shared item definitions whose stats, requirements, drop levels, speeds, or durability differ in the Season 9 server suite. Keep owner-confirmed defaults where already identical (e.g., Archangel weapons). | First Season 9 data PR; establishes `Updates/SeasonNine/` base pattern. |
| **4.2** | **Season 9-only items (281 items)** | Add the 281 item definitions present in the Season 9 suite and absent in HNMO (new weapons, sets, wings/cloaks, jewels, mix materials, event/specialty items) with `en/vi/de/zh-CN` captions and item stats/options. | *Task 1 follow-up:* unblocks implementing the 40% / 1M zen socket weapon mix (`docs/audit/findings/socket-weapon-mix.md`) if owner approves keeping it Season 9-only. |
| **4.3** | **Season 9 socket options** | Restore water socket "shield protection increase" level 5 to `30 %` in Season 9 (when still at Season 6's `20 %` set by `SocketShieldProtectionUpdatePlugIn`), and align wind/other Season 9 socket option differences against the Season 9 suite. | Depends on `SocketShieldProtectionUpdatePlugIn` ordering so S9 ends at `30 %` while S6 stays `20 %`. |
| **4.4** | **Season 9 monsters** | Add Season 9-only monsters/NPCs (including Nars, Debenter, Uruk Mountain, Tormented Square, and Season 9 NPCs/event mobs) and apply Season 9 monster stat diffs on shared monsters. Keep `client/web/public/data/Monsters/Season9.json` in sync and track open model-less IDs (`580, 581, 584, 643, 689`). | Needed before map spawn placements in 4.5. |
| **4.5** | **Season 9 maps and gates** | Add Season 9 maps (`91` Acheron, `92` Acheron Arca War, `95` Debenter, `96` Debenter Arca Battle, `97` Chaos Castle Survival, `98`/`99` Illusion Temple League, `100` Uruk Mountain, `101` Uruk Mountain Arca War, `102` Tormented Square; verify `82`/`93`/`94`), terrain `.att` walkability, safe zones, gates (`345+`), Season 9 warp rules, and spawns; update `world.ts` (`MAP_NAMES`, `SHARED_FOLDERS`) and `SeasonData.cs` if needed. | Coordinates with `/data/s9/` web client paths and `SeasonData.IsSeasonNineOnlyMapFile`. |

### Explicitly Deferred (Not Part of This Sequence)

- **NPC shops** for Arena Guard (`239`), Jin (`583`/`604`), and Moss (`492`): deferred per owner decision on 21 Nov until after the Season 9 data set lands.

## Verification Standard (Per PR)

1. Unit/initialization test that:
   - Fails before the update when `HNMO_SEASON=s9` (Season 9 still has old Season 6 value or lacks new entries),
   - Passes after the update when `HNMO_SEASON=s9`,
   - Verifies custom operator values are preserved, and
   - Verifies `HNMO_SEASON=s6` data is unchanged while the update plug-in is marked installed.
2. Full test runs via `heavy.ps1`: `HNMO.Persistence.Initialization.Tests` (including `AllCaptionsCanBeTranslatedAsync`) and `HNMO.Tests`.
3. Zero new build warnings and no third-party suite names or local disk paths.
