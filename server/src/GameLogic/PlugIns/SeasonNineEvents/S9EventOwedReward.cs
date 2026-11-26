// <copyright file="S9EventOwedReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineEvents;

using System;

/// <summary>
/// Pending Season 9 event item/Zen reward awaiting free bag space or next login.
/// </summary>
public sealed class S9EventOwedReward
{
    /// <summary>Gets or sets unique reward identifier.</summary>
    public string RewardId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets recipient account login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets recipient character name (or empty for any character on account).</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC timestamp when queued.</summary>
    public DateTime QueuedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets English reward reason shown on delivery.</summary>
    public string ReasonEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese reward reason shown on delivery.</summary>
    public string ReasonVi { get; set; } = string.Empty;

    /// <summary>Gets or sets Zen amount.</summary>
    public uint Zen { get; set; }

    /// <summary>Gets or sets optional item group (<c>-1</c> if none).</summary>
    public int ItemGroup { get; set; } = -1;

    /// <summary>Gets or sets optional item number.</summary>
    public short ItemNumber { get; set; }

    /// <summary>Gets or sets optional item level.</summary>
    public byte ItemLevel { get; set; }

    /// <summary>Gets or sets optional item count.</summary>
    public int ItemCount { get; set; }
}
