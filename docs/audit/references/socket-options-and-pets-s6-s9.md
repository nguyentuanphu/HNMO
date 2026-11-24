# Reference: Socket System Tables, Pets, Wings, Weapons, Orbs & Scrolls (S6 & S9 vs HNMO)

> Sanitized reference captured during the `server/src/Persistence/Initialization/VersionSeasonSix/Items (files 2)` audit (2026-11-24) so future checks do not need to re-read these files.

## 1. Socket Options (`S6` & `S9` Legacy Suites vs `SocketSystem.cs`)
Every Fire, Ice, Lightning, Water, Wind, Earth option (levels 1–5), Socket Bonus, and Package Bonus in `SocketSystem.cs` matches the Season 6 suite table (`SocketOptionSettings.ini`), with three Season 9 differences recorded below for Season 9 reconciliation:

| Element | Opt # | Description | Bonus Type | S6 Suite (Lv 1–5) | S9 Suite (Lv 1–5) | HNMO (`SocketSystem.cs`) |
| --- | --- | --- | --- | --- | --- | --- |
| Fire (`1`) | `0` | `(Level) Atk/Wiz Inc` | `TotalLevel / N` | `20, 19, 18, 17, 14` | Same | `1/20 .. 1/14` |
| Fire (`1`) | `1` | `Attack Speed Inc` | Flat | `7, 8, 9, 10, 11` | Same | `7, 8, 9, 10, 11` |
| Fire (`1`) | `2` | `Max Atk/Wiz Inc` | Flat | `30, 32, 35, 40, 50` | Same | `30, 32, 35, 40, 50` |
| Fire (`1`) | `3` | `Min Atk/Wiz Inc` | Flat | `20, 22, 25, 30, 35` | Same | `20, 22, 25, 30, 35` |
| Fire (`1`) | `4` | `Atk/Wiz Inc` | Flat | `20, 22, 25, 30, 35` | Same | `20, 22, 25, 30, 35` |
| Fire (`1`) | `5` | `AG Cost Dec` | `%` | `40, 41, 42, 43, 44` | Same | `0.40 .. 0.44` |
| Water (`2`) | `10` | `Block Rating Inc` | `%` | `10, 11, 12, 13, 14` | Same | `1.10 .. 1.14` (`DefenseRatePvm` mult) |
| Water (`2`) | `11` | `Defense Inc` | Flat | `30, 33, 36, 39, 42` | Same | `30, 33, 36, 39, 42` (`DefenseFinal`) |
| Water (`2`) | `12` | `Shield Prot Inc` | `%` | `7, 10, 15, 20, 20` | `7, 10, 15, 20, 30` | `0.07 .. 0.20, 0.20` |
| Water (`2`) | `13` | `Damage Reduction` | `%` | `4, 5, 6, 7, 8` | Same | `0.04 .. 0.08` |
| Water (`2`) | `14` | `Damage Reflection` | `%` | `5, 6, 7, 8, 9` | Same | `0.05 .. 0.09` |
| Ice (`3`) | `16` | `Monster Kill Life` | `1 / N` | `8, 7, 6, 5, 4` | Same | `1/8 .. 1/4` |
| Ice (`3`) | `17` | `Monster Kill Mana` | `1 / N` | `8, 7, 6, 5, 4` | Same | `1/8 .. 1/4` |
| Ice (`3`) | `18` | `Skill Attack Inc` | Flat | `37, 40, 45, 50, 60` | Same | `37, 40, 45, 50, 60` |
| Ice (`3`) | `19` | `Attack Rating Inc` | Flat | `25, 27, 30, 35, 40` | Same | `25, 27, 30, 35, 40` |
| Ice (`3`) | `20` | `Item Durability Inc` | `%` | `30, 32, 34, 36, 38` | Same | `0.30 .. 0.38` |
| Wind (`4`) | `21` | `Auto Life Rec Inc` | Flat | `8, 10, 13, 16, 20` | Same | `8, 10, 13, 16, 20` |
| Wind (`4`) | `22` | `Max Life Inc` | `%` | `4, 5, 6, 7, 8` | Same | `1.04 .. 1.08` |
| Wind (`4`) | `23` | `Max Mana Inc` | `%` | `4, 5, 6, 7, 8` | Same | `1.04 .. 1.08` |
| Wind (`4`) | `24` | `Auto Mana Rec Inc` | Flat | `7, 14, 21, 28, 35` | Same | `7, 14, 21, 28, 35` |
| Wind (`4`) | `25` | `Max AG Inc` | Flat | `25, 30, 35, 40, 50` | **`50, 70, 90, 110, 130`** | `25, 30, 35, 40, 50` (S6) |
| Wind (`4`) | `26` | `AG Value Inc` | Flat | `3, 5, 7, 10, 15` | **`5, 7, 9, 11, 13`** | `3, 5, 7, 10, 15` (S6) |
| Light (`5`) | `29` | `Excellent Dmg Inc` | Flat | `15, 20, 25, 30, 40` | Same | `15, 20, 25, 30, 40` |
| Light (`5`) | `30` | `Excellent Rate Inc` | `%` | `10, 11, 12, 13, 14` | Same | `0.10 .. 0.14` |
| Light (`5`) | `31` | `Critical Dmg Inc` | Flat | `30, 32, 35, 40, 50` | Same | `30, 32, 35, 40, 50` |
| Light (`5`) | `32` | `Critical Rate Inc` | `%` | `8, 9, 10, 11, 12` | Same | `0.08 .. 0.12` |
| Earth (`6`) | `36` | `Health Inc` | Flat | `30, 32, 34, 36, 38` | Same | `30, 32, 34, 36, 38` |

