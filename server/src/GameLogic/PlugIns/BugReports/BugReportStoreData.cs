// <copyright file="BugReportStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

/// <summary>The saved bug reports.</summary>
public sealed class BugReportStoreData
{
    /// <summary>Gets or sets the number of the next report.</summary>
    public int NextNumber { get; set; } = 1;

    /// <summary>Gets or sets the reports, oldest first.</summary>
    public List<BugReport> Reports { get; set; } = [];

    /// <summary>Gets or sets the number of the next group of automatic error reports.</summary>
    public int NextAutoNumber { get; set; } = 1;

    /// <summary>Gets or sets the groups of automatic error reports, oldest first.</summary>
    public List<AutoErrorGroup> AutoGroups { get; set; } = [];

    /// <summary>Gets or sets the Sydney day the counts below are of (yyyy-MM-dd).</summary>
    public string AutoDay { get; set; } = string.Empty;

    /// <summary>Gets or sets how many automatic reports each account sent on <see cref="AutoDay"/> (for the cap only).</summary>
    public Dictionary<string, int> AutoAccountCounts { get; set; } = [];

    /// <summary>Gets or sets how many automatic reports the server took on <see cref="AutoDay"/>.</summary>
    public int AutoServerCount { get; set; }
}
