// <copyright file="GuildOwedReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A ladder reward which waits for room in the bag (zen and items; the coins are given at once).
/// </summary>
public sealed class GuildOwedReward
{
    /// <summary>Gets or sets the reference; it makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the reason (logs).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the week.</summary>
    public string Week { get; set; } = string.Empty;

    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the reward (zen and items).</summary>
    public MissionReward Reward { get; set; } = new();
}