- **Socket Bonus Options** (chance `30%`; note S6 `Bonus #5 Max Life` had rate `100` in `.ini` vs `30` in S9 XML; HNMO uses `0.30f` for all 6):
  - Physical Weapons (`0..4`, `Fire+Light+Ice`): Atk `+11` (S9 Lv4 `+22`); (`Light+Ice+Fire`): Skill `+11` (S9 Lv4 `+22`).
  - Staves/Sticks (`5`): Atk/Wiz `+5` (S9 Lv4 `+10`); Skill `+11` (S9 Lv4 `+22`).
  - Armors/Shields (`Water+Earth+Wind`): Def `+24` (S9 Lv4 `+27`); (`Earth+Wind+Water`): Max HP `+29` (S9 Lv4 `+31`).
- **Special Package Options**:
  - `1` of each element: Double Damage Chance `+3%` (`100%` rate).
  - `1 Fire, 3 Water, 1 Ice, 3 Wind, 1 Lightning, 2 Earth`: Ignore Defense Chance `+1%` (`100%` rate).
- **Socket Crafting Mixes (`42`..`45`)**:
  - `42 SeedCreation`: 1× +4..+15 Exc + 1× +4..+15 Ancient + Chaos + Creation + Harmony + `1M` zen; 80% base / 90% max.
  - `43 SeedSphereCreation`: Chaos + Creation + Seed + Sphere + `1M` zen; 80% base / 90% max.
  - `44 MountSeedSphere`: Seed Sphere + Socket Item + Chaos + Creation + `1M` zen; 100%.
  - `45 RemoveSeedSphere`: Socket Item + `1M` zen; 100%.

## 2. Pets (`Pets.cs` & `S6 ItemList.xml`)
- Standard pets (`13:0` Angel, `13:1` Imp, `13:2` Uniria) drop from monsters (`ReqLevel` 23/28/25, `Durability = 255`); `13:3` Dinorant (`ReqLevel 110`, Fire Breath, `-10%` dmg taken, `+15%` atk dmg, `CanFly`, `0.3` option chance).
- Trainable pets: `13:4` Dark Horse (`ReqLevel 218`, Earthshake, `-15% - 0.5%/lvl` dmg taken, `5 + 2*lvl + Agi/20` def) and `13:5` Dark Raven (slot 1), `PetExperienceFormula = "level * level * level * 100 * (level + 10)"`, `MaximumItemLevel = 50`.
- `13:37` Fenrir (`ReqLevel 300`, `2x2`, Plasma Storm, `CanFly`), Black (`+10%` dmg), Blue (`-10%` dmg taken), Gold (`+0.5*TotalLevel` HP/MP, `TotalLevel/12` phys dmg, `TotalLevel/25` wiz dmg), plus `MovementSpeedCombinationBonus`.
- Cash shop pets (`Durability = 255`, `Repair = 0`): `13:64` Demon (`+40%` dmg, `+10` spd), `13:65` Spirit of Guardian (`-30%` dmg, `+50` HP), `13:67` Rudolf, `13:80` Panda (`+50%` EXP, `+50` DefFinal), `13:106` Unicorn (`1.5x` Zen, `+50` DefFinal, non-tradable), `13:123` Skeleton (`+20%` dmg, `+10` spd, `+30%` EXP, `+30%` w/ Skeleton Ring).

## 3. Potions, Weapons & Wings Notes
- **Halloween Buff Scrolls (`14:46..50`)**: S6 `IGC_ItemOptionManager.xml` specifies `Time="1800"` (30 minutes) for all 5; `Potions.cs:485` sets `JackOLanternBlessings` (`14:46`) to `32` minutes (`47..50` are `30` min).
- **Lv380 Weapons (`Weapons.cs:536-555`)**: Bone Blade (`0:22`), Explosion Blade (`0:23`), Phoenix Soul Star (`0:35`), Soleil Scepter (`2:14`), Sylph Wind Bow (`4:21`), Grand Viper Staff (`5:12`), Storm Blitz Stick (`5:19`) — matches S6/S9 `(Group, Number)` pairs.
- **Wings (`Wings.cs`)**: Small wings `12:130..135`, 1st `12:0..2,41`, 2nd `12:3..6,42,49` & `13:30`, 3rd `12:36..40,43,50`, `SecondWingOptionsUpdatePlugIn` adds Max AG & Attack Speed on 2nd wings (except Cape of Fighter).
