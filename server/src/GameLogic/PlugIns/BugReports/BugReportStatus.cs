// <copyright file="BugReportStatus.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
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
