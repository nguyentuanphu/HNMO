// <copyright file="HnmoSaveFolder.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.IO;
using System.Threading;
using HNMO.Interfaces;

/// <summary>
/// The HNMO folder's <c>save</c> folder, where every runtime state file of the game server belongs, and the
/// one-time move of the state files older versions wrote into the program folder.
/// </summary>
/// <remarks>
/// The game server runs in the package's <c>server</c> folder, which a new version replaces. A configured path that is
/// a bare file name (<c>hnmo-state.json</c>) or a <c>save/…</c> path relative to that folder is therefore mapped
/// into the package's <c>save</c> folder, the folder <see cref="HostSettingsStore.ResolveDefaultPath"/> finds for
/// <c>hnmo-settings.json</c>. Absolute paths elsewhere stay as they are.
/// </remarks>
public sealed class HnmoSaveFolder
{
    /// <summary>The subfolder of the save folder (<c>save/old</c>) the originals of moved files are copied to.</summary>
    public const string OldDirectoryName = HnmoStateStore.OldDirectoryName;

    /// <summary>The file name of the worlds list.</summary>
    public const string WorldsFileName = "hnmo-worlds.json";

    /// <summary>The folder of the save folder the database log retention page exports old entries to.</summary>
    public const string ArchivesDirectoryName = "archives";

