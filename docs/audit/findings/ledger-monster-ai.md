# Ledger: monster data and monster AI

- Read at commit `4b75b2c5` (origin/claude/hnmo-main-handover-54co2m, 21 Nov 2026), worktree HNMO-fix3b.
- Suite files compared (read-only):
  - S6: `the Season 6 suite/.../Files/Data/Monster/Settings/MonsterList.xml` (625 rows), `.../Data/CommonServer.cfg`
  - S9: `the Season 9 suite/.../FileServer/Data/Monsters/MonsterList.xml` (654 rows), `MonsterSpawn.xml`, `.../Data/CommonServer.cfg`
  - The two monster tables are identical for every shared index except 412 (Dark Elf trainee, respawn 10 s in S6, 10800 s in S9).
- HNMO data: dumped from a fresh in-memory Season 6 database (`VersionSeasonSix.DataInitialization`, all initializers, 504 monster definitions, 6488 spawn areas) by a throwaway tool, then compared by script. HNMO has no Season 9 maps or monsters (no Acheron, Deventer, Uruk...); the S9 table was compared for the shared indexes only.
- Scripts and outputs (scratchpad, throwaway): `monster-dump/` (C# dump tool), `monster-cmp/compare.mjs` + `monster-diff.json` + `monsters-value-diffs.txt`, `monster-cmp/spawns.mjs` + `spawn-diff.txt`.

## Unit 1: monster definitions (data)

Paths: `server/src/Persistence/Initialization/VersionSeasonSix/Maps/*.cs` (CreateMonsters), `Version075/Maps`, `Version095d/Maps` (shared definitions), `Updates/*` that touch `Monsters`. Status: **Checked by script** (all 504 definitions, 18 fields each); the files were not read line by line, the dump is the evidence.

| Rule | HNMO | Suites | Result |
|---|---|---|---|
| Stats with an extra zero | 168 HP 65000, 426 DR 1900, 184 DR 600, 197 AR 7800, 574 AR 11400 | 6500, 190, 60, 780, 1140; neighbours of the same event agree | different, fixed #212 |
| Elvenland 418-425 | 419-421 had each other's names/stats (419 named Polluted Butterfly with L13 stats); all 8: AR 10, DR = defense, attack 1500 ms, view 5; Cursed Lich range 1 with Meteorite | 419 Hideous Rabbit L19, 420 Werewolf L24, 421 Polluted Butterfly L13 (both clients' Monsters.json agree), real AR/DR/speeds/ranges | different, fixed #214 |
| Blood Castle 1-8 monsters (84-143, 428-433) | much stronger, e.g. BC1 Chief Skeleton Warrior L56 HP 5000 vs L33 HP 1100; BC8 Magic Skeleton HP 215000 vs 65000 | weaker | different, deliberate (FixBloodCastleMonsterAttributesUpdatePlugIn swaps BC7/BC8 on purpose) - owner question Q1 |
| Kundun (275) | "Illusion of Kundun 7": L140, HP 140000, dmg 1070-1105, AR 1200, range 10, view 7, respawn 10800 s | L147, HP 5,000,000, dmg 2000-2500, AR 2000, range 15, view 10, respawn 43200 s | different - Q2 |
| Kundun HP refill | none (no monster regenerates; `AttackableNpcBase.RestoreHealth` only used by the Castle Siege statue) | `CommonServer.cfg` KundunRefillHPSec 400, KundunRefillHP 15000, KundunRefillHPTime 600 | missing - Q2 |
| Kanturu bosses 361-364 (Nightmare, Maya, hands) | much stronger: Maya HP 5,000,000 vs 300; Nightmare def 7500 vs 1000; speeds 400 vs 500-700; respawn 0 vs 10 | weaker table values | different, event bosses - Q3 |
| Raklion 434-440 | e.g. Berserker HP 70000 vs 33000, Dark Elf HP 1,000,000 vs 75000, Balram/Soram trainee HP 90000/100000 vs 37000/40000 | weaker | different - Q3 |
| Imperial Guardian 504-521, gates 524-528 | other names and much higher stats | table rows are placeholders (L1); real stats in `ImperialGuardian_MonsterStats.dat` (binary, not compared) | not compared |
| Chaos Castle 162-168 / 426-427 resistances, CC5 dmg 165-168 | HNMO 1-3 points higher resistances; CC5 min 165 max 168 | suite has min 168 > max 165 (suite typo) | owner question Q4 (resistances), HNMO keeps 165-168 |
| Respawn time | 3 s for Bull Fighter, Budge Dragon, Yeti, Elite Yeti, Mega/Alpha Crust; 3 s Phantom Knight / Great Drakan (suite 30); Gorgon 10 (150); Dark Phoenix 15 (150); golden monsters 600 (they are spawned by the golden invasion, so the value is not used) | 10 s / 30 s / 150 s | different - Q5 |
| Move speed | Karutan 569-574 400 ms per step | 700-900 ms | different - Q6 |
| Attack speed | 50 rows differ, e.g. Axe Warrior 2000/1400, White Wizard 1400/2200, Karutan 1500/1700-1800, trainees 1700/1500 | see monster-diff.json | different - Q6 |
| View / attack range | Kentauros 355 attack 1 view 7 (suite 4/5); Blood Soldier 1 (146) attack 2 (suite 6); Dark Skull Soldier 1 (86) attack 4 (suite 1); Tanker 348 move range 0 (suite 6) | | different - Q6 |
| Resistances | 152 monsters have other ice/poison values (20 same, 10 swapped, 122 other); 142 monsters have no lightning resistance at all (suite 1-35); 4 map files store a resistance as `WaterResistance` (DevilSquare7, Elvenland 422, ImperialGuardian1, RaklionBoss) while lightning skills check `LightningResistance` | Ice/Poison/Light/Fire per row | different, mapping unclear - Q4 |
| Small differences (1-3 points) | Dreadfear 360 DR 906/905, Necron 5 AR 642/645, Ghost Napin AR 983/980, Condra DR 406/405, Ice Napin/Shadow Master attack 2550/2500, Death Centurion 5 def 460/430, Bone Scorpion defense 283 (suites 485; 283 is the suites' defense rate) and defense rate 363 (suites 283) | | listed, not fixed (Bone Scorpion: Q6) |
| Only in suites | 122 (S6) / 151 (S9) indexes, mostly S9 content and NPCs HNMO doesn't have | | not applicable |

## Unit 2: monster spawns (data)

Compared with `MonsterSpawn.xml` (S9) per map and monster, total count. Evidence in `spawn-diff.txt`.

- Event maps (Blood Castle, Chaos Castle, Devil Square, Illusion Temple, Kalima event spots, Imperial Guardian, Kanturu event, Raklion boss, Doppelganger) are only in HNMO's list: the suites keep them in the event files. Not compared.
- Field maps: 26 (map, monster) pairs have another count, mostly by 1-6 monsters, e.g. Atlans Queen Rainer 6 vs 13, Alquamos 11 vs 15, Kalima 6 Schriker 6 10 vs 16, Kalima 7 Schriker 7 10 vs 16, Dungeon Skeleton Warrior 33 vs 29, Lost Tower Death Knight 71 vs 65. Q7.
- Elvenland spawn positions and numbers match the suites exactly (so #214 only changes the definitions).
- The suites give each spawn a `Distance` (spawn spread, 30 for most field spots); HNMO has spawn areas instead. Not a rule difference.

## Unit 3: `server/src/GameLogic/NPC` (all 22 files read in full)

Files: AttackAreaTargetInDirectionTrapIntelligence.cs, AttackAreaWhenPressedTrapIntelligence.cs, AttackSingleWhenPressedTrapIntelligence.cs, AttackableNpcBase.cs, BasicMonsterIntelligence.cs, Destructible.cs, GateNpc.cs, GuardIntelligence.cs, ISummonable.cs, KillMoney.cs, MerchantNpc.cs, Monster.cs, MonsterPushExtensions.cs, NonPlayerCharacter.cs, NpcIntelligenceFactory.cs, NpcTargetSelection.cs, NullMonsterIntelligence.cs, RandomAttackInRangeTrapIntelligence.cs, SoccerBall.cs, SummonedMonsterIntelligence.cs, Trap.cs, TrapIntelligenceBase.cs. Also read in full: `GameLogic/Walker.cs`, `GameLogic/PlayerSummon.cs`, `GameLogic/INpcIntelligence.cs`. Not read: `server/src/Pathfinding` (A* path search, no game rules besides walkability, which is `AIgrid`/safezone flags passed in by Monster.WalkToAsync).

| Rule | HNMO does | Classic / suites | Result |
|---|---|---|---|
| When the AI runs | Only while at least one player observes the monster (`Monster.OnFirstObserverAdded` starts, last observer pauses); tick = AttackDelay | classic runs all monsters | same in effect |
| Aggro | Nearest observing player (or player summon) not in safe zone, not invisible, alive; visible (line of sight) preferred; then attacks if within AttackRange + line of sight, walks to it if within ViewRange+1, otherwise wanders. Target kept only while within ViewRange (`IsCurrentTargetValid`) | ViewRange from MonsterList | same idea; small inconsistency ViewRange+1 vs ViewRange, not fixed |
| Passive monsters | None: every monster with a view range aggroes | suites have no passive flag in MonsterList (AI types in MonsterAIUnit/Automata only for special monsters) | same |
| Fight back when hit from beyond view range | `RegisterHit` sets the attacker as target only if there is none, and the next tick drops it again when the attacker is beyond ViewRange: an archer hitting from 6-7 tiles is never chased | classic: a hit monster goes after its attacker ("pulling") | different - Q8 |
| Retarget when several players hit | Never while the current target stays valid | classic: switches to the top damage dealer (binary behaviour, not in the files) | different - Q8 |
| Chase distance / leash / return home | No leash: `RandomMoveAsync` moves around the CURRENT position (not the spawn), so a pulled monster stays where it was pulled and wanders on from there; it never walks home | classic: monsters wander inside their spawn area (spawn spot + Distance) | different - Q9 |
| Heal when pulled away / out of fight | Monsters never regenerate HP | classic: Kundun refill (settings above); normal monster regen not in the files | different - Q2/Q9 |
| Exp and drop owner | Whole exp (party share) and the drop go to the player who landed the killing hit (`AttackableNpcBase.OnDeathAsync`) | classic: exp split by damage dealt, drop to the top damage dealer | different - Q10 |
| Attack speed | AI timer interval = AttackDelay; one attack per tick | AttackSpeed from MonsterList | same (values differ, Q6) |
| Walking | Path finder (max 16 steps), step delay = MoveDelay x tile distance / movement speed factor | MoveSpeed | same |
| Frozen / stunned / asleep | no attack while stunned/asleep; no walk while frozen; hit ends sleep and shortens freeze | | same |
| Monster in safe zone | does not attack from inside the safe zone; can't walk into it (AIgrid) | | same |
| Summoned monsters (elf summons) | follow the owner (2 tiles idle, 5 when fighting), teleport if not observed by the owner, target the nearest monster within 8 of the owner, never attack the owner; hits by others retarget only within attack range; health + SummonedMonsterHealthIncrease | | same as classic as far as the files say |
| Guards | attack outlaws (PK1+) within view range outside the safe zone; walk back towards their spawn point when more than 7 tiles away | classic guards attack murderers | same |
| Traps | hit the players on/in range of the trap (by AttackRange/direction), skip safe zone tiles; don't check IsAlive (a corpse on the trap is hit again; death is guarded by PlayerState so no double death) | | same, cosmetic note |
| Spawn | random point in the spawn area, not in the safe zone for monsters/traps, must be walkable for walking NPCs; respawn after RespawnDelay | | same |
| Gate NPC (Dark Lord summon gate) | warps the owner's party within 2 tiles, closes after MaximumPartySize entries or owner death | | same |
| Push (Crywolf/Selupan skills) | no push of stunned/frozen players, not into safe zone | | same |

## Unit 4: special AIs (read in full)

`Crywolf/CrywolfMonsterIntelligence.cs`, `Raklion/SelupanIntelligence.cs`, `Raklion/IronKnightIntelligence.cs`, `MiniGames/Doppelganger/DoppelgangerMonsterIntelligence.cs`, `MiniGames/ImperialGuardian/ImperialGuardianMonsterIntelligence.cs`, `CastleSiege/Intelligence/*.cs` (7 files). Not read: `Crywolf/CrywolfContext.cs` (1442 lines) and `Raklion/RaklionContext.cs` (679 lines): event flow, not monster AI - left for the event units.

| Rule | HNMO does | Suites | Result |
|---|---|---|---|
| Crywolf monsters | walk to goals by waypoints; Balgass escapes at low HP with a chance; skills with push/stun/remove effect/mana and AG decrease by configured chance; leader revives its group; ballista bombards | `CryWolf.xml` settings exist, not diffed in this unit | read, settings not compared |
| Selupan | patterns by HP, berserk adds damage per level, heal = 5 % of CURRENT health, invincibility, summon, teleport 10 tries, fall push | Raklion settings are in `Data/Events/Raklion.dat` (binary) | read, not compared (heal rule documented in RaklionEventDefinition) |
| Iron Knight | stab with configured chance instead of a normal hit | | read |
| Doppelganger | walks the path to the magic circle, attacks attackers or (if AttacksFirst) the nearest player in attack range; keeps ticking when unobserved (Pause is empty) on purpose | | read |
| Imperial Guardian | skills by chance (area, stun), rage once below RageHealthPercentage (+damage/defense) | `ImperialGuardian_Monster_Info.xml` not diffed | read, settings not compared |
| Castle Siege crown/switch/statue | crown: attackers only, current user kept; switch: participants only; statue regenerates every 5 s | | read |

## Owner questions

1. Blood Castle (and Chaos Castle, Kalima) monsters are much stronger in HNMO than in the suites (e.g. BC1 Chief Skeleton Warrior L56/HP 5000 vs L33/HP 1100). An HNMO update swapped BC7/BC8 deliberately. Keep HNMO's numbers, or use the suites'?
2. Kundun (275): HNMO has 140,000 HP and no HP refill; the suites have 5,000,000 HP plus a refill (400 per second and 15,000 every 600 s, if read as the classic server reads it). Use the suites' Kundun, and add the refill?
3. Kanturu (Maya, Nightmare) and Raklion monsters: HNMO's values are far above the suites' table. Keep?
4. Resistances: HNMO's ice/poison values match the suites for only 20 of 152 monsters, 142 monsters have no lightning resistance, and 4 map files use WaterResistance. Re-import all four resistances from the suites' table?
5. Respawn: several Lorencia/Noria/Atlans/Icarus monsters respawn in 3 s (suites 10 s; Phantom Knight/Great Drakan 30 s, Gorgon/Dark Phoenix 150 s). Keep the faster respawn?
6. Speeds and ranges: 35 move speeds, 50 attack speeds, 32 view ranges, 22 attack ranges, 29 move ranges differ (list in monster-diff.json, e.g. Karutan scorpions 400 vs 900 ms per step, Kentauros attack range 1 vs 4, Tanker move range 0 vs 6, Bone Scorpion defense 283 vs 485). Take the suites' values?
7. Field spawn counts: 26 small differences (Atlans Queen Rainer 6 vs 13, Kalima 6/7 Schriker 10 vs 16, ...). Take the suites' counts?
8. Monster targeting: should a monster hit from beyond its view range chase the attacker, and should it switch to the player who deals the most damage (classic behaviour, not settled by the suites' files)? This changes "pulling" a lot.
9. Leash: should monsters wander only inside their spawn area and walk back there (and heal) when pulled away? HNMO lets them drift from wherever they are.
10. Exp and drops: HNMO gives the whole kill to the last hitter; classic splits exp by damage and gives the drop to the top damage dealer. Change?

## PRs

- #212: Monster data: five stats with an extra zero (https://github.com/nguyentuanphu/HNMO/pull/212).
- #214: Elvenland monsters 418-425 as in the suites (https://github.com/nguyentuanphu/HNMO/pull/214).
