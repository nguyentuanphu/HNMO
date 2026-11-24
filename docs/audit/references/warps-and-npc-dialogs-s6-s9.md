# Reference: Warp List, Teleport Ally Level Zones & PK Warp Rules (S6 & S9 Suites vs HNMO)

> Sanitized reference captured during the `server/src/GameLogic/PlayerActions (files 1)` audit (2026-11-24) so future checks do not need to re-read these suite files.

## 1. Warp Table (`IGC_MoveReq.xml` — S6 & S9)
Standard warp gates share the same base `MinLevel`, `MaxLevel`, `ReqMoney`, and `GateNumber` across S6 and S9 (`S6` includes `Arena=50`, `Stadium=49`, `Crywolf=48`; `S9` omits `Stadium` and `Crywolf` from `MoveReq.xml`):

| Warp | `MinLevel` | `MaxLevel` | `ReqMoney` | `GateNumber` |
|---|---|---|---|---|
| Arena (Index 1) | `50` | `400` | `2,000` | `50` |
| Stadium (Index 49, S6 only) | `50` | `400` | `2,000` | `345` |
| Lorencia (2) / Noria (3) / Elbeland (31) | `10` | `400` | `2,000` | `17` / `27` / `267` |
| Elbeland2 (32) / Elbeland3 (43) | `10` | `400` | `2,500` / `3,000` | `268` / `269` |
| Devias 1–4 (4–7) | `20` | `400` | `2,000..3,500` | `22, 72, 73, 74` |
| Dungeon 1–3 (8–10) | `30 / 40 / 50` | `400` | `3,000..4,000` | `2, 6, 10` |
| Atlans 1–3 (11–13) | `70 / 80 / 90` | `220` | `4,000..5,000` | `49, 75, 76` |
| LostTower 1–3 (14–16) | `50` | `400` | `5,000..6,000` | `42, 31, 33` |
| LostTower 4–5 (17–18) / 6–7 (19–20) | `60` / `70` | `400` | `6,500..8,000` | `35, 37, 39, 41` |
| Tarkan 1–2 (21–22) | `140` | `400` | `8,000 / 8,500` | `57, 77` |
| Aida 1–2 (25, 27) | `150` | `400` | `8,500` | `119, 140` |
| Kanturu Ruins 1–2 (28, 29) | `160` | `350` | `9,000` | `138, 141` |
| Kanturu Ruins 3 (45) | `160` | `400` | `15,000` | `334` |
| Kanturu Relics (30) | `230` | `400` | `12,000` | `139` |
| Icarus (23) / Karutan 1–2 (46–47) | `170` | `400` | `10,000 / 13,000 / 14,000` | `63 / 335 / 344` |
| LaCleon (34) | `280` | `400` | `15,000` | `287` |
| Crywolf (48, S6 only) | `300` | `400` | `15,000` | `118` |
| Swamp of Calmness (33) | `400` | `400` | `15,000` | `273` |
| Vulcanus (42) | `30` | `400` | `15,000` | `294` |

## 2. Teleport / Summon Zone Restrictions (`IGC_MoveLevel.xml`) & Quest Maps
- **`Teleport Ally` (`Skill 15`) / Summon**: Same-map party ally summon with `Range + 1`; prevents caster from being in safezone (`0x39` Stone, `0x3D` Stun, `0x48` Sleep, `0x92` Freeze2, `0x93` Earth Binds prevent teleport).
- **Barracks of Balgass (`Map 41`)**: NPC `407` Werewolf Quarrel, `3,000,000` Zen fee, requires active quest `5` or `6` (`LegacyQuestGroup 0`); warps nearby party members within range `10`.
- **Refuge of Balgass (`Map 42`)**: NPC `408` Gatekeeper, `0` Zen, requires active quest `6`; warps nearby party members within range `10`.
- **Loren Market (`Map 79`) <-> Lorencia (`Map 0`)**: NPC Julia (`NpcWindow.JuliaWarpMarketServer`), bidirectional warp.
- **PK Warp Cost Multiplier in HNMO (`WarpAction.cs:22`)**: `50x` base Zen cost when `HeroState >= PlayerKiller1stStage` (`>= PK level 2`).
- **Atlans Riding Restriction**: Uniria (`13:2`) and Dinorant (`13:3`) are blocked from entering Atlans (`Map 7`).
