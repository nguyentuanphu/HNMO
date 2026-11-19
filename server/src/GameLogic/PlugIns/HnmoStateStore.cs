// <copyright file="HnmoStateStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

/// <summary>
/// Single atomic JSON state store (<c>save/hnmo-state.json</c>) shared by all plug-ins that keep
/// state outside the relational database, with per-plug-in sections, <c>.bak</c> recovery, and
/// automatic migration from legacy per-plug-in JSON files into <c>save/old/</c>.
/// </summary>
public sealed class HnmoStateStore
{
    /// <summary>The unified state file name.</summary>
    public const string StateFileName = "hnmo-state.json";

    /// <summary>The subfolder where migrated legacy JSON files are moved.</summary>
    public const string OldDirectoryName = "old";

    private static readonly JsonSerializerOptions DocumentJsonOptions = new() { WriteIndented = false };
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly ConcurrentDictionary<string, Lazy<HnmoStateStore>> Shared = new(PathComparer);

    private static readonly IReadOnlyDictionary<string, string> LegacyFileToSection =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["account-safety.json"] = "accountSafety",
            ["offline-whispers.json"] = "offlineWhispers",
            ["community.json"] = "community",
            ["gift-codes.json"] = "giftCodes",
            ["guilds.json"] = "guilds",
            ["missions.json"] = "missions",
            ["onboarding.json"] = "onboarding",
            ["resets.json"] = "resets",
            ["top-races.json"] = "topRaces",
            ["world-bosses.json"] = "worldBosses",
        };

    private readonly object _lock = new();
    private readonly Dictionary<string, JsonElement> _sections = new(StringComparer.Ordinal);
    private readonly string _filePath;
    private readonly string? _legacySectionKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoStateStore"/> class.
    /// </summary>
    /// <param name="filePath">The resolved path to the state file.</param>
    /// <param name="legacySectionKey">Optional section key when loading a single-section legacy file at <paramref name="filePath"/>.</param>
    public HnmoStateStore(string filePath, string? legacySectionKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this._filePath = Path.GetFullPath(filePath);
        this._legacySectionKey = legacySectionKey;
        this.LoadAndMigrate();
    }

    /// <summary>Gets the resolved path to the state file on disk.</summary>
    public string FilePath => this._filePath;

    /// <summary>Gets the backup file path (<c>.bak</c>).</summary>
    public string BackupFilePath => this._filePath + ".bak";

    /// <summary>
    /// Resolves a configured legacy or state file path to the shared state file path.
    /// </summary>
    /// <param name="filePath">The configured file path, or <c>null</c>/whitespace for in-memory only.</param>
    /// <returns>The resolved state file path, or <c>null</c> for in-memory only.</returns>
    public static string? ResolveStateFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(filePath.Trim());
        var fileName = Path.GetFileName(fullPath);
        if (string.Equals(fileName, StateFileName, StringComparison.OrdinalIgnoreCase)
            || LegacyFileToSection.ContainsKey(fileName))
        {
            var directory = Path.GetDirectoryName(fullPath);
            return string.IsNullOrEmpty(directory) ? StateFileName : Path.Combine(directory, StateFileName);
        }

        return fullPath;
    }

    /// <summary>
    /// Gets the lock of this file's state. A wrapper that reads a section, changes it and writes it back holds it
    /// for the whole step, so two wrappers over the same file (each game server, website request and admin page
    /// opens its own) can't overwrite each other's change. The store's own methods take the same lock again.
    /// </summary>
    public object SyncRoot => this._lock;

    /// <summary>
    /// Opens or returns the shared <see cref="HnmoStateStore"/> for a configured file path.
    /// </summary>
    /// <param name="filePath">The file path, or <c>null</c>/whitespace for in-memory only.</param>
    /// <param name="legacySectionKey">Optional section key when <paramref name="filePath"/> points to a custom single-section file.</param>
    /// <returns>The shared store, or <c>null</c> when <paramref name="filePath"/> is empty.</returns>
    public static HnmoStateStore? Open(string? filePath, string? legacySectionKey = null)
    {
        var resolved = ResolveStateFilePath(filePath);
        if (resolved is null)
        {
            return null;
        }

        return resolved is null ? null : Shared.GetOrAdd(
            resolved,
            path => new Lazy<HnmoStateStore>(() => new HnmoStateStore(path, legacySectionKey), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    /// <summary>
    /// Resolves a time zone identifier (default Sydney) with cross-platform Windows/IANA fallback.
    /// </summary>
    /// <param name="timeZoneId">The requested time zone id.</param>
    /// <returns>The resolved time zone.</returns>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        var candidates = string.IsNullOrWhiteSpace(timeZoneId)
            ? new[] { "Australia/Sydney", "AUS Eastern Standard Time" }
            : new[] { timeZoneId.Trim(), "Australia/Sydney", "AUS Eastern Standard Time" };

        foreach (var id in candidates)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    /// <summary>
    /// Converts a UTC timestamp into the wall-clock time of <paramref name="zone"/>.
    /// </summary>
    /// <param name="utc">The UTC timestamp.</param>
    /// <param name="zone">The target time zone.</param>
    /// <returns>The local wall-clock time in <paramref name="zone"/>.</returns>
    public static DateTime ToTimeZone(DateTime utc, TimeZoneInfo zone)
    {
        var normalized = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(normalized, zone);
    }

    /// <summary>
    /// Reads and deserializes a section, or returns <c>null</c> when the section does not exist or cannot be parsed.
    /// </summary>
    /// <typeparam name="T">The section type.</typeparam>
    /// <param name="sectionKey">The section key.</param>
    /// <param name="options">The JSON serializer options.</param>
    /// <returns>The deserialized section value, or <c>default</c>.</returns>
    public T? ReadSection<T>(string sectionKey, JsonSerializerOptions options)
    {
        lock (this._lock)
        {
            if (!this._sections.TryGetValue(sectionKey, out var element))
            {
                return default;
            }

            try
            {
                return element.Deserialize<T>(options);
            }
            catch (JsonException)
            {
                return default;
            }
        }
    }

    /// <summary>
    /// Writes a section and atomically flushes the unified state file (plus <c>.bak</c>) to disk.
    /// </summary>
    /// <typeparam name="T">The section type.</typeparam>
    /// <param name="sectionKey">The section key.</param>
    /// <param name="value">The section value.</param>
    /// <param name="options">The JSON serializer options.</param>
    public void WriteSection<T>(string sectionKey, T value, JsonSerializerOptions options)
    {
        lock (this._lock)
        {
            var hadPrevious = this._sections.TryGetValue(sectionKey, out var previous);
            this._sections[sectionKey] = JsonSerializer.SerializeToElement(value, options);
            try
            {
                this.SaveLocked();
            }
            catch
            {
                if (hadPrevious)
                {
                    this._sections[sectionKey] = previous;
                }
                else
                {
                    this._sections.Remove(sectionKey);
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Returns whether a section is currently present in the store.
    /// </summary>
    /// <param name="sectionKey">The section key.</param>
    /// <returns><c>true</c> if the section exists.</returns>
    public bool HasSection(string sectionKey)
    {
        lock (this._lock)
        {
            return this._sections.ContainsKey(sectionKey);
        }
    }

    /// <summary>
    /// Removes a section cleanly and flushes the state file when it was present.
    /// </summary>
    /// <param name="sectionKey">The section key.</param>
    public void RemoveSection(string sectionKey)
    {
        lock (this._lock)
        {
            if (this._sections.Remove(sectionKey))
            {
                this.SaveLocked();
            }
        }
    }

    private void LoadAndMigrate()
    {
        lock (this._lock)
        {
            this._sections.Clear();

            var loaded = this.TryLoadFile(this._filePath, this._legacySectionKey, out var loadedFromLegacyFormat);
            if (!loaded)
            {
                if (File.Exists(this._filePath))
                {
                    TryCopyQuietly(this._filePath, this._filePath + ".broken");
                }

                if (this.TryLoadFile(this.BackupFilePath, this._legacySectionKey, out loadedFromLegacyFormat))
                {
                    TryCopyQuietly(this.BackupFilePath, this._filePath);
                }
            }

            var migrated = loadedFromLegacyFormat;
            if (string.Equals(Path.GetFileName(this._filePath), StateFileName, StringComparison.OrdinalIgnoreCase))
            {
                migrated |= this.MigrateLegacyFilesLocked();
            }

            if (migrated)
            {
                this.SaveLocked();
            }
        }
    }

    private bool TryLoadFile(string path, string? legacySectionKey, out bool loadedFromLegacyFormat)
    {
        loadedFromLegacyFormat = false;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0)
            {
                return false;
            }

            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("sections", out var sectionsElement)
                && sectionsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in sectionsElement.EnumerateObject())
                {
                    this._sections[property.Name] = property.Value.Clone();
                }

                return true;
            }

            if (!string.IsNullOrEmpty(legacySectionKey))
            {
                this._sections[legacySectionKey] = root.Clone();
                loadedFromLegacyFormat = true;
                return true;
            }

            return false;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool MigrateLegacyFilesLocked()
    {
        var directory = Path.GetDirectoryName(this._filePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        var migratedAny = false;
        var oldDirectory = Path.Combine(directory, OldDirectoryName);

        foreach (var (legacyFileName, sectionKey) in LegacyFileToSection)
        {
            var legacyPath = Path.Combine(directory, legacyFileName);
            if (!File.Exists(legacyPath))
            {
                continue;
            }

            try
            {
                if (!this._sections.ContainsKey(sectionKey))
                {
                    var bytes = File.ReadAllBytes(legacyPath);
                    if (bytes.Length > 0)
                    {
                        using var document = JsonDocument.Parse(bytes);
                        this._sections[sectionKey] = document.RootElement.Clone();
                        migratedAny = true;
                    }
                }

                Directory.CreateDirectory(oldDirectory);
                var destination = Path.Combine(oldDirectory, legacyFileName);
                File.Move(legacyPath, destination, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // Leave unreadable or broken legacy file untouched so it can be inspected or retried.
            }
        }

        return migratedAny;
    }

    private void SaveLocked()
    {
        var directory = Path.GetDirectoryName(this._filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var isUnified = string.Equals(Path.GetFileName(this._filePath), StateFileName, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(this._legacySectionKey);

        byte[] bytes;
        if (isUnified)
        {
            var payload = new StateDocument
            {
                Version = 1,
                Sections = this._sections,
            };

            bytes = JsonSerializer.SerializeToUtf8Bytes(payload, DocumentJsonOptions);
        }
        else if (this._sections.TryGetValue(this._legacySectionKey!, out var singleSection))
        {
            bytes = JsonSerializer.SerializeToUtf8Bytes(singleSection, DocumentJsonOptions);
        }
        else
        {
            bytes = "{}"u8.ToArray();
        }
        if (Directory.Exists(this._filePath))
        {
            throw new IOException($"Unified state path '{this._filePath}' is a directory.");
        }

        var tempPath = $"{this._filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(this._filePath))
            {
                File.Copy(this._filePath, this.BackupFilePath, overwrite: true);
            }

            File.Move(tempPath, this._filePath, overwrite: true);

            if (!File.Exists(this.BackupFilePath))
            {
                File.Copy(this._filePath, this.BackupFilePath, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static void TryCopyQuietly(string sourcePath, string destinationPath)
    {
        try
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort backup/broken copy.
        }
    }

    private sealed class StateDocument
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("sections")]
        public Dictionary<string, JsonElement> Sections { get; set; } = new(StringComparer.Ordinal);
    }
}
