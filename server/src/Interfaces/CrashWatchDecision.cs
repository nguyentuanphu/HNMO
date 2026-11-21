// <copyright file="CrashWatchDecision.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>
/// Result of evaluating whether <see cref="CrashWatchPolicy"/> should record and auto-restart after a server exit.
/// </summary>
/// <param name="IsCrash">Whether this exit represents an unexpected crash.</param>
/// <param name="ShouldRestart">Whether an automatic restart should be launched.</param>
/// <param name="DelaySeconds">Backoff delay before restarting.</param>
/// <param name="CrashesLastHour">Count of unexpected crashes within the last 60 minutes (including this one).</param>
/// <param name="SummaryEn">English summary notice.</param>
/// <param name="SummaryVi">Vietnamese summary notice.</param>
public sealed record CrashWatchDecision(
    bool IsCrash,
    bool ShouldRestart,
    int DelaySeconds,
    int CrashesLastHour,
    string SummaryEn,
    string SummaryVi);
