# Reference: Scheduled Invasions & Season 6 Token/LMS Events (S6 & S9 vs HNMO)

> Sanitized reference captured during the `InvasionEvents` and `S6Events` audits (2026-11-24).

## 1. Golden Invasion (`S9` `the suite's Golden Invasion config file`, `S6` `the suite's Golden Invasion config file`, `the suite's invasion manager config file`)
- **Classic Golden Monsters (`0..8`)**:
  - `53` Golden Titan (Map `2` Devias, count `1`), `54` Golden Soldier (Map `2` Devias, count `2`), `78` Golden Goblin (Map `3` Noria, count `1`), `79` Golden Dragon (Maps `0, 2, 3, 4` Lorencia/Noria/Devias/LostTower, count `2`), `80` Golden Lizard King (Map `7` Atlans, count `1`), `81` Golden Vepar (Map `7` Atlans, count `2`), `82` Golden Tantalos (Map `8` Tarkan, count `1`), `83` Golden Wheel (Map `8` Tarkan, count `2`).
- **Season 6+ Expanded Golden Monsters (`S6`/`S9`)**:
  - `493` Golden Dark Knight (Map `1` Dungeon, `1`), `494` Golden Devil (Map `4` LostTower, `1`), `495` Golden Stone Golem (Map `33` Aida, `1`), `496` Golden Crust (Map `10` Icarus, `1`), `497` Golden Satyros (Map `37` Kanturu Ruins, `1`), `498` Golden Twin Tail (Map `38` Kanturu Relics, `1`), `499` Golden Iron Knight (Map `57` Raklion, `1`), `500` Golden Napin (Map `56` Swamp of Calmness, `1`), `501` Great Golden Dragon (Maps `37, 57`, `1`), `502` Golden Rabbit (Map `51` Elbeland, `1`).
- **S6 Alternate Regen Mode (`the suite's Golden Invasion config file`)**: Respawn after `60` minutes if not killed (`EnableInvasion = 0` when managed via schedule manager).

## 2. White Wizard Invasion (`S9` `the suite's White Wizard invasion config file`)
- Schedule: `ToOpen = 3 min`, `PlayDuration = 30 min`, `ToClose = 25 min`; maps Lorencia (`0`), Noria (`3`), Devias (`2`).
- Spawn list: `1` x `135` White Wizard (`Respawn=0`, `AttackFirst=1`), `15` x `136` Destructive Ogre Soldier (`Respawn=0`, `AttackFirst=0`), `15` x `137` Destructive Ogre Archer (`Respawn=0`, `AttackFirst=1`).

## 3. Last Man Standing (`S9` `the suite's Last Man Standing config file`)
- `DieCount = 2`, `MinPlayers = 2`, `MaxPlayers = 500`, `Duration = 15 min`, `Registration.Time = 15 min` (NPC `258` on Map `0` Lorencia `(145, 140)`, `ReqMoney = 1_000_000`), arena Map `6` (Stadium/Arena) with reset-tiered rooms (`0..10` through `71..80`).

## 4. Cherry Blossom Spirit & Lucky Coin Exchanges (`S6` ItemBags)
- **Cherry Blossom (`NPC 450`, Golden Branch `14:90`)**:
  - Tiers `10` / `50` / `255` branches; `Mix_Cherry_Blossom_Gold_Reward` grants rings/pendants exc, sets/weapons exc, Ancient set pieces (`Rate="1000"`), jewels (`Chaos/Bless/Soul/Life/Creation/Harmony`), and pets (`Panda/Skeleton`).
- **Delgado Lucky Coin (`NPC 478`, Lucky Coin `14:100`)**:
  - Tiers `10` / `20` / `30` coins; `Mix_LuckyCoin(10)` drops `+5..+7` non-exc + exc equipment/jewels; `(20)` higher-rate exc/jewels; `(30)` `Cat 12 Index 100` (`+0..+5`, custom Lucky Ticket / ticket box).
  - HNMO `S6EventsConfiguration` adapts these into lightweight deterministic/weighted Zen + WCoinC + consumable/jewel tables.
