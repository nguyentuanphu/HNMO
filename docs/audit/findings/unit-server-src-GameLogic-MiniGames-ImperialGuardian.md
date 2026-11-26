# Audit Findings: `server/src/GameLogic/MiniGames/ImperialGuardian`

**Status**: Checked (all 14 files read in full, 1,351 lines)
**References**: [`diff-events-all.txt`](../data/diff-events-all.txt), [`parser-events-all.md`](parser-events-all.md), [`ledger-combat-items-events-2026-11-22.md`](ledger-combat-items-events-2026-11-22.md)

## Scope Covered
- `ImperialGuardianContext.cs` (500 lines), `ImperialGuardianEventDefinition.cs` (255 lines), `ImperialGuardianMonsterIntelligence.cs` (115 lines), `ImperialGuardianGate.cs` (99 lines), `ImperialGuardianMonsterSkill.cs` (78 lines), `IImperialGuardianViewPlugIn.cs` (46 lines), `ImperialGuardianEnterResult.cs` (46 lines), `ImperialGuardianFeaturePlugIn.cs` (38 lines), `ImperialGuardianMonsterScaling.cs` (36 lines), `ImperialGuardianWeather.cs` (31 lines), `ImperialGuardianResult.cs` (26 lines), `ImperialGuardianTimerType.cs` (26 lines), `ImperialGuardianExperienceReward.cs` (21 lines), `ImperialGuardianFragmentDrop.cs` (21 lines).

## Rule & Suite Comparison

| File & Line | Rule / Formula | Suite / Reference Value | Verdict |
|---|---|---|---|
| `ImperialGuardianEventDefinition.cs:27-43`, `ImperialGuardianContext.cs:79,277` | `StandbyDuration = 60s` (Zone 0 uses `EnterDuration = 60s`), `ZoneDuration = 10m` (`600s`), `StartNextZoneWhenCleared = true`, `IsPartyRequired = true`, `ExitDuration = 60s` (`LootTime`), `CountdownDuration = 30s`. | S6/S9 suites (`ImperialGuardian.ini`): `StandByTime = 180s`, `RoundDurationTime = 600s` (next zone starts when timer finishes in classic mode), `LootTime = 20s` (`diff-events-all.txt:57-59`). | Differs on standby (`60s` vs `180s`), loot time (`60s` vs `20s`), and early zone advance (`StartNextZoneWhenCleared = true`); Same on `600s` zone timer |
| `ImperialGuardianEventDefinition.cs:54-88,206` | Day-indexed waves `(day * 10) + zone` (`1..6` Mon-Sat maps, `7` Sunday map); Blocking gates `{524,525,527,528}`, Early attackable `{525,528}`, Late attackable `{524,526,527}`, Traps `{523}`; Mon-Sat bosses `508/509/510/511/507/506` drop Secromicon fragments `14:103..108` (`1/2/3` count at `50/29/21%`). | Matches S6/S9 daily rotation, gate unlock sequence, and Secromicon fragments `1..6`. | Same |
| `ImperialGuardianEventDefinition.cs:93-107,183-198` | Default `ExperienceRewards` has a single fallback tier `100,000` EXP (`x2` on Sunday) unless configured by initializer/admin; `MonsterScalings` empty by default; `RageHealthPercentage = 0` by default (boss rage disabled unless >0 configured, `RageSkill = 59`, `RageBonus = 150`). | Differs from S6/S9 when not populated by initializer/admin: classic suites scale IG EXP & monster stats by level and trigger boss rage below HP threshold. | Differs (`RageHealthPercentage = 0` default, check initializer) |
| `ImperialGuardianEventDefinition.cs:112,117-177`, `ImperialGuardianMonsterIntelligence.cs:40-114` | `SkillChance = 30%`; configures AoE/single skills and stun chances for `504..517` bosses/minions; triggers rage buff (`+RageBonus` phys dmg & def) once when HP falls `<= RageHealthPercentage`. | Matches S6/S9 IG boss skills and animations (`504` Gaion..`517` Head Mounter). | Same |
| `ImperialGuardianGate.cs:53-67`, `ImperialGuardianContext.cs:137-141,448-460` | Gates dynamically toggle `TerrainAttributeType.Water` across `4`-directions in front of spawn (`Direction - 1`), blocking movement until gate dies. | Matches client Imperial Guardian terrain blocking (`TerrainAttributeType.Water`). | Same |
| `ImperialGuardianContext.cs:253-268` | Fails immediately when `PlayerCount == 0` or `ZoneDuration` expires before clear; ignores Reflect/Poison/Bleeding on gates (`ImperialGuardianGate.cs:70-88`). | Prevents empty instance hanging and gate DoT exploits. | Sound |

## Bugs / Open Issues
- **Default configuration vs suite (`ImperialGuardianEventDefinition.cs:27,38,183`)**: `StandbyDuration` (`60s` vs `180s`), `ExitDuration` (`60s` vs `20s`), and `StartNextZoneWhenCleared = true` differ from classic INI defaults (though friendlier to players); `RageHealthPercentage = 0` disables IG boss rage unless configured >0. Record in `differences-summary.md` for owner review.
