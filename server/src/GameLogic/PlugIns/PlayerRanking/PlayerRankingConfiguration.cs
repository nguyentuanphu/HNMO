// <copyright file="PlayerRankingConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PlayerRanking;

/// <summary>
/// Configuration for <see cref="PlayerRankingPlugIn"/> (controls the in-game ranking panel and bulk add-stats window).
/// </summary>
public sealed class PlayerRankingConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the in-game Player Rankings panel (<c>Shift+R</c> / <c>/rank</c>) is enabled.
    /// </summary>
    public bool EnableRankings { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the bulk Add Stats window (<c>Shift+A</c>) is enabled.
    /// </summary>
    public bool EnableBulkAddStats { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of entries returned per leaderboard tab (<c>5..100</c>).
    /// </summary>
    public int TopCount { get; set; } = 20;

    /// <summary>
    /// Gets or sets how many seconds database ranking snapshots are cached (<c>0..300</c>).
    /// </summary>
    public int CacheSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether the Top Online Players tab is shown.
    /// </summary>
    public bool ShowOnlineLeaderboard { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Top Guilds tab is shown.
    /// </summary>
    public bool ShowGuildRankings { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Mini-Game event leaderboards (Blood Castle, Devil Square, Chaos Castle) are shown.
    /// </summary>
    public bool ShowEventRankings { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether character rows display an online status indicator.
    /// </summary>
    public bool ShowOnlineStatus { get; set; } = true;
}
