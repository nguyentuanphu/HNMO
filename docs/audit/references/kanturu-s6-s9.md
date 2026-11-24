# Kanturu (Maya / Nightmare / Tower of Refinement) S6 vs S9 Reference

Derived from legacy Season 6 event configuration (`Kanturu.dat`, `KanturuMonsterSetBase.txt`) and Season 9 event configuration (`IGC_KanturuEvent.xml`).

## 1. Event Enabling, Entry & Drop Rules
| Setting | Season 6 | Season 9 |
|---|---|---|
| Event Enable | Config / Event on (`1`) | `Enable="1"` |
| Moonstone Pendant Check | Check value `1` (required to enter / stay during Maya & Nightmare) | `MoonStoneCheck="1"` (Item `13:38`) |
| Moonstone Pendant Drop Rate (Kanturu Relics) | Server drop config | `MoonStoneDropRate="10"` (`n / 10000`) |
| Jewel of Harmony Drop Rate (Kanturu maps) | Server drop config | `JewelOfHarmonyDropRate="20"` (`n / 10000`) |

## 2. General States (`KANTURU_STATE_TYPE`)
| State ID | Meaning | SwitchType | Notes |
|---|---|---|---|
| `0` | `None` | `0` (GameServer) | Inactive |
| `1` | `BattleStandby` | `0` (GameServer) | Sub-cycles: `0 None`, `1 Start = 1140s`, `2 Notify = 60s`, `3 End` |
| `2` | `BattleOfMaya` | `0` (GameServer) | 3 battle waves + hands of Maya |
| `3` | `BattleOfNightmare` | `0` (GameServer) | Guardian wave + Nightmare boss |
| `4` | `TowerOfRefinement` | `0` (GameServer) | Post-victory open window + closing warning |
| `5` | `End` | `1` (`0s`) | Event teardown |

## 3. Battle of Maya (`State = 2`)
S6 and S9 define identical timings and mechanics:
- **IceStormCastRate**: `60 / 10000`
- **Battle Time Limits** (`BattleTimes`):
  - Battle `0` (Wave 1 — one hand): `900s` (15 min)
  - Battle `1` (Wave 2 — other hand): `900s` (15 min)
  - Battle `2` (Wave 3 — both hands): `1200s` (20 min)
- **AI Settings**:
  - AI Group Numbers: `LeftHand = 20`, `RightHand = 21`, `Hands = 22`
  - AI Change Timers: `3s`, `60s`, `60s`, `900s`
- **Detail State Cycles (`0..18`)**:
  - `0 None` (`0s`)
  - `1 Standby1` (`120s`), `2 Notify` (`15s`), `3 Start1` (GS), `4 Maya1` (GS), `5 End Maya1` (`5s`), `6 End-cycle` (`3s`)
  - `7 Standby2` (`60s`), `8 Start2` (GS), `9 Maya2` (GS), `10 End Maya2` (`5s`), `11 End-cycle` (`3s`)
  - `12 Standby3` (`120s`), `13 Start3` (GS), `14 Maya3` (GS), `15 End Maya3` (`5s`), `16 End-cycle` (`10s`)
  - `17 End` (`3s`), `18 End-cycle` (GS)

### Monster Spawn Counts (S6 `KanturuMonsterSetBase.txt`)
- **Stage 0 (Wave 1)**: `364` Maya body `(188, 110)` + `30` Blade Hunter (`354`) + `10` Dreadfear (`360`) = **40 monsters**.
- **Stage 1 (Wave 2)**: `364` Maya body + `12` Twin Tail (`359`) + `8` Dreadfear (`360`) = **20 monsters**.
- **Stage 2 (Nightmare Stage)**: `4` Dreadfear (`360`) + `4` Persona (`358`) + `7` Genocider (`357`) = **15 guardians** (+ `361` Nightmare `(78, 137)`).
- **Stage 3/4/5**: `362` Left Hand (`202, 83`), `363` Right Hand (`189, 82`), and both hands + `364` Maya (`188, 110`).
- **Stage 6 (Nightmare Summons)**: `7` Dreadfear (`360`) around `X=75..82, Y=111..133`.

## 4. Battle of Nightmare (`State = 3`) & Tower of Refinement (`State = 4`)
- **Nightmare AI Group**: `23` (handles HP-triggered teleports and minion waves).
- **Nightmare Detail Cycles**: `0 None`, `1 Idle = 5s`, `2 Notify = 10s`, `3 Start = 1200s` (20 min battle limit), `4 End = 5s`, `5 End-Cycle` (GS).
- **Tower of Refinement Detail Cycles**:
  - `0 None`
  - `1 Revitalization = 82500s` (22h 55m open)
  - `2 Notify = 300s` (5m closing warning)
  - `3 Close = 2220s` (37m post-close)
  - `4 Notify = 180s` (3m final eject warning)
  - `5 End = 5s`
  - Total open window before closing warning completion: `82800s` (**23 hours**).
