// <copyright file="BugReport.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

/// <summary>A bug report of a player.</summary>
public sealed class BugReport
{
    /// <summary>Gets or sets the report's id (e.g. <c>B12</c>).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was sent (UTC).</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's name.</summary>
    public string Character { get; set; } = string.Empty;

    /// <summary>Gets or sets the game server and season (e.g. <c>s6 server 0</c>).</summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>Gets or sets the map and the position.</summary>
    public string Place { get; set; } = string.Empty;

    /// <summary>Gets or sets the category: display, skill, item, quest, connection or other.</summary>
    public string Category { get; set; } = "other";

    /// <summary>Gets or sets what the player wrote.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets what the game added (version, browser, device, recent errors).</summary>
    public string Info { get; set; } = string.Empty;

    /// <summary>Gets or sets the status.</summary>
    public BugReportStatus Status { get; set; }

    /// <summary>Gets or sets the report this one repeats, for <see cref="BugReportStatus.Duplicate"/>.</summary>
    public string? DuplicateOf { get; set; }

    /// <summary>Gets or sets the server owner's answer.</summary>
    public string? Reply { get; set; }

    /// <summary>Gets or sets when the answer was written (UTC).</summary>
    public DateTime? ReplyUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether the player has seen the answer or the status change.</summary>
    public bool Seen { get; set; } = true;

    /// <summary>Gets or sets the coins given for the confirmed bug (0 for none).</summary>
    public int RewardGiven { get; set; }

    /// <summary>Gets or sets when it was sent to the HNMO developer (UTC).</summary>
    public DateTime? ForwardedUtc { get; set; }
}
