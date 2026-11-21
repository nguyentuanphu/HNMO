// <copyright file="PortOwner.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Which program listens on a port, so "another program uses port 44406" can say which one and where it is: the host
/// then closes the right program instead of guessing (owner's report, 2026-11-21).
/// </summary>
internal static class PortOwner
{
    /// <summary>Describes the program listening on a TCP port, e.g. <c>HNMO.Startup (PID 1234, D:\Old\server\HNMO.Startup.exe)</c>.</summary>
    /// <param name="port">The port.</param>
    /// <returns>The description; <c>null</c> when it can't be found out.</returns>
    public static string? Describe(int port)
    {
        try
        {
            var pid = OperatingSystem.IsWindows() ? ListeningPidFromNetstat(Run("netstat", "-ano -p TCP"), port)
                : OperatingSystem.IsLinux() ? ListeningPidFromProc(port)
                : int.TryParse(Run("lsof", $"-nP -iTCP:{port} -sTCP:LISTEN -t").Split('\n')[0].Trim(), out var macPid) ? macPid : null;
            if (pid is not { } id)
            {
                return null;
            }

            using var process = Process.GetProcessById(id);
            string? path = null;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // Another user's program: the name and id are enough.
            }

            return path is null
                ? $"{process.ProcessName} (PID {id})"
                : $"{process.ProcessName} (PID {id}, {path})";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or IOException)
        {
            return null;
        }
    }

    /// <summary>The process id listening on a port, from the output of <c>netstat -ano -p TCP</c> (Windows).</summary>
    /// <param name="netstat">The output.</param>
    /// <param name="port">The port.</param>
    /// <returns>The process id, if a line listens on that port.</returns>
    internal static int? ListeningPidFromNetstat(string netstat, int port)
    {
        foreach (var line in netstat.Split('\n'))
        {
            // "  TCP    0.0.0.0:44406          0.0.0.0:0              LISTENING       1234"
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 5
                && parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase)
                && parts[1].EndsWith(":" + port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                && parts[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
            {
                return pid;
            }
        }

        return null;
    }

    /// <summary>The socket inode listening on a port, from <c>/proc/net/tcp</c> or <c>tcp6</c> (Linux).</summary>
    /// <param name="procNetTcp">The file's text.</param>
    /// <param name="port">The port.</param>
    /// <returns>The inode, if a line listens on that port.</returns>
    internal static string? ListeningInode(string procNetTcp, int port)
    {
        var hexPort = ":" + port.ToString("X4", CultureInfo.InvariantCulture);
        foreach (var line in procNetTcp.Split('\n'))
        {
            // "   0: 00000000:AD76 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 123456 1 ..."
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 10 && parts[1].EndsWith(hexPort, StringComparison.OrdinalIgnoreCase) && parts[3] == "0A")
            {
                return parts[9];
            }
        }

        return null;
    }

    private static int? ListeningPidFromProc(int port)
    {
        var inode = new[] { "/proc/net/tcp", "/proc/net/tcp6" }.Where(File.Exists).Select(f => ListeningInode(File.ReadAllText(f), port)).FirstOrDefault(i => i is not null);
        if (inode is null)
        {
            return null;
        }

        var socket = $"socket:[{inode}]";
        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid))
            {
                continue;
            }

            try
            {
                if (Directory.EnumerateFiles(Path.Combine(dir, "fd")).Any(fd => new FileInfo(fd).LinkTarget == socket))
                {
                    return pid;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Another user's program: not visible.
            }
        }

        return null;
    }

    private static string Run(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })
            ?? throw new InvalidOperationException($"{fileName} didn't start.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        return output;
    }
}
