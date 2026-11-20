// <copyright file="GuildMaximumMembersTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Collections.Immutable;
using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.AttributeSystem;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.Views.Guild;
using HNMO.GameServer;
using HNMO.Interfaces;
using HNMO.Persistence.BasicModel;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;

/// <summary>
/// A guild has at most as many members as its master's guild size (level / 10, plus leadership / 10 for a Dark Lord),
/// and never more than 80, like the Season 6/9 server suites (owner's choice, 2026-11-20).
/// </summary>
[TestFixture]
public class GuildMaximumMembersTest
{
    private const uint GuildId = 1;

    /// <summary>A master whose guild size is above 80 can't take an 81st member, but an 80th.</summary>
    /// <param name="memberCount">The members the guild has, the master included.</param>
    /// <param name="denied">Whether another member is denied.</param>
    /// <returns>The task.</returns>
    [TestCase(79, false)]
    [TestCase(80, true)]
    public async Task GuildSizeAbove80IsCappedAt80Async(int memberCount, bool denied)
    {
        Assert.That(await this.IsJoinDeniedAsync(guildSize: 120, memberCount).ConfigureAwait(false), Is.EqualTo(denied));
    }

    /// <summary>A master whose guild size is 60 can't take a 61st member, but a 60th.</summary>
    /// <param name="memberCount">The members the guild has, the master included.</param>
    /// <param name="denied">Whether another member is denied.</param>
    /// <returns>The task.</returns>
    [TestCase(59, false)]
    [TestCase(60, true)]
    public async Task GuildSizeBelow80StaysTheLimitAsync(int memberCount, bool denied)
    {
        Assert.That(await this.IsJoinDeniedAsync(guildSize: 60, memberCount).ConfigureAwait(false), Is.EqualTo(denied));
    }

    /// <summary>The cap of a new configuration is 80 members.</summary>
    [Test]
    public void DefaultCapIs80()
    {
        Assert.That(new GuildLimitsConfiguration().MaximumMembers, Is.EqualTo(80));
    }

    private async Task<bool> IsJoinDeniedAsync(int guildSize, int memberCount)
    {
        var guildServer = new Mock<IGuildServer>();
        guildServer
            .Setup(server => server.GetGuildListAsync(GuildId))
            .Returns(new ValueTask<IImmutableList<GuildListEntry>>(
                Enumerable.Range(0, memberCount).Select(i => new GuildListEntry { PlayerName = $"Member{i}" }).ToImmutableList()));
        var gameServerContext = CreateGameServer(guildServer.Object);

        var guildMaster = await PlayerTestHelper.CreatePlayerAsync(gameServerContext).ConfigureAwait(false);
        guildMaster.GuildStatus = new GuildMemberStatus(GuildId, GuildPosition.GuildMaster);
        guildMaster.Attributes!.AddElement(new SimpleElement(guildSize, AggregateType.AddRaw), Stats.MaximumGuildSize);
        var requester = await PlayerTestHelper.CreatePlayerAsync(gameServerContext).ConfigureAwait(false);

        var plugIn = new GuildLimitsPlugIn { Configuration = new GuildLimitsConfiguration() };
        var eventArgs = new CancelEventArgs();
        await plugIn.GuildJoinRequestingAsync(requester, guildMaster, eventArgs).ConfigureAwait(false);

        if (eventArgs.Cancel)
        {
            Mock.Get(requester.ViewPlugIns.GetPlugIn<IGuildJoinResponsePlugIn>()!).Verify(v => v!.ShowGuildJoinResponseAsync(GuildRequestAnswerResult.GuildFull), Times.Once);
        }

        return eventArgs.Cancel;
    }

    private static IGameServerContext CreateGameServer(IGuildServer guildServer)
    {
        var gameConfiguration = new GameConfiguration();
        gameConfiguration.Maps.Add(new GameMapDefinition());
        var mapInitializer = new MapInitializer(gameConfiguration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);

        var gameServer = new GameServerContext(
            new GameServerDefinition { GameConfiguration = gameConfiguration, ServerConfiguration = new HNMO.DataModel.Configuration.GameServerConfiguration() },
            guildServer,
            new Mock<IEventPublisher>().Object,
            new Mock<ILoginServer>().Object,
            new Mock<IFriendServer>().Object,
            new InMemoryPersistenceContextProvider(),
            mapInitializer,
            new NullLoggerFactory(),
            new PlugInManager(new List<HNMO.PlugIns.PlugInConfiguration>(), new NullLoggerFactory(), null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameServer.PlugInManager;
        mapInitializer.PathFinderPool = gameServer.PathFinderPool;
        return gameServer;
    }
}
