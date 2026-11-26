# Audit Findings: `server/src/GameLogic/MiniGames/Doppelganger`

**Status**: Checked (all 12 files read in full, 1,862 lines)
**References**: [`diff-events-all.txt`](../data/diff-events-all.txt), [`parser-events-all.md`](parser-events-all.md), [`ledger-combat-items-events-2026-11-22.md`](ledger-combat-items-events-2026-11-22.md)

## Scope Covered
- `DoppelgangerContext.cs` (835 lines), `DoppelgangerEventDefinition.cs` (406 lines), `DoppelgangerMonsterIntelligence.cs` (274 lines), `DoppelgangerPathArea.cs` (73 lines), `DoppelgangerMonsterScaling.cs` (37 lines), `DoppelgangerState.cs` (31 lines), `DoppelgangerResult.cs` (26 lines), `DoppelgangerMonsterSpawn.cs` (24 lines), `DoppelgangerPath.cs` (22 lines), `DoppelgangerHerdSize.cs` (21 lines), `DoppelgangerFeaturePlugIn.cs` (28 lines), `IDoppelgangerEventViewPlugIn.cs` (75 lines).

## Rule & Suite Comparison

| File & Line | Rule / Formula | Suite / Reference Value | Verdict |
|---|---|---|---|
| `diff-events-all.txt:37-39`, `DoppelgangerContext.cs:114-121,222-238` | `GameDuration = 600s` (10m), `ExitDuration = 60s`, `MaxPlayers = 5`, `EnterDuration = 30s` (configured in `DoppelgangerInitializer`). | S6/S9 suites (`DoppelGanger_Config.ini` / `.lua`): `ReadyTime = 60s`, `PlayTime = 600s`, `EndTime = 60s`. | Differs on enter/ready (`30s` vs `60s`); Same on play (`600s`), exit (`60s`) |
| `DoppelgangerEventDefinition.cs:25-95` | `MaximumGoalCount = 3`; `PlayInfoInterval = 1s`; `HerdInterval = 3s`; `AttackFirstChance = 0.7` (`AlwaysAttacking = {533}`); `IceWalkerSpawnTime = 5m55s`; `IceWalkerMissionDuration = 60s` (`30s` countdown warning); `IceWalkerMissionFailedMultiplier = 2.0f`; `IceWalkerPos = 3..18`. | Matches S6/S9 Double Goer 3-breach failure limit, progressive 3s waves (`1/2/3` base + extra players), Ice Walker sub-boss & 2x penalty mechanics. | Same |
| `DoppelgangerEventDefinition.cs:116-136,168-184` | `InterimChestCount = 3`, `InterimChestDuration = 60s`, `LarvaChance = 0.6` (spawns `1` larva `532` per `_initialPlayerCount`); interim chest bosses `529/530` (`MadButcher` at `1m/4m`, `TerribleButcher` at `7m` + `538` DL clone); `InterimRewardChest = 541`, `FinalRewardChest = 542`. | Matches S6/S9 boss spawn cadence and 3-chest (1 real / larva trap) mechanic. | Same |
| `DoppelgangerEventDefinition.cs:247-405` | 4 map paths (`65` Ice, `66` Fire, `67` Water, `68` Ground), `23` overlapping areas (`0..22`); linear default monster scaling every `50` combined levels (`50..800`) across `1..5` players (`+50%` HP, `+20%` dmg/def per additional player). | Maps `65..68` match S6/S9; scaling table is HNMO's clean formula approximation of suite monster scaling (tunable via admin panel). | Same maps; HNMO formula scaling |
| `DoppelgangerContext.cs:164-219,277-354,667-679` | Only one chest per `InterimChestGroup` opens (`TryOpen()` CAS); player dying or leaving map fails individually (`FailPlayerAsync`); defense failure at `3` breaches fails all remaining players; `FinalRewardChest` spawns at alive finisher position and drops generator loot owned by party/player. | Matches S6/S9 win/fail conditions and chest locking. | Sound |
| `DoppelgangerMonsterIntelligence.cs:142-180,202-273` | Walks path forward (`UpdatePathPosition` monotonic), ignores safezone flags around magic circle (`CanWalkOnSafezone = true`), stops walking when attacking target in range (`AttackRange`) with LoS, stops walking when frozen/stunned/asleep. | Path-following AI correctly prevents monster kiting away from lane. | Sound |

## Bugs / Open Issues
- **Configuration difference (`DoppelgangerInitializer` / `events.csv`)**: `EnterDuration` is `30s` vs `60s` (`LuaSetReadyTime(1)`) in both S6 and S9 suites. Flagged for `differences-summary.md` review.
