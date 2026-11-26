// <copyright file="WebsiteTrackerController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Economy;
using HNMO.Interfaces;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

/// <summary>Serves a bounded, allowlisted event feed with current host and player privacy controls.</summary>
/// <param name="persistence">The host configuration source.</param>
/// <param name="servers">The running servers.</param>
/// <param name="cache">Short-lived cache of raw activity rows.</param>
/// <param name="clock">The clock for the retention window.</param>
[ApiController]
[AllowAnonymous]
[Route("api/website/tracker")]
public sealed class WebsiteTrackerController(IPersistenceContextProvider persistence, IDictionary<int, IGameServer> servers, IMemoryCache cache, TimeProvider clock) : ControllerBase
{
    internal const int MaximumEvents = 24;
    private static readonly MemoryCache ItemCache = new(new MemoryCacheOptions { SizeLimit = 128 });
    private const string CacheKey = "website-tracker-events";
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    /// <summary>Returns only eligible event types, Sydney timestamps and permitted character labels.</summary>
    /// <returns>A bounded snapshot with its source retention.</returns>
    [HttpGet]
    public async ValueTask<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        if (await this.ReadActiveTrackerAsync().ConfigureAwait(false) is not { } tracker)
        {
            return this.Ok(new { enabled = false, events = Array.Empty<object>() });
        }

        var (settings, config) = tracker;
        var retentionDays = Math.Clamp(config.RetentionDays, 1, 3650);
        var from = clock.GetUtcNow().UtcDateTime.AddDays(-retentionDays);
        if (!cache.TryGetValue<RawWindow>(CacheKey, out var window)
            || window is null || window.FileName != config.FileName || window.RetentionDays != retentionDays)
        {
            var rows = ActivityLogStore.Open(config.FileName).QueryRecent(from, MaximumEvents * 2);
            window = new(config.FileName, retentionDays, rows);
            cache.Set(CacheKey, window, TimeSpan.FromSeconds(15));
        }

