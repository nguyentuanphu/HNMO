// <copyright file="HostSettingsValidator.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
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

        if (settings.ConnectionIdleTimeoutSeconds is < 5 or > 600)
        {
            errors.Add("ConnectionIdleTimeoutSeconds must be between 5 and 600.");
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

        // The admin panel can be reached from the website: its paths must not point outside the HNMO folder, or an
        // administrator could have files written, or programs started, anywhere on the host.
        foreach (var (name, path) in new[]
        {
            (nameof(HostSettings.DataPath), settings.DataPath),
            (nameof(HostSettings.VendorPath), settings.VendorPath),
            (nameof(HostSettings.PostgresPath), settings.PostgresPath),
            (nameof(HostSettings.SavePath), settings.SavePath),
            (nameof(HostSettings.ServerPath), settings.ServerPath),
            (nameof(HostSettings.LicenseFilePath), settings.LicenseFilePath),
        })
        {
            if (!IsInsideInstallFolder(path))
            {
                errors.Add($"{name} must be a relative path inside the HNMO folder (for example \"../save\").");
            }
        }

        if (string.IsNullOrWhiteSpace(settings.ServerProgram) || settings.ServerProgram.IndexOfAny(['/', '\\', ':']) >= 0 || settings.ServerProgram.Contains("..", StringComparison.Ordinal))
        {
            errors.Add("ServerProgram must be a program name only, without a folder.");
        }

        return errors;
    }

    /// <summary>
    /// Tells whether a path, taken relative to one of the HNMO folder's parts (server, web), stays inside the HNMO
    /// folder: it is relative and climbs at most out of that part ("../save" is fine, "../../x" or "C:\x" is not).
    /// </summary>
    /// <param name="path">The configured path.</param>
    /// <returns><c>true</c> if the path stays inside the HNMO folder.</returns>
    public static bool IsInsideInstallFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        var depth = 1;
        foreach (var segment in path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (--depth < 0)
                {
                    return false;
                }
            }
            else if (segment != ".")
            {
                depth++;
            }
        }

        return true;
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
