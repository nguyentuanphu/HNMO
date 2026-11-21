// <copyright file="WorldBossOwedReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A reward which waits to be given: its zen and items need the character in the game with room in the bag; its
/// coins are given at once (to the account).
/// </summary>
public sealed class WorldBossOwedReward
{
    /// <summary>Gets or sets the reference; it makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the reason (for the coins history and the logs).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the English name of the boss.</summary>
    public string BossEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name of the boss.</summary>
    public string BossVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the tier, e.g. "top1".</summary>
    public string Tier { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name of the account.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward.</summary>
    public MissionReward Reward { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the coins were given.</summary>
    public bool CoinsGiven { get; set; }

    /// <summary>Gets or sets when it was earned.</summary>
    public DateTime EarnedAtUtc { get; set; }
}
