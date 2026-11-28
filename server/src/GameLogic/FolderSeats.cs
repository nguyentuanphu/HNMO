// <copyright file="FolderSeats.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

/// <summary>
/// The seats of one computer: at most as many HNMO folders as the licence allows hold one at the same time. A seat is a
/// small file per folder, <c>&lt;machine id&gt;.&lt;folder code&gt;.seat</c>, in a directory every user of the computer can
/// write (<see cref="DefaultDirectory"/>), with the time the folder started running (<c>since</c>: the earliest process
/// start of its live programs) and its last heartbeat (<c>beat</c>). Every program of a folder (Season 6.3, Season 9, the
/// Arena) writes the same file, so they share one seat; each keeps the earlier of the file's <c>since</c> and its own
/// start. The folders whose files have a heartbeat of the last <see cref="Expiry"/> are lined up by <c>since</c> (then by
/// folder code): the first ones, up to the folders allowed, hold a seat, the others wait. So when the hub switches the
/// limit on, the folder which has been running longest keeps its seat, whichever asked first. A program which dies stops
/// beating, and its folder's place is free after <see cref="Expiry"/>; a program whose own heartbeat lapsed that long
/// lines up again from then, as if it had just started.
/// </summary>
/// <remarks>
/// Only plain files: written beside and renamed into place (atomic on Windows, macOS and Linux), read with every share
/// mode so a rename is never blocked, timestamps inside them rather than the file system's. No named mutex or other
/// API of one system.
/// </remarks>
public sealed class FolderSeats
{
    /// <summary>How long a folder's seat lasts without a heartbeat.</summary>
    public static readonly TimeSpan Expiry = TimeSpan.FromMinutes(2);

    /// <summary>How often a program beats (and checks whether its folder got a seat).</summary>
    public static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(30);

    private const string Extension = ".seat";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly Lazy<DateTime> ThisProcessStart = new(ReadProcessStart);

    private readonly Func<DateTime> _utcNow;

    private readonly DateTime _processStart;

    private DateTime _started;

    private DateTime? _lastBeat;

    /// <summary>Initializes a new instance of the <see cref="FolderSeats"/> class.</summary>
    /// <param name="directory">The directory of the seats (<see cref="DefaultDirectory"/>; another one in tests).</param>
    /// <param name="machineId">This computer's id (<c>PlayerLicense.MachineId</c>): the seats are kept per computer.</param>
    /// <param name="folderCode">This folder's code (<c>FolderIdentity</c>).</param>
    /// <param name="folderPath">This folder's path, shown to the other folders' admin panels.</param>
    /// <param name="utcNow">The clock (tests).</param>
    /// <param name="processStartUtc">When this program started (UTC); none: this process's start time (tests give one).</param>
    public FolderSeats(string directory, string machineId, string folderCode, string folderPath, Func<DateTime>? utcNow = null, DateTime? processStartUtc = null)
    {
        this.Directory = directory;
        this.MachineId = SafeName(machineId);
        this.FolderCode = SafeName(folderCode);
        this.FolderPath = folderPath;
        this._utcNow = utcNow ?? (() => DateTime.UtcNow);
        this._processStart = processStartUtc ?? ThisProcessStart.Value;
        this._started = this._processStart;
    }

    /// <summary>Gets the directory of the seats.</summary>
    public string Directory { get; }

    /// <summary>Gets this computer's id.</summary>
    public string MachineId { get; }

    /// <summary>Gets this folder's code.</summary>
    public string FolderCode { get; }

    /// <summary>Gets this folder's path.</summary>
    public string FolderPath { get; }

    /// <summary>Gets this folder's seat file.</summary>
    public string OwnFile => Path.Combine(this.Directory, $"{this.MachineId}.{this.FolderCode}{Extension}");

