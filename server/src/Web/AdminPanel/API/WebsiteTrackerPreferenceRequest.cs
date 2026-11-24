// <copyright file="WebsiteTrackerPreferenceRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

/// <summary>Bridge-authenticated account request with an optional new preference.</summary>
public sealed class WebsiteTrackerPreferenceRequest
{
    /// <summary>Gets or sets the login name supplied by the session bridge.</summary>
    public string? LoginName { get; set; }

    /// <summary>Gets or sets the password supplied by the session bridge.</summary>
    public string? Password { get; set; }

    /// <summary>Gets or sets the new choice, if supplied.</summary>
    public bool? OptedOut { get; set; }
}