        var result = Project(window.Rows.Where(entry => entry.TimestampUtc >= from), settings.TrackerShowCharacterNames, WebsiteTrackerPrivacyStore.Open());
        return this.Ok(new { enabled = true, retentionDays, timeZone = Sydney.Id, events = result });
    }

    /// <summary>Reports whether public item history is available.</summary>
    /// <returns>The availability of the tracker and its activity log.</returns>
    [HttpGet("status")]
    public async ValueTask<IActionResult> GetStatusAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        return this.Ok(new { enabled = await this.ReadActiveTrackerAsync().ConfigureAwait(false) is not null });
    }

    /// <summary>Reads recent activity for a supplied origin serial, without opening private inventories.</summary>
    /// <param name="serial">The real origin serial, using the 32-character or dashed GUID format.</param>
    /// <returns>A privacy-filtered snapshot limited to the active log's retention.</returns>
    [HttpGet("item")]
    public async ValueTask<IActionResult> GetItemAsync([FromQuery] Guid serial)
    {
        this.Response.Headers.CacheControl = "no-store";
        if (serial == Guid.Empty)
        {
            return this.BadRequest();
        }

        if (await this.ReadActiveTrackerAsync().ConfigureAwait(false) is not { } tracker)
        {
            return this.Ok(new { enabled = false, events = Array.Empty<object>() });
        }

        var (settings, config) = tracker;
        var retentionDays = Math.Clamp(config.RetentionDays, 1, 3650);
        var from = clock.GetUtcNow().UtcDateTime.AddDays(-retentionDays);
        var rows = ItemCache.GetOrCreate((config.FileName, retentionDays, serial), entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            return ActivityLogStore.Open(config.FileName).QueryItemHistory(serial, from, MaximumEvents * 2);
        })!;
        var result = ProjectItemHistory(rows.Where(row => row.TimestampUtc >= from), serial, settings.TrackerShowCharacterNames, WebsiteTrackerPrivacyStore.Open());
        return this.Ok(new { enabled = true, retentionDays, timeZone = Sydney.Id, events = result });
    }

    /// <summary>Reads the most recent verified jewel snapshot under the current tracker policy.</summary>
    /// <returns>The snapshot and visible hunters, or a disabled or unavailable result.</returns>
    [HttpGet("jewels")]
    public async ValueTask<IActionResult> GetJewelsAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        if (await this.ReadActiveTrackerAsync().ConfigureAwait(false) is not { } tracker)
        {
            return this.Ok(new { enabled = false });
        }

        var cancellationToken = this.HttpContext.RequestAborted;
        var economy = await PlugInSettings.ReadActiveAsync<EconomyConfiguration>(persistence, typeof(EconomyDashboardPlugIn), cancellationToken).ConfigureAwait(false);
        if (economy is not { Enabled: true })
        {
            return this.Ok(new { enabled = false });
        }

        var cached = cache.GetOrCreate("website-tracker-jewels", options =>
        {
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            return new JewelCache(economy.FileName, EconomyStore.Open(economy.FileName).ReadLatestJewelSnapshot());
        });
        if (cached is null || cached.FileName != economy.FileName)
        {
            cached = new JewelCache(economy.FileName, EconomyStore.Open(economy.FileName).ReadLatestJewelSnapshot());
            cache.Set("website-tracker-jewels", cached, TimeSpan.FromSeconds(15));
        }

        if (cached.Snapshot is not { JewelActivity: { } info } snapshot)
        {
            return this.Ok(new { enabled = true, available = false });
        }

        var capturedUtc = DateTime.SpecifyKind(snapshot.CapturedAtUtc, DateTimeKind.Utc);
        var capturedAt = TimeZoneInfo.ConvertTime(new DateTimeOffset(capturedUtc), Sydney);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(capturedAt.Date, Sydney);
        var hunterWindowAvailable = dayStartUtc >= clock.GetUtcNow().UtcDateTime.AddDays(-Math.Clamp(tracker.Config.RetentionDays, 1, 3650));
        var hunters = Array.Empty<object>();
        if (hunterWindowAvailable && tracker.Settings.TrackerShowCharacterNames && info.Hunters.Count > 0
            && servers.Values.OfType<IGameServerContextProvider>().OrderBy(server => server.Context.Id).FirstOrDefault() is { } server)
        {
            var candidates = info.Hunters.Take(50).ToArray();
            var logins = candidates.Select(row => row.AccountKey);
            using var playerContext = persistence.CreateNewPlayerContext(server.Context.Configuration);
            var eligible = await playerContext.ReadEligibleAccountKeysAsync(logins, cancellationToken).ConfigureAwait(false);
            var hidden = WebsiteTrackerPrivacyStore.Open().ReadOptOuts(logins);
            hunters = candidates.Where(row => eligible.Contains(row.AccountKey)
                    && !hidden.Contains(WebsiteTrackerPrivacyStore.Key(row.AccountKey))
                    && IsCharacterName(row.Character) && !string.Equals(row.AccountKey, row.Character, StringComparison.OrdinalIgnoreCase))
                .Take(10).Select(row => (object)new { row.Character, row.Pieces }).ToArray();
        }

        return this.Ok(new
        {
            enabled = true,
            available = true,
            snapshot.DateSydney,
            capturedAt,
            info.InventoryAndVaultPieces,
            info.BankPieces,
            info.LogWindowCapped,
            showHunters = tracker.Settings.TrackerShowCharacterNames,
            hunterWindowAvailable,
            hunters,
        });
    }

    /// <summary>Reads recorded PvP defeats by mode under the current host and player settings.</summary>
    /// <returns>A bounded set of combat groups and the active retention.</returns>
    [HttpGet("pvp")]
    public async ValueTask<IActionResult> GetPvpAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        if (await this.ReadActiveTrackerAsync().ConfigureAwait(false) is not { } tracker)
        {
            return this.Ok(new { enabled = false });
        }

        var (settings, config) = tracker;
        var retentionDays = Math.Clamp(config.RetentionDays, 1, 3650);
        var from = clock.GetUtcNow().UtcDateTime.AddDays(-retentionDays);
        var window = cache.GetOrCreate("website-tracker-pvp", options =>
        {
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            return ReadPvpWindow(config.FileName, retentionDays, from);
        });

        if (window is null || window.FileName != config.FileName || window.RetentionDays != retentionDays)
        {
            window = ReadPvpWindow(config.FileName, retentionDays, from);
            cache.Set("website-tracker-pvp", window, TimeSpan.FromSeconds(15));
        }

        var privacy = WebsiteTrackerPrivacyStore.Open();
        var groups = window.Groups.Select(group => new
        {
            mode = group.Mode,
            events = ProjectPvp(group.Rows, from, settings.TrackerShowCharacterNames, privacy),
        }).ToArray();
        return this.Ok(new { enabled = true, retentionDays, timeZone = Sydney.Id, groups });
    }

    /// <summary>Projects recent combat rows; either player's choice can remove a row.</summary>
    /// <param name="rows">The recorded defeats.</param>
    /// <param name="from">The live retention cutoff.</param>
    /// <param name="showNames">Whether character names may be shown.</param>
    /// <param name="privacy">Account-level privacy choices.</param>
    /// <returns>The bounded set of safe defeat records.</returns>
    internal static object[] ProjectPvp(IEnumerable<ActivityLogEntry> rows, DateTime from, bool showNames, WebsiteTrackerPrivacyStore privacy)
        => AllowedEvents(rows.Where(row => row.Kind == ActivityKind.PvpKill && row.TimestampUtc >= from && !string.IsNullOrWhiteSpace(row.PartnerAccount)), privacy, isPvp: true)
            .Select(entry => (object)new
            {
                time = Time(entry),
                winner = showNames ? CharacterName(entry.Character, entry) : null,
                defeated = showNames ? CharacterName(entry.PartnerCharacter, entry) : null,
                entry.ServerId,
                entry.MapNumber,
            }).ToArray();

    private static PvpWindow ReadPvpWindow(string? fileName, int retentionDays, DateTime from)
    {
        var store = ActivityLogStore.Open(fileName);
        return new(fileName, retentionDays,
        [
            ("field", store.QueryPvpKills(ActivityPvpMode.Field, from, MaximumEvents * 2)),
            ("duel", store.QueryPvpKills(ActivityPvpMode.Duel, from, MaximumEvents * 2)),
            ("arena", store.QueryPvpKills(ActivityPvpMode.Arena, from, MaximumEvents * 2)),
            ("siege", store.QueryPvpKills(ActivityPvpMode.Siege, from, MaximumEvents * 2)),
        ]);
    }

    /// <summary>Projects recent rows with live privacy preferences, without returning raw activity records.</summary>
    /// <param name="rows">The recent raw activity rows.</param>
    /// <param name="showNames">Whether the host permits character names.</param>
    /// <param name="privacy">The authoritative account preferences.</param>
    /// <returns>The eligible public page.</returns>
    public static object[] Project(IEnumerable<ActivityLogEntry> rows, bool showNames, WebsiteTrackerPrivacyStore privacy)
    {
        return AllowedEvents(rows, privacy)
            .Select(entry => (object)new
            {
                kind = PublicKind(entry.Kind),
                time = Time(entry),
                character = showNames ? CharacterName(entry.Character, entry) : null,
                partner = showNames && !string.IsNullOrWhiteSpace(entry.PartnerAccount) ? CharacterName(entry.PartnerCharacter, entry) : null,
            }).ToArray();
    }

    /// <summary>Projects only the queried item's snapshots and filters current opt-outs.</summary>
    /// <param name="rows">Matching rows returned by the activity store.</param>
    /// <param name="serial">The requested item serial.</param>
    /// <param name="showNames">Whether this host permits character names.</param>
    /// <param name="privacy">Current account preferences.</param>
    /// <returns>The bounded public history.</returns>
    internal static object[] ProjectItemHistory(IEnumerable<ActivityLogEntry> rows, Guid serial, bool showNames, WebsiteTrackerPrivacyStore privacy)
    {
        var token = serial.ToString("N");
        return AllowedEvents(rows.Where(row => row.ItemSerials?.Split(',').Contains(token, StringComparer.Ordinal) is true), privacy)
            .Select(entry => (object)new
            {
                kind = PublicKind(entry.Kind),
                time = Time(entry),
                character = showNames ? CharacterName(entry.Character, entry) : null,
                partner = showNames && !string.IsNullOrWhiteSpace(entry.PartnerAccount) ? CharacterName(entry.PartnerCharacter, entry) : null,
                before = ItemSnapshot(entry.BeforeItems?.FirstOrDefault(item => item.Serial == serial)),
                after = ItemSnapshot(entry.AfterItems?.FirstOrDefault(item => item.Serial == serial)),
            }).ToArray();
    }

    private static object? ItemSnapshot(ActivityItemState? item)
        => item is null ? null : new { item.Group, item.Number, item.Level, item.Quantity, item.Durability, item.Sockets, item.HasSkill };

    private static DateTimeOffset Time(ActivityLogEntry entry)
        => TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(entry.TimestampUtc, DateTimeKind.Utc)), Sydney);

    private static IEnumerable<ActivityLogEntry> AllowedEvents(IEnumerable<ActivityLogEntry> rows, WebsiteTrackerPrivacyStore privacy, bool isPvp = false)
    {
        var candidates = rows.Where(row => !string.IsNullOrWhiteSpace(row.Account) && (isPvp || PublicKind(row.Kind) is not null))
            .Take(MaximumEvents * 2).ToArray();
        var optedOut = privacy.ReadOptOuts(candidates.SelectMany(entry => new[] { entry.Account, entry.PartnerAccount }).Where(login => !string.IsNullOrWhiteSpace(login)));
        return candidates.Where(entry => !optedOut.Contains(WebsiteTrackerPrivacyStore.Key(entry.Account))
                && (string.IsNullOrWhiteSpace(entry.PartnerAccount) || !optedOut.Contains(WebsiteTrackerPrivacyStore.Key(entry.PartnerAccount))))
            .Take(MaximumEvents);
    }

    private async ValueTask<(WebsiteConfiguration Settings, ActivityLogConfiguration Config)?> ReadActiveTrackerAsync()
    {
        var settings = await PlugInSettings.ReadActiveAsync<WebsiteConfiguration>(persistence, typeof(WebsitePlugIn), this.HttpContext.RequestAborted).ConfigureAwait(false);
        if (settings is not { TrackerEnabled: true })
        {
            return null;
        }

        var activity = servers.Values.OfType<IGameServerContextProvider>().OrderBy(server => server.Context.Id)
            .Select(server => ActivityLogPlugIn.Get(server.Context)).FirstOrDefault(plugin => plugin is not null && plugin.Configuration?.Enabled is not false);
        return activity is not null ? (settings, activity.Configuration ?? new()) : null;
    }

    private static bool IsCharacterName(string? name)
        => name is { Length: > 0 and <= 10 } && name.All(character => char.IsLetterOrDigit(character) || character is '_' or '-');

    private static string? CharacterName(string? name, ActivityLogEntry entry)
        => IsCharacterName(name)
            && !string.Equals(entry.Account, name, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(entry.PartnerAccount, name, StringComparison.OrdinalIgnoreCase)
            ? name : null;

    private static string? PublicKind(ActivityKind kind) => kind switch
    {
        ActivityKind.Trade => "trade",
        ActivityKind.Drop => "drop",
        ActivityKind.Pickup => "pickup",
        ActivityKind.ChaosMix => "mix",
        ActivityKind.NpcBuy => "npc-buy",
        ActivityKind.NpcSell => "npc-sell",
        ActivityKind.PersonalStore => "personal-store",
        ActivityKind.ItemMarket => "item-market",
        ActivityKind.Loot => "loot",
        ActivityKind.Upgrade => "upgrade",
        ActivityKind.Consume => "consume",
        ActivityKind.Grant => "grant",
        _ => null,
    };

    private sealed record RawWindow(string FileName, int RetentionDays, IReadOnlyList<ActivityLogEntry> Rows);
    private sealed record JewelCache(string FileName, EconomyDailySnapshot? Snapshot);
    private sealed record PvpWindow(string? FileName, int RetentionDays, (string Mode, IReadOnlyList<ActivityLogEntry> Rows)[] Groups);
}
