// <copyright file="HostSettingsValidator.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Provides validation for <see cref="HostSettings"/>.
/// </summary>
public static class HostSettingsValidator
{
    /// <summary>
    /// Validates the given <see cref="HostSettings"/> instance.
    /// </summary>
    /// <param name="settings">The settings to validate.</param>
    /// <param name="checkPortAvailability">Whether to probe TCP port availability for changed ports.</param>
    /// <param name="currentlyBoundPorts">Ports already bound by the running instance (skipped in availability checks).</param>
    /// <returns>A list of validation error messages; empty when valid.</returns>
    public static IReadOnlyList<string> Validate(
        HostSettings settings,
        bool checkPortAvailability = false,
        IReadOnlySet<int>? currentlyBoundPorts = null)
    {
        var errors = new List<string>();
        ValidatePort(settings.WebPort, nameof(settings.WebPort), errors);
        ValidatePort(settings.AdminPort, nameof(settings.AdminPort), errors);
        ValidatePort(settings.ConnectServerPort, nameof(settings.ConnectServerPort), errors);
        ValidatePort(settings.DatabasePort, nameof(settings.DatabasePort), errors);

        var ports = new[]
        {
            (Name: nameof(settings.WebPort), Port: settings.WebPort),
            (Name: nameof(settings.AdminPort), Port: settings.AdminPort),
            (Name: nameof(settings.ConnectServerPort), Port: settings.ConnectServerPort),
            (Name: nameof(settings.DatabasePort), Port: settings.DatabasePort),
        };

        var seen = new Dictionary<int, string>();
        foreach (var (name, port) in ports)
        {
            if (port is < 1 or > 65535)
            {
                continue;
            }

            if (seen.TryGetValue(port, out var first))
            {
                errors.Add($"Port {port} is assigned to both {first} and {name}.");
            }
            else
            {
                seen[port] = name;
                if (checkPortAvailability && (currentlyBoundPorts is null || !currentlyBoundPorts.Contains(port)) && !IsPortAvailable(port))
                {
                    errors.Add($"Port {port} ({name}) is already in use.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(settings.PublicAddress) || !IsValidHostOrUrl(settings.PublicAddress))
        {
            errors.Add("PublicAddress must be a valid hostname, IP address, or HTTP(S) URL.");
        }

        if (string.IsNullOrWhiteSpace(settings.OpenBrowserUrl)
            || !Uri.TryCreate(settings.OpenBrowserUrl, UriKind.Absolute, out var browserUri)
            || (browserUri.Scheme != Uri.UriSchemeHttp && browserUri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("OpenBrowserUrl must be a valid HTTP or HTTPS URL.");
        }

        if (settings.RelayConnectTimeoutSeconds is < 1 or > 120)
        {
            errors.Add("RelayConnectTimeoutSeconds must be between 1 and 120.");
        }

        if (settings.RelayAllowedHosts.Length == 0 || settings.RelayAllowedHosts.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("RelayAllowedHosts must contain at least one valid host.");
        }

        if (string.IsNullOrWhiteSpace(settings.DatabaseHost))
        {
            errors.Add("DatabaseHost is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.DatabaseName))
        {
            errors.Add("DatabaseName is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.DatabaseUser))
        {
            errors.Add("DatabaseUser is required.");
        }

        return errors;
    }

    private static void ValidatePort(int port, string name, List<string> errors)
    {
        if (port is < 1 or > 65535)
        {
            errors.Add($"{name} must be between 1 and 65535.");
        }
    }

    private static bool IsValidHostOrUrl(string value)
    {
        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return !string.IsNullOrWhiteSpace(uri.Host);
        }

        return Uri.CheckHostName(trimmed) != UriHostNameType.Unknown;
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (System.Net.Sockets.SocketException)
        {
            return false;
        }
    }
}
