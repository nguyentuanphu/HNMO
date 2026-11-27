// <copyright file="OfflineTradeDecision.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.OfflineTrade;

/// <summary>
/// Decision produced by <see cref="OfflineTradeEngine"/>.
/// </summary>
/// <param name="Allowed">Whether starting off-trade is allowed.</param>
/// <param name="Reason">Refusal reason when <paramref name="Allowed"/> is <c>false</c>.</param>
/// <param name="ZenFee">Zen fee to charge upon start.</param>
/// <param name="MaxDurationHours">Maximum offline shop lifetime in hours (<c>0</c> = unlimited).</param>
/// <param name="StopWhenSoldOut">Whether to stop the offline session once all items sell.</param>
public readonly record struct OfflineTradeDecision(
    bool Allowed,
    OfflineTradeDenialReason Reason,
    int ZenFee,
    int MaxDurationHours,
    bool StopWhenSoldOut);
