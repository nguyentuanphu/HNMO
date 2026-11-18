// <copyright file="CrashWatchPolicy.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure crash detection and restart backoff policy for <c>/control</c> launcher supervision.
/// </summary>
public static class CrashWatchPolicy
{
    /// <summary>Evaluates whether an exited game server process warrants a crash alert and auto-restart.</summary>
    /// <param name="settings">Maintenance &amp; crash watch settings.</param>
    /// <param name="exitCode">Process exit code.</param>
    /// <param name="intentionalStop">Whether stopped deliberately via <c>/control</c> or host shutdown.</param>
    /// <param name="priorCrashTimesUtc">Prior unexpected crash timestamps.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>The crash watch decision.</returns>
    public static CrashWatchDecision Evaluate(
        MaintenanceSettings settings,
        int exitCode,
        bool intentionalStop,
        IReadOnlyList<DateTimeOffset> priorCrashTimesUtc,
        DateTimeOffset utcNow)
    {
        if (intentionalStop || exitCode == HostSettingsStore.RestartExitCode)
        {
            return new CrashWatchDecision(
                IsCrash: false,
                ShouldRestart: false,
                DelaySeconds: 0,
                CrashesLastHour: 0,
                SummaryEn: string.Empty,
                SummaryVi: string.Empty);
        }

        var windowStart = utcNow.AddHours(-1);
        var recentCount = priorCrashTimesUtc.Count(t => t >= windowStart) + 1;
        var cap = Math.Clamp(settings.MaxCrashRestartsPerHour, 1, 60);
        var baseDelay = Math.Clamp(settings.CrashRestartDelaySeconds, 1, 300);

        if (!settings.AutoRestartOnCrash)
        {
            return new CrashWatchDecision(
                IsCrash: true,
                ShouldRestart: false,
                DelaySeconds: 0,
                CrashesLastHour: recentCount,
                SummaryEn: $"Game server stopped unexpectedly (exit code {exitCode}). Auto-restart is OFF.",
                SummaryVi: $"Máy chủ game dừng đột ngột (mã thoát {exitCode}). Tự khởi động lại đang TẮT.");
        }

        if (recentCount > cap)
        {
            return new CrashWatchDecision(
                IsCrash: true,
                ShouldRestart: false,
                DelaySeconds: 0,
                CrashesLastHour: recentCount,
                SummaryEn: $"Game server crashed (exit code {exitCode}). Hourly restart limit ({cap}/h) reached — manual check required.",
                SummaryVi: $"Máy chủ gặp sự cố (mã {exitCode}). Đã vượt giới hạn tự khởi động ({cap}/giờ) — cần kiểm tra thủ công.");
        }

        var delay = baseDelay * Math.Min(4, recentCount);
        return new CrashWatchDecision(
            IsCrash: true,
            ShouldRestart: true,
            DelaySeconds: delay,
            CrashesLastHour: recentCount,
            SummaryEn: $"Game server crashed (exit code {exitCode}). Auto-restarting in {delay}s (#{recentCount} this hour).",
            SummaryVi: $"Máy chủ gặp sự cố (mã {exitCode}). Đang tự khởi động lại sau {delay} giây (lần {recentCount} trong giờ).");
    }
}
