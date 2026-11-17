// <copyright file="WebsiteMailSettings.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

/// <summary>Off-by-default outgoing email settings, stored in the unified server state.</summary>
public sealed record WebsiteMailSettings
{
    /// <summary>Gets or sets a value indicating whether account confirmation email may be sent.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the SMTP server address.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP submission port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Gets or sets the sender's plain email address.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP authentication name, or empty for an authenticated local relay.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the data-protected SMTP credential; never bind it to a browser control.</summary>
    public string ProtectedPassword { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a stored SMTP credential exists, without exposing it.</summary>
    public bool HasPassword { get; set; }
}
