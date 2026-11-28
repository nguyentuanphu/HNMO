// <copyright file="AutoErrorStatus.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

/// <summary>What the server's owner decided about a group of automatic error reports.</summary>
public enum AutoErrorStatus
{
    /// <summary>Not looked at, or back after it was marked fixed.</summary>
    Open,

    /// <summary>The owner marked it fixed; a new report of it opens it again.</summary>
    Fixed,

    /// <summary>The owner ignores it; it is still counted, but keeps no new samples.</summary>
    Ignored,
}
