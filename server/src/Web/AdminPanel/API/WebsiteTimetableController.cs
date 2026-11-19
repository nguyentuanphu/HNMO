// <copyright file="WebsiteTimetableController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.MiniGames;
using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.GameServer;
using HNMO.Interfaces;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Public read-only website API exposing the event, invasion, and world boss timetable
/// (<c>api/website/timetable</c>). Returns <c>off: true</c> while <see cref="TimetablePlugIn"/> is disabled.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/timetable")]
public sealed class WebsiteTimetableController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    IDictionary<int, IGameServer> servers,
    ILogger<WebsiteTimetableController> logger) : ControllerBase
{
    /// <summary>Gets the public event, invasion, and world boss timetable JSON.</summary>
    /// <returns>The timetable snapshot JSON.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var (_, isActive, savedConfig) = await PlugInSettings.ReadStoredAsync<TimetableConfiguration>(
            persistence,
            typeof(TimetablePlugIn),
            cancellationToken).ConfigureAwait(false);

        if (!isActive)
        {
            var offSnapshot = TimetableEngine.BuildSnapshot(
                new TimetableConfiguration(),
                enabled: false,
                DateTime.UtcNow,
                [],
                []);
            return this.Content(TimetableStateWriter.Write(offSnapshot), "application/json");
        }

        var config = savedConfig ?? new TimetableConfiguration();
        var utcNow = DateTime.UtcNow;

        // If a live game server is running in this process, use its full live context (including active events & fights).
        var liveServer = servers.Values.OfType<GameServer>().OrderBy(s => s.Context.Id).FirstOrDefault();
        if (liveServer is not null)
        {
            var liveSources = TimetableSourceCollector.CollectFromContext(liveServer.Context, config, utcNow);
            var liveWinners = TimetableStore.Open(config.FileName).ReadFindGmWinners();
            return this.Content(
                TimetableStateWriter.Write(TimetableEngine.BuildSnapshot(config, enabled: true, utcNow, liveSources, liveWinners)),
                "application/json");
        }

        GameConfiguration? gameConfiguration = null;
        try
        {
            gameConfiguration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not load GameConfiguration while building the website timetable.");
        }

        var sources = new List<TimetableSourceItem>();
        if (config.ShowMiniGames)
        {
            await CollectMiniGamesAsync(persistence, gameConfiguration, utcNow, sources, cancellationToken).ConfigureAwait(false);
        }

        if (config.ShowInvasions)
        {
            await CollectInvasionsAsync(persistence, gameConfiguration, sources, cancellationToken).ConfigureAwait(false);
        }

        if (config.ShowWorldBosses)
        {
            var bossConfig = await PlugInSettings.ReadActiveAsync<WorldBossesConfiguration>(
                persistence,
                typeof(WorldBossesPlugIn),
                () => new WorldBossesConfiguration(),
                cancellationToken).ConfigureAwait(false);
            if (bossConfig is not null)
            {
                var tz = TimetableEngine.ResolveTimeZone(bossConfig.TimeZone);
                foreach (var boss in bossConfig.Bosses.Where(b => b.Enabled))
                {
                    sources.Add(TimetableSourceCollector.FromWorldBoss(boss, tz, utcNow, gameConfiguration));
                }
            }
        }

        var winners = TimetableStore.Open(config.FileName).ReadFindGmWinners();
        var snapshot = TimetableEngine.BuildSnapshot(config, enabled: true, utcNow, sources, winners);
        return this.Content(TimetableStateWriter.Write(snapshot), "application/json");
    }

    private static async Task CollectMiniGamesAsync(
        IPersistenceContextProvider persistence,
        GameConfiguration? gameConfiguration,
        DateTime utcNow,
        List<TimetableSourceItem> target,
        CancellationToken cancellationToken)
    {
        var specs = new (Type PlugInType, MiniGameType Key, Func<PeriodicTaskConfiguration> Defaults)[]
        {
            (typeof(BloodCastleStartPlugIn), MiniGameType.BloodCastle, () => BloodCastleStartConfiguration.Default),
            (typeof(DevilSquareStartPlugIn), MiniGameType.DevilSquare, () => DevilSquareStartConfiguration.Default),
            (typeof(ChaosCastleStartPlugIn), MiniGameType.ChaosCastle, () => ChaosCastleStartConfiguration.Default),
            (typeof(IllusionTempleStartPlugIn), MiniGameType.IllusionTemple, () => IllusionTempleStartConfiguration.Default),
            (typeof(KanturuStartPlugIn), MiniGameType.Kanturu, () => KanturuStartConfiguration.Default),
        };

        foreach (var spec in specs)
        {
            var taskConfig = await PlugInSettings.ReadActiveAsync(
                persistence,
                spec.PlugInType,
                spec.Defaults,
                cancellationToken).ConfigureAwait(false);
            if (taskConfig is not null)
            {
                target.Add(TimetableSourceCollector.FromMiniGame(
                    spec.Key,
                    taskConfig,
                    gameConfiguration,
                    taskConfig.GetNextStartUtc(utcNow, TimeZoneInfo.Utc)));
            }
        }
    }

    private static async Task CollectInvasionsAsync(
        IPersistenceContextProvider persistence,
        GameConfiguration? gameConfiguration,
        List<TimetableSourceItem> target,
        CancellationToken cancellationToken)
    {
        var standardInvasions = new (Type PlugInType, Func<PeriodicInvasionConfiguration> Defaults)[]
        {
            (typeof(GoldenInvasionPlugIn), () => InvasionConfigurationDefaults.Golden),
            (typeof(RedDragonInvasionPlugIn), () => InvasionConfigurationDefaults.RedDragon),
            (typeof(WhiteWizardInvasionPlugIn), () => InvasionConfigurationDefaults.WhiteWizard),
        };

        foreach (var spec in standardInvasions)
        {
            var config = await PlugInSettings.ReadActiveAsync(
                persistence,
                spec.PlugInType,
                spec.Defaults,
                cancellationToken).ConfigureAwait(false);
            if (config is not null)
            {
                target.Add(TimetableSourceCollector.FromInvasion(spec.PlugInType, config, gameConfiguration));
            }
        }

        // Extra invasions are IDisabledByDefault: require stored row IsActive == true.
        await CollectDisabledByDefaultInvasionAsync<PeriodicInvasionConfiguration>(
            persistence,
            typeof(RabbitInvasionPlugIn),
            () => InvasionConfigurationDefaults.Rabbits,
            gameConfiguration,
            target,
            cancellationToken).ConfigureAwait(false);

        await CollectDisabledByDefaultInvasionAsync<PeriodicInvasionConfiguration>(
            persistence,
            typeof(SnowmanInvasionPlugIn),
            () => InvasionConfigurationDefaults.Snowmen,
            gameConfiguration,
            target,
            cancellationToken).ConfigureAwait(false);

        await CollectDisabledByDefaultInvasionAsync<FindTheGmConfiguration>(
            persistence,
            typeof(FindTheGmInvasionPlugIn),
            () => new FindTheGmConfiguration(),
            gameConfiguration,
            target,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task CollectDisabledByDefaultInvasionAsync<TConfig>(
        IPersistenceContextProvider persistence,
        Type plugInType,
        Func<TConfig> defaults,
        GameConfiguration? gameConfiguration,
        List<TimetableSourceItem> target,
        CancellationToken cancellationToken)
        where TConfig : PeriodicInvasionConfiguration
    {
        var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<TConfig>(persistence, plugInType, cancellationToken).ConfigureAwait(false);
        if (!isActive)
        {
            return;
        }

        target.Add(TimetableSourceCollector.FromInvasion(plugInType, saved ?? defaults(), gameConfiguration));
    }
}
