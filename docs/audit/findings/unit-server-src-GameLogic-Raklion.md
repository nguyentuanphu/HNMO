# Audit Findings: `server/src/GameLogic/Raklion`

**Status**: Checked (all 9 files read in full, 1,820 lines)
**References**: [`invasions-and-s6-events-s6-s9.md`](../references/invasions-and-s6-events-s6-s9.md), [`diff-events-all.txt`](../data/diff-events-all.txt), [`ledger-combat-items-events-2026-11-22.md`](ledger-combat-items-events-2026-11-22.md)

## Scope Covered
- `IRaklionEventViewPlugIn.cs` (65 lines), `IronKnightIntelligence.cs` (58 lines), `RaklionContext.cs` (679 lines), `RaklionEventDefinition.cs` (257 lines), `RaklionPlugIn.cs` (113 lines), `RaklionState.cs` (66 lines), `SelupanIntelligence.cs` (455 lines), `SelupanSkill.cs` (56 lines), `SelupanState.cs` (62 lines).

## Rule & Suite Comparison

| File & Line | Rule / Formula | Suite / Reference Value | Verdict |
|---|---|---|---|
| `RaklionEventDefinition.cs:20-45` | Maps `57` (Raklion) / `58` (Hatchery); waves `1` (Spider Eggs), `2` (Selupan), `3` (Summons); `SpiderEggNotifyCount = 5`. | Matches S6/S9 Raklion maps `57`/`58` and `5..15` egg notification threshold (`diff-events-all.txt:77-78`). | Same |
| `RaklionEventDefinition.cs:50-71` | `SelupanAppearanceDelay = 5s`, `HatcheryCloseDelay = 5m` (`300s`), `HatcheryOpenDelay = 12h` (`43200s`), `PlayerRemovalDelay = 5m`, `SelupanSkillDelay = 1500ms`. | S9 suite: `appear=5s, close=300s, open=43200s`; S6 suite: `appear=5s, close=300s, open=300s` (`diff-events-all.txt:77-78`). | Same as S9 (differs from S6 `open=300s`) |
| `RaklionEventDefinition.cs:77-112` | Pattern thresholds `{80, 60, 50, 40, 20, 10}`% HP -> Patterns `1..7`; `BerserkDamagePerLevel = 300` with levels `{0, 1, 2, 2, 3, 4, 4}`; skills unlock at Pattern `2` (Freeze), `4` (Heal `5%`), `5` (Summon), `7` (Invincibility `10s`). | Matches S6/S9 Selupan 7-phase boss pattern progression and skill unlock cadence. | Same |
| `RaklionEventDefinition.cs:117-165` | Teleport `3..6` tiles; `FreezeDuration = 5s`; `AreaSkillRadius = 6`; `FallRadius = 4` (`<4` strict) & push `4`; `IceStrikePushDistance = 10`; `LimitedPushDistance = 2` for classes not in `FullyPushedCharacterClassNumbers = {4,6,7,12,13,16,17}` (DK, MG, DL). | Matches classic Selupan melee-class full pushback list (note Rage Fighter `24/25` receives limited `2`-tile push like Elf/DW/Sum). | Same |
| `IronKnightIntelligence.cs:27-57` | `IronKnightStabChance = 30%` checks target active, range, not in safezone, line of sight -> shows Iron Knight stab skill (`ShowIronKnightStabAsync`) instead of normal swing. | Custom configured default (`30%`; classic binary rate undocumented, `:172`). | HNMO configurable default |
| `RaklionPlugIn.cs:43-95`, `RaklionContext.cs:240-249,577-617` | Blocks warp gate entry into Hatchery once `State >= CloseDoor` (`CanEnterHatchery`); warps late entrants/post-battle players (`PlayerRemovalDelay`) out to safezone on next tick. | Matches S6/S9 Hatchery lock and post-battle eviction. | Same |
| `SelupanIntelligence.cs:130-165,276-449` | Weighted random skill selection (attacks weight `3` vs utility `2`), `Summon` gated by missing wave-3 summons, `Invincibility` gated while active, `Freeze` hits then applies stunned magic effect `61`, `Fall`/`IceStrike` apply class-based pushback, `Teleport` waits `500ms` vanish animation before relocating. | Faithful boss AI matching S6/S9 Selupan mechanics. | Same |

## Bugs / Open Issues
- **Intentional S9 parameter vs S6 (`RaklionEventDefinition.cs:60`)**: `HatcheryOpenDelay = 12h` matches S9 suite (`43200s`) rather than S6 (`300s`). Admin panel can tune this per season if needed.
- No functional code bugs found in the state machine or AI.
