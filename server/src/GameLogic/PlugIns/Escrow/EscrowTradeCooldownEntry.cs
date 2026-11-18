// <copyright file="EscrowTradeCooldownEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;

/// <summary>
/// Temporary holding lock preventing items received through escrow from being re-traded too quickly.
/// </summary>
public sealed class EscrowTradeCooldownEntry
{
    /// <summary>Gets or sets unique cooldown entry identifier.</summary>
    public string CooldownId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets recipient account login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets item group.</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets item number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets item upgrade level.</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets item display name.</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC expiry timestamp of the trade lock.</summary>
    public DateTime LockedUntilUtc { get; set; }
}
