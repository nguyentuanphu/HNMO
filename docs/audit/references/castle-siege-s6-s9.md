# Castle Siege & Land of Trials S6 vs S9 Reference

Derived from legacy Season 6/Season 9 `the suite's Castle Siege config file`, `CommonServer.cfg` (Hunt Zone drops & Mark of the Lord), and `the suite's common settings file` (`CastleOwnerGuildDestroyLimit`). Note that S6 and S9 share identical structure, upgrade tables, and rules in `the suite's Castle Siege config file` (only the battle hour in the sample timetable differs: S6 `20:00-22:00` vs S9 `08:00-10:00`).

## 1. General Flags & Combat Rules
| Setting | S6 / S9 Default | Description |
|---|---|---|
| `Enable` | `1` | Event active |
| `FriendlyFire` | `1` | Allows attacking self-team companions during siege |
| `ResetAccumulatedTimeAfterKill` | `0` | Whether killing Guild Master resets Crown accumulated time |
| `LordMixDailyLimit` | `1` | Daily Senior Mix / Lord Mix limit |
| `TowerEntryWithoutAlliance` | `0` | Allow Senior / Tower entry without alliance for castle winner |
| `DamageReduction OppositeTeam` | `40` | 40% PvP damage reduction vs opposite side during siege |
| `DamageReduction SelfTeam` | `20` | 20% PvP damage reduction vs self side during siege |
| `CastleOwnerGuildDestroyLimit` | `1` | Disallow disbanding guild while owning castle or during siege (`the suite's common settings file`) |

## 2. Land of Trials Drops & Mark of the Lord (`CommonServer.cfg`)
- `IsDropSetItemInCastleHuntZone = 1`
- `SetItemInCastleHuntZoneDropRate = 8` (`8 / 10000`, ancient drop rate)
- `SetItemInCastleHuntZoneDropLevel = 75` (min monster level)
- `MarkOfTheLord = 100` (`100 / 10000`, drop rate)

## 3. Cycle Timetable (`Days = 14` two-week cycle)
| Stage | Name | Day | Time (S6) | Time (S9) |
|---|---|---|---|---|
| `1` | Register Period | `0` | `00:00` | `00:00` |
| `2` | Idle | `1` | `23:59` | `23:59` |
| `3` | Register Mark Of The Lord | `2` | `00:00` | `00:00` |
| `4` | Idle | `4` | `00:00` | `00:00` |
| `5` | Castle Attack Guild Notification | `4` | `12:00` | `12:00` |
| `6` | Ready For Castle Siege | `4` | `19:00` | `19:00` |
| `7` | Castle Siege Start | `7` | `20:00` | `08:00` |
| `8` | Castle Siege End | `7` | `22:00` | `10:00` |
| `9` | End of Cycle | `14` | `00:00` | `00:00` |

## 4. NPC Layout (`NPCSettings`, Valley of Loren)
- **Guardian Statues (`283`)** (Side `1` / Defense, `DBSave=1`, base `MaxHP=1,500,000`):
  - `#1` `(94, 227)`, `#2` `(94, 182)`, `#3` `(82, 130)`, `#4` `(107, 130)`
- **Castle Gates (`277`)** (Side `1` / Defense, `DBSave=1`, base `MaxHP=1,900,000`):
  - `#1` `(93, 204)`, `#2` `(81, 161)`, `#3` `(107, 161)`, `#4` `(67, 114)`, `#5` `(93, 114)`, `#6` `(119, 114)`
- **Cannon Towers (`288`)** (Side `1` / Defense, `DBSave=0`, `MaxHP=3,000,000`):
  - 9 towers at `(75,130)`, `(88,130)`, `(99,130)`, `(114,130)`, `(94,151)`, `(88,182)`, `(100,182)`, `(94,221)`, `(94,233)`
- **Shield (`215`)**: `(94, 242)`, `MaxHP=10,000`
- **Defense Catapult / Slingshot Defense (`222`)**: `(80, 188)` and `(105, 188)`
- **Attack Catapult / Slingshot Attack (`221`)**: `(63, 19)` and `(119, 19)` (Dir `5`)

## 5. Defense, Health & Recovery Upgrade Tables
### Castle Gates (`277`)
| Level | Defense Value | Defense Cost (`JoG` / Zen) | Max HP Value | HP Cost (`JoG` / Zen) |
|---|---|---|---|---|
| `0` | `100` | — | `1,900,000` | — |
| `1` | `180` | `2` / `3,000,000` | `2,500,000` | `2` / `1,000,000` |
| `2` | `300` | `3` / `3,000,000` | `3,500,000` | `3` / `1,000,000` |
| `3` | `520` | `4` / `3,000,000` | `5,200,000` | `4` / `1,000,000` |

### Guardian Statues (`283`)
| Level | Defense Value | Defense Cost (`JoG` / Zen) | Max HP Value | HP Cost (`JoG` / Zen) | Regen Cost (`JoG` / Zen) |
|---|---|---|---|---|---|
| `0` | `80` | — | `1,500,000` | — | — |
| `1` | `180` | `3` / `3,000,000` | `2,200,000` | `3` / `1,000,000` | `3` / `5,000,000` |
| `2` | `340` | `5` / `3,000,000` | `3,400,000` | `5` / `1,000,000` | `5` / `5,000,000` |
| `3` | `550` | `7` / `3,000,000` | `5,000,000` | `7` / `1,000,000` | `7` / `5,000,000` |
