// <copyright file="BotForbiddenHomeMapTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests.Offline;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Bots;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.Resets;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Verifies that a map in <see cref="BotConfiguration.MapsForbiddenForBots"/> stays forbidden
/// even when it is the bot's own <see cref="CharacterClass.HomeMap"/> (party-leader following,
/// illegal-map escape, and post-reset relocation).
/// </summary>
[TestFixture]
public class BotForbiddenHomeMapTests
{
    /// <summary>
    /// A party follower must leave the party instead of following the leader onto a forbidden map
    /// even when that map happens to be the follower's <see cref="CharacterClass.HomeMap"/>.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LeaderFollow_LeavesPartyWhenLeaderIsOnForbiddenHomeMapAsync()
    {
        var (gameContext, homeMapDef, allowedMapDef) = CreateTwoMapContext();
        ConfigureForbiddenMaps(gameContext, homeMapDef);

        var bot = await CreateBotOnMapAsync(gameContext, allowedMapDef, homeMapDef).ConfigureAwait(false);
        var leader = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var homeGameMap = await gameContext.GetMapAsync((ushort)homeMapDef.Number).ConfigureAwait(false);
        leader.SetCurrentMapSilently(homeGameMap!);
        leader.Position = new HNMO.Pathfinding.Point(100, 100);

        var party = gameContext.PartyManager.CreateParty();
        await party.AddAsync(leader).ConfigureAwait(false);
        await party.AddAsync(bot).ConfigureAwait(false);

        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 11, 22, 6, 0, 0, TimeSpan.Zero));
        var navigator = new BotNavigator(bot, timeProvider);
        Assert.That(navigator.HasLeaderSettled(leader), Is.False);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        var handled = await navigator.TryHandleCrossMapLeaderFollowAsync(bot.CurrentMap!, leader).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(bot.Party, Is.Null, "Bot must leave the party rather than follow onto its forbidden home map.");
            Assert.That(bot.CurrentMap?.Definition.Number, Is.EqualTo(allowedMapDef.Number));
        });
    }

    /// <summary>
    /// A bot spawned on a forbidden <see cref="CharacterClass.HomeMap"/> treats that map as illegal
    /// and warps away to a legal non-forbidden map.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task Navigator_EscapesFromForbiddenHomeMapAsync()
    {
        var (gameContext, homeMapDef, allowedMapDef) = CreateTwoMapContext();
        ConfigureForbiddenMaps(gameContext, homeMapDef);

        var allowedGate = allowedMapDef.ExitGates.First();
        gameContext.Configuration.WarpList.Add(new Persistence.BasicModel.WarpInfo
        {
            Name = "AllowedMap",
            Costs = 0,
            LevelRequirement = 1,
            Gate = allowedGate,
        });
        AddSafeMonsterSpawn(gameContext, allowedMapDef, level: 10);

        var bot = await CreateBotOnMapAsync(gameContext, homeMapDef, homeMapDef).ConfigureAwait(false);
        bot.Attributes![Stats.Level] = 50;
        bot.Attributes[Stats.MaximumHealth] = 1000;
        bot.Attributes[Stats.CurrentHealth] = 1000;
        bot.Attributes.AddElement(new HNMO.AttributeSystem.SimpleElement(200, HNMO.AttributeSystem.AggregateType.AddRaw), Stats.MinimumPhysBaseDmg);
        bot.Attributes.AddElement(new HNMO.AttributeSystem.SimpleElement(200, HNMO.AttributeSystem.AggregateType.AddRaw), Stats.MaximumPhysBaseDmg);
        bot.Attributes.AddElement(new HNMO.AttributeSystem.SimpleElement(200, HNMO.AttributeSystem.AggregateType.AddRaw), Stats.DefensePvm);

        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 11, 22, 6, 0, 0, TimeSpan.Zero));
        var navigator = new BotNavigator(bot, timeProvider);
        timeProvider.Advance(TimeSpan.FromMinutes(2));

        var escaped = await navigator.TryEscapeOrWarpAsync().ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(escaped, Is.True);
            Assert.That(bot.CurrentMap?.Definition.Number, Is.EqualTo(allowedMapDef.Number), "Bot on a forbidden home map must treat it as illegal and warp out.");
        });
    }

    /// <summary>
    /// Resetting a bot must never warp it onto its <see cref="CharacterClass.HomeMap"/> when that map
    /// is listed in <see cref="BotConfiguration.MapsForbiddenForBots"/>.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ResetHandler_DoesNotMoveOntoForbiddenHomeMapAsync()
    {
        var (gameContext, homeMapDef, allowedMapDef) = CreateTwoMapContext();
        ConfigureForbiddenMaps(gameContext, homeMapDef);

        var bot = await CreateBotOnMapAsync(gameContext, allowedMapDef, homeMapDef).ConfigureAwait(false);
        bot.Attributes![Stats.Level] = 400;

        var resetConfig = new ResetConfiguration
        {
            RequiredLevel = 400,
            LevelAfterReset = 1,
            PointsPerReset = 100,
            MoveHome = true,
        };

        var reset = await BotResetHandler.TryResetAsync(bot, resetConfig, payCosts: false).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(reset, Is.True);
            Assert.That(bot.CurrentMap?.Definition.Number, Is.Not.EqualTo(homeMapDef.Number), "Reset must not place a bot onto a forbidden home map.");
        });
    }

    private static (IGameContext Context, GameMapDefinition HomeMap, GameMapDefinition AllowedMap) CreateTwoMapContext()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var cfgCtx = gameContext.PersistenceContextProvider.CreateNewContext();

        var homeMap = gameContext.Configuration.Maps.First();
        homeMap.Number = 0;
        homeMap.Name = "Lorencia";
        homeMap.TerrainData = CreateWalkableTerrain();
        var homeGate = cfgCtx.CreateNew<ExitGate>();
        homeGate.Map = homeMap;
        homeGate.IsSpawnGate = true;
        homeGate.X1 = 100;
        homeGate.Y1 = 100;
        homeGate.X2 = 101;
        homeGate.Y2 = 101;
        homeMap.ExitGates.Add(homeGate);

        var allowedMap = cfgCtx.CreateNew<GameMapDefinition>();
        allowedMap.Number = 2;
        allowedMap.Name = "Devias";
        allowedMap.TerrainData = CreateWalkableTerrain();
        var allowedGate = cfgCtx.CreateNew<ExitGate>();
        allowedGate.Map = allowedMap;
        allowedGate.IsSpawnGate = true;
        allowedGate.X1 = 50;
        allowedGate.Y1 = 50;
        allowedGate.X2 = 51;
        allowedGate.Y2 = 51;
        allowedMap.ExitGates.Add(allowedGate);
        gameContext.Configuration.Maps.Add(allowedMap);

        return (gameContext, homeMap, allowedMap);
    }

    private static void ConfigureForbiddenMaps(IGameContext gameContext, GameMapDefinition forbidden)
    {
        var botPlugIn = gameContext.FeaturePlugIns.GetPlugIn<BotFeaturePlugIn>() ?? new BotFeaturePlugIn();
        botPlugIn.Configuration = new BotConfiguration();
        botPlugIn.Configuration.MapsForbiddenForBots.Add(forbidden);
        if (gameContext.FeaturePlugIns.GetPlugIn<BotFeaturePlugIn>() is null)
        {
            gameContext.FeaturePlugIns.AddPlugIn(botPlugIn, true);
        }
    }

    private static async ValueTask<OfflinePlayer> CreateBotOnMapAsync(IGameContext gameContext, GameMapDefinition currentMapDef, GameMapDefinition homeMapDef)
    {
        var bot = await PlayerTestHelper.CreateOfflineLevelingPlayerAsync(gameContext).ConfigureAwait(false);
        await bot.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        bot.IsAlive = true;

        var cfgCtx = gameContext.PersistenceContextProvider.CreateNewContext();
        var charClass = cfgCtx.CreateNew<CharacterClass>();
        charClass.HomeMap = homeMapDef;
        bot.SelectedCharacter!.CharacterClass = charClass;
        bot.SelectedCharacter.CurrentMap = currentMapDef;

        var gameMap = await gameContext.GetMapAsync((ushort)currentMapDef.Number).ConfigureAwait(false);
        bot.SetCurrentMapSilently(gameMap!);
        bot.Position = new HNMO.Pathfinding.Point(100, 100);
        return bot;
    }

    private static void AddSafeMonsterSpawn(IGameContext gameContext, GameMapDefinition map, int level)
    {
        var cfgCtx = gameContext.PersistenceContextProvider.CreateNewContext();
        var monster = cfgCtx.CreateNew<MonsterDefinition>();
        monster.Number = 30002;
        monster.ObjectKind = NpcObjectKind.Monster;
        var levelAttr = cfgCtx.CreateNew<MonsterAttribute>();
        levelAttr.AttributeDefinition = Stats.Level;
        levelAttr.Value = level;
        monster.Attributes.Add(levelAttr);

        var spawn = cfgCtx.CreateNew<MonsterSpawnArea>();
        spawn.MonsterDefinition = monster;
        spawn.X1 = 120;
        spawn.Y1 = 120;
        spawn.X2 = 130;
        spawn.Y2 = 130;
        spawn.Quantity = 5;
        map.MonsterSpawns.Add(spawn);
    }

    private static byte[] CreateWalkableTerrain()
    {
        // All coordinates walkable, non-safezone (TerrainData format: 3-byte header + 256*256 attribute bytes).
        var data = new byte[ushort.MaxValue + 3];
        data[0] = 0;
        data[1] = 255;
        data[2] = 255;
        return data;
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => this._utcNow;

        public void Advance(TimeSpan delta) => this._utcNow += delta;
    }
}
