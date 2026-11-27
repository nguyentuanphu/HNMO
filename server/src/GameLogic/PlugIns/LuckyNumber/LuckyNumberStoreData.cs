// <copyright file="LuckyNumberStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;

/// <summary>
/// Root store document stored in <see cref="HnmoStateStore"/> under key <c>luckyNumber</c>.
/// </summary>
public sealed class LuckyNumberStoreData
{
    /// <summary>Gets or sets recent daily rounds (newest first).</summary>
    public List<LuckyNumberRound> Rounds { get; set; } = [];

    /// <summary>Gets or sets unclaimed jewel rewards keyed by lowercase login name.</summary>
    public Dictionary<string, List<LuckyNumberOwedReward>> OwedByAccount { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
