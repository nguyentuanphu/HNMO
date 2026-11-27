// <copyright file="CatchupCapStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CatchupCap;

using System.IO;
using System.Text.Json;

/// <summary>
/// Persistent state stored under section <c>catchupCap</c> in <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class CatchupCapStoreData
{
    /// <summary>Gets or sets UTC timestamp when the first player hit the level trigger.</summary>
    public DateTime? LevelTriggeredAtUtc { get; set; }

    /// <summary>Gets or sets character name of the first player who reached the initial level cap.</summary>
    public string LevelFirstPlayer { get; set; } = string.Empty;

    /// <summary>Gets or sets day key (yyyy-MM-dd in configured timezone) when level cap triggered.</summary>
    public string LevelTriggerDayKey { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC timestamp when the first player hit the reset trigger.</summary>
    public DateTime? ResetTriggeredAtUtc { get; set; }

    /// <summary>Gets or sets character name of the first player who hit the reset trigger.</summary>
    public string ResetFirstPlayer { get; set; } = string.Empty;

    /// <summary>Gets or sets day key when reset cap triggered.</summary>
    public string ResetTriggerDayKey { get; set; } = string.Empty;
}
