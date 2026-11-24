# Parser Findings: Castle Siege (`diff-castle-siege.txt`)

- **Checked on**: 2026-11-24
- **Outputs**: [diff-castle-siege.txt](../data/diff-castle-siege.txt) (58 lines), [s9/diff-castle-siege.txt](../data/s9/diff-castle-siege.txt) (58 lines)
- **Sources**: the Season 6 suite's Castle Siege config file, the Season 9 suite's Castle Siege config file, both suites' common server config files, and HNMO `CastleSiegeInitializer.cs`, `CastleSiegeSchedule.cs`, `CastleSiegePlugIn.cs`, `CastleSiegeCrownMechanics.cs`

## Fix Summary

`parseSuiteCastleSiege` previously looked for `Days` on `<Cycle>` and `<State>` tags, producing `cycleDays: [0,...]` and `stateCount: 0`. The Castle Siege XML uses `<CycleSettings Days="14" ...>` with `<Cycle Stage="..." Day="..." Hour="..." Minute="..." />` child elements, plus separate `<DefenseUpgrade>`, `<HealthUpgrade>`, `<StatueHealthRecoveryUpgrade>`, and `<DefenseSettings>` blocks.

## Hand-Verified Sample Values

1. **Schedule cadence (`CycleSettings`)**:
   - Hand check in the Season 6 suite's Castle Siege config file: `<CycleSettings Days="14" ...>`, `<Cycle Stage="7" Day="7" Hour="20" Minute="0" />`, `<Cycle Stage="8" Day="7" Hour="22" Minute="0" />`.
   - Hand check in the Season 9 suite's Castle Siege config file: `<CycleSettings Days="14" ...>`, `<Cycle Stage="7" Day="7" Hour="8" Minute="0" />`, `<Cycle Stage="8" Day="7" Hour="10" Minute="0" />`.
   - HNMO `CastleSiegeInitializer.cs:200-212` & `CastleSiegeSchedule.cs:20`: `WeekDuration = 7` days, battle Saturday `20:00–22:00` (**differs** cadence `7d` vs `14d`; S6 hour `same`, S9 hour `differs`).
   - Parser output matches hand check: `# S6 cycles=9, cycleDays=14, npcs=24`, `# S9 cycles=9, cycleDays=14, npcs=24`.
2. **Gate HP Lv2 (`HealthUpgrade/Gates/Health`)**:
   - Hand check in both suite XMLs: `<Health Value="3500000" ReqJewelOfGuardian="3" ReqZen="1000000" />`.
   - HNMO `CastleSiegeInitializer.cs:259`: `AddUpgrade(configuration.GateLifeUpgrades, 2, 3, 1_000_000, 3_500_000)` (**same**).
3. **PvP Flags (`CastleSiegeEvent`, `DamageReduction`)**:
   - Hand check in both suite XMLs: `FriendlyFire="1"`, `<DamageReduction OppositeTeam="40" SelfTeam="20" />`.
   - HNMO `CastleSiegePlugIn.cs:147-154`: cancels same-side attacks (**differs**); team PvP damage reduction missing (**missing in HNMO**).
