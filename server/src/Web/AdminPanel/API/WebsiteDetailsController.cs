// <copyright file="WebsiteDetailsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Moderation;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameServer;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// The website's slower-changing data, read from the server: the character ranking (level, master level,
/// resets), the event rankings, the upcoming event start times and the configured drop groups. Only
/// public game facts: permitted character names and scores, never accounts. Ranking candidates are cached briefly.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/details")]
public sealed class WebsiteDetailsController(
    IDictionary<int, IGameServer> servers,
    IPersistenceContextProvider persistence,
    MiniGameReadOperations miniGames,
    IMemoryCache cache,
    ILogger<WebsiteDetailsController> logger) : ControllerBase
{
    private const int RankingSize = 20;
    private static readonly TimeSpan CacheTime = TimeSpan.FromSeconds(15);
    private static readonly SemaphoreSlim Refresh = new(1, 1);
    private static (DateTime At, DetailsData Value)? _cache;

    /// <summary>Gets the rankings, upcoming events and drop groups.</summary>
    /// <returns>The details, or 503 while no game server runs.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        var running = servers.Values.OfType<GameServer>().OrderBy(server => server.Context.Id).ToArray();
        if (running.Length == 0)
        {
            return this.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        this.Response.Headers.CacheControl = "no-store";
        if (_cache is { } cached && DateTime.UtcNow - cached.At < CacheTime)
        {
            return this.Ok(await this.PublishAsync(cached.Value, running[0].Context.Configuration).ConfigureAwait(false));
        }

        DetailsData value;
        await Refresh.WaitAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        try
        {
            if (_cache is { } fresh && DateTime.UtcNow - fresh.At < CacheTime)
            {
                value = fresh.Value;
            }
            else
            {
                value = await this.ReadAsync(running).ConfigureAwait(false);
                _cache = (DateTime.UtcNow, value);
            }
        }
        finally
        {
            Refresh.Release();
        }

        return this.Ok(await this.PublishAsync(value, running[0].Context.Configuration).ConfigureAwait(false));
    }

    private async Task<DetailsData> ReadAsync(GameServer[] running)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        return new(
            await this.TryAsync("character ranking", () => this.CharactersAsync(running[0].Context.Configuration, cancellationToken)).ConfigureAwait(false),
            await this.TryAsync("event rankings", () => this.EventRankingsAsync(cancellationToken)).ConfigureAwait(false),
            await this.TryAsync("event schedule", () => Task.FromResult(Upcoming(running))).ConfigureAwait(false),
            await this.TryAsync("drop groups", () => Task.FromResult(Drops(running[0]))).ConfigureAwait(false));
    }

    private async Task<object> PublishAsync(DetailsData data, GameConfiguration configuration)
    {
        var rankings = await this.TryAsync("ranking visibility", () => this.ProjectRankingsAsync(data, configuration)).ConfigureAwait(false);
        return new
        {
            updatedAt = DateTimeOffset.UtcNow,
            characters = rankings?.Characters,
            eventRankings = rankings?.EventRankings,
            bannedCharacters = rankings?.BannedCharacters,
            data.Upcoming,
            data.Drops,
        };
    }

    private async Task<PublishedRankings> ProjectRankingsAsync(DetailsData data, GameConfiguration configuration)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        var settings = await PlugInSettings.ReadActiveAsync<WebsiteConfiguration>(
            persistence, typeof(WebsitePlugIn), cancellationToken).ConfigureAwait(false);
        var showNames = settings is { TrackerShowCharacterNames: true };
        var showBans = settings is { TrackerShowCharacterNames: true, PublishBannedCharacters: true };
        var now = DateTime.UtcNow;
        var bans = showBans
            ? cache.GetOrCreate("website-character-bans", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTime;
                return BanManagerService.Default.Search(null, BanTargetKind.CharacterBan, true, now, limit: RankingSize).Items;
            }) ?? []
            : [];
        var names = (data.Characters ?? []).Select(entry => entry.Name)
            .Concat((data.EventRankings ?? []).SelectMany(board => board.Entries).Select(entry => entry.CharacterName))
            .Concat(bans.Select(ban => ban.TargetDisplay));
        using var context = persistence.CreateNewPlayerContext(configuration);
        var owners = await context.ReadRankingOwnersAsync(names, cancellationToken).ConfigureAwait(false);
        var privacy = WebsiteTrackerPrivacyStore.Open();
        var blocked = privacy.ReadOptOuts(owners.Select(owner => owner.Account));
        var visible = owners.GroupBy(owner => owner.Character, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        bool CanPublish(string character, bool ranking) => visible.TryGetValue(character, out var owner)
            && IsPublicCandidate(character, owner.Account, owner.State, blocked, ranking);

        var characters = data.Characters?.Select((entry, index) => (Entry: entry, Rank: index + 1))
                .Where(row => CanPublish(row.Entry.Name, true))
                .Select(row => new { row.Rank, name = showNames ? row.Entry.Name : null, className = row.Entry.CharacterClassName, row.Entry.Level, row.Entry.MasterLevel, row.Entry.Resets }).ToArray();
        var eventRankings = data.EventRankings?.Select(board => new
            {
                board.Type,
                entries = board.Entries.Where(entry => CanPublish(entry.CharacterName, true))
                    .Select(entry => new { entry.Rank, name = showNames ? entry.CharacterName : null, entry.ClassName, score = entry.TotalScore, games = entry.GameCount }).ToArray(),
            }).ToArray();
        var bannedCharacters = showBans
            ? bans.Where(ban => ban.IsEnforcedAt(now) && CanPublish(ban.TargetDisplay, false)).Select(ban => ban.TargetDisplay).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : null;
        return new PublishedRankings(characters, eventRankings, bannedCharacters);
    }

    internal static bool IsPublicCandidate(string character, string login, AccountState state, IReadOnlySet<string> optOuts, bool ranking)
        => !string.IsNullOrWhiteSpace(login) && character is { Length: > 0 and <= 10 }
            && character.All(c => char.IsLetterOrDigit(c) || c is '_' or '-')
            && !string.Equals(character, login, StringComparison.OrdinalIgnoreCase)
            && !optOuts.Contains(WebsiteTrackerPrivacyStore.Key(login))
            && (!ranking || state is not (AccountState.Banned or AccountState.TemporarilyBanned));

    private async Task<T?> TryAsync<T>(string part, Func<Task<T>> read)
        where T : class
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning(error, "The website's {Part} couldn't be read.", part);
            return null;
        }
    }

    private async Task<IReadOnlyList<CharacterSummary>> CharactersAsync(GameConfiguration configuration, CancellationToken cancellationToken)
    {
        using var context = persistence.CreateNewPlayerContext(configuration);
        return await context.GetCharacterRankingAsync(Stats.Level.Id, Stats.MasterLevel.Id, Stats.Resets.Id, RankingSize, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<EventRanking>> EventRankingsAsync(CancellationToken cancellationToken)
    {
        var first = await miniGames.LeaderboardAsync(null, null, cancellationToken).ConfigureAwait(false);
        var rankings = new List<EventRanking>();
        foreach (var type in first.AvailableTypes.Take(6))
        {
            var board = type == first.MiniGameType ? first : await miniGames.LeaderboardAsync(type, null, cancellationToken).ConfigureAwait(false);
            rankings.Add(new(type.ToString(), board.Entries.Take(10).ToArray()));
        }

        return rankings;
    }

    private sealed record EventRanking(string Type, MiniGameLeaderboardEntry[] Entries);

    private sealed record PublishedRankings(object? Characters, object? EventRankings, string[]? BannedCharacters);

    private sealed record DetailsData(IReadOnlyList<CharacterSummary>? Characters, IReadOnlyList<EventRanking>? EventRankings, object? Upcoming, object? Drops);

    private static object Upcoming(GameServer[] running)
    {
        var now = DateTime.UtcNow;
        var english = CultureInfo.GetCultureInfo("en");
        var vietnamese = CultureInfo.GetCultureInfo("vi");
        return running.Select(server =>
        {
            var context = server.Context;
            var inEnglish = UpcomingEvents.Get(context.PlugInManager, now, context.ServerTimeZone, TimeSpan.FromHours(24), english);
            var inVietnamese = UpcomingEvents.Get(context.PlugInManager, now, context.ServerTimeZone, TimeSpan.FromHours(24), vietnamese);
            return new
            {
                serverId = context.Id,
                events = inEnglish.Select((entry, index) => new
                {
                    name = entry.Name,
                    nameVi = index < inVietnamese.Count ? inVietnamese[index].Name : entry.Name,
                    startsAt = DateTime.SpecifyKind(entry.StartsAtUtc, DateTimeKind.Utc),
                }).ToList(),
            };
        }).ToList();
    }

    private static object Drops(GameServer server)
    {
        return server.Context.Configuration.DropItemGroups
            .Where(group => group.Chance > 0)
            .OrderByDescending(group => group.Chance)
            .Take(60)
            .Select(group => new
            {
                name = group.Description.Value,
                chancePercent = Math.Round(group.Chance * 100, 4),
                minimumMonsterLevel = group.MinimumMonsterLevel,
                maximumMonsterLevel = group.MaximumMonsterLevel,
                monster = group.Monster?.Designation.Value,
                itemType = group.ItemType.ToString(),
                itemCount = group.PossibleItems.Count,
            }).ToList();
    }
}