    /// <summary>
    /// Gets the directory every user of the computer can write: on Windows <c>%ProgramData%\HNMO\seats</c>; elsewhere
    /// <c>/var/tmp/hnmo-seats</c> (kept across restarts), or <c>/tmp/hnmo-seats</c> where there is no <c>/var/tmp</c>.
    /// </summary>
    /// <returns>The directory.</returns>
    public static string DefaultDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "HNMO", "seats");
        }

        return System.IO.Directory.Exists("/var/tmp") ? "/var/tmp/hnmo-seats" : "/tmp/hnmo-seats";
    }

    /// <summary>
    /// Beats for this folder and tells whether it holds a seat: it does when fewer than <paramref name="allowed"/> folders
    /// with a live seat file started running before it (equal times: the lower folder code first). Seat files without a
    /// heartbeat for <see cref="Expiry"/> are removed.
    /// </summary>
    /// <param name="allowed">The folders allowed at once (the licence's <c>folders</c>; 1 without a licence).</param>
    /// <returns>Whether this folder holds a seat, and the folders which hold one.</returns>
    /// <exception cref="IOException">The seats' directory or this folder's file can't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">This user may not write in the seats' directory.</exception>
    public FolderSeatResult Refresh(int allowed)
    {
        allowed = Math.Max(1, allowed);
        EnsureDirectory(this.Directory);
        var now = this._utcNow();
        if (this._lastBeat is { } last && !(now - last < Expiry))
        {
            // This program didn't beat for the expiry (it hung, or the clock jumped): the others saw it gone, so it lines
            // up again from now instead of taking a seat back from a folder which got it meanwhile.
            this._started = now;
        }

        this._lastBeat = now;
        var started = this._started < now ? this._started : now;
        var own = ReadSeat(this.OwnFile);
        var since = own is { } kept && IsLive(kept, now) && kept.Since < started ? kept.Since : started;
        this.Write(new SeatRecord { Folder = this.FolderCode, Machine = this.MachineId, Path = this.FolderPath, Since = since, Beat = now });

        var line = this.Live(now, removeStale: true)
            .Where(s => s.Folder != this.FolderCode)
            .Append(new SeatRecord { Folder = this.FolderCode, Machine = this.MachineId, Path = this.FolderPath, Since = since, Beat = now })
            .OrderBy(s => s.Since)
            .ThenBy(s => s.Folder, StringComparer.Ordinal)
            .ToList();
        var place = line.FindIndex(s => s.Folder == this.FolderCode);
        var holders = line.Take(allowed).ToList();
        return new FolderSeatResult(place < allowed, allowed, place, holders.Where(s => s.Folder != this.FolderCode).Select(s => s.Path ?? s.Folder).ToList());
    }

    /// <summary>Gives this folder's seat up at once (the folder limit was switched off).</summary>
    public void Release()
    {
        // Switched on again later, the folder lines up by its start once more.
        this._lastBeat = null;
        this._started = this._processStart;
        try
        {
            File.Delete(this.OwnFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It ends by itself after the expiry.
        }
    }

    /// <summary>The live seat files of this computer.</summary>
    /// <param name="nowUtc">The time.</param>
    /// <param name="removeStale">Whether to remove the files without a heartbeat for <see cref="Expiry"/>.</param>
    /// <returns>The seats.</returns>
    internal List<SeatRecord> Live(DateTime nowUtc, bool removeStale)
    {
        var live = new List<SeatRecord>();
        if (!System.IO.Directory.Exists(this.Directory))
        {
            return live;
        }

        foreach (var file in System.IO.Directory.EnumerateFiles(this.Directory, $"{this.MachineId}.*{Extension}"))
        {
            var seat = ReadSeat(file);
            if (seat is not null && IsLive(seat, nowUtc) && seat.Folder is { Length: > 0 })
            {
                live.Add(seat);
                continue;
            }

            if (removeStale && (seat is not null || IsOldFile(file, nowUtc)))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Another user's file without the right to remove it: it is ignored all the same.
                }
            }
        }

        return live;
    }

    /// <summary>This process's start time (UTC), read once; where the system doesn't tell it, the time of the first read.</summary>
    /// <returns>The time.</returns>
    private static DateTime ReadProcessStart()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.StartTime.ToUniversalTime();
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or Win32Exception or PlatformNotSupportedException)
        {
            return DateTime.UtcNow;
        }
    }

    private static bool IsLive(SeatRecord seat, DateTime nowUtc)
        => nowUtc - seat.Beat < Expiry && seat.Beat - nowUtc < Expiry;

    private static bool IsOldFile(string file, DateTime nowUtc)
    {
        try
        {
            return nowUtc - File.GetLastWriteTimeUtc(file) >= Expiry;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static SeatRecord? ReadSeat(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<SeatRecord>(stream, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string SafeName(string value)
    {
        var safe = new string(value.Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant();
        return safe.Length > 0 ? safe : "unknown";
    }

    /// <summary>Makes the directory, writable by every user of the computer.</summary>
    /// <param name="directory">The directory.</param>
    private static void EnsureDirectory(string directory)
    {
        if (System.IO.Directory.Exists(directory))
        {
            return;
        }

        System.IO.Directory.CreateDirectory(directory);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                LetEveryUserWrite(directory);
            }
            else
            {
                // rwx for everyone and no sticky bit: any user's program may replace or remove a seat file.
                File.SetUnixFileMode(directory, (UnixFileMode)0b111_111_111);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or PlatformNotSupportedException)
        {
            // Another program made it first, or the system refuses: the seats work for this user all the same.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void LetEveryUserWrite(string directory)
    {
        var info = new DirectoryInfo(directory);
        var security = info.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        info.SetAccessControl(security);
    }

    private void Write(SeatRecord seat)
    {
        var temporary = Path.Combine(this.Directory, $"{this.MachineId}.{this.FolderCode}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(seat, JsonOptions));
            File.Move(temporary, this.OwnFile, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // left behind; harmless
            }
        }
    }

    /// <summary>A folder's seat file.</summary>
    internal sealed class SeatRecord
    {
        /// <summary>Gets or sets the folder's code.</summary>
        public string Folder { get; set; } = string.Empty;

        /// <summary>Gets or sets the computer's id.</summary>
        public string Machine { get; set; } = string.Empty;

        /// <summary>Gets or sets the folder's path.</summary>
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets when the folder started running (UTC): the earliest process start of its live programs. Files of
        /// earlier versions hold the time the folder first asked; they line up by it all the same.
        /// </summary>
        public DateTime Since { get; set; }

        /// <summary>Gets or sets the last heartbeat (UTC).</summary>
        public DateTime Beat { get; set; }
    }
}
