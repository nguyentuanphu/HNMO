# Audit Findings — `server/src/GameLogic/PlugIns/WanderingMerchants`

- **Unit**: `server/src/GameLogic/PlugIns/WanderingMerchants` (4 files, 245 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `MerchantSpawnState.cs`, `WanderingMerchantsConfiguration.cs`, `WanderingMerchantsPlugIn.cs`, `WanderingMerchantsState.cs`

## Rules & Cross-Check
| Rule | HNMO Implementation | Season 6 / 9 Suite Reference | Status |
| --- | --- | --- | --- |
| Wandering NPC respawn/move interval | `WanderingMerchantsPlugIn.cs:134`: random `60..180` minutes (`MinimumSpawnDuration`..`MaximumSpawnDuration`) across `SpawnTrigger.Wandering` spots | `CommonServer.cfg`: `QuestNPCTeleportTime = 9000` seconds (`150` min) | Differs (random 60–180m vs fixed 150m) |

## Observations
- **Minor — `WanderingMerchantsPlugIn.cs:91-95`**: If a wandering NPC has only 1 `PossibleSpawns` entry and is already spawned (`oldMerchant?.SpawnArea == nextSpawn`), `HandleMerchantAsync` returns without advancing `NextWanderingAt`, causing `IsItTimeToStart` to remain true on subsequent ticks.
