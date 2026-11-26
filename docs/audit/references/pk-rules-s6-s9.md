# Reference: Player Killer (PK) / Outlaw Rules (S6 & S9 Suites & Desktop Clients vs HNMO)

> Read-only audit from the Season 6 server suite config, Season 9 server suite PK system XML, Season 6.3 / Season 9 desktop client packs, desktop C++ source (`client/src/source`), and HNMO server (`server/src/GameLogic/OutlawPoints.cs`), captured 2026-11-27 (Sydney time).

## 1. Plain Answer to the Second-Kill Question

**After the second kill, does the character stay PK1 (`HeroState.PlayerKiller1stStage`, client level `5` / `PVP_MURDERER1`) for a real stretch of time in S6 and in S9, and for how long?**

- **In Season 6**: **Yes — for 3 hours (10,800 seconds)** under the default `PKTIME = 1` multiplier (unless reduced faster by killing monsters or cleared with `/pkclear` / PK clear). Classic S6 runs an independent countdown timer per PK stage (`3 * 60 * 60` seconds per stage, preserved in `OutlawPoints.FormerStageSeconds`), so entering PK1 after the 2nd kill gives a full 3-hour PK1 stage.
- **In Season 9**: **Yes (until cleared by monster kills or `/pkclear`, not dropped in 1 second)**. The S9 PK system XML defines cumulative `ReqPoint` thresholds (`0` Normal, `21,600` Warning, `43,200` PK Level 2 / client level `5`, `86,400` Murderer Level / client level `6`), `PointDeductionDivisor = 2`, and `MurdererPointIncrease = 3600`, with **no** `PKTIME` / per-second online decay setting — its own specification states players must hunt monsters (`MonsterLevel / 2`) or use `/pkclear` to clear points. Thus a character reaching `43,200` stays at PK1 / level `5` while idle until hunting monsters lowers points below `43,200`.
- **Why HNMO differs today (`server/src/GameLogic/OutlawPoints.cs`)**: HNMO combined **both** mechanisms — it sets points on the 2nd kill to the exact threshold (`43,200`, via `Math.Max(current, PlayerKiller1stStagePoints)`) **and** decays points by `1` per second online. As a result, `43,200` becomes `43,199` after **1 second**, immediately dropping the character back to `PlayerKillWarning` with ~12 hours of Warning points remaining.

---

## 2. Server Suite Configuration Values

### 2.1 Season 6 Common Config & Server INI
- **Common settings file** (PK section, lines 127–134):
  - `PKTIME = 1` (time multiplier)
  - `PKItemDrop = 0` (`1: On, 0: Off`)
  - `PkClearSwitch = 1` (`1: On, 0: Off`)
  - `PkClearCost = 1000000` (Zen cost for single PK clear)
  - `PkClearKillMultipler = 1` (`Price = PkClearCost * PK_Count`)
  - `PKLevelIncreaseOff = 0` (`1: On, 0: Off`)
  - `ApplyHeroSystem = 1` (`1: On, 0: Off`)
- **Common INI** (line 149): `CanEnterEventWithPK = 0`
- **GameServer INI** (`GameServer_Regular`, `GameServer_Siege`, `GameServer_Normal+CS`, lines 30 & 38):
  - `NonPK = 0`
  - `PkLimitFree = 0`
- **Absent in S6 config files on disk**: `PkLimitTime` / `PKLimitTime` does **not** appear in any S6 config/INI/XML file on disk, and there is no per-level PK time/points XML table in S6 (only `PKTIME = 1` in the common settings file).

### 2.2 Season 9 PK System File (and Common INI / Commands / Gens)
From the Season 9 PK system XML (8 `<PK>` nodes total: 4 in `<General>` + 4 in `<ExpDeduction>`), S9 common settings/INI, commands INI, and Gens INI:
- **Absent in S9 common settings file**: `PKTIME` and `PKLevelIncreaseOff` are removed (moved to the PK system XML).
- **PK system XML root attributes**:
  - `DisablePKLevelIncrease="0"`, `DisablePenalty="0"`, `PKCanUseshops="0"`, `DropExpensiveItems="0"`, `MaxItemLevelDrop="8"`, `PointDeductionDivisor="2"` (`MonsterLevel / PointDeductionDivisor`), `MurdererPointIncrease="3600"`
