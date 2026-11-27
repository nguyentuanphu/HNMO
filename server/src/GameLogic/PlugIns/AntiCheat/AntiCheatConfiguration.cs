// <copyright file="AntiCheatConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AntiCheat;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="AntiCheatPlugIn"/> (per-IP connection cap, attack rate authority, safe-zone audit, and violation log).
/// </summary>
public sealed class AntiCheatConfiguration
{
    /// <summary>Gets or sets a value indicating whether enforcement is active. Off by default.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the shared HNMO state file path.</summary>
    [StringLength(200)]
    public string FileName { get; set; } = "../save/" + HnmoStateStore.StateFileName;

    /// <summary>Gets or sets maximum simultaneous connections allowed from a single IP address (0 = unlimited).</summary>
    [Range(0, 100)]
    public int MaxConnectionsPerIp { get; set; } = 3;

    /// <summary>Gets or sets a value indicating whether loopback addresses (127.0.0.1 / ::1) are exempt from the per-IP limit (useful for local web bridge).</summary>
    public bool ExemptLoopbackFromIpLimit { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether server-side attack interval validation is active.</summary>
    public bool EnableAttackFrequencyCheck { get; set; } = true;

    /// <summary>Gets or sets base minimum milliseconds allowed between attacks before Agility speed scaling applies.</summary>
    [Range(50, 2000)]
    public int BaseMinAttackIntervalMs { get; set; } = 420;

    /// <summary>Gets or sets minimum floor in milliseconds between attacks regardless of Agility or speed buffs.</summary>
    [Range(30, 1000)]
    public int MinimumAttackIntervalFloorMs { get; set; } = 70;

    /// <summary>Gets or sets tolerance multiplier (&gt;= 0.5) applied to computed interval to avoid false positives under network jitter.</summary>
    [Range(0.3, 1.5)]
    public double LatencyToleranceFactor { get; set; } = 0.7;

    /// <summary>Gets or sets a value indicating whether attacks attempted by players above <see cref="HitHackMaxAgility"/> trigger a hit-hack flag.</summary>
    public bool EnableHitHackAgilityCheck { get; set; }

    /// <summary>Gets or sets maximum agility threshold when <see cref="EnableHitHackAgilityCheck"/> is enabled.</summary>
    [Range(1000, 65535)]
    public int HitHackMaxAgility { get; set; } = 32767;

    /// <summary>Gets or sets number of violations within <see cref="StrikeWindowMinutes"/> before disconnecting the player (0 = log only).</summary>
    [Range(0, 1000)]
    public int StrikesBeforeDisconnect { get; set; } = 10;

    /// <summary>Gets or sets the rolling window in minutes for strike accumulation.</summary>
    [Range(1, 1440)]
    public int StrikeWindowMinutes { get; set; } = 10;

    /// <summary>Gets or sets a value indicating whether blocked safe-zone attack attempts are recorded to the violation log.</summary>
    public bool LogSafeZoneAttempts { get; set; } = true;

    /// <summary>Gets or sets maximum recent audit entries kept in storage.</summary>
    [Range(50, 5000)]
    public int MaxAuditEntries { get; set; } = 500;
}
