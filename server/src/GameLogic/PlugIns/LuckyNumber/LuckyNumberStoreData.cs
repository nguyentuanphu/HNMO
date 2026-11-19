// <copyright file="LuckyNumberStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
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
