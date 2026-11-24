# Reference: Party EXP, EXP Table Formulas, and Outlaw (PK) Settings (S6 & S9 Suites vs HNMO)

> Sanitized reference captured during the `server/src/GameLogic (files 3)` audit (2026-11-24) so future checks do not need to re-read these suite files.

## 1. Party Experience Bonuses (S6 & S9 Suite `CommonServer.cfg`)
Both Season 6 and Season 9 legacy suite configurations share the exact same party EXP bonus percentages:
- **Normal Party**:
  - 2 members: `160` (160%)
  - 3 members: `180` (180%)
  - 4 members: `200` (200%)
  - 5 members: `220` (220%)
- **Set Party** (distinct classes):
  - 3 members: `230` (230%)
  - 4 members: `270` (270%)
  - 5 members: `300` (300%)
- S9 suite `the suite's common settings file` additionally defines `AutoParty = 1` and party chat RGB `0x66, 0xCC, 0xFF`.

## 2. Normal & Master Experience Table Formulas (S6 & S9 Suite `ExpCalc.lua`)
Both Season 6 and Season 9 legacy suite scripts use the same formulas:
- **Normal EXP table (`Level`)**:
  - `Exp = 10 * Level * Level * (Level + 9)`
  - If `Level > 255`: `Exp = Exp + 1000 * (Level - 255) * (Level - 255) * ((Level - 255) + 9)`
- **Master EXP table (`MasterLevel`, `MaxNormalLevel`)**:
  - `TotalLevel = MasterLevel + MaxNormalLevel`
  - `Exp = (TotalLevel + 9) * TotalLevel * TotalLevel * 10 + ((TotalLevel - 255) + 9) * (TotalLevel - 255)^2 * 1000`
  - `Exp = (Exp - 3892250000) / 2`
  - If `MasterLevel > 200`: `Exp = Exp * ((TotalLevel - 600)^2 * 1.2 / 100000.0 + 1.0)`
- Both `ExpSystem.xml` files support `CalcType` (`0` Static only, `1` Static+Dynamic, `2` Static*Dynamic, `3` Dynamic only), `StaticExp` (`Normal="1000.0"`, `Master="1000.0"`, `Event="0.0"`, `Quest="1.0"`), and `DynamicExpRangeList` tiered by reset/level ranges.

## 3. Outlaw / PK Settings & HNMO (`OutlawPoints.cs`)
- **S6 `CommonServer.cfg`**:
  - `PKTIME = 1` (multiplier)
  - `PKItemDrop = 0`
  - `PkClearSwitch = 1`, `PkClearCost = 1_000_000`, `PkClearKillMultipler = 1` (`Price = PkClearCost * PK_Count`)
  - `PKLevelIncreaseOff = 0`
- **S9 `the suite's common settings file`**: `CanEnterEventWithPK = 0`
- **HNMO `OutlawPoints.cs`**:
  - Thresholds: `WarningPoints = 21_600` (6h), `PlayerKiller1stStagePoints = 43_200` (12h), `PlayerKiller2ndStagePoints = 86_400` (24h), `FurtherKillPoints = 3_600` (1h per kill while already 2nd stage murderer).
  - Monster kill reduction: `MonsterLevel / 2` (`MonsterLevelDivisor = 2`).
  - Legacy conversion: `ConvertFormerRemainingTime` converts old per-stage 3-hour (`10_800s`) timers cleanly to cumulative points.

## 4. HNMO `GameLogic (files 3)` Key Runtime Rules
- **`LicensedPlayerLimit.cs`**: `DefaultMaximum = 10`, shared across S6/S9/Arena via `ArenaShared`, bots excluded, 3s elsewhere-cache, 30s heartbeat, 1-week grace via `DeveloperHub.AllowOverLimit`.
- **`LoginAttemptGuard.cs`**: `5` failures per account / `20` per non-loopback address within `10 min` locks for `10 min`.
- **`MoneyDistribution.cs`**: scales in `double` precision by `Stats.MoneyAmountRate`, largest-remainder split across shares, redistributes forfeited shares, optional `ClampMoneyOnPickup`.
- **`Party.cs` & `PartyManager.cs`**: preserves offline slots via `OfflinePartyMember` (matching name + `CharacterId`), updates `Stats.NearbyPartyMemberCount`, health poll every `500 ms`, calls `RateTuningPlugIn.GetPartyExpMultiplierOrDefault`, applies `IExperienceCalculationPlugIn` and `IPlayerGainedExperiencePlugIn`.
