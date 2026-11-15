// <copyright file="ResetStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

/// <summary>
/// What the reset options remember of a character.
/// </summary>
public sealed class ResetCharacterRecord
{
    /// <summary>Gets or sets the character's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the character class number.</summary>
    public int Class { get; set; }

    /// <summary>Gets or sets the relifes done.</summary>
    public int Relifes { get; set; }

    /// <summary>Gets or sets the resets after the last reset or relife.</summary>
    public int Resets { get; set; }

    /// <summary>Gets or sets a value indicating whether the delegated reset is on.</summary>
    public bool Delegated { get; set; }

    /// <summary>Gets or sets the day of the counts below (yyyy-MM-dd).</summary>
    public string DayKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the resets of that day (every source).</summary>
    public int DayResets { get; set; }

    /// <summary>Gets or sets the relifes of that day.</summary>
    public int DayRelifes { get; set; }

    /// <summary>Gets or sets the delegated resets of that day.</summary>
    public int DayDelegated { get; set; }

    /// <summary>Gets or sets the delegated resets the player wasn't told about yet.</summary>
    public int DelegatedUntold { get; set; }

    /// <summary>Gets or sets why the last delegated reset couldn't be done (a <see cref="ResetResult"/> name), until one is done.</summary>
    public string DelegatedBlocked { get; set; } = string.Empty;

    /// <summary>Gets or sets when the last reset or relife was done.</summary>
    public DateTime? LastAtUtc { get; set; }
}

/// <summary>
/// A reset or relife (the admin panel's log).
/// </summary>
public sealed class ResetLogEntry
{
    /// <summary>Gets or sets when.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the character's id.</summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets "reset" or "relife".</summary>
    public string Kind { get; set; } = "reset";

    /// <summary>Gets or sets where it was done.</summary>
    public ResetSource Source { get; set; }

    /// <summary>Gets or sets the level before.</summary>
    public int LevelBefore { get; set; }

    /// <summary>Gets or sets the resets after.</summary>
    public int ResetsAfter { get; set; }

    /// <summary>Gets or sets the relifes after.</summary>
    public int RelifesAfter { get; set; }

    /// <summary>Gets or sets the zen paid.</summary>
    public long Zen { get; set; }

    /// <summary>Gets or sets the coins paid (every coin type together; see <see cref="CoinsText"/>).</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the coins paid by type, e.g. "10 GoblinPoints".</summary>
    public string CoinsText { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the VIP advantages counted.</summary>
    public bool Vip { get; set; }
}

/// <summary>
/// The saved state of the reset options, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class ResetStoreData
{
    /// <summary>Gets or sets the characters, by character id.</summary>
    public Dictionary<string, ResetCharacterRecord> Characters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the resets per month (yyyy-MM) and character id.</summary>
    public Dictionary<string, Dictionary<string, int>> Months { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the resets and relifes, newest first.</summary>
    public List<ResetLogEntry> Log { get; set; } = new();
}

/// <summary>
/// Keeps the <see cref="ResetStoreData"/>; with a file, it outlasts a restart of the server. All game servers, the
/// admin panel and the website of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class ResetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, ResetStore> Shared = new(StringComparer.Ordinal);

    private readonly string? _filePath;
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public ResetStore(string? filePath = null)
    {
        this._filePath = filePath;
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the data.</summary>
    public ResetStoreData Data { get; }

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static ResetStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new ResetStore(p.Length == 0 ? null : p));
    }

    /// <summary>Marks the data as changed.</summary>
    public void Changed() => this._changed = true;

    /// <summary>
    /// Saves the data when it changed. The caller holds <see cref="Lock"/>.
    /// </summary>
    public void SaveIfChanged()
    {
        if (!this._changed || this._filePath is null)
        {
            this._changed = false;
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(this._filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = this._filePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(this.Data, JsonOptions));
            File.Move(temporary, this._filePath, true);
            this._changed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It's saved with the next change.
        }
    }

    private ResetStoreData Load()
    {
        if (this._filePath is null || !File.Exists(this._filePath))
        {
            return new ResetStoreData();
        }

        try
        {
            var data = JsonSerializer.Deserialize<ResetStoreData>(File.ReadAllText(this._filePath), JsonOptions) ?? new ResetStoreData();
            data.Characters = new Dictionary<string, ResetCharacterRecord>(data.Characters, StringComparer.Ordinal);
            data.Months = data.Months.ToDictionary(p => p.Key, p => new Dictionary<string, int>(p.Value, StringComparer.Ordinal), StringComparer.Ordinal);
            return data;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            try
            {
                File.Copy(this._filePath, this._filePath + ".broken", true);
            }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException)
            {
                // nothing more to do
            }

            return new ResetStoreData();
        }
    }
}
