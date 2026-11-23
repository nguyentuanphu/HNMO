// <copyright file="WebsiteTrackerController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
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
    private const string CacheKey = "website-tracker-events";
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    /// <summary>Returns only eligible event types, Sydney timestamps and permitted character labels.</summary>
    /// <returns>A bounded snapshot with its source retention.</returns>
    [HttpGet]
    public async ValueTask<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var settings = await PlugInSettings.ReadActiveAsync<WebsiteConfiguration>(persistence, typeof(WebsitePlugIn), this.HttpContext.RequestAborted).ConfigureAwait(false);
        var activity = servers.Values.OfType<IGameServerContextProvider>().OrderBy(server => server.Context.Id)
            .Select(server => ActivityLogPlugIn.Get(server.Context)).FirstOrDefault(plugin => plugin is not null && plugin.Configuration?.Enabled is not false);
        if (settings is not { TrackerEnabled: true } || activity is null)
        {
            return this.Ok(new { enabled = false, events = Array.Empty<object>() });
        }

        var config = activity.Configuration ?? new ActivityLogConfiguration();
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

    /// <summary>Projects recent rows with live privacy preferences, without returning raw activity records.</summary>
    /// <param name="rows">The recent raw activity rows.</param>
    /// <param name="showNames">Whether the host permits character names.</param>
    /// <param name="privacy">The authoritative account preferences.</param>
    /// <returns>The eligible public page.</returns>
    public static object[] Project(IEnumerable<ActivityLogEntry> rows, bool showNames, WebsiteTrackerPrivacyStore privacy)
    {
        var candidates = rows.Where(row => !string.IsNullOrWhiteSpace(row.Account) && PublicKind(row.Kind) is not null)
            .Take(MaximumEvents * 2).ToArray();
        var optedOut = privacy.ReadOptOuts(candidates.SelectMany(entry => new[] { entry.Account, entry.PartnerAccount }).Where(login => !string.IsNullOrWhiteSpace(login)));
        return candidates.Where(entry => !optedOut.Contains(WebsiteTrackerPrivacyStore.Key(entry.Account))
                && (string.IsNullOrWhiteSpace(entry.PartnerAccount) || !optedOut.Contains(WebsiteTrackerPrivacyStore.Key(entry.PartnerAccount))))
            .Take(MaximumEvents)
            .Select(entry => (object)new
            {
                kind = PublicKind(entry.Kind),
                time = TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(entry.TimestampUtc, DateTimeKind.Utc)), Sydney),
                character = showNames ? CharacterName(entry.Character, entry) : null,
                partner = showNames && !string.IsNullOrWhiteSpace(entry.PartnerAccount) ? CharacterName(entry.PartnerCharacter, entry) : null,
            }).ToArray();
    }

    private static string? CharacterName(string? name, ActivityLogEntry entry)
        => name is { Length: > 0 and <= 10 }
            && name.All(character => char.IsLetterOrDigit(character) || character is '_' or '-')
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
        _ => null,
    };

    private sealed record RawWindow(string FileName, int RetentionDays, IReadOnlyList<ActivityLogEntry> Rows);
}
