// <copyright file="EconomyStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Economy;

using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.Persistence;

/// <summary>
/// Persists daily economy snapshots per Sydney calendar day in <c>hnmo.player_feature_state</c>
/// (<c>economy.dailySnapshot</c>) and computes anomaly detections and drop/sink metrics.
/// </summary>
public sealed class EconomyStore
{
    /// <summary>Feature key stored in <c>hnmo.player_feature_state</c>.</summary>
    public const string FeatureKey = "economy.dailySnapshot";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly ConcurrentDictionary<string, Lazy<EconomyStore>> Stores = new(PathComparer);
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();

    private readonly object _gate = new();
    private readonly string _resolvedStatePath;
    private readonly Dictionary<string, EconomyDailySnapshot> _snapshots = new(StringComparer.Ordinal);
    private IHnmoPlayerStateRowBackend? _loadedBackend;

    private EconomyStore(string resolvedStatePath)
    {
        this._resolvedStatePath = resolvedStatePath;
    }

    /// <summary>
    /// Opens or returns the shared <see cref="EconomyStore"/> bound to <paramref name="fileName"/>.
    /// </summary>
    /// <param name="fileName">State key or path.</param>
    /// <returns>The store instance.</returns>
    public static EconomyStore Open(string? fileName = null)
    {
        var key = string.IsNullOrWhiteSpace(fileName) ? HnmoStateStore.StateFileName : fileName.Trim();
        var resolved = HnmoStateStore.ResolveStateFilePath(key) ?? Path.GetFullPath(HnmoStateStore.StateFileName);
        return Stores.GetOrAdd(resolved, p => new Lazy<EconomyStore>(() => new EconomyStore(p), true)).Value;
    }

    /// <summary>Clears cached store wrappers (tests only).</summary>
    public static void ResetForTests() => Stores.Clear();

