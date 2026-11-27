// <copyright file="CombiningLeftoversStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CombiningLeftovers;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>Persistent state stored under section <c>combiningLeftovers</c> in <see cref="HnmoStateStore"/>.</summary>
public sealed class CombiningLeftoversStoreData
{
    /// <summary>Gets or sets newest-first log entries.</summary>
    public List<LeftoversLogEntry> Log { get; set; } = [];

    /// <summary>Gets or sets pending item/Zen rewards owed when a character's bag was full, keyed by character ID.</summary>
    public Dictionary<string, List<GiftCodeOwedReward>> Owed { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
