// <copyright file="HostSetupReport.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Startup;

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.Integrity;
using HNMO.Interfaces;

/// <summary>
/// The first-setup report (owner's choice, 2026-11-21): what the HNMO developer's hub is told once about a new host. The
/// system (OS, CPU name and cores, RAM, free disk, HNMO and .NET versions, machine id) and the game (display name,
/// servers and seasons on, public address, language). The host's optional name and contact are added by
/// <see cref="DeveloperHub.SendSetupAsync"/>; the hub adds the country and the IP it came from. Never player accounts.
/// </summary>
internal static class HostSetupReport
{
    /// <summary>Builds the report.</summary>
    /// <param name="contexts">The game servers of this program.</param>
    /// <param name="settings">The host settings, if read.</param>
    /// <param name="saveDir">The save folder (for the free disk).</param>
    /// <returns>The report.</returns>
    public static JsonObject Build(IReadOnlyList<IGameServerContext> contexts, HostSettings? settings, string saveDir)
    {
        var first = contexts.FirstOrDefault();
        var servers = new JsonArray();
        foreach (var context in contexts)
        {
            servers.Add(context.Id);
        }

        var seasons = new JsonArray { "s6" };
        if (settings?.SeasonNineEnabled == true)
        {
            seasons.Add("s9");
        }

        if (settings?.ArenaEnabled == true)
        {
            seasons.Add("arena");
        }

        return new JsonObject
        {
            ["system"] = new JsonObject
            {
                ["os"] = RuntimeInformation.OSDescription,
                ["arch"] = RuntimeInformation.OSArchitecture.ToString(),
                ["cpu"] = CpuName(),
                ["cores"] = Environment.ProcessorCount,
                ["ramGb"] = Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824.0, 1),
                ["freeDiskGb"] = FreeDiskGb(saveDir),
                ["dotnet"] = RuntimeInformation.FrameworkDescription,
                ["legacyMachine"] = PlayerLicense.LegacyMachineId,
            },
            ["game"] = new JsonObject
            {
                ["name"] = first is null ? GameNameConfiguration.DefaultName : GameName(first),
                ["servers"] = servers,
                ["seasons"] = seasons,
                ["publicAddress"] = settings?.PublicAddress ?? string.Empty,
                ["language"] = CultureInfo.CurrentUICulture.Name,
            },
        };
    }

    private static string GameName(IGameServerContext context)
    {
        var configuration = context.Configuration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(GameNamePlugIn).GUID);
        if (configuration is not { IsActive: true } || string.IsNullOrWhiteSpace(configuration.CustomConfiguration))
        {
            return GameNameConfiguration.DefaultName;
        }

        try
        {
            using var document = JsonDocument.Parse(configuration.CustomConfiguration);
            return document.RootElement.TryGetProperty(nameof(GameNameConfiguration.Name), out var name) && name.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(name.GetString())
                ? name.GetString()!.Trim()
                : GameNameConfiguration.DefaultName;
        }
        catch (JsonException)
        {
            return GameNameConfiguration.DefaultName;
        }
    }

    private static double? FreeDiskGb(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            return string.IsNullOrEmpty(root) ? null : Math.Round(new DriveInfo(root).AvailableFreeSpace / 1073741824.0, 1);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static string CpuName()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return (Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null) as string)?.Trim() ?? "unknown";
            }

            if (OperatingSystem.IsMacOS())
            {
                using var process = Process.Start(new ProcessStartInfo("sysctl", "-n machdep.cpu.brand_string") { RedirectStandardOutput = true, UseShellExecute = false });
                var text = process?.StandardOutput.ReadToEnd().Trim();
                process?.WaitForExit(5000);
                return string.IsNullOrEmpty(text) ? "unknown" : text;
            }

            var line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name", StringComparison.Ordinal));
            return line?[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim() ?? "unknown";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return "unknown";
        }
    }
}
