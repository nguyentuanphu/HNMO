// <copyright file="AllianceMinimumMembersTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Collections.Immutable;
using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.GameLogic;
using HNMO.GameLogic.Gens;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.Views.Guild;
using HNMO.GameServer;
using HNMO.Interfaces;
using HNMO.Persistence.BasicModel;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;

/// <summary>
/// Each guild of an alliance needs at least 10 members (Season 6/9 server suites: AllianceMinGuildMember 10).
/// </summary>
[TestFixture]
public class AllianceMinimumMembersTest
{
    private const uint RequesterGuildId = 1;
    private const uint TargetGuildId = 2;

    /// <summary>The alliance request depends on the members of both guilds.</summary>
    /// <param name="requesterMembers">The members of the requesting guild.</param>
    /// <param name="targetMembers">The members of the other guild.</param>
    /// <param name="denied">Whether the request is denied.</param>
    /// <returns>The task.</returns>
    [TestCase(10, 10, false)]
    [TestCase(9, 10, true)]
    [TestCase(10, 9, true)]
    public async Task AllianceNeedsTenMembersInEachGuildAsync(int requesterMembers, int targetMembers, bool denied)
    {
        var (requester, target) = await CreateGuildMastersAsync(requesterMembers, targetMembers).ConfigureAwait(false);
        var eventArgs = new CancelEventArgs();

        await new GuildLimitsPlugIn { Configuration = new GuildLimitsConfiguration() }
            .GuildRelationshipChangingAsync(requester, target, GuildRelationshipType.Alliance, GuildRelationshipRequestType.Join, eventArgs)
            .ConfigureAwait(false);

        Assert.That(eventArgs.Cancel, Is.EqualTo(denied));
    }

    /// <summary>Hostility and leaving aren't limited; 0 turns the rule off.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task OtherRequestsAndZeroAreNotLimitedAsync()
    {
        var (requester, target) = await CreateGuildMastersAsync(2, 2).ConfigureAwait(false);
        var plugIn = new GuildLimitsPlugIn { Configuration = new GuildLimitsConfiguration() };
        var hostility = new CancelEventArgs();
        var leave = new CancelEventArgs();
        var off = new CancelEventArgs();

        await plugIn.GuildRelationshipChangingAsync(requester, target, GuildRelationshipType.Hostility, GuildRelationshipRequestType.Join, hostility).ConfigureAwait(false);
        await plugIn.GuildRelationshipChangingAsync(requester, target, GuildRelationshipType.Alliance, GuildRelationshipRequestType.Leave, leave).ConfigureAwait(false);
        await new GuildLimitsPlugIn { Configuration = new GuildLimitsConfiguration { MinimumAllianceGuildMembers = 0 } }
            .GuildRelationshipChangingAsync(requester, target, GuildRelationshipType.Alliance, GuildRelationshipRequestType.Join, off).ConfigureAwait(false);

        Assert.That(new[] { hostility.Cancel, leave.Cancel, off.Cancel }, Is.All.False);
    }

    private static async Task<(HNMO.GameLogic.Player Requester, HNMO.GameLogic.Player Target)> CreateGuildMastersAsync(int requesterMembers, int targetMembers)
    {
        var guildServer = new Mock<IGuildServer>();
        guildServer.Setup(server => server.GetGuildListAsync(RequesterGuildId)).Returns(new ValueTask<IImmutableList<GuildListEntry>>(Members(requesterMembers)));
        guildServer.Setup(server => server.GetGuildListAsync(TargetGuildId)).Returns(new ValueTask<IImmutableList<GuildListEntry>>(Members(targetMembers)));
        var gameServer = CreateGameServer(guildServer.Object);

        var requester = await PlayerTestHelper.CreatePlayerAsync(gameServer).ConfigureAwait(false);
        requester.GuildStatus = new GuildMemberStatus(RequesterGuildId, GuildPosition.GuildMaster);
        var target = await PlayerTestHelper.CreatePlayerAsync(gameServer).ConfigureAwait(false);
        target.GuildStatus = new GuildMemberStatus(TargetGuildId, GuildPosition.GuildMaster);
        return (requester, target);
    }

    private static IImmutableList<GuildListEntry> Members(int count)
        => Enumerable.Range(0, count).Select(i => new GuildListEntry { PlayerName = $"Member{i}" }).ToImmutableList();

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
        var gens = gameServer.FeaturePlugIns.GetPlugIn<GensFeaturePlugIn>();
        if (gens is not null)
        {
            // These tests are about guilds, not gens: the guild rules of the gens (same gens required) are off.
            gens.Configuration = new GensConfiguration { GuildRequiresGens = false, AllianceRequiresSameGens = false };
        }
        return gameServer;
    }
}
