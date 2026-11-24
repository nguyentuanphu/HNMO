# Unit Audit Findings — `server/src/GameLogic/PlugIns/WorldBosses`

- **Unit**: `server/src/GameLogic/PlugIns/WorldBosses` (24 files, 3,139 lines)
- **Checked on**: 2026-11-24

## Scope Covered

- `CastleSiegeWeeklyConfiguration.cs`, `RankCoins.cs`, `WeeklyRankingConfiguration.cs`, `WeeklyRankingEntry.cs`, `WeeklyResult.cs`, `WeeklyWinner.cs`, `WorldBossAction.cs`, `WorldBossAliveState.cs`, `WorldBossAnnouncement.cs`, `WorldBossBoardEvent.cs`, `WorldBossBoardWriter.cs`, `WorldBossDamageEntry.cs`, `WorldBossDefinition.cs`, `WorldBossEngine.cs`, `WorldBossesConfiguration.cs`, `WorldBossesPlugIn.cs`, `WorldBossKillLine.cs`, `WorldBossKillOutcome.cs`, `WorldBossKillRecord.cs`, `WorldBossOwedReward.cs`, `WorldBossSchedule.cs`, `WorldBossState.cs`, `WorldBossStore.cs`, `WorldBossStoreData.cs`

## Findings & Bugs

1. **Party grouping drops `Party` key when Party Master is offline (`WorldBossesPlugIn.cs:239`)**:
   - `var party = (player.Party?.PartyMaster as Player)?.SelectedCharacter?.Name;` casts `PartyMaster` (`IPartyMember`) to `Player`. When a party master temporarily disconnects (becoming `OfflinePartyMember`), `as Player` is `null` and online party members are ranked solo instead of grouped under `player.Party?.PartyMaster?.Name`.
2. **Custom HNMO feature logic (OK)**:
   - Schedule timezone/DST handling (`WorldBossSchedule.ToUtc`), damage tracking (clamped per hit to `MaximumHealth`), party-vs-player ranking, participation thresholds (`ParticipationMinimumPercent` / `ParticipationMinimumDamage`), `WorldBossOwedReward` atomic Zen/Item delivery + idempotent `CoinGrant` references, weekly Monday rollover (`RollWeek`), MiniGame/CastleSiege weekly rankings, and cross-plugin kill hooks (`CommunityPlugIn`, `TopRacesPlugIn`, `SeasonPlanPlugIn`, `GuildsPlugIn`) are solid and race-safe under `Store.Lock` + `SpawnLock`.
