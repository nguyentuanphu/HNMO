// <copyright file="MissionAccountState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// The earned coins of an account.
/// </summary>
public sealed class MissionAccountState
{
    /// <summary>Gets or sets the game day of the daily counters.</summary>
    public string DayKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the earned coins of the day, over all capped sources.</summary>
    public int EarnedToday { get; set; }

    /// <summary>Gets or sets the counted seconds in the game of the day (auto play counts half).</summary>
    public int OnlineSecondsToday { get; set; }

    /// <summary>Gets or sets the full hours which were paid today.</summary>
    public int OnlineHoursPaidToday { get; set; }

    /// <summary>Gets or sets the coins for the online time of the day.</summary>
    public int OnlineCoinsToday { get; set; }

    /// <summary>Gets or sets the month of <see cref="CalendarDays"/>.</summary>
    public string CalendarMonth { get; set; } = string.Empty;

    /// <summary>Gets or sets the claimed days of the login calendar in its month.</summary>
    public int CalendarDays { get; set; }

    /// <summary>Gets or sets the game day of the last claim of the login calendar.</summary>
    public string? CalendarLastDay { get; set; }

    /// <summary>Gets or sets the account's own invite code.</summary>
    public string? InviteCode { get; set; }

    /// <summary>Gets or sets the login name of the account which invited this one.</summary>
    public string? InvitedBy { get; set; }

    /// <summary>Gets or sets a value indicating whether the invite reward was handled.</summary>
    public bool InviteRewarded { get; set; }

    /// <summary>Gets or sets the network address of the last login.</summary>
    public string? LastAddress { get; set; }

    /// <summary>Gets or sets the earned coins which wait for room under the daily cap.</summary>
    public List<OwedCoinsState> Owed { get; set; } = new();
}