    /// <summary>Converts a UTC timestamp into the Sydney calendar date key (<c>yyyy-MM-dd</c>).</summary>
    /// <param name="utc">UTC timestamp.</param>
    /// <returns>Sydney date key.</returns>
    public static string SydneyDateKey(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SydneyZone)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Counts jewel pieces represented by <paramref name="item"/> (bundles expand by <c>(Level + 1) * 10</c>).</summary>
    /// <param name="item">The item.</param>
    /// <returns>Number of jewels represented, or 0 if not a jewel.</returns>
    public static int CountJewelPieces(Item? item)
    {
        if (item?.Definition is null)
        {
            return 0;
        }

        var name = item.Definition.Name.ValueInNeutralLanguage ?? string.Empty;
        if (!name.Contains("Jewel", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (item.Definition.Group == 12)
        {
            return (item.Level + 1) * 10;
        }

        return item.IsStackable() ? Math.Max(1, (int)Math.Round(item.Durability)) : 1;
    }

    /// <summary>
    /// Detects sudden global or per-account jumps between <paramref name="previous"/> and <paramref name="current"/>.
    /// </summary>
    /// <param name="previous">Previous daily snapshot, or <c>null</c> for first snapshot.</param>
    /// <param name="current">Current daily snapshot.</param>
    /// <param name="config">Threshold configuration.</param>
    /// <returns>List of flagged anomalies.</returns>
    public static List<EconomyAnomaly> DetectAnomalies(
        EconomyDailySnapshot? previous,
        EconomyDailySnapshot current,
        EconomyConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(config);

        var anomalies = new List<EconomyAnomaly>();
        if (previous is null)
        {
            return anomalies;
        }

        var pctThreshold = Math.Max(1, config.AnomalyJumpPercentThreshold);
        CheckMetric(anomalies, current.DateSydney, "Global", string.Empty, "Zen", previous.TotalZen, current.TotalZen, config.AnomalyMinZenDelta, pctThreshold);
        CheckMetric(anomalies, current.DateSydney, "Global", string.Empty, "Coins", previous.TotalCoins, current.TotalCoins, config.AnomalyMinCoinDelta, pctThreshold);
        CheckMetric(anomalies, current.DateSydney, "Global", string.Empty, "Jewels", previous.TotalJewels, current.TotalJewels, config.AnomalyMinJewelDelta, pctThreshold);

        var prevByAccount = previous.AccountSamples.ToDictionary(a => a.AccountLogin, StringComparer.OrdinalIgnoreCase);
        foreach (var curr in current.AccountSamples)
        {
            prevByAccount.TryGetValue(curr.AccountLogin, out var prev);
            var prevZen = prev?.Zen ?? 0;
            var prevCoins = prev?.Coins ?? 0;
            var prevJewels = prev?.Jewels ?? 0;
            CheckMetric(anomalies, current.DateSydney, "Account", curr.AccountLogin, "Zen", prevZen, curr.Zen, config.AnomalyMinZenDelta, pctThreshold);
            CheckMetric(anomalies, current.DateSydney, "Account", curr.AccountLogin, "Coins", prevCoins, curr.Coins, config.AnomalyMinCoinDelta, pctThreshold);
            CheckMetric(anomalies, current.DateSydney, "Account", curr.AccountLogin, "Jewels", prevJewels, curr.Jewels, config.AnomalyMinJewelDelta, pctThreshold);
        }

        return anomalies;
    }

    /// <summary>
    /// Computes and persists a snapshot for <paramref name="utcNow"/>'s Sydney date using player accounts,
    /// optional external balances, and activity log rates from <see cref="ActivityLogStore"/>.
    /// </summary>
    /// <param name="accounts">Player accounts to scan.</param>
    /// <param name="externalBalances">Optional external asset entries (Jewel Bank, Item Market).</param>
    /// <param name="config">Economy dashboard configuration.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="world">The installed world used for the verified jewel projection, when available.</param>
    /// <param name="jewelBank">The active Jewel Bank settings, if the bank is enabled.</param>
    /// <returns>The persisted snapshot.</returns>
    public EconomyDailySnapshot CaptureSnapshot(
        IReadOnlyList<Account> accounts,
        IReadOnlyList<EconomyAccountTotals> externalBalances,
        EconomyConfiguration config,
        DateTime utcNow,
        GameConfiguration? world = null,
        JewelBankConfiguration? jewelBank = null)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(externalBalances);
        ArgumentNullException.ThrowIfNull(config);

        var byAccount = new Dictionary<string, EconomyAccountTotals>(StringComparer.OrdinalIgnoreCase);
        var accountCount = 0;

        foreach (var acc in accounts)
        {
            if (acc.IsBot || acc.IsTemplate || string.IsNullOrWhiteSpace(acc.LoginName))
            {
                continue;
            }

            accountCount++;
            var entry = GetOrCreateAccount(byAccount, acc.LoginName);
            entry.Coins += (long)acc.WCoinC + acc.WCoinP + acc.GoblinPoints;

            if (acc.Vault is { } vault)
            {
                entry.Zen += vault.Money;
                AddItems(entry, vault.Items);
            }

            foreach (var ch in acc.Characters)
            {
                if (ch.Inventory is { } inv)
                {
                    entry.Zen += inv.Money;
                    AddItems(entry, inv.Items);
                }
            }
        }

        foreach (var ext in externalBalances)
        {
            if (string.IsNullOrWhiteSpace(ext.AccountLogin))
            {
                continue;
            }

            var entry = GetOrCreateAccount(byAccount, ext.AccountLogin);
            entry.Zen += ext.Zen;
            entry.Coins += ext.Coins;
            entry.Jewels += ext.Jewels;
            entry.ValuableItems += ext.ValuableItems;
        }

        var dateKey = SydneyDateKey(utcNow);
        var (dayStartUtc, dayEndUtc) = SydneyDayWindowUtc(dateKey);
        var dayActivities = ActivityLogStore.Open(config.FileName).Query(new ActivityLogQuery
        {
            FromUtc = dayStartUtc,
            ToUtc = dayEndUtc,
            MaxResults = 2000,
        });

        var allAccounts = byAccount.Values.ToList();
        var topHolders = allAccounts
            .OrderByDescending(a => a.Zen)
            .ThenByDescending(a => a.Jewels)
            .ThenByDescending(a => a.Coins)
            .Take(10)
            .ToList();

        var mixes = world?.JewelMixes;
        var bankJewels = jewelBank?.Jewels?.Select(jewel => (jewel.Group, jewel.Number)).ToArray() ?? [];
        var jewelActivity = mixes is { Count: > 0 } || bankJewels.Length > 0
            ? EconomyJewelActivity.Capture(accounts, dayActivities, dayActivities.Count == 2000, new ConfiguredJewelCounter(mixes ?? [], bankJewels))
            : null;
        if (jewelActivity is not null && jewelBank is not null && !string.IsNullOrWhiteSpace(jewelBank.FileName))
        {
            jewelActivity = jewelActivity with { BankPieces = EconomyJewelActivity.ReadBankPieces(accounts, jewelBank.FileName) };
        }

        var snapshot = new EconomyDailySnapshot
        {
            DateSydney = dateKey,
            CapturedAtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            AccountCount = accountCount,
            TotalZen = allAccounts.Sum(a => a.Zen),
            TotalCoins = allAccounts.Sum(a => a.Coins),
            TotalJewels = allAccounts.Sum(a => a.Jewels),
            JewelActivity = jewelActivity,
            TotalValuableItems = allAccounts.Sum(a => a.ValuableItems),
            DropsPickedPerDay = dayActivities.Count(e => e.Kind == ActivityKind.Pickup),
            ChaosMixesPerDay = dayActivities.Count(e => e.Kind == ActivityKind.ChaosMix),
            NpcBuyZenSinkPerDay = dayActivities.Where(e => e.Kind == ActivityKind.NpcBuy).Sum(e => Math.Max(0, -e.ZenDelta)),
            NpcSellZenDropPerDay = dayActivities.Where(e => e.Kind == ActivityKind.NpcSell).Sum(e => Math.Max(0, e.ZenDelta)),
            TopHolders = topHolders,
            AccountSamples = allAccounts,
        };

        this.SaveSnapshot(snapshot, config);
        return snapshot;
    }

    /// <summary>Saves or updates a daily snapshot (detecting anomalies vs the most recent earlier day).</summary>
    /// <param name="snapshot">The snapshot to store.</param>
    /// <param name="config">Dashboard config used for anomaly detection and retention.</param>
    public void SaveSnapshot(EconomyDailySnapshot snapshot, EconomyConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(config);

        var retentionDays = Math.Clamp(config.RetentionDays, 7, 3650);
        lock (this._gate)
        {
            var backend = this.EnsureLoadedLocked();
            var previous = this._snapshots.Values
                .Where(s => string.Compare(s.DateSydney, snapshot.DateSydney, StringComparison.Ordinal) < 0)
                .OrderByDescending(s => s.DateSydney, StringComparer.Ordinal)
                .FirstOrDefault();

            snapshot.Anomalies = DetectAnomalies(previous, snapshot, config);
            this._snapshots[snapshot.DateSydney] = snapshot;

            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            var row = new HnmoFeatureRow(
                FeatureKey,
                snapshot.DateSydney,
                json,
                snapshot.CapturedAtUtc,
                snapshot.CapturedAtUtc.AddDays(retentionDays));
            backend.UpsertBatch([row], []);
        }
    }

    /// <summary>Loads the most recent public jewel source without reading private account samples on PostgreSQL.</summary>
    /// <returns>The stored snapshot with the required jewel projection, if one exists.</returns>
    public EconomyDailySnapshot? ReadLatestJewelSnapshot()
    {
        var backend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._resolvedStatePath);
        if (backend is not IHnmoPlayerStateRowQueryBackend indexed)
        {
            return this.LoadSnapshots(1).LastOrDefault();
        }

        if (indexed.LoadLatestEconomyJewels() is not { } row)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EconomyDailySnapshot>(row.PayloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Returns stored snapshots sorted chronologically by Sydney date ascending.</summary>
    /// <param name="maxDays">Maximum days to return.</param>
    /// <returns>Snapshots.</returns>
    public IReadOnlyList<EconomyDailySnapshot> LoadSnapshots(int maxDays = 60)
    {
        var take = Math.Clamp(maxDays, 1, 3650);
        lock (this._gate)
        {
            this.EnsureLoadedLocked();
            var list = this._snapshots.Values
                .OrderBy(s => s.DateSydney, StringComparer.Ordinal)
                .ToList();
            return list.Count <= take ? list : list.Skip(list.Count - take).ToList();
        }
    }

    private static (DateTime StartUtc, DateTime EndUtc) SydneyDayWindowUtc(string dateSydney)
    {
        var day = DateTime.ParseExact(dateSydney, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var startLocal = DateTime.SpecifyKind(day, DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(day.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(startLocal, SydneyZone), TimeZoneInfo.ConvertTimeToUtc(endLocal, SydneyZone));
    }

    private static EconomyAccountTotals GetOrCreateAccount(Dictionary<string, EconomyAccountTotals> map, string login)
    {
        if (!map.TryGetValue(login, out var entry))
        {
            entry = new EconomyAccountTotals { AccountLogin = login };
            map[login] = entry;
        }

        return entry;
    }

    private static void AddItems(EconomyAccountTotals target, IEnumerable<Item> items)
    {
        foreach (var item in items)
        {
            var jewelCount = CountJewelPieces(item);
            if (jewelCount > 0)
            {
                target.Jewels += jewelCount;
            }
            else if (ActivityLogPlugIn.IsValuableItem(item))
            {
                target.ValuableItems++;
            }
        }
    }

    private static void CheckMetric(
        List<EconomyAnomaly> anomalies,
        string dateSydney,
        string scope,
        string accountLogin,
        string metric,
        long prev,
        long curr,
        long minDelta,
        int pctThreshold)
    {
        var delta = curr - prev;
        if (delta < minDelta)
        {
            return;
        }

        var pct = prev <= 0 ? 100.0 : (delta * 100.0) / prev;
        if (pct >= pctThreshold)
        {
            anomalies.Add(new EconomyAnomaly
            {
                DateSydney = dateSydney,
                Scope = scope,
                AccountLogin = accountLogin,
                Metric = metric,
                PreviousValue = prev,
                NewValue = curr,
                PercentIncrease = Math.Round(pct, 1),
            });
        }
    }

    private static TimeZoneInfo ResolveSydneyZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        }
    }

    private IHnmoPlayerStateRowBackend EnsureLoadedLocked()
    {
        var backend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._resolvedStatePath);
        if (ReferenceEquals(this._loadedBackend, backend))
        {
            return backend;
        }

        this._snapshots.Clear();
        backend.EnsureSchema();
        foreach (var row in backend.LoadAll(FeatureKey))
        {
            try
            {
                var snap = JsonSerializer.Deserialize<EconomyDailySnapshot>(row.PayloadJson, JsonOptions);
                if (snap is not null && !string.IsNullOrWhiteSpace(snap.DateSydney))
                {
                    this._snapshots[snap.DateSydney] = snap;
                }
            }
            catch (JsonException)
            {
                // Ignore corrupt snapshot row safely.
            }
        }

        this._loadedBackend = backend;
        return backend;
    }
}
