// <copyright file="DeveloperHubAddress.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>
/// The HNMO developer's hub: where servers send their reports and learn of the newest release (owner, 2026-11-21: no
/// HNMO folder points at where HNMO is built).
/// </summary>
public static class DeveloperHubAddress
{
    /// <summary>The hub's address unless <c>HNMO_DEVELOPER_HUB</c> names another.</summary>
    public const string Default = "https://hnmobug.netlify.app";

    /// <summary>Gets the hub's address (empty: off).</summary>
    public static string Url => (Environment.GetEnvironmentVariable("HNMO_DEVELOPER_HUB") ?? Default).Trim().TrimEnd('/');

    /// <summary>Gets the address of the newest release's details (version, date, notes, download link).</summary>
    public static string LatestRelease => Url.Length == 0 ? string.Empty : Url + "/api/latest";

    /// <summary>Gets the address of the licence-gated update check (<c>POST /api/update-check</c>).</summary>
    public static string UpdateCheck => Url.Length == 0 ? string.Empty : Url + "/api/update-check";
}
