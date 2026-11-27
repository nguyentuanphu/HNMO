# Unit Audit: `server/src/GameLogic (files 3)`

- **Checked at**: `9321d2af` (2026-11-24)
- **Status**: `Checked`
- **Reference saved**: [party-exp-and-pk.md](../references/party-exp-and-pk.md)
- **Files read in full (25)**:
  - `server/src/GameLogic/LazyExtensions.cs`
  - `server/src/GameLogic/LicensedPlayerLimit.cs`
  - `server/src/GameLogic/LimitedObjectPool.cs`
  - `server/src/GameLogic/LocateableExtensions.cs`
  - `server/src/GameLogic/LoggerExtensions.cs`
  - `server/src/GameLogic/LoginAttemptGuard.cs`
  - `server/src/GameLogic/MagicEffect.cs`
  - `server/src/GameLogic/MagicEffectPowerUpExtensions.cs`
  - `server/src/GameLogic/MagicEffectsList.cs`
  - `server/src/GameLogic/MapHost.cs`
  - `server/src/GameLogic/MapInitializer.cs`
  - `server/src/GameLogic/MasterSkillExtensions.cs`
  - `server/src/GameLogic/MerchantStoreExtensions.cs`
  - `server/src/GameLogic/Metrics.cs`
  - `server/src/GameLogic/MoneyDistribution.cs`
  - `server/src/GameLogic/MoneyShare.cs`
  - `server/src/GameLogic/NullDropGenerator.cs`
  - `server/src/GameLogic/ObjectExtensions.cs`
  - `server/src/GameLogic/ObservableExtensions.cs`
  - `server/src/GameLogic/ObserverToWorldViewAdapter.cs`
  - `server/src/GameLogic/OfflinePartyMember.cs`
  - `server/src/GameLogic/OutlawPoints.cs`
  - `server/src/GameLogic/Party.cs`
  - `server/src/GameLogic/PartyManager.cs`
  - `server/src/GameLogic/PathFinderPoolingPolicy.cs`

## Comparison & Findings
1. **Party EXP eligibility (`Party.cs:513-535`) — edge-case difference**:
   - `InternalDistributeExperienceAfterKillAsync` includes every party member matching `p.Attributes is { } && (p == killer || killer.Observers.Contains(p))`, whereas `IsEligibleForMoney` (`Party.cs:284-290`) and `GetQuestDropItemGroupsAsync` (`Party.cs:255-258`) additionally check `!p.IsAtSafezone()` (and `GetQuestDropItemGroupsAsync` checks `p.IsAlive`). If a party member stands inside a safezone (or dead) while within the killer's observer range, they still receive EXP shares.
   - Party multiplier delegates cleanly to `RateTuningPlugIn.GetPartyExpMultiplierOrDefault(recipients)`, matching S6/S9 `160/180/200/220%` (normal) and `230/270/300%` (set party) when configured, and notifies `IExperienceCalculationPlugIn` and `IPlayerGainedExperiencePlugIn`.
2. **Master skill mana-rate formula (`MasterSkillExtensions.cs:15, 96`)**:
   - `FormulaManaRateIncrease = "(2 + (((((((level - 30) ^ 3) + 25000) / 499) / 6)))) * 1.5"` cached up to level 20.
3. **Outlaw (PK) points (`OutlawPoints.cs`)**:
   - Cumulative point thresholds (`21,600` warning / `43,200` 1st stage / `86,400` 2nd stage / `+3,600` per further kill / `monsterLevel / 2` reduction) and legacy 3-hour-stage conversion (`ConvertFormerRemainingTime`) are consistent.
4. **Licensed player limit & login guard (`LicensedPlayerLimit.cs`, `LoginAttemptGuard.cs`)**:
   - `LicensedPlayerLimit` enforces cross-instance account deduplication (3s `Elsewhere` cache, 30s heartbeat, bots exempt, 1-week grace via `DeveloperHub.AllowOverLimit`). Note: `Publish()` overwrites `_elsewhere` only on `Elsewhere()` refresh, so brief DB failure gracefully falls back to local count.
   - `LoginAttemptGuard` locks at 5 account failures / 20 non-loopback IP failures in a 10-minute window for 10 minutes.
5. **Magic effects & power-ups (`MagicEffect.cs`, `MagicEffectPowerUpExtensions.cs`, `MagicEffectsList.cs`)**:
   - `MagicEffectsList` uses identity-aware expiry cleanup (`ReferenceEquals(current, effect)`) and stale-entry recovery in `UpdateEffect`.
   - `MagicEffectPowerUpExtensions` applies `Stats.SleepStrBonusChance` on top of skill chance and PvP chance and chains master skill duration/power-up extensions.
6. **Money distribution, maps, observers, offline party reconnect**:
   - `MoneyDistribution` performs scaling in `double` precision to avoid `float` rounding above ~16.7M and distributes remainders via largest-remainder method.
   - `MapInitializer` respects `MonsterAmountPlugIn` multipliers on non-minigame automatic monster spawns and cleans up tracking on map/NPC disposal.
   - `PartyManager` restores renamed offline members using `OfflinePartyMember.CharacterId`.

## Re-check 2026-11-27 (changes 9321d2af..a108e846)

Commits read (every changed file read in full):

- 39443f93 fix(gamelogic): fix anticheat speed/time, season coin race, party master exp gate, chaos jewel count, and gens quest reward

Evidence:

- Party.AwardExperienceAsync: normal EXP stops at the catch-up cap; master EXP needs catch-up cap + config max + master class + SeasonPlan MasterLevels unlocked. DIFFERS from the owner decision of 26 Nov night ("bỏ điều kiện Season Plan cho EXP master"): the SeasonPlan gate is removed by PR #418 (open). Until #418 merges, a master-class character at the cap with the gate locked gets no EXP at all.
