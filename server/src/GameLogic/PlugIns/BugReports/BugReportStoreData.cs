// <copyright file="BugReportStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

/// <summary>The saved bug reports.</summary>
public sealed class BugReportStoreData
{
    /// <summary>Gets or sets the number of the next report.</summary>
    public int NextNumber { get; set; } = 1;

    /// <summary>Gets or sets the reports, oldest first.</summary>
    public List<BugReport> Reports { get; set; } = [];
}
