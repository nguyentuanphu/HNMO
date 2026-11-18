// <copyright file="HostSettingsStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>
/// Loads, migrates, and atomically saves <see cref="HostSettings"/> in <c>save/hnmo-settings.json</c>.
/// </summary>
public static class HostSettingsStore
{
    /// <summary>
    /// The exit code returned by the server process when a full restart is requested.
    /// </summary>
    public const int RestartExitCode = 75;

    /// <summary>
    /// The default settings file name inside the <c>save</c> directory.
    /// </summary>
    public const string FileName = "hnmo-settings.json";

    /// <summary>
    /// The environment variable that points to <c>hnmo-settings.json</c>.
    /// </summary>
    public const string EnvironmentVariableName = "HNMO_SETTINGS_FILE";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Resolves the path to <c>save/hnmo-settings.json</c> from the environment or standard package layout.
    /// </summary>
    /// <param name="baseDirectory">The application base directory.</param>
    /// <returns>The resolved file path.</returns>
    public static string ResolveDefaultPath(string baseDirectory)
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        var parentSave = Path.GetFullPath(Path.Combine(baseDirectory, "..", "save"));
        if (Directory.Exists(parentSave))
        {
            return Path.Combine(parentSave, FileName);
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, "save", FileName));
    }

    /// <summary>
    /// Loads <see cref="HostSettings"/> if the file already exists; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="settingsPath">The path to <c>hnmo-settings.json</c>.</param>
    /// <returns>The loaded settings or <c>null</c>.</returns>
    public static HostSettings? TryLoad(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        var json = File.ReadAllText(settingsPath);
        return JsonSerializer.Deserialize<HostSettings>(json, JsonOptions);
    }

    /// <summary>
    /// Loads <see cref="HostSettings"/> from <paramref name="settingsPath"/>, migrating from legacy
    /// <c>appsettings.json</c> and <c>ConnectionSettings.xml</c> on first run.
    /// </summary>
    /// <param name="settingsPath">The target <c>save/hnmo-settings.json</c> path.</param>
    /// <param name="webAppSettingsPath">Optional path to <c>web/appsettings.json</c>.</param>
    /// <param name="connectionSettingsXmlPath">Optional path to <c>server/ConnectionSettings.xml</c>.</param>
    /// <returns>The loaded or migrated <see cref="HostSettings"/>.</returns>
    public static HostSettings LoadOrMigrate(
        string settingsPath,
        string? webAppSettingsPath = null,
        string? connectionSettingsXmlPath = null)
    {
        var existing = TryLoad(settingsPath);
        if (existing is not null)
        {
            return existing;
        }

        var settings = new HostSettings();
        if (!string.IsNullOrWhiteSpace(webAppSettingsPath) && File.Exists(webAppSettingsPath))
        {
            MigrateFromWebAppSettings(settings, webAppSettingsPath);
        }

        if (!string.IsNullOrWhiteSpace(connectionSettingsXmlPath) && File.Exists(connectionSettingsXmlPath))
        {
            MigrateFromConnectionSettingsXml(settings, connectionSettingsXmlPath);
        }

        Save(settingsPath, settings);
        return settings;
    }

    /// <summary>
    /// Saves <paramref name="settings"/> atomically to <paramref name="settingsPath"/>.
    /// </summary>
    /// <param name="settingsPath">The destination file path.</param>
    /// <param name="settings">The settings to write.</param>
    public static void Save(string settingsPath, HostSettings settings)
    {
        var fullPath = Path.GetFullPath(settingsPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = fullPath + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, fullPath, overwrite: true);
    }

    private static void MigrateFromWebAppSettings(HostSettings settings, string webAppSettingsPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(webAppSettingsPath));
            var root = doc.RootElement;

            if (root.TryGetProperty("Urls", out var urls) && urls.GetString() is { } urlsStr && !string.IsNullOrWhiteSpace(urlsStr))
            {
                settings.WebUrls = urlsStr;
                var firstUrl = urlsStr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                if (firstUrl is not null && Uri.TryCreate(firstUrl.Replace("://*:", "://localhost:").Replace("://0.0.0.0:", "://localhost:"), UriKind.Absolute, out var webUri))
                {
                    settings.WebPort = webUri.Port;
                }
            }

            if (root.TryGetProperty("HNMO", out var hnmo))
            {
                if (hnmo.TryGetProperty("WebRoot", out var v) && v.GetString() is { } s)
                {
                    settings.WebRoot = s;
                }

                if (hnmo.TryGetProperty("DataPath", out v) && v.GetString() is { } d)
                {
                    settings.DataPath = d;
                }

                if (hnmo.TryGetProperty("VendorPath", out v) && v.GetString() is { } vp)
                {
                    settings.VendorPath = vp;
                }
            }

            if (root.TryGetProperty("Relay", out var relay))
            {
                if (relay.TryGetProperty("AllowedHosts", out var hosts) && hosts.ValueKind == JsonValueKind.Array)
                {
                    var list = hosts.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToArray();
                    if (list.Length > 0)
                    {
                        settings.RelayAllowedHosts = list;
                    }
                }

                if (relay.TryGetProperty("AllowLoopback", out var allow) && allow.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    settings.RelayAllowLoopback = allow.GetBoolean();
                }

                if (relay.TryGetProperty("ConnectTimeoutSeconds", out var timeout) && timeout.TryGetInt32(out var t))
                {
                    settings.RelayConnectTimeoutSeconds = t;
                }
            }

            if (root.TryGetProperty("Launcher", out var launcher))
            {
                if (launcher.TryGetProperty("Enabled", out var en) && en.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    settings.LauncherEnabled = en.GetBoolean();
                }

                if (launcher.TryGetProperty("PostgresPath", out var v) && v.GetString() is { } pg)
                {
                    settings.PostgresPath = pg;
                }

                if (launcher.TryGetProperty("SavePath", out v) && v.GetString() is { } sp)
                {
                    settings.SavePath = sp;
                }

                if (launcher.TryGetProperty("DatabasePort", out v) && v.TryGetInt32(out var dbPort))
                {
                    settings.DatabasePort = dbPort;
                }

                if (launcher.TryGetProperty("DatabasePassword", out v) && v.GetString() is { } dbPw)
                {
                    settings.DatabasePassword = dbPw;
                }

                if (launcher.TryGetProperty("ServerPath", out v) && v.GetString() is { } srv)
                {
                    settings.ServerPath = srv;
                }

                if (launcher.TryGetProperty("ServerProgram", out v) && v.GetString() is { } srvProg)
                {
                    settings.ServerProgram = srvProg;
                }

                if (launcher.TryGetProperty("AdminUrl", out v) && v.GetString() is { } adminUrl)
                {
                    if (Uri.TryCreate(adminUrl, UriKind.Absolute, out var adminUri))
                    {
                        settings.AdminPort = adminUri.Port;
                    }
                }

                if (launcher.TryGetProperty("ConnectServerPort", out v) && v.TryGetInt32(out var csPort))
                {
                    settings.ConnectServerPort = csPort;
                }

                if (launcher.TryGetProperty("OpenBrowserUrl", out v) && v.GetString() is { } openUrl)
                {
                    settings.OpenBrowserUrl = openUrl;
                    if (Uri.TryCreate(openUrl, UriKind.Absolute, out var openUri))
                    {
                        settings.PublicAddress = openUri.Host;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Keep defaults if legacy appsettings.json is malformed.
        }
    }

    private static void MigrateFromConnectionSettingsXml(HostSettings settings, string connectionSettingsXmlPath)
    {
        try
        {
            var doc = XDocument.Load(connectionSettingsXmlPath);
            var connStr = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "ConnectionString")
                ?.Value;
            if (string.IsNullOrWhiteSpace(connStr))
            {
                return;
            }

            foreach (var part in connStr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                var key = part[..eq].Trim();
                var val = part[(eq + 1)..].Trim();
                if (key.Equals("Server", StringComparison.OrdinalIgnoreCase) || key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    settings.DatabaseHost = val;
                }
                else if (key.Equals("Port", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out var port))
                {
                    settings.DatabasePort = port;
                }
                else if (key.Equals("Database", StringComparison.OrdinalIgnoreCase))
                {
                    settings.DatabaseName = val;
                }
                else if (key.Equals("User Id", StringComparison.OrdinalIgnoreCase) || key.Equals("Username", StringComparison.OrdinalIgnoreCase))
                {
                    settings.DatabaseUser = val;
                }
                else if (key.Equals("Password", StringComparison.OrdinalIgnoreCase))
                {
                    settings.DatabasePassword = val;
                }
            }
        }
        catch
        {
            // Keep defaults if legacy ConnectionSettings.xml cannot be parsed.
        }
    }
}
