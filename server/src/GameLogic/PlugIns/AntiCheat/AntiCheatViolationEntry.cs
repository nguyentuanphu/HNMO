// <copyright file="AntiCheatViolationEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AntiCheat;

using System;

/// <summary>
/// Recorded anti-cheat or connection-cap event stored in <see cref="HnmoStateStore"/> (<c>antiCheatAudit</c>).
/// </summary>
public sealed class AntiCheatViolationEntry
{
    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>Gets or sets Sydney local timestamp.</summary>
    public string OccurredAtSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets account login name.</summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>Gets or sets character name.</summary>
    public string Character { get; set; } = string.Empty;

    /// <summary>Gets or sets normalized client IP address.</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>Gets or sets violation type (<c>IpLimit</c>, <c>AttackRate</c>, <c>AgilityCap</c>, <c>SafeZoneAttack</c>).</summary>
    public string ViolationType { get; set; } = string.Empty;

    /// <summary>Gets or sets human-readable diagnostic summary.</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>Gets or sets enforcement action taken (<c>Logged</c>, <c>Blocked</c>, <c>Disconnected</c>).</summary>
    public string ActionTaken { get; set; } = "Logged";
}
