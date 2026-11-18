// <copyright file="S6EventsOwedReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;

/// <summary>
/// Queued item/Zen reward awaiting free inventory space or next login delivery.
/// </summary>
public sealed class S6EventsOwedReward
{
    /// <summary>Gets or sets unique reward identifier.</summary>
    public string RewardId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets recipient account login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets recipient character name (empty = any character on account).</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets English reward reason.</summary>
    public string ReasonEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese reward reason.</summary>
    public string ReasonVi { get; set; } = string.Empty;

    /// <summary>Gets or sets Zen amount.</summary>
    public uint Zen { get; set; }

    /// <summary>Gets or sets item group (-1 = none).</summary>
    public int ItemGroup { get; set; } = -1;

    /// <summary>Gets or sets item number.</summary>
    public short ItemNumber { get; set; }

    /// <summary>Gets or sets item upgrade level.</summary>
    public byte ItemLevel { get; set; }

    /// <summary>Gets or sets item count.</summary>
    public int ItemCount { get; set; } = 1;

    /// <summary>Gets or sets UTC creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
