// <copyright file="BugReportsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

using HNMO.DataModel.Entities;

/// <summary>The settings of the bug reports (admin panel, Plugins).</summary>
public sealed class BugReportsConfiguration
{
    /// <summary>Gets or sets the reports one account may send in a Sydney day.</summary>
    public int DailyLimitPerAccount { get; set; } = 5;

    /// <summary>Gets or sets the longest text of a report.</summary>
    public int MaxTextLength { get; set; } = 1000;

    /// <summary>Gets or sets a value indicating whether a confirmed bug gives its reporter coins.</summary>
    public bool RewardConfirmedBugs { get; set; }

    /// <summary>Gets or sets the coins for a confirmed bug.</summary>
    public CashShopCoinType RewardCoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets how many coins a confirmed bug gives.</summary>
    public int RewardAmount { get; set; } = 100;

    /// <summary>
    /// Gets or sets a Discord link (webhook) which also gets a notice when a report is sent to the HNMO developer
    /// (optional; the report itself goes to the developer's hub).
    /// </summary>
    public string DeveloperWebhookUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether reports sent to the HNMO developer name the character.</summary>
    public bool ShareCharacterNames { get; set; }

    /// <summary>Gets or sets the state file.</summary>
    public string FileName { get; set; } = HnmoStateStore.StateFileName;
}
