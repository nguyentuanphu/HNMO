// <copyright file="AntiCheatEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AntiCheat;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

/// <summary>
/// Pure anti-cheat evaluation rules for per-IP connection caps, agility-aware attack interval limits, and rolling strike disconnects.
/// </summary>
public static class AntiCheatEngine
{
    /// <summary>
    /// Normalizes a raw IP address or endpoint string cleanly.
    /// </summary>
    /// <param name="rawAddress">The input address string.</param>
    /// <returns>Canonical IP string or empty when absent.</returns>
    public static string NormalizeIp(string? rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
        {
            return string.Empty;
        }

        var trimmed = rawAddress.Trim();
        var colon = trimmed.LastIndexOf(':');
        if (colon > 0 && trimmed.Count(c => c == ':') == 1)
        {
            trimmed = trimmed[..colon];
        }

        if (IPAddress.TryParse(trimmed, out var ip))
        {
            return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();
        }

        return trimmed;
    }

    /// <summary>Checks whether a normalized IP address represents local loopback.</summary>
    /// <param name="normalizedIp">The normalized IP string.</param>
    /// <returns><see langword="true"/> if loopback.</returns>
    public static bool IsLoopback(string? normalizedIp)
    {
        if (string.IsNullOrWhiteSpace(normalizedIp))
        {
            return false;
        }

        return IPAddress.TryParse(normalizedIp, out var ip) && IPAddress.IsLoopback(ip);
    }

    /// <summary>
    /// Evaluates whether a new connection from <paramref name="candidateIp"/> exceeds <see cref="AntiCheatConfiguration.MaxConnectionsPerIp"/>.
    /// </summary>
    /// <param name="config">Active configuration.</param>
    /// <param name="candidateIp">Normalized candidate IP.</param>
    /// <param name="currentlyConnectedCount">How many players with the same IP are already authenticated/online (excluding candidate).</param>
    /// <returns><see langword="true"/> if connection should be rejected.</returns>
    public static bool ExceedsIpLimit(AntiCheatConfiguration config, string candidateIp, int currentlyConnectedCount)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.Enabled || config.MaxConnectionsPerIp <= 0 || string.IsNullOrWhiteSpace(candidateIp))
        {
            return false;
        }

        if (config.ExemptLoopbackFromIpLimit && IsLoopback(candidateIp))
        {
            return false;
        }

        return currentlyConnectedCount >= config.MaxConnectionsPerIp;
    }

    /// <summary>
    /// Computes the minimum allowed milliseconds between consecutive player attacks given base attack speed / agility.
    /// </summary>
    /// <param name="config">Active configuration.</param>
    /// <param name="attackSpeedStat">Current attack speed attribute value.</param>
    /// <returns>The allowed minimum millisecond interval.</returns>
    public static double ComputeMinAttackIntervalMs(AntiCheatConfiguration config, double attackSpeedStat)
    {
        ArgumentNullException.ThrowIfNull(config);
        var speed = Math.Max(0, attackSpeedStat);
        var rawMs = Math.Max(config.MinimumAttackIntervalFloorMs, config.BaseMinAttackIntervalMs - (speed * 0.45));
        return Math.Max(config.MinimumAttackIntervalFloorMs * 0.7, rawMs * config.LatencyToleranceFactor);
    }

    /// <summary>
    /// Checks whether an attack delta violates the computed minimum interval, and prunes/counts strikes in the rolling window.
    /// </summary>
    /// <param name="config">Active configuration.</param>
    /// <param name="elapsedMs">Elapsed milliseconds since prior attack (<c>null</c> for first attack).</param>
    /// <param name="attackSpeedStat">Player attack speed attribute.</param>
    /// <param name="agilityStat">Player total agility attribute.</param>
    /// <param name="strikeTimestampsUtc">Mutable list of recent violation timestamps for this player.</param>
    /// <param name="nowUtc">Current UTC timestamp.</param>
    /// <returns>Evaluation outcome indicating violation type and whether disconnect threshold was reached.</returns>
    public static (bool IsViolation, string ViolationType, string Reason, bool ShouldDisconnect) EvaluateAttack(
        AntiCheatConfiguration config,
        double? elapsedMs,
        double attackSpeedStat,
        double agilityStat,
        List<DateTime> strikeTimestampsUtc,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(strikeTimestampsUtc);

        if (!config.Enabled)
        {
            return (false, string.Empty, string.Empty, false);
        }

        var cutoff = nowUtc.AddMinutes(-Math.Max(1, config.StrikeWindowMinutes));
        strikeTimestampsUtc.RemoveAll(t => t < cutoff);

        string violationType = string.Empty;
        string reason = string.Empty;

        if (config.EnableHitHackAgilityCheck && agilityStat > config.HitHackMaxAgility)
        {
            violationType = "AgilityCap";
            reason = $"Agility {agilityStat:0} exceeded HitHackMaxAgility ({config.HitHackMaxAgility}).";
        }
        else if (config.EnableAttackFrequencyCheck && elapsedMs.HasValue)
        {
            var minAllowed = ComputeMinAttackIntervalMs(config, attackSpeedStat);
            if (elapsedMs.Value < minAllowed)
            {
                violationType = "AttackRate";
                reason = $"Attack interval {elapsedMs.Value:0.0}ms below minimum {minAllowed:0.0}ms (Speed={attackSpeedStat:0}).";
            }
        }

        if (string.IsNullOrEmpty(violationType))
        {
            return (false, string.Empty, string.Empty, false);
        }

        strikeTimestampsUtc.Add(nowUtc);
        var disconnect = config.StrikesBeforeDisconnect > 0 && strikeTimestampsUtc.Count >= config.StrikesBeforeDisconnect;
        return (true, violationType, reason, disconnect);
    }
}
