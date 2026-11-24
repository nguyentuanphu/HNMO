# Unit: server/src/GameLogic/NPC

Checked against: Season 6 suite (commands settings file, `CommonServer.cfg`, monster/spawn/AI roles) and Season 9 suite (commands settings file).

Scope: `server/src/GameLogic/NPC` (`22` files, `2986` lines).

## Summary

- Implements monster/guard/summon/trap/soccer-ball runtime behavior driven by `MonsterDefinition`, `MonsterSpawnArea`, and `GameplayRules`.
- Most rules match standard MU behavior (guard targeting PK1+ outlaws outside safezone within `ViewRange`, Battle Soccer ball push distance, party/owner portal gate capacity, summon follow leash, 16-step A* walk chunks).
- HNMO adds configurable spawn-area leash recovery (`MaximumWalkHomeTime = 20s`) with full HP restore (`RestoreHealth(int.MaxValue)`) and `OutlawPoints.GetMonsterKillPoints` reduction on kill.

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `AttackableNpcBase.cs:25-26` | `MaximumDropDistance = 2` tiles around killed NPC; `IceSkillNumber = 7` freeze-break reaction on hit | Suite monster drop scatter radius (`<= 2` tiles) and Ice spell effect | Same |
| `AttackableNpcBase.cs:404-406,448` | `SplitExpByDamage` shares EXP across damagers; `RemoveOutlawPointsAsync` via `OutlawPoints.GetMonsterKillPoints` on monster kill | Classic top-damager/party EXP share + PK kill time reduction | HNMO custom (PK point system / configurable boss EXP split) |
| `AttackableNpcBase.cs:530-545` | `GivesMoneyForKills` disables zen drops inside `DevilSquare` and `ChaosCastle` | Suite DS/CC event rules suppress normal mob zen drops (reward via ranking/gem) | Same |
| `BasicMonsterIntelligence.cs:20,251-280` | `MaximumWalkHomeTime = 20s`; leashes monster past `MoveRange + LeashExtraRange`, restores full HP (`int.MaxValue`) while returning | Suite leashes/teleports back when pulled past `MoveRange` (`MaximumWalkHomeTime` `20s` safeguard is HNMO custom) | Differs / HNMO custom |
| `BasicMonsterIntelligence.cs:65,312,328,379` | First AI tick jitter `0..99ms` + `AttackDelay`; attacks within `AttackRange`, chases provoked or within `max(ViewRange, MoveRange+LeashExtraRange+2)` | Suite monster `MoveRange`/`ViewRange`/`AttackRange` chasing | Same |
| `GateNpc.cs:16,77,104` | Town Portal gate summon range `2` tiles, `1000ms` check interval, closes after `MaximumPartySize` (`5`) entries | Suite Dark Lord Summon spell (`<= party` size `5`) | Same |
| `GuardIntelligence.cs:41-45,64-70` | Attacks players with `State >= PlayerKiller1stStage` (`IsAlive && !IsAtSafezone()`) within `ViewRange`; `20%` idle wander chance, walks back if distance `> 7` | Suite Lorencia/Devias/Noria guards attack PK1+ outside safezones | Same |
| `Monster.cs:172,367-370,405` | Path sliced to max `16` steps; step delay `MoveDelay * max(1, dist) / speedFactor`; max initial step threshold `1.5` (`sqrt(2)`) | Suite `MonsterList` `MoveSpeed` tick timing | Same |
| `NonPlayerCharacter.cs:103,308-317` | Max `100` random spawn retries inside walkable terrain; idle roam clamped to `SpawnArea` & `SpawnPoint +/- MoveRange` | Suite `MonsterSetBase` box + `MoveRange` roam | Same |
| `SoccerBall.cs:106` | Battle Soccer ball pushed `3` tiles when hit with skill, `2` tiles with basic attack | Suite `EnableBattleSoccer` ball kick (`2..3` tiles along attacker dir) | Same |
| `SummonedMonsterIntelligence.cs:55,68-73` | Summon scans targets within `8` tiles of owner; follows owner when distance `> 2` (idle) or `> 5` (in combat) | Suite Elf/Dark Lord summon follow leash | Same |
| `TrapIntelligenceBase.cs:84`, `RandomAttackInRangeTrapIntelligence.cs:53` | Trap AI initial jitter `0..999ms`; attacks targets within `AttackRange + 1` | Suite trap `Monster` (`#100..103`) trigger range | Same |
