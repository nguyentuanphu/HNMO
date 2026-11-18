// <copyright file="GmPermissionStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GmManager;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.Persistence;

/// <summary>
/// Thread-safe GM assignment and command audit store backed by <see cref="HnmoStateStore"/>
/// and <see cref="IHnmoPlayerStateRowBackend"/> (<c>gm.permissions</c>).
/// </summary>
public sealed class GmPermissionStore
{
    private const string FeatureKey = "gm.permissions";
    private const string SectionKey = "gmManager";
    private const int MaxAuditEntries = 200;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly Lazy<GmPermissionStore> SharedDefault = new(() => new GmPermissionStore(ResolveDefaultPath()));

    private readonly object _gate = new();
    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend? _rowBackend;
    private readonly HnmoFeatureRowSync<GmAssignment> _assignmentSync = new();
    private readonly string? _activityLogFileName;
    private StoredState _state;

    /// <summary>Initializes a new instance of the <see cref="GmPermissionStore"/> class.</summary>
    /// <param name="filePath">Optional state file path (or null for in-memory default).</param>
    /// <param name="activityLogFileName">Optional isolated activity log path for unit tests.</param>
    public GmPermissionStore(string? filePath = null, string? activityLogFileName = null)
    {
        this._stateStore = string.IsNullOrWhiteSpace(filePath)
            ? null
            : (HnmoStateStore.Open(filePath) ?? new HnmoStateStore(filePath));
        this._rowBackend = this._stateStore is null ? null : HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore.FilePath);
        this._activityLogFileName = activityLogFileName;
        this._state = this.LoadInitial();
    }

    /// <summary>Gets the shared default store.</summary>
    public static GmPermissionStore Default => SharedDefault.Value;

    /// <summary>Gets or sets a value indicating whether unlisted GMs are denied by default.</summary>
    public bool RequireListedForGmCommands
    {
        get
        {
            lock (this._gate)
            {
                return this._state.RequireListedForGmCommands;
            }
        }

        set
        {
            lock (this._gate)
            {
                this._state.RequireListedForGmCommands = value;
                this.PersistSettingsLocked();
            }
        }
    }

    /// <summary>Lists all GM permission assignments ordered by character name.</summary>
    /// <returns>All configured GM assignments.</returns>
    public IReadOnlyList<GmAssignment> ListAssignments()
    {
        lock (this._gate)
        {
            return this._state.Assignments.Values
                .OrderBy(a => a.CharacterName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <summary>Reads a single GM assignment by character name, if configured.</summary>
    /// <param name="characterName">The character name.</param>
    /// <returns>The assignment, or <c>null</c>.</returns>
    public GmAssignment? GetAssignment(string characterName)
    {
        var key = Canonical(characterName);
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        lock (this._gate)
        {
            return this._state.Assignments.TryGetValue(key, out var item) ? item : null;
        }
    }

    /// <summary>Creates or updates a GM character's permission mask.</summary>
    /// <param name="assignment">The assignment to save.</param>
    public void UpsertAssignment(GmAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        var key = Canonical(assignment.CharacterName);
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Character name is required.", nameof(assignment));
        }

        lock (this._gate)
        {
            assignment.UpdatedAtUtc = DateTime.UtcNow;
            this._state.Assignments[key] = assignment;
            this._assignmentSync.MarkDirty(key);
            if (this._rowBackend is not null)
            {
                this._assignmentSync.SaveChanges(this._rowBackend, FeatureKey, this._state.Assignments, JsonOptions, useDirtySetOnly: true);
            }

            this.PersistSettingsLocked();
        }
    }

    /// <summary>Removes a character from GM assignments.</summary>
    /// <param name="characterName">The character name.</param>
    /// <returns>True if an assignment existed and was removed.</returns>
    public bool RemoveAssignment(string characterName)
    {
        var key = Canonical(characterName);
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        lock (this._gate)
        {
            var removed = this._state.Assignments.Remove(key);
            if (removed)
            {
                this._assignmentSync.MarkDirty(key);
                if (this._rowBackend is not null)
                {
                    this._assignmentSync.SaveChanges(this._rowBackend, FeatureKey, this._state.Assignments, JsonOptions, useDirtySetOnly: true);
                }

                this.PersistSettingsLocked();
            }

            return removed;
        }
    }

    /// <summary>Reads recent GM command audit entries newest-first.</summary>
    /// <param name="limit">Maximum entries to return.</param>
    /// <returns>Audit entries.</returns>
    public IReadOnlyList<GmCommandAuditEntry> ListAuditEntries(int limit = 100)
    {
        lock (this._gate)
        {
            return this._state.Audits
                .OrderByDescending(a => a.TimestampUtc)
                .Take(Math.Clamp(limit, 1, MaxAuditEntries))
                .ToList();
        }
    }

    /// <summary>Checks whether <paramref name="characterName"/> may execute <paramref name="commandKey"/> and records an audit entry.</summary>
    /// <param name="characterName">The invoking GM character name.</param>
    /// <param name="accountLogin">The invoking GM account login.</param>
    /// <param name="commandKey">The parsed command key.</param>
    /// <param name="fullCommandText">The full chat command string.</param>
    /// <returns>Authorization verdict and required permission.</returns>
    public (bool Allowed, GmPermission RequiredPermission) AuthorizeAndAudit(
        string characterName,
        string accountLogin,
        string commandKey,
        string fullCommandText)
    {
        var required = GmCommandPermissionCatalog.Resolve(commandKey);
        var key = Canonical(characterName);
        bool allowed;
        string outcome;

        lock (this._gate)
        {
            if (this._state.Assignments.TryGetValue(key, out var assignment))
            {
                allowed = (assignment.Permissions & required) == required;
                outcome = allowed ? "Allowed" : $"Denied (missing {required})";
            }
            else if (this._state.RequireListedForGmCommands)
            {
                allowed = false;
                outcome = "Denied (GM not listed in GM Manager)";
            }
            else
            {
                allowed = true;
                outcome = "Allowed (legacy unlisted GM)";
            }

            var entry = new GmCommandAuditEntry
            {
                TimestampUtc = DateTime.UtcNow,
                GmCharacter = characterName,
                Account = accountLogin,
                Command = commandKey,
                CommandText = fullCommandText.Length > 200 ? fullCommandText[..200] : fullCommandText,
                RequiredPermission = required,
                Allowed = allowed,
                Outcome = outcome,
            };

            this._state.Audits.Add(entry);
            if (this._state.Audits.Count > MaxAuditEntries)
            {
                this._state.Audits.RemoveRange(0, this._state.Audits.Count - MaxAuditEntries);
            }

            this.PersistSettingsLocked();
        }

        this.WriteActivityLog(characterName, accountLogin, commandKey, required, allowed, outcome, fullCommandText);
        return (allowed, required);
    }

    private static string ResolveDefaultPath()
    {
        var env = Environment.GetEnvironmentVariable("HNMO_STATE_PATH");
        return string.IsNullOrWhiteSpace(env)
            ? Path.Combine(AppContext.BaseDirectory, "save", "hnmo-state.json")
            : env;
    }

    private static string Canonical(string? characterName)
        => (characterName ?? string.Empty).Trim().ToLowerInvariant();

    private StoredState LoadInitial()
    {
        var loaded = this._stateStore?.ReadSection<StoredState>(SectionKey, JsonOptions) ?? new StoredState();
        loaded.Assignments ??= new Dictionary<string, GmAssignment>(StringComparer.OrdinalIgnoreCase);
        loaded.Audits ??= [];

        if (this._rowBackend is not null)
        {
            this._assignmentSync.LoadOrMigrate(this._rowBackend, FeatureKey, loaded.Assignments, JsonOptions);
        }

        return loaded;
    }

    private void PersistSettingsLocked()
    {
        this._stateStore?.WriteSection(SectionKey, this._state, JsonOptions);
    }

    private void WriteActivityLog(string characterName, string accountLogin, string commandKey, GmPermission required, bool allowed, string outcome, string fullCommandText)
    {
        if (this._stateStore is null && string.IsNullOrWhiteSpace(this._activityLogFileName))
        {
            return;
        }

        ActivityLogPlugIn.RecordExternal(
            new ActivityLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Kind = ActivityKind.Administration,
                Account = accountLogin,
                Character = characterName,
                PartnerCharacter = "GM Command",
                ItemSummary = commandKey,
                Details = $"{outcome} | Perm: {required} | Cmd: {(fullCommandText.Length > 120 ? fullCommandText[..120] : fullCommandText)}",
            },
            this._activityLogFileName);
    }

    private sealed class StoredState
    {
        public bool RequireListedForGmCommands { get; set; }

        public Dictionary<string, GmAssignment> Assignments { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public List<GmCommandAuditEntry> Audits { get; set; } = [];
    }
}
