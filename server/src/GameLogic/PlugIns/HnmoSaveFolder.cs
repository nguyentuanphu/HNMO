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
    /// Moves the state files an older version wrote into the program folders into the save folder, once: the
    /// originals are copied to <c>save/old/&lt;program folder&gt;/</c> first, then removed, so a second run finds
    /// nothing to do. When the save folder already has a file of that name, the sections of two state files are
    /// merged (a section only the old file has is taken, a section both have keeps the save folder's); of any other
    /// file the save folder's is kept.
    /// </summary>
    /// <remarks>
    /// Each season's program moves only its own files (Season 6.3 the top level and the worlds list, Season 9 the
    /// <c>s9</c> folders, the Arena the <c>arena</c> folders), so programs starting at the same time never move one file.
    /// </remarks>
    /// <param name="season">The season of this program (<see cref="HnmoSeasonInstance"/>).</param>
    /// <returns>What was done, one line per decision, for the log.</returns>
    public IReadOnlyList<string> MoveOldStateFiles(string season)
    {
        var log = new List<string>();
        foreach (var program in this.ProgramFolders)
        {
            foreach (var (source, relative) in this.FindOldFiles(program, season))
            {
                var target = Path.Combine(this.Directory, StripProgramSave(relative));
                var oldCopy = Path.Combine(this.Directory, OldDirectoryName, Path.GetFileName(program), relative);
                try
                {
                    this.MoveOne(source, target, oldCopy, log);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    log.Add($"Couldn't move {source} to {target} ({ex.Message}); it stays where it is and is moved on the next start.");
                }
            }
        }

        return log;
    }

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

    private static void RemoveWithBackup(string source, string oldCopy)
    {
        File.Delete(source);
        var backup = source + ".bak";
        if (File.Exists(backup))
        {
            File.Copy(backup, oldCopy + ".bak", overwrite: true);
            File.Delete(backup);
        }
    }

    private IEnumerable<(string Source, string Relative)> FindOldFiles(string program, string season)
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
                yield return (source, relative);
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
            yield return (source, Path.GetRelativePath(program, source));
        }
    }

    private void MoveOne(string source, string target, string oldCopy, List<string> log)
    {
        CopyToOld(source, oldCopy);
        if (!File.Exists(target))
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
            RemoveWithBackup(source, oldCopy);
            log.Add($"Moved {source} to {target} (original copied to {oldCopy}).");
            return;
        }

        var sourceSections = HnmoStateStore.TryReadSections(source);
        var targetSections = HnmoStateStore.TryReadSections(target);
        if (sourceSections is null || targetSections is null)
        {
            RemoveWithBackup(source, oldCopy);
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

        RemoveWithBackup(source, oldCopy);
        log.Add($"Merged {source} into {target} (original copied to {oldCopy}).");
    }
}