- **`PKClearCommand` element**:
  - `Enable="1"`, `Cost="1000000"`, `CostMultiplyByKillCount="1"` (`Commands` INI: `PKClearCMD = /pkclear`, `SetPKCMD = /setpk`)
- **Related S9 INI keys**:
  - `Common` INI line 142: `CanEnterEventWithPK = 0`
  - `GensSystem` INI line 75: `GensPkPenaltyAddNeedZenMapMove = 2` (2x warp Zen penalty vs non-Gens 50x)
  - `GameServer.ini` line 30: `NonPK = 0` (`PkLimitFree` absent)

#### S9 `<General>` (4 `<PK>` rows) & `<ExpDeduction>` (4 `<PK>` rows / `<LevelRange>`)
| `<General>` `Level` | Client Enum / Label (S6 -> S9 `Text_eng.bmd`) | `ReqPoint` | Item Lose Rate (Monster / Player) | Zen Deduction (Monster / Player) | Warp Multiplier | DL Summon | `<ExpDeduction>` `LevelRange` (`Start..End -> DeductionRate`) |
|---|---|---:|---:|---:|---:|---:|---|
| `3` (`Normal`) | `PVP_NEUTRAL` (`[488] Commoner`) | `0` | `6%` / `0%` | `4%` / `0%` | `1` | `1` | `11..150`: `3%`, `151..220`: `2%`, `221..MAX`: `1%` |
| `4` (`Warning`) | `PVP_CAUTION` (`[489] Outlaw Warning`) | `21600` | `25%` / `25%` | `4%` / `0%` | `1` | `1` | `11..150`: `5%`, `151..220`: `5%`, `221..MAX`: `5%` |
| `5` (`PK Level 2`) | `PVP_MURDERER1` (S6 `[490] 1st Stage Outlaw`, S9 `[490] Outlaw`) | `43200` | `50%` / `50%` | `4%` / `0%` | `50` | `0` | `11..150`: `10%`, `151..220`: `10%`, `221..MAX`: `10%` |
| `6` (`Murderer Level`) | `PVP_MURDERER2` (S6 `[491] 2nd Stage Outlaw`, S9 `[491] Stage`) | `86400` | `90%` / `90%` | `4%` / `0%` | `50` | `0` | `11..150`: `20%`, `151..220`: `20%`, `221..MAX`: `20%` |

*(Notes in S9 PK system XML header: from PK level 2 upward in S9 naming / `Level 5`+, players reconnect in Lorencia and cannot connect to Non-PVP servers; from Murderer / `Level 6`, monsters attack and any player may attack freely; Pasi the Mage in Lorencia displays remaining PK points.)*

---

## 3. Desktop Client Representation (S6.3 & S9 Client Source & `Text_eng.bmd`)

- **PK state constants** (`client/src/source/Core/Globals/_define.h:473-478`, `MonsterSetup.cpp:117-118`):
  - `1`: `PVP_HERO2` (`Hero` RGB `(100, 120, 255)`)
  - `2`: `PVP_HERO1` (`Hero` RGB `(140, 180, 255)`)
  - `3`: `PVP_NEUTRAL` (`Commoner` RGB `(200, 220, 255)`)
  - `4`: `PVP_CAUTION` (`Outlaw Warning` RGB `(255, 150, 60)` — comment `//pk1` in `Chat.cpp:142`)
  - `5`: `PVP_MURDERER1` (S6 `1st Stage Outlaw` / S9 `Outlaw`, RGB `(255, 80, 30)` — comment `//pk2` in `Chat.cpp:143`)
  - `6`: `PVP_MURDERER2` (S6 `2nd Stage Outlaw` / S9 `Stage`, RGB `(255, 0, 0)` — comment `//pk3` in `Chat.cpp:144`)