    /// <summary>The name of the program's own (wrong) save folder, relative to the program folder.</summary>
    private const string ProgramSaveName = "save";

    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static readonly Lazy<HnmoSaveFolder> DefaultFolder = new(CreateDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoSaveFolder"/> class.
    /// </summary>
    /// <param name="directory">The package's save folder.</param>
    /// <param name="programFolders">The program folders (the server folder, the working folder) whose state files belong in <paramref name="directory"/>.</param>
    /// <param name="relativeTo">The folder relative paths are resolved against (the working folder).</param>
    public HnmoSaveFolder(string directory, IEnumerable<string> programFolders, string relativeTo)
    {
        this.Directory = Normalize(directory);
        this.RelativeTo = Normalize(relativeTo);
        this.ProgramFolders = programFolders
            .Select(Normalize)
            .Where(p => !Same(p, this.Directory) && !IsUnder(p, this.Directory))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Gets the save folder of this program: the package's <c>save</c>.</summary>
    public static HnmoSaveFolder Default => DefaultFolder.Value;

    /// <summary>Gets the full path of the save folder.</summary>
    public string Directory { get; }

    /// <summary>Gets the program folders whose state files are mapped into <see cref="Directory"/>.</summary>
    public IReadOnlyList<string> ProgramFolders { get; }

    /// <summary>Gets the folder relative paths are resolved against.</summary>
    public string RelativeTo { get; }

    /// <summary>
    /// Maps a configured path into the save folder: a bare file name or a <c>save/…</c> path relative to the program
    /// folder (and the absolute <c>&lt;program&gt;/save/…</c> paths code builds) go into <see cref="Directory"/>;
    /// anything else, an absolute path an admin chose for example, stays where it is.
    /// </summary>
    /// <param name="path">The configured path.</param>
    /// <returns>The full path to use.</returns>
    public string MapPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var trimmed = path.Trim();
        var rooted = Path.IsPathRooted(trimmed);
        var full = Path.GetFullPath(trimmed, this.RelativeTo);
        if (IsUnder(full, this.Directory))
        {
            return full;
        }

        var fileName = Path.GetFileName(full);
        var directory = Path.GetDirectoryName(full) ?? string.Empty;
        foreach (var program in this.ProgramFolders)
        {
            var programSave = Path.Combine(program, ProgramSaveName);
            if (IsUnder(full, programSave))
            {
                return Path.Combine(this.Directory, Path.GetRelativePath(programSave, full));
            }

            if (Same(directory, program) && (!rooted || IsStateFileName(fileName)))
            {
                return Path.Combine(this.Directory, fileName);
            }
        }

        return full;
    }

    /// <summary>
    /// Takes the state files an older version wrote into the program folders into the save folder, once. Each
    /// original is copied to <c>save/old/&lt;program folder&gt;/</c> and then into the save folder: when the save folder
    /// already has a file of that name, the sections of two state files are merged (a section only the old file has is
    /// taken, a section both have keeps the save folder's); of any other file the save folder's is kept. The
    /// originals stay where they are, so an older version started after a rollback still finds them, and a marker
    /// (<c>save/old/server-moved.json</c>, one per season) makes sure a later start never takes them again.
    /// </summary>
    /// <remarks>
    /// Each season's program takes only its own files (Season 6.3 the top level, the worlds list and the exported
    /// database logs, Season 9 the <c>s9</c> folders, the Arena the <c>arena</c> folders), so programs starting at the
    /// same time never write one file. When a file couldn't be taken, no marker is written and the next start tries
    /// again; that repeats nothing, because a merge never replaces what the save folder has.
    /// </remarks>
    /// <param name="season">The season of this program (<see cref="HnmoSeasonInstance"/>).</param>
    /// <param name="nowUtc">The time written into the marker.</param>
    /// <returns>What was done, one line per decision, for the log.</returns>
    public IReadOnlyList<string> MoveOldStateFiles(string season, DateTime nowUtc)
    {
        var log = new List<string>();
        var marker = this.MarkerPath(season);
        if (File.Exists(marker))
        {
            return log;
        }

        var taken = new List<MovedFile>();
        var failed = false;
        foreach (var program in this.ProgramFolders)
        {
            foreach (var (source, relative, isArchive) in this.FindOldFiles(program, season))
            {
                var target = Path.Combine(this.Directory, StripProgramSave(relative));
                var oldCopy = Path.Combine(this.Directory, OldDirectoryName, Path.GetFileName(program), relative);
                try
                {
                    if (isArchive)
                    {
                        CopyArchive(source, target, log);
                    }
                    else
                    {
                        this.TakeOne(source, target, oldCopy, log);
                    }

                    taken.Add(new MovedFile(source, target));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failed = true;
                    log.Add($"Couldn't take {source} into {target} ({ex.Message}); the next start tries again.");
                }
            }
        }

        if (taken.Count == 0 || failed)
        {
            return log;
        }

        WriteMarker(marker, nowUtc, taken);
        log.Add($"The copies in the server folder ({string.Join(", ", taken.Select(t => t.Source))}) are no longer used; they stay there for an older version. Marker: {marker}.");
        return log;
    }

    /// <summary>Gets the marker file which says the program folders' files of a season were taken.</summary>
    /// <param name="season">The season key.</param>
    /// <returns>The marker's path.</returns>
    public string MarkerPath(string season) => Path.Combine(
        this.Directory,
        OldDirectoryName,
        season is HnmoSeasonInstance.SeasonNine or HnmoSeasonInstance.Arena ? $"server-moved-{season}.json" : "server-moved.json");

    private static HnmoSaveFolder CreateDefault()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var directory = Path.GetDirectoryName(HostSettingsStore.ResolveDefaultPath(baseDirectory)) ?? Path.Combine(baseDirectory, ProgramSaveName);
        var workingDirectory = System.IO.Directory.GetCurrentDirectory();
        return new HnmoSaveFolder(directory, [baseDirectory, workingDirectory], workingDirectory);
    }

    private static bool IsStateFileName(string fileName) =>
        string.Equals(fileName, HnmoStateStore.StateFileName, StringComparison.OrdinalIgnoreCase)
        || HnmoStateStore.IsLegacyFileName(fileName);

    private static string StripProgramSave(string relative)
    {
        var prefix = ProgramSaveName + Path.DirectorySeparatorChar;
        return relative.StartsWith(prefix, PathComparison) ? relative[prefix.Length..] : relative;
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool Same(string a, string b) => string.Equals(Normalize(a), Normalize(b), PathComparison);

    private static bool IsUnder(string path, string directory)
    {
        var prefix = Normalize(directory) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, PathComparison);
    }

