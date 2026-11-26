// <copyright file="AntiCheatPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AntiCheat;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Server-side anti-cheat enforcement &amp; violation audit plug-in (per-IP multi-client limits, attack cadence checks, safe-zone audit).
/// Off until enabled in Admin Panel (<c>/anti-cheat</c>).
/// </summary>
[PlugIn]
[Display(Name = "Anti-Cheat Authority & Audit", Description = "Per-IP connection cap, agility-aware attack rate authority, safe-zone audit, and violation log.")]
[Guid("7A18C4E9-5D3B-4B8F-9C21-38D6B9E10F44")]
public sealed class AntiCheatPlugIn :
    IFeaturePlugIn,
    ISupportCustomConfiguration<AntiCheatConfiguration>,
    ISupportDefaultCustomConfiguration,
    IPlayerStateChangedPlugIn,
    IAttackSpeedCheckPlugIn
{
    private readonly ConditionalWeakTable<Player, PlayerAttackTracker> _trackers = new();
    private readonly TimeProvider _timeProvider;
    private AntiCheatAuditStore? _auditStore;

    /// <summary>Initializes a new instance of the <see cref="AntiCheatPlugIn"/> class.</summary>
    public AntiCheatPlugIn()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AntiCheatPlugIn"/> class with a deterministic <see cref="TimeProvider"/> for testing.</summary>
    /// <param name="timeProvider">Time provider.</param>
    internal AntiCheatPlugIn(TimeProvider timeProvider)
    {
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public AntiCheatConfiguration? Configuration { get; set; }

    /// <summary>
    /// Records a blocked safe-zone attack attempt when the active plug-in is configured to audit them.
    /// </summary>
    /// <param name="player">Attacking player.</param>
    /// <param name="skill">Optional skill attempted.</param>
    public static void RecordSafeZoneAttempt(Player player, Skill? skill)
    {
        if (player?.GameContext is not { } context)
        {
            return;
        }

        var instance = context.PlugInManager.GetActivePlugInsOf<IAttackSpeedCheckPlugIn>()
            .OfType<AntiCheatPlugIn>()
            .FirstOrDefault();
        if (instance?.Configuration is not { Enabled: true, LogSafeZoneAttempts: true } cfg)
        {
            return;
        }

        instance.GetStore(cfg).Append(
            new AntiCheatViolationEntry
            {
                OccurredAtUtc = instance._timeProvider.GetUtcNow().UtcDateTime,
                Account = player.Account?.LoginName ?? string.Empty,
                Character = player.SelectedCharacter?.Name ?? string.Empty,
                IpAddress = AntiCheatEngine.NormalizeIp((player as IHasIpAddress)?.IpAddress),
                ViolationType = "SafeZoneAttack",
                Details = skill is null
                    ? "Melee attack attempted inside safezone."
                    : $"Skill #{skill.Number} ({skill.Name}) attempted inside safezone.",
                ActionTaken = "Blocked",
            },
            cfg.MaxAuditEntries);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new AntiCheatConfiguration();

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (this.Configuration is not { Enabled: true } cfg)
        {
            return;
        }

        if (currentState != PlayerState.Authenticated || previousState != PlayerState.LoginScreen)
        {
            return;
        }

        var ip = AntiCheatEngine.NormalizeIp((player as IHasIpAddress)?.IpAddress);
        if (string.IsNullOrWhiteSpace(ip) || cfg.MaxConnectionsPerIp <= 0)
        {
            return;
        }

        var concurrentOthers = 0;
        await player.GameContext.ForEachPlayerAsync(other =>
        {
            if (!ReferenceEquals(other, player)
                && other.Account is not null
                && other.PlayerState.CurrentState != PlayerState.Initial
                && other.PlayerState.CurrentState != PlayerState.LoginScreen
                && other.PlayerState.CurrentState != PlayerState.Disconnected
                && other.PlayerState.CurrentState != PlayerState.Finished
                && string.Equals(AntiCheatEngine.NormalizeIp((other as IHasIpAddress)?.IpAddress), ip, StringComparison.Ordinal))
            {
                concurrentOthers++;
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);

        if (!AntiCheatEngine.ExceedsIpLimit(cfg, ip, concurrentOthers))
        {
            return;
        }

        this.GetStore(cfg).Append(
            new AntiCheatViolationEntry
            {
                OccurredAtUtc = this._timeProvider.GetUtcNow().UtcDateTime,
                Account = player.Account?.LoginName ?? string.Empty,
                Character = string.Empty,
                IpAddress = ip,
                ViolationType = "IpLimit",
                Details = $"Concurrent sessions ({concurrentOthers + 1}) exceeded MaxConnectionsPerIp ({cfg.MaxConnectionsPerIp}).",
                ActionTaken = "Disconnected",
            },
            cfg.MaxAuditEntries);

        var isVi = player.Culture.TwoLetterISOLanguageName == "vi";
        await player.ShowBlueMessageAsync(
            isVi
                ? $"Giới hạn tối đa {cfg.MaxConnectionsPerIp} tài khoản trên cùng một địa chỉ IP."
                : $"Connection limit reached: maximum {cfg.MaxConnectionsPerIp} accounts per IP address.").ConfigureAwait(false);
        await player.DisconnectAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask AttackCheatCheckAsync(Player player, Skill? skill, SpeedHackCheckEventArgs eventArgs)
    {
        if (this.Configuration is not { Enabled: true } cfg || player.Attributes is null)
        {
            return;
        }

        var nowTimestamp = this._timeProvider.GetTimestamp();
        var nowUtc = this._timeProvider.GetUtcNow().UtcDateTime;
        var tracker = this._trackers.GetOrCreateValue(player);
        var attackSpeed = Math.Max(player.Attributes[Stats.AttackSpeed], player.Attributes[Stats.MagicSpeed]);
        var agility = player.Attributes[Stats.TotalAgility];

        double? elapsedMs = null;
        (bool IsViolation, string ViolationType, string Reason, bool ShouldDisconnect) eval;
        lock (tracker.Lock)
        {
            if (tracker.LastAttackTimestamp != 0)
            {
                elapsedMs = this._timeProvider.GetElapsedTime(tracker.LastAttackTimestamp, nowTimestamp).TotalMilliseconds;
            }

            tracker.LastAttackTimestamp = nowTimestamp;
            eval = AntiCheatEngine.EvaluateAttack(cfg, elapsedMs, attackSpeed, agility, tracker.StrikesUtc, nowUtc);
        }

        if (!eval.IsViolation)
        {
            return;
        }

        eventArgs.Severity = eval.ShouldDisconnect ? SpeedHackViolationSeverity.Reject : SpeedHackViolationSeverity.Soft;
        this.GetStore(cfg).Append(
            new AntiCheatViolationEntry
            {
                OccurredAtUtc = nowUtc,
                Account = player.Account?.LoginName ?? string.Empty,
                Character = player.SelectedCharacter?.Name ?? string.Empty,
                IpAddress = AntiCheatEngine.NormalizeIp((player as IHasIpAddress)?.IpAddress),
                ViolationType = eval.ViolationType,
                Details = skill is null ? eval.Reason : $"Skill #{skill.Number}: {eval.Reason}",
                ActionTaken = eval.ShouldDisconnect ? "Disconnected" : "Logged",
            },
            cfg.MaxAuditEntries);

        if (eval.ShouldDisconnect)
        {
            var isVi = player.Culture.TwoLetterISOLanguageName == "vi";
            await player.ShowBlueMessageAsync(
                isVi
                    ? "Phát hiện tốc độ tấn công bất thường. Máy chủ đã ngắt kết nối."
                    : "Abnormal attack rate detected. You have been disconnected.").ConfigureAwait(false);
            await player.DisconnectAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Overload forwarding attack speed check without a specific skill context.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="eventArgs">The event args to mark a violation.</param>
    /// <returns>A task representing the asynchronous check.</returns>
    public ValueTask AttackCheatCheckAsync(Player player, SpeedHackCheckEventArgs eventArgs) =>
        this.AttackCheatCheckAsync(player, null, eventArgs);

    private AntiCheatAuditStore GetStore(AntiCheatConfiguration cfg) =>
        this._auditStore ??= AntiCheatAuditStore.Open(cfg.FileName);

    private sealed class PlayerAttackTracker
    {
        public object Lock { get; } = new();

        public long LastAttackTimestamp { get; set; }

        public List<DateTime> StrikesUtc { get; } = [];
    }
}
