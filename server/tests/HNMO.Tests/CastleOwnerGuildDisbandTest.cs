// <copyright file="CastleOwnerGuildDisbandTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.CastleSiege;
using HNMO.GameLogic.PlayerActions.Guild;
using HNMO.GameServer;
using HNMO.Interfaces;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// The guild which owns the castle can't be disbanded (classic MU; Season 6/9 server suites: CastleOwnerGuildDestroyLimit).
/// </summary>
[TestFixture]
public class CastleOwnerGuildDisbandTest
{
    private const uint OwnerGuildId = 10;
    private const uint OtherGuildId = 20;

    /// <summary>The owner guild owns the castle; another guild doesn't.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task OnlyTheOwnerGuildOwnsTheCastleAsync()
    {
        var (guildServer, context) = await CreateAsync(isOccupied: true).ConfigureAwait(false);

        Assert.Multiple(async () =>
        {
            Assert.That(await GuildKickPlayerAction.OwnsTheCastleAsync(guildServer, OwnerGuildId, context).ConfigureAwait(false), Is.True);
            Assert.That(await GuildKickPlayerAction.OwnsTheCastleAsync(guildServer, OtherGuildId, context).ConfigureAwait(false), Is.False);
        });
    }

    /// <summary>Without an occupied castle or without Castle Siege no guild owns it.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NoOwnerWithoutOccupiedCastleAsync()
    {
        var (guildServer, context) = await CreateAsync(isOccupied: false).ConfigureAwait(false);

        Assert.Multiple(async () =>
        {
            Assert.That(await GuildKickPlayerAction.OwnsTheCastleAsync(guildServer, OwnerGuildId, context).ConfigureAwait(false), Is.False);
            Assert.That(await GuildKickPlayerAction.OwnsTheCastleAsync(guildServer, OwnerGuildId, null).ConfigureAwait(false), Is.False);
        });
    }

    private static async ValueTask<(IGuildServer GuildServer, CastleSiegeContext Context)> CreateAsync(bool isOccupied)
    {
        var persistenceContextProvider = new InMemoryPersistenceContextProvider();
        BasicModel.GameConfiguration gameConfiguration;
        BasicModel.CastleSiegeConfiguration castleSiegeConfiguration;
        Guid persistentOwnerGuildId;
        using (var persistenceContext = persistenceContextProvider.CreateNewContext())
        {
            gameConfiguration = persistenceContext.CreateNew<BasicModel.GameConfiguration>();
            var normalMap = persistenceContext.CreateNew<BasicModel.GameMapDefinition>();
            normalMap.Number = 0;
            normalMap.TerrainData = new byte[65_539];
            gameConfiguration.Maps.Add(normalMap);
            var siegeMap = persistenceContext.CreateNew<BasicModel.GameMapDefinition>();
            siegeMap.Number = 30;
            siegeMap.TerrainData = new byte[65_539];
            gameConfiguration.Maps.Add(siegeMap);

            castleSiegeConfiguration = persistenceContext.CreateNew<BasicModel.CastleSiegeConfiguration>();
            castleSiegeConfiguration.Enabled = true;
            castleSiegeConfiguration.CastleSiegeMapDefinition = siegeMap;
            castleSiegeConfiguration.StateSchedule.Add(new BasicModel.CastleSiegeStateScheduleEntry { State = CastleSiegeState.Ready, DayOfWeek = DayOfWeek.Monday });
            castleSiegeConfiguration.StateSchedule.Add(new BasicModel.CastleSiegeStateScheduleEntry { State = CastleSiegeState.Start, DayOfWeek = DayOfWeek.Tuesday });
            gameConfiguration.CastleSiegeConfiguration = castleSiegeConfiguration;

            var ownerGuild = persistenceContext.CreateNew<BasicModel.Guild>();
            ownerGuild.Name = "Owners";
            persistentOwnerGuildId = ownerGuild.Id;
            var siegeData = persistenceContext.CreateNew<BasicModel.CastleSiegeData>();
            siegeData.OwnerGuildId = persistentOwnerGuildId;
            siegeData.IsOccupied = isOccupied;
            await persistenceContext.SaveChangesAsync().ConfigureAwait(false);
        }

        var guildServer = new Mock<IGuildServer>();
        guildServer.Setup(server => server.GetPersistentGuildIdAsync(OwnerGuildId)).Returns(new ValueTask<Guid?>(persistentOwnerGuildId));
        guildServer.Setup(server => server.GetPersistentGuildIdAsync(OtherGuildId)).Returns(new ValueTask<Guid?>(Guid.NewGuid()));

        var mapInitializer = new MapInitializer(gameConfiguration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameServerContext = new GameServerContext(
            new BasicModel.GameServerDefinition
            {
                GameConfiguration = gameConfiguration,
                ServerConfiguration = new BasicModel.GameServerConfiguration(),
            },
            guildServer.Object,
            new Mock<IEventPublisher>().Object,
            new Mock<ILoginServer>().Object,
            new Mock<IFriendServer>().Object,
            persistenceContextProvider,
            mapInitializer,
            NullLoggerFactory.Instance,
            new PlugInManager([], NullLoggerFactory.Instance, null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameServerContext.PlugInManager;
        mapInitializer.PathFinderPool = gameServerContext.PathFinderPool;

        var context = new CastleSiegeContext(gameServerContext, castleSiegeConfiguration);
        await context.InitializeAsync(new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc)).ConfigureAwait(false);
        return (guildServer.Object, context);
    }
}
