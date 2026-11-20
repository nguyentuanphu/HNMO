// <copyright file="DeveloperHubState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>What a server keeps of its exchanges with the HNMO developer's hub (<c>save/developer-hub.json</c>).</summary>
public sealed class DeveloperHubState
{
    /// <summary>Gets or sets when the last weekly report was taken (UTC).</summary>
    public DateTime? LastWeeklyUtc { get; set; }

    /// <summary>Gets or sets when a weekly report was last tried (UTC).</summary>
    public DateTime? LastWeeklyTryUtc { get; set; }

    /// <summary>Gets or sets the last licence request.</summary>
    public DeveloperHubRequest? Request { get; set; }

    /// <summary>
    /// Gets or sets when more players than allowed were first in the game at once (UTC): from then, more may play for a
    /// week while the admin panel asks the host to get a licence; after it, the limit holds. A valid licence clears it.
    /// </summary>
    public DateTime? GraceStartedUtc { get; set; }

    /// <summary>Gets or sets the ids of licences the developer withdrew.</summary>
    public List<string> WithdrawnLicences { get; set; } = [];
}
