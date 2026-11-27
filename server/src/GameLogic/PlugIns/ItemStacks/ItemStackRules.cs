// <copyright file="ItemStackRules.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemStacks;

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Entities;
using HNMO.Persistence;

/// <summary>
/// HNMO's stack rules, answered for <see cref="HNMO.DataModel.ItemExtensions.StackLimit"/>: how many pieces of an item one slot holds.
/// Free kinds stack for everybody; the other kinds for an account which unlocked them. The account is the one whose
/// request the server is handling (<see cref="CurrentPlayer"/>, set around each packet). A stack which already holds
/// more than one piece keeps its kind's limit everywhere, so it's counted right where no player is known (a website
/// purchase, a delivery).
/// </summary>
public static class ItemStackRules
{
    /// <summary>The feature key of an account's unlocks in the state row backend.</summary>
    public const string AccountFeatureKey = "itemStacks.account";

    private static readonly AsyncLocal<Player?> CurrentPlayerHolder = new();

    private static readonly ConcurrentDictionary<string, HashSet<string>> UnlocksByLogin = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static volatile Snapshot? _snapshot;

    /// <summary>Gets or sets the player whose request is being handled in this flow.</summary>
    public static Player? CurrentPlayer
    {
        get => CurrentPlayerHolder.Value;
        set => CurrentPlayerHolder.Value = value;
    }

    /// <summary>Gets the active configuration, or <c>null</c> when the plug-in isn't active.</summary>
    public static ItemStackConfiguration? Configuration => _snapshot?.Configuration;

    /// <summary>Sets the configuration (by the plug-in), and makes <see cref="HNMO.DataModel.ItemExtensions.StackLimit"/> use these rules.</summary>
    /// <param name="configuration">The configuration, or <c>null</c> to switch the rules off.</param>
    public static void Configure(ItemStackConfiguration? configuration)
    {
        if (configuration is null)
        {
            _snapshot = null;
            return;
        }

        var kinds = new Dictionary<(byte Group, short Number), ItemStackKind>();
        foreach (var kind in configuration.Kinds)
        {
            foreach (var range in kind.Items)
            {
                foreach (var key in ParseRange(range))
                {
                    kinds.TryAdd(key, kind);
                }
            }
        }

        _snapshot = new Snapshot(configuration, kinds);
        HNMO.DataModel.ItemExtensions.StackLimit = Resolve;
    }

    /// <summary>Finds the kind of an item, if HNMO's rules stack it.</summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <returns>The kind, or <c>null</c>.</returns>
    public static ItemStackKind? KindOf(byte group, short number) =>
        _snapshot is { } snapshot && snapshot.Kinds.TryGetValue((group, number), out var kind) ? kind : null;

    /// <summary>Tells whether the account has unlocked a kind.</summary>
    /// <param name="login">The account login name.</param>
    /// <param name="kindKey">The kind key.</param>
    /// <returns><c>true</c> if unlocked.</returns>
    public static bool IsUnlocked(string login, string kindKey) => UnlocksOf(login).Contains(kindKey);

    /// <summary>The kinds an account has unlocked.</summary>
    /// <param name="login">The account login name.</param>
    /// <returns>The kind keys.</returns>
    public static IReadOnlySet<string> UnlocksOf(string login)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return new HashSet<string>();
        }

        return UnlocksByLogin.GetOrAdd(login, LoadUnlocks);
    }

    /// <summary>Records an unlock (after the payment was saved).</summary>
    /// <param name="login">The account login name.</param>
    /// <param name="kindKey">The kind key.</param>
    public static void AddUnlock(string login, string kindKey)
    {
        var unlocks = new HashSet<string>(UnlocksOf(login), StringComparer.OrdinalIgnoreCase) { kindKey };
        var backend = Backend();
        backend.EnsureSchema();
        var payload = JsonSerializer.Serialize(new AccountUnlocks { Unlocked = [.. unlocks.Order(StringComparer.Ordinal)] }, JsonOptions);
        backend.UpsertBatch([new HnmoFeatureRow(AccountFeatureKey, login, payload, DateTime.UtcNow)], []);
        UnlocksByLogin[login] = unlocks;
    }

    /// <summary>Forgets the cached unlocks (tests, and an admin change of the rows).</summary>
    public static void ResetCache() => UnlocksByLogin.Clear();

    /// <summary>The stack limit of an item under HNMO's rules, or <c>null</c> for the item's own limit.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The limit, or <c>null</c>.</returns>
    public static int? Resolve(Item item)
    {
        if (_snapshot is not { Configuration.Enabled: true } snapshot
            || item.Definition is not { } definition
            || !snapshot.Kinds.TryGetValue((definition.Group, definition.Number), out var kind))
        {
            return null;
        }

        var limit = Math.Clamp(kind.Limit, 1, byte.MaxValue);
        if (kind.Free || item.Durability > 1)
        {
            return Math.Max(limit, definition.Durability);
        }

        return CurrentPlayer?.Account?.LoginName is { Length: > 0 } login && IsUnlocked(login, kind.Key)
            ? Math.Max(limit, definition.Durability)
            : null;
    }

    /// <summary>Reads <c>group:number</c> or <c>group:from-to</c>.</summary>
    /// <param name="range">The text.</param>
    /// <returns>The item keys.</returns>
    internal static IEnumerable<(byte Group, short Number)> ParseRange(string range)
    {
        var parts = range.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var group))
        {
            yield break;
        }

        var bounds = parts[1].Split('-', 2, StringSplitOptions.TrimEntries);
        if (!short.TryParse(bounds[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var from))
        {
            yield break;
        }

        var to = from;
        if (bounds.Length == 2 && !short.TryParse(bounds[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out to))
        {
            yield break;
        }

        for (var number = from; number <= to && number >= 0; number++)
        {
            yield return (group, number);
        }
    }

    private static HashSet<string> LoadUnlocks(string login)
    {
        var row = Backend().LoadAll(AccountFeatureKey).FirstOrDefault(r => string.Equals(r.ScopeId, login, StringComparison.OrdinalIgnoreCase));
        var unlocks = row is null ? null : JsonSerializer.Deserialize<AccountUnlocks>(row.PayloadJson, JsonOptions);
        return new HashSet<string>(unlocks?.Unlocked ?? [], StringComparer.OrdinalIgnoreCase);
    }

    private static IHnmoPlayerStateRowBackend Backend()
    {
        var file = _snapshot?.Configuration.FileName;
        var key = string.IsNullOrWhiteSpace(file) ? HnmoStateStore.StateFileName : file.Trim();
        var resolved = HnmoStateStore.ResolveStateFilePath(key) ?? System.IO.Path.GetFullPath(HnmoStateStore.StateFileName);
        return HnmoPlayerStateRowStoreRegistry.GetBackend(resolved);
    }

    private sealed record Snapshot(ItemStackConfiguration Configuration, Dictionary<(byte Group, short Number), ItemStackKind> Kinds);

    private sealed class AccountUnlocks
    {
        public List<string> Unlocked { get; set; } = [];
    }
}
