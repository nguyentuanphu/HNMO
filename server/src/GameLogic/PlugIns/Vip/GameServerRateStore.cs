// <copyright file="GameServerRateStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence;

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> and <see cref="IHnmoPlayerStateRowBackend"/>
/// storing per-server Drop &amp; Zen rate multipliers (<c>server.rates</c>) and resolving VIP-only requirements.
/// </summary>
public sealed class GameServerRateStore
{
    /// <summary>Unified state store section key.</summary>
    public const string SectionKey = "serverRates";

    /// <summary>Row-store feature key.</summary>
    public const string FeatureServerRates = "server.rates";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, GameServerRateStore> Shared = new(StringComparer.Ordinal);

    private readonly object _gate = new();
    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<ServerRatesEntry> _sync = new();
    private readonly Dictionary<string, ServerRatesEntry> _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameServerRateStore"/> class.
    /// </summary>
    /// <param name="filePath">Optional state file path/key; null keeps in-memory state only.</param>
    public GameServerRateStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this._entries = this.Load();
    }

    /// <summary>Opens the default unified-state store (<see cref="HnmoStateStore.StateFileName"/>).</summary>
    /// <returns>The default <see cref="GameServerRateStore"/>.</returns>
    public static GameServerRateStore OpenDefault() => Open(HnmoStateStore.StateFileName);

    /// <summary>Opens or returns the singleton store for the given file name or path.</summary>
    /// <param name="fileName">Configured state file path or name.</param>
    /// <returns>The store instance.</returns>
    public static GameServerRateStore Open(string? fileName)
    {
        var key = string.IsNullOrWhiteSpace(fileName) ? string.Empty : fileName.Trim();
        var resolved = key.Length == 0
            ? string.Empty
            : (HnmoStateStore.ResolveStateFilePath(key) ?? Path.GetFullPath(key, AppContext.BaseDirectory));
        return Shared.GetOrAdd(resolved, p => new GameServerRateStore(p.Length == 0 ? null : p));
    }

    /// <summary>Returns whether a server definition requires active VIP access (<see cref="Stats.IsVip"/>).</summary>
    /// <param name="definition">Server definition.</param>
    /// <returns><c>true</c> if the server requires <see cref="Stats.IsVip"/>.</returns>
    public static bool IsVipOnly(GameServerDefinition? definition) =>
        definition?.JoinRequirements?.Any(IsVipRequirement) is true;

    /// <summary>Returns whether an <see cref="IGameServerContext"/> requires active VIP access (<see cref="Stats.IsVip"/>).</summary>
    /// <param name="context">Game server context.</param>
    /// <returns><c>true</c> if the server requires <see cref="Stats.IsVip"/>.</returns>
    public static bool IsVipOnly(IGameServerContext? context) =>
        context?.JoinRequirements?.Any(IsVipRequirement) is true;

    /// <summary>Checks whether a requirement targets <see cref="Stats.IsVip"/>.</summary>
    /// <param name="requirement">Attribute requirement.</param>
    /// <returns><c>true</c> if it checks <see cref="Stats.IsVip"/>.</returns>
    public static bool IsVipRequirement(AttributeRequirement? requirement) =>
        requirement is { MinimumValue: >= 1 } && requirement.Attribute?.Id == Stats.IsVip.Id;

    /// <summary>Reads the configured Drop and Zen multipliers for a server (defaulting to <c>1.0</c>).</summary>
    /// <param name="serverId">Zero-based game server identifier.</param>
    /// <returns>Tuple of <c>(DropRate, ZenRate)</c>.</returns>
    public (float DropRate, float ZenRate) GetRates(byte serverId)
    {
        var key = KeyOf(serverId);
        lock (this._stateStore?.SyncRoot ?? this._gate)
        {
            if (this._entries.TryGetValue(key, out var entry))
            {
                return (NormalizeRate(entry.DropRate), NormalizeRate(entry.ZenRate));
            }

            return (1.0f, 1.0f);
        }
    }

    /// <summary>Updates and persists the Drop and Zen multipliers for a server.</summary>
    /// <param name="serverId">Zero-based game server identifier.</param>
    /// <param name="dropRate">Item drop multiplier (<c>0.1</c> to <c>50.0</c>).</param>
    /// <param name="zenRate">Zen drop multiplier (<c>0.1</c> to <c>50.0</c>).</param>
    /// <returns>Saved entry snapshot.</returns>
    public ServerRatesEntry SetRates(byte serverId, float dropRate, float zenRate)
    {
        var key = KeyOf(serverId);
        var saved = new ServerRatesEntry
        {
            ServerId = serverId,
            DropRate = NormalizeRate(dropRate),
            ZenRate = NormalizeRate(zenRate),
        };

        lock (this._stateStore?.SyncRoot ?? this._gate)
        {
            this._entries[key] = saved;
            this._sync.MarkDirty(key);
            this._sync.SaveChanges(this._rowBackend, FeatureServerRates, this._entries, JsonOptions);
        }

        return new ServerRatesEntry
        {
            ServerId = saved.ServerId,
            DropRate = saved.DropRate,
            ZenRate = saved.ZenRate,
        };
    }

    private static string KeyOf(byte serverId) => serverId.ToString(CultureInfo.InvariantCulture);

    private static float NormalizeRate(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            return 1.0f;
        }

        return Math.Clamp(value, 0.1f, 50.0f);
    }

    private Dictionary<string, ServerRatesEntry> Load()
    {
        var dict = this._stateStore?.ReadSection<Dictionary<string, ServerRatesEntry>>(SectionKey, JsonOptions)
            ?? new Dictionary<string, ServerRatesEntry>(StringComparer.Ordinal);
        if (this._sync.LoadOrMigrate(this._rowBackend, FeatureServerRates, dict, JsonOptions))
        {
            this._stateStore?.WriteSection(SectionKey, dict, JsonOptions);
        }

        return dict;
    }
}
