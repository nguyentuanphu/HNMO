// <copyright file="CustomRankStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CustomRanks;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// Thread-safe store persisting player-selected custom titles and admin-granted achievement keys
/// via <see cref="HnmoPlayerStateRowStoreRegistry"/> and <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class CustomRankStore
{
    /// <summary>Section key inside <see cref="HnmoStateStore"/>.</summary>
    public const string SectionKey = "customRanks";

    /// <summary>Row feature key inside <see cref="IHnmoPlayerStateRowBackend"/>.</summary>
    public const string FeatureCharacters = "customRanks.character";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, CustomRankStore> Shared = new(StringComparer.Ordinal);

    private readonly object _gate = new();
    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<CustomRankCharacterRecord> _charSync = new();
    private readonly CustomRankStoreData _data;

    /// <summary>Initializes a new instance of the <see cref="CustomRankStore"/> class.</summary>
    /// <param name="filePath">Optional state file key or path; null uses in-memory only.</param>
    public CustomRankStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this._data = this.Load();
    }

    /// <summary>Opens or returns the singleton store for the given file name or path.</summary>
    /// <param name="fileName">Configured state file path or name.</param>
    /// <returns>The store instance.</returns>
    public static CustomRankStore Open(string? fileName)
    {
        var key = string.IsNullOrWhiteSpace(fileName) ? string.Empty : fileName.Trim();
        var resolved = key.Length == 0
            ? string.Empty
            : (HnmoStateStore.ResolveStateFilePath(key) ?? Path.GetFullPath(key, AppContext.BaseDirectory));
        return Shared.GetOrAdd(resolved, p => new CustomRankStore(p.Length == 0 ? null : p));
    }

    /// <summary>Gets a snapshot copy of a character's record if present.</summary>
    /// <param name="characterName">The character name.</param>
    /// <returns>A cloned record or <c>null</c>.</returns>
    public CustomRankCharacterRecord? GetCharacter(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return null;
        }

        lock (this._gate)
        {
            var key = NormalizeKey(characterName);
            return this._data.Characters.TryGetValue(key, out var record) ? Clone(record) : null;
        }
    }

    /// <summary>Saves a character's preferred rank tier identifier.</summary>
    /// <param name="characterName">The character name.</param>
    /// <param name="selectedRankId">The tier identifier, or empty for auto.</param>
    public void SetSelection(string characterName, string selectedRankId)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return;
        }

        var display = characterName.Trim();
        var key = NormalizeKey(display);
        lock (this._gate)
        {
            var record = this.GetOrCreateMutable(key, display);
            record.SelectedRankId = selectedRankId.Trim();
            this.SaveKeyLocked(key);
        }
    }

    /// <summary>Grants an achievement key to a character.</summary>
    /// <param name="characterName">The character name.</param>
    /// <param name="achievementKey">The achievement key.</param>
    /// <returns><c>true</c> if added; <c>false</c> if invalid or already held.</returns>
    public bool GrantAchievement(string characterName, string achievementKey)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(achievementKey))
        {
            return false;
        }

        var display = characterName.Trim();
        var key = NormalizeKey(display);
        var trimmedAch = achievementKey.Trim();
        lock (this._gate)
        {
            var record = this.GetOrCreateMutable(key, display);
            if (record.AchievementKeys.Any(k => string.Equals(k, trimmedAch, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            record.AchievementKeys.Add(trimmedAch);
            this.SaveKeyLocked(key);
            return true;
        }
    }

    /// <summary>Revokes an achievement key from a character.</summary>
    /// <param name="characterName">The character name.</param>
    /// <param name="achievementKey">The achievement key.</param>
    /// <returns><c>true</c> if removed.</returns>
    public bool RevokeAchievement(string characterName, string achievementKey)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(achievementKey))
        {
            return false;
        }

        var key = NormalizeKey(characterName);
        var trimmedAch = achievementKey.Trim();
        lock (this._gate)
        {
            if (!this._data.Characters.TryGetValue(key, out var record))
            {
                return false;
            }

            var removed = record.AchievementKeys.RemoveAll(k => string.Equals(k, trimmedAch, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                this.SaveKeyLocked(key);
            }

            return removed;
        }
    }

    /// <summary>Returns all character records currently carrying granted achievement keys.</summary>
    /// <returns>Sorted character records.</returns>
    public IReadOnlyList<CustomRankCharacterRecord> ListAchievementHolders()
    {
        lock (this._gate)
        {
            return this._data.Characters.Values
                .Where(r => r.AchievementKeys.Count > 0)
                .OrderBy(r => r.CharacterName, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList();
        }
    }

    private static string NormalizeKey(string characterName) => characterName.Trim().ToLowerInvariant();

    private static CustomRankCharacterRecord Clone(CustomRankCharacterRecord source) => new()
    {
        CharacterName = source.CharacterName,
        SelectedRankId = source.SelectedRankId,
        AchievementKeys = [.. source.AchievementKeys],
    };

    private CustomRankCharacterRecord GetOrCreateMutable(string key, string displayName)
    {
        if (!this._data.Characters.TryGetValue(key, out var record))
        {
            record = new CustomRankCharacterRecord { CharacterName = displayName };
            this._data.Characters[key] = record;
        }

        return record;
    }

    private void SaveKeyLocked(string key)
    {
        if (this._stateStore is null)
        {
            return;
        }

        this._charSync.MarkDirty(key);
        this._charSync.SaveChanges(this._rowBackend, FeatureCharacters, this._data.Characters, JsonOptions, useDirtySetOnly: true);
    }

    private CustomRankStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new CustomRankStoreData();
        }

        var data = this._stateStore.ReadSection<CustomRankStoreData>(SectionKey, JsonOptions) ?? new CustomRankStoreData();
        data.Characters = new Dictionary<string, CustomRankCharacterRecord>(data.Characters, StringComparer.OrdinalIgnoreCase);
        if (this._charSync.LoadOrMigrate(this._rowBackend, FeatureCharacters, data.Characters, JsonOptions))
        {
            this._stateStore.WriteSection(SectionKey, new CustomRankStoreData(), JsonOptions);
        }

        return data;
    }
}
