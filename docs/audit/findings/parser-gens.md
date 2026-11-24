# Parser Findings: Gens System (`diff-gens.txt`)

- **Checked on**: 2026-11-24
- **Outputs**: [diff-gens.txt](../data/diff-gens.txt) (35 lines), [s9/diff-gens.txt](../data/s9/diff-gens.txt) (35 lines)
- **Sources**: the Season 6 suite's Gens system INI and Gens ranking table files, the Season 9 suite's Gens system XML file, and HNMO `server/src/GameLogic/Gens/GensConfiguration.cs`

## Fix Summary

- Season 6 `GensSystem.ini` uses section `[Gens]` rather than `[GensSystem]`/`[General]`, and `GensRanking.dat` lists its 14 rank rows without a leading section header (`0`).
- Season 9 `GensSystem.xml` uses `<General>`, `<RewardSettings>`, `<RankingSettings><Gens ... /></RankingSettings>`, and `<BattleZoneSettings><MapList><Map ... /></MapList>` rather than `<Rank>` / `<Reward>`.

## Hand-Verified Sample Values

1. **Social & Anti-Abuse Limits**:
   - Hand check in the Season 6 suite's Gens INI (`[Gens]`): `GensInitialContributePoint = 10`, `GensAbusingPenaltyWarning = 3`, `GensAbusingPenaltyLimit = 6`, `GensAbusingResetTime = 60`, `GensCanJoinPartyWhileOppositeGens = 0`, `GensCanJoinGuildWhileOppositeGens = 0`.
   - Hand check in the Season 9 suite's Gens XML: `<GensSystem InitialContributePoint="10" AbusingPenaltyWarning="3" AbusingPenaltyLimit="6" AbusingResetTime="60" ...>`, `<General CanJoinPartyWhileOppositeGens="0" CanJoinGuildWhileOppositeGens="0" ... />`, `<RewardSettings StartDay="1" EndDay="7" MinRankToGainReward="300" />`.
   - HNMO `GensConfiguration.cs:22,91,96,101,132-137,157,172`: `StartingContribution = 10`, `AbuseWarning = 3`, `AbuseLimit = 6`, `AbuseResetTime = 60m`, `RewardStartDay = 1..7`, `AllowPartyWithOtherGens = false` (**same**); `GuildRequiresGens = false` and `AllianceRequiresSameGens = false` (**differs**).
2. **Rank 1 (Grand Duke) & Rank 9 (Knight)**:
   - Hand check in the Season 6 suite's Gens ranking table and Season 9 suite's Gens XML: Class `1` -> `ReqContributePoint = 0`, `RankingPosition = 1`, reward `14:141` (`Shinning Jewellery Case`) x `30`; Class `9` -> `ReqContributePoint = 10000`, `RankingPosition = 0`, reward `0:0 x 0`.
   - HNMO `GensConfiguration.cs:108,116,144`: Rank `1` sets `MinimumContribution = 10000`, `MaximumRankingPosition = 1`, reward `14:141` x `30` (**differs** via 10k anti-low-pop guard on top 300 ranks, reward **same**); Rank `9` (`10000`) **same**.
