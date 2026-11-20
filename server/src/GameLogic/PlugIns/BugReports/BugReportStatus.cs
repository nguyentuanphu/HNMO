// <copyright file="BugReportStatus.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

/// <summary>Where a bug report stands.</summary>
public enum BugReportStatus
{
    /// <summary>Not looked at yet.</summary>
    New,

    /// <summary>The server's owner confirmed the bug.</summary>
    Confirmed,

    /// <summary>Not a bug.</summary>
    NotABug,

    /// <summary>Fixed.</summary>
    Fixed,

    /// <summary>The same bug as another report.</summary>
    Duplicate,
}
