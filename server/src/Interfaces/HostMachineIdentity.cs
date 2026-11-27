// <copyright file="HostMachineIdentity.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Stable computer identity for HNMO licence binding and update authorization.
/// </summary>
public static class HostMachineIdentity
{
    private static readonly Lazy<string> RealMachineId = new(ReadMachineId);

    /// <summary>
    /// Gets the id of this computer for a license bound to it: the first 16 hex digits of the SHA-256 of the
    /// system's own id (Windows MachineGuid, Linux /etc/machine-id, macOS IOPlatformUUID), falling back to
    /// <see cref="LegacyMachineId"/> when none is present.
    /// </summary>
    public static string MachineId => RealMachineId.Value;

    /// <summary>
    /// Gets the id earlier HNMO versions used: the first 16 hex digits of the SHA-256 of the computer's name.
    /// </summary>
    public static string LegacyMachineId
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName.ToUpperInvariant())))[..16].ToLowerInvariant();

    /// <summary>The id made from a system id.</summary>
    /// <param name="systemId">The system's own id (MachineGuid, machine-id, IOPlatformUUID).</param>
    /// <returns>The id, or <c>null</c> for an empty one.</returns>
    public static string? MachineIdOf(string? systemId)
    {
        var id = systemId?.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return string.IsNullOrEmpty(id) || id.All(c => c == '0')
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hnmo-machine|" + id)))[..16].ToLowerInvariant();
    }

    private static string ReadMachineId()
    {
        try
        {
            string? systemId = null;
            if (OperatingSystem.IsWindows())
            {
                systemId = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null) as string;
            }
            else if (OperatingSystem.IsMacOS())
            {
                systemId = ReadMacPlatformUuid();
            }
            else
            {
                foreach (var file in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
                {
                    if (File.Exists(file))
                    {
                        systemId = File.ReadAllText(file);
                        break;
                    }
                }
            }

            return MachineIdOf(systemId) ?? LegacyMachineId;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or System.ComponentModel.Win32Exception or ExternalException)
        {
            return LegacyMachineId;
        }
    }

    private static string? ReadMacPlatformUuid()
    {
        using var process = Process.Start(new ProcessStartInfo("ioreg", "-rd1 -c IOPlatformExpertDevice") { RedirectStandardOutput = true, UseShellExecute = false });
        if (process is null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        var line = output.Split('\n').FirstOrDefault(l => l.Contains("IOPlatformUUID", StringComparison.Ordinal));
        var parts = line?.Split('"');
        return parts is { Length: >= 4 } ? parts[3] : null;
    }
}
