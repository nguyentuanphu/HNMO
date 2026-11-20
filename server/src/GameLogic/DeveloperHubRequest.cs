// <copyright file="DeveloperHubRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>A licence request and the one-time secret its answer is fetched with.</summary>
public sealed class DeveloperHubRequest
{
    /// <summary>Gets or sets the hub's id of the request.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the one-time secret.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was sent (UTC).</summary>
    public DateTime SentUtc { get; set; }

    /// <summary>Gets or sets its status: open, issued or declined.</summary>
    public string Status { get; set; } = "open";
}