    private static void CopyToOld(string source, string oldCopy)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(oldCopy)!);
        File.Copy(source, oldCopy, overwrite: true);
    }

    private static void CopyArchive(string source, string target, List<string> log)
    {
        if (File.Exists(target))
        {
            log.Add($"Kept {target}; {source} has the same name.");
            return;
        }

        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target);
        log.Add($"Copied {source} to {target}.");
    }

    private static void WriteMarker(string marker, DateTime nowUtc, List<MovedFile> taken)
    {
        var utc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var sydney = HnmoStateStore.ToTimeZone(utc, HnmoStateStore.ResolveTimeZone(null));
        var document = new
        {
            movedAtUtc = utc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            movedAtSydney = sydney.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            files = taken.Select(t => new { source = t.Source, target = t.Target }).ToArray(),
        };
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        var temp = marker + ".tmp";
        File.WriteAllText(temp, System.Text.Json.JsonSerializer.Serialize(document, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, marker, overwrite: true);
    }

    private IEnumerable<(string Source, string Relative, bool IsArchive)> FindOldFiles(string program, string season)
    {
        var secondary = season is HnmoSeasonInstance.SeasonNine or HnmoSeasonInstance.Arena;
        string[] programFolderFiles = secondary
            ? [Path.Combine(season, HnmoStateStore.StateFileName)]
            : [HnmoStateStore.StateFileName, WorldsFileName];
        foreach (var relative in programFolderFiles)
        {
            var source = Path.Combine(program, relative);
            if (File.Exists(source))
            {
                yield return (source, relative, false);
            }
        }

        var programSave = Path.Combine(program, ProgramSaveName);
        if (Same(programSave, this.Directory) || IsUnder(this.Directory, programSave))
        {
            yield break;
        }

        var folder = secondary ? Path.Combine(programSave, season) : programSave;
        if (!System.IO.Directory.Exists(folder))
        {
            yield break;
        }

        foreach (var source in System.IO.Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            yield return (source, Path.GetRelativePath(program, source), false);
        }

        // The database logs the retention page exported (Season 6.3's page only).
        var archives = Path.Combine(programSave, ArchivesDirectoryName);
        if (secondary || !System.IO.Directory.Exists(archives))
        {
            yield break;
        }

        foreach (var source in System.IO.Directory.GetFiles(archives, "*", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            yield return (source, Path.GetRelativePath(program, source), true);
        }
    }

    private void TakeOne(string source, string target, string oldCopy, List<string> log)
    {
        CopyToOld(source, oldCopy);
        if (!File.Exists(target))
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
            log.Add($"Copied {source} to {target} (and to {oldCopy}).");
            return;
        }

        var sourceSections = HnmoStateStore.TryReadSections(source);
        var targetSections = HnmoStateStore.TryReadSections(target);
        if (sourceSections is null || targetSections is null)
        {
            log.Add($"Kept {target}; {source} is not a state file to merge, so it was only copied to {oldCopy}.");
            return;
        }

        var missing = sourceSections.Where(s => !targetSections.ContainsKey(s.Key)).ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
        foreach (var key in sourceSections.Keys.Where(targetSections.ContainsKey).Order(StringComparer.Ordinal))
        {
            log.Add($"Section '{key}': kept the one of {target} (newer), the one of {source} is in {oldCopy}.");
        }

        if (missing.Count > 0)
        {
            new HnmoStateStore(target).AddMissingSections(missing);
            foreach (var key in missing.Keys.Order(StringComparer.Ordinal))
            {
                log.Add($"Section '{key}': taken from {source} into {target}.");
            }
        }

        log.Add($"Merged {source} into {target} (and copied it to {oldCopy}).");
    }

    private sealed record MovedFile(string Source, string Target);
}
