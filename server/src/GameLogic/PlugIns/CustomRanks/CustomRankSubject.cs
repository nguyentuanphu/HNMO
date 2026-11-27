// <copyright file="CustomRankSubject.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CustomRanks;

/// <summary>
/// Character metrics evaluated against custom rank tiers.
/// </summary>
/// <param name="CharacterName">The character's name.</param>
/// <param name="Level">Current normal level.</param>
/// <param name="MasterLevel">Current master level.</param>
/// <param name="Resets">Current reset count.</param>
/// <param name="VipLevel">Current VIP level (<c>0</c> if non-VIP).</param>
/// <param name="MissionsClaimed">Number of missions/achievements claimed or rewarded.</param>
/// <param name="ArenaRating">The account's HNMO Arena ranked rating (<c>0</c> if it never played a ranked duel).</param>
public sealed record CustomRankSubject(
    string CharacterName,
    int Level,
    int MasterLevel,
    int Resets,
    int VipLevel,
    int MissionsClaimed,
    int ArenaRating = 0);
