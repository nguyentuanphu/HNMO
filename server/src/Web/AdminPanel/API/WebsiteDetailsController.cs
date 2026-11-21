// <copyright file="WebsiteDetailsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameServer;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// The website's slower-changing data, read from the server: the character ranking (level, master level,
/// resets), the event rankings, the upcoming event start times and the configured drop groups. Only
/// public game facts: character names and scores, never accounts. The rankings are cached for a minute.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/details")]
public sealed class WebsiteDetailsController(
    IDictionary<int, IGameServer> servers,
    IPersistenceContextProvider persistence,
    MiniGameReadOperations miniGames,
    ILogger<WebsiteDetailsController> logger) : ControllerBase
{
    private const int RankingSize = 20;
    private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(1);
    private static readonly SemaphoreSlim Refresh = new(1, 1);
    private static (DateTime At, object Value)? _cache;

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
            return this.Ok(cached.Value);
        }

        await Refresh.WaitAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        try
        {
            if (_cache is { } fresh && DateTime.UtcNow - fresh.At < CacheTime)
            {
                return this.Ok(fresh.Value);
            }

            var value = await this.ReadAsync(running).ConfigureAwait(false);
            _cache = (DateTime.UtcNow, value);
            return this.Ok(value);
        }
        finally
        {
            Refresh.Release();
        }
    }

    private async Task<object> ReadAsync(GameServer[] running)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        return new
        {
            updatedAt = DateTimeOffset.UtcNow,
            characters = await this.TryAsync("character ranking", () => this.CharactersAsync(running[0].Context.Configuration, cancellationToken)).ConfigureAwait(false),
            eventRankings = await this.TryAsync("event rankings", () => this.EventRankingsAsync(cancellationToken)).ConfigureAwait(false),
            upcoming = await this.TryAsync("event schedule", () => Task.FromResult<object>(Upcoming(running))).ConfigureAwait(false),
            drops = await this.TryAsync("drop groups", () => Task.FromResult<object>(Drops(running[0]))).ConfigureAwait(false),
        };
    }

    // A part that can't be read comes back as null (the website says it's unavailable), never as old or made-up data.
    private async Task<object?> TryAsync(string part, Func<Task<object>> read)
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

    private async Task<object> CharactersAsync(GameConfiguration configuration, CancellationToken cancellationToken)
    {
        // With the running configuration, so that the ranking has the class names.
        using var context = persistence.CreateNewPlayerContext(configuration);
        var characters = await context.GetCharacterRankingAsync(Stats.Level.Id, Stats.MasterLevel.Id, Stats.Resets.Id, RankingSize, cancellationToken).ConfigureAwait(false);
        return characters.Select((entry, index) => new { rank = index + 1, entry.Name, className = entry.CharacterClassName, entry.Level, entry.MasterLevel, entry.Resets }).ToList();
    }

    private async Task<object> EventRankingsAsync(CancellationToken cancellationToken)
    {
        var first = await miniGames.LeaderboardAsync(null, null, cancellationToken).ConfigureAwait(false);
        var rankings = new List<object>();
        foreach (var type in first.AvailableTypes)
        {
            var board = type == first.MiniGameType ? first : await miniGames.LeaderboardAsync(type, null, cancellationToken).ConfigureAwait(false);
            rankings.Add(new
            {
                type = type.ToString(),
                entries = board.Entries.Take(10).Select(entry => new { entry.Rank, name = entry.CharacterName, className = entry.ClassName, score = entry.TotalScore, games = entry.GameCount }).ToList(),
            });
        }

        return rankings;
    }

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