- **Text pack strings** (decoded from S6.3 & S9 `Data/Local/Eng/Text_eng.bmd`):
  - S6 (`3244` entries): `[487] "Hero"`, `[488] "Commoner"`, `[489] "Outlaw Warning"`, `[490] "1st Stage Outlaw"`, `[491] "2nd Stage Outlaw"`, `[2865] "You cannot enter if you are a 1st Stage Outlaw."`, `[3363] "Players with an Outlaw or Killer status cannot enter this area."`.
  - S9 (`4347` entries): `[487] "Hero"`, `[488] "Commoner"`, `[489] "Outlaw Warning"`, `[490] "Outlaw"`, `[491] "Stage"`, `[2865]` / `[3363]` identical plus `[3897] "Players with an Outlaw or Killer status cannot use this function."`, `[4409] "Cannot enter if a party member has Outlaw or Killer status."`.
- **Client gameplay checks**:
  - `MoveCommandWindow.cpp:289`: warp window gates normal moves with `Hero->PK < PVP_MURDERER1` (`< 5`; HNMO web/server matches at `HeroState.PlayerKiller1stStage` / state `5` paying 50x Zen, or 2x into Gens BattleZone on PR `#386`).
  - `ZzzCharacter.cpp` / `ZzzInterface.cpp`: guards, targeting without Ctrl, and forced PvP trigger at `>= PVP_MURDERER2` (`6`).
  - `PersonalShopTitleImp.cpp:204-260`: mirrors name colors across `PVP_CAUTION` (4), `PVP_MURDERER1` (5), `PVP_MURDERER2` (6).

---

## 4. Per-Level Progression & Decay Comparison (S6 vs S9 vs HNMO)

| Stage (Server `HeroState` / Client `PK`) | S6 (`CommonServer.cfg`) | S9 (`PlayerKillSystem` XML) | HNMO Today (`OutlawPoints.cs`) |
|---|---|---|---|
| **Normal** (`Normal = 3`) | `0` | `ReqPoint = 0` | `points == 0` |
| **Warning** (`PlayerKillWarning = 4`) | Enters on 1st kill (`3h` stage timer * `PKTIME=1`); clears to Normal when stage timer hits `0` | `ReqPoint = 21600`; drops on monster kill (`MobLv / 2`) | **1st kill**: sets `21,600`; held while `1..43,199`; `-1/sec` online & `-MobLv/2` per mob -> stays Warning `6h` |
| **PK1** (`PlayerKiller1stStage = 5`, `PK Level 2` / `1st Stage Outlaw`) | Enters on 2nd kill (**3h** stage timer -> drops to Warning or stays 3h at PK1) | `ReqPoint = 43200`; drops on monster kill (`MobLv / 2`); Warp `50x`, no shops (`PKCanUseshops=0`), no DL summon | **2nd kill**: `Math.Max(cur, 43_200)` sets **43,200**; held while `43,200..86,399`; `-1/sec` online & `-MobLv/2` -> **drops to Warning after 1 second** |
| **PK2 / Murderer** (`PlayerKiller2ndStage = 6`) | Enters on 3rd kill (`3h` stage timer per stage/step); extra kills reset/extend cap (`PKTIME = 1`) | `ReqPoint = 86400`; further kills add `MurdererPointIncrease = 3600` (uncapped); drops on monster kill (`MobLv / 2`) | **3rd kill**: sets `86,400`; **4th+ kill**: `+3,600`; held while `>= 86,400`; `-1/sec` -> drops to PK1 after 1 second (or after `(pts - 86,399)` sec), then holds PK1 for `12h` |
| **Online Time Decay** | `1 s/s` (`PKTIME = 1`) on per-stage timer | Not configured in XML (XML comment states clearing requires hunting monsters / `/pkclear`) | `1 pt/s` (`HeroStateRegeneration` every second online) |
| **Monster Kill Decay** | Reduces PK timer per monster kill | `- floor(MonsterLevel / 2)` (`PointDeductionDivisor = 2`) | `- floor(MonsterLevel / 2)` (`OutlawPoints.GetMonsterKillPoints`) |
| **After 1st / 2nd / 3rd / 4th+ Kills** | `4` (3h) -> `5` (3h) -> `6` (3h) -> `6` (+cap) | Thresholds: `21600` (`4`) -> `43200` (`5`) -> `86400` (`6`) -> `+3600` (`6`) | `21,600` (`4`, 6h) -> `43,200` (`5` for **1s**, then `4` for ~12h) -> `86,400` (`6` for **1s**, then `5` for 12h) -> `+3,600` (`6`) |
