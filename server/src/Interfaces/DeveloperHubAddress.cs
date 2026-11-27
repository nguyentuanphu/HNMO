// <copyright file="DeveloperHubAddress.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Interfaces;

using System;

/// <summary>
/// The HNMO developer's hub: where servers send their reports and learn of the newest release (owner, 2026-11-21: no
/// HNMO folder points at where HNMO is built).
/// </summary>
public static class DeveloperHubAddress
{
    /// <summary>The hub's address unless <c>HNMO_DEVELOPER_HUB</c> names another.</summary>
    public const string Default = "https://hnmobug.netlify.app";

    /// <summary>The environment variable which names another hub, or turns the hub off.</summary>
    public const string EnvironmentVariableName = "HNMO_DEVELOPER_HUB";

    /// <summary>
    /// Gets the hub's address; empty means off: nothing is sent and no release is looked up. Test runs set
    /// <c>HNMO_DEVELOPER_HUB</c> to an empty value or to <c>off</c> (Windows drops an empty environment variable, so
    /// there "off" is the way), so that test machines never report to the real hub as hosts.
    /// </summary>
    public static string Url => Resolve(Environment.GetEnvironmentVariable(EnvironmentVariableName));

    /// <summary>Gets the address of the newest release's details (version, date, notes, download link).</summary>
    public static string LatestRelease => Url.Length == 0 ? string.Empty : Url + "/api/latest";

    /// <summary>Gets the hub address an <c>HNMO_DEVELOPER_HUB</c> value gives.</summary>
    /// <param name="value">The variable's value; <c>null</c> when it isn't set.</param>
    /// <returns>The address without a trailing slash, the default when unset, empty when off.</returns>
    public static string Resolve(string? value)
    {
        if (value is null)
        {
            return Default;
        }

        var trimmed = value.Trim().TrimEnd('/');
        return trimmed.Equals("off", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : trimmed;
    }
}
