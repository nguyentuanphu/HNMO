# Audit Findings: `server/src/GameLogic/Gens`

- **Unit**: `server/src/GameLogic/Gens` (11 files, 1,065 lines)
- **Status**: `Checked` on 2026-11-24
- **Reference saved**: [gens-system-s6-s9.md](file:///D:/WorkSpace/HNMO-ag/docs/audit/references/gens-system-s6-s9.md)

## Files Reviewed (in full)
1. `GensBattleZonePlugIn.cs` (1–162)
2. `GensConfiguration.cs` (1–282)
3. `GensFeaturePlugIn.cs` (1–39)
4. `GensKillContribution.cs` (1–35)
5. `GensMembershipPlugIn.cs` (1–52)
6. `GensPlayerExtensions.cs` (1–52)
7. `GensRankBonus.cs` (1–33)
8. `GensRankDefinition.cs` (1–35)
9. `GensRankingPlugIn.cs` (1–166)
10. `GensRankReward.cs` (1–37)
11. `GensRelationshipRulesPlugIn.cs` (1–161)

## Rules & Behavior Summary
- `GensBattleZonePlugIn`: Exempts opposing-Gens kills in Battle Zone maps (`63` Vulcanus by default) from PK/outlaw penalties; blocks non-Gens characters from warping into Battle Zones; excludes duels and self-kills; counts recent kills per `(killer, victim)` pair (`AbuseWarningKillCount = 3`, `AbuseLimitKillCount = 6`, `AbuseResetTime = 60 min`); awards/subtracts contribution (`gain = 0` when victim `< MinimumVictimContribution`) and recalculates rank.
- `GensConfiguration` / `GensRankDefinition` / `GensKillContribution` / `GensRankBonus` / `GensRankReward`: Defines MinLevel `50`, StartingContribution `10`, StartingRank `14`, ranks `1..14`, monthly rewards (`14:141..144`), level-diff contribution table, and rank-diff bonus table.
- `GensRankingPlugIn`: Periodically (default every `2 hours`) recalculates rankings across all active members sorted by `Contribution DESC, JoinedAt ASC, Id ASC`, purges expired `GensAbuse` rows, and pushes updated rank/position to online members via immutable `RankingSnapshot`.
- `GensRelationshipRulesPlugIn`: Enforces no party in Battle Zone (and kicks party members upon entering Battle Zone), no mixed-Gens party (`AllowPartyWithOtherGens = false`), and optional guild/alliance Gens rules (`GuildRequiresGens`, `AllianceRequiresSameGens`).
- `GensMembershipPlugIn` & `GensPlayerExtensions`: Loads `GensMember` upon world entry before map add so observers see it, and resends `ShowGensInfoAsync` on map entry/respawn.

## Discrepancies & Observations
1. **`GuildRequiresGens` and `AllianceRequiresSameGens` default to `false` (`GensConfiguration.cs:172,180`)**: Explicitly documented as differing from original rules (where S6/S9 `CanJoinGuildWhileOppositeGens = 0` and `CanJoinUnionWhileOppositeGens = 0` enforce matching Gens).
2. **`MinimumContribution = 10000` on Ranks `1..8` (`GensConfiguration.cs:108-115`)**: Prevents low-contribution characters from claiming Top-300 ranks (`1..8`) on low-population servers, whereas S6/S9 data files specify `ReqContributePoint = 0` for Classes `1..8`. (Intentional anti-exploit guard matching retail `>= 10,000` requirement.)
3. **Unimplemented S6/S9 Battle Zone extras in this module**: Opposite-Gens regular chat hiding (`CanSeeOppositeGensChat = 0`), PK Battle Zone warp Zen doubling (`PkPenaltyAddNeedZenMapMove = 2`), and per-map Battle Zone EXP/Drop bonus configs (`BattleZoneAddExperience`, S9 per-family map bonuses) are not handled in `GameLogic/Gens`.
4. **Bug/Gap check on `QuestCompletionAction.cs:184`**: `QuestRewardType.GensAttribution` quest rewards are unimplemented, so quest contribution cannot reach `GensMember`.
