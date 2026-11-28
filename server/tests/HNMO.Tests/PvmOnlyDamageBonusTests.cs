// <copyright file="PvmOnlyDamageBonusTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.GameLogic.Views;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GameMap = HNMO.GameLogic.GameMap;
using MonsterAttribute = HNMO.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;
using MonsterSpawnArea = HNMO.Persistence.BasicModel.MonsterSpawnArea;

/// <summary>
/// The PvM-only damage bonuses (<see cref="Stats.BaseDamageBonusPvm"/>, <see cref="Stats.ExcellentDamageBonusPvm"/>, ...)
/// count on a hit on a monster and not on a hit on a player; the Season 9 Muun options marked non-PvP use them.
/// </summary>
/// <remarks>
/// The hits are excellent hits (chance 1, no critical hit), so the damage is the maximum base damage times 1.2 plus the
/// excellent damage bonus, the same every time: the difference of two hits is the bonus.
/// </remarks>
[TestFixture]
public class PvmOnlyDamageBonusTests
{
    private GameConfiguration _configuration = null!;

    /// <summary>Creates the Season 6 data once.</summary>
    /// <returns>A task.</returns>
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new Persistence.Initialization.VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        this._configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>The PvM excellent damage bonus adds to a hit on a monster, never to a hit on a player.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheExcellentBonusCountsOnMonstersOnlyAsync()
    {
        var (attacker, monster, player) = await this.CreateAsync().ConfigureAwait(false);
        var monsterBefore = await HitAsync(attacker, monster).ConfigureAwait(false);
        var playerBefore = await HitAsync(attacker, player).ConfigureAwait(false);

        attacker.Attributes!.AddElement(new ConstantElement(70), Stats.ExcellentDamageBonusPvm);

        Assert.That(await HitAsync(attacker, monster).ConfigureAwait(false), Is.EqualTo(monsterBefore + 70));
        Assert.That(await HitAsync(attacker, player).ConfigureAwait(false), Is.EqualTo(playerBefore));
    }

    /// <summary>The PvM base damage bonus raises a hit on a monster (1.2 times, as the maximum base damage of an excellent hit), not on a player.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheBaseDamageBonusCountsOnMonstersOnlyAsync()
    {
        var (attacker, monster, player) = await this.CreateAsync().ConfigureAwait(false);
        var monsterBefore = await HitAsync(attacker, monster).ConfigureAwait(false);
        var playerBefore = await HitAsync(attacker, player).ConfigureAwait(false);

        attacker.Attributes!.AddElement(new ConstantElement(100), Stats.BaseDamageBonusPvm);

        Assert.That(await HitAsync(attacker, monster).ConfigureAwait(false), Is.EqualTo(monsterBefore + 120));
        Assert.That(await HitAsync(attacker, player).ConfigureAwait(false), Is.EqualTo(playerBefore));
    }

    /// <summary>The Muun options marked non-PvP land on the PvM stats; the others on the general ones.</summary>
    [Test]
    public void TheNonPvpMuunOptionsUseThePvmStats()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MuunCompanion.StatOf(8)?.Stat, Is.EqualTo(Stats.BaseDamageBonusPvm));
            Assert.That(MuunCompanion.StatOf(10)?.Stat, Is.EqualTo(Stats.ExcellentDamageBonusPvm));
            Assert.That(MuunCompanion.StatOf(11)?.Stat, Is.EqualTo(Stats.CriticalDamageChancePvm));
            Assert.That(MuunCompanion.StatOf(12)?.Stat, Is.EqualTo(Stats.SkillDamageBonusPvm));
            Assert.That(MuunCompanion.StatOf(14)?.Stat, Is.EqualTo(Stats.BaseMaxDamageBonusPvm));
            Assert.That(MuunCompanion.StatOf(1)?.Stat, Is.EqualTo(Stats.BaseDamageBonus));
            Assert.That(MuunCompanion.StatOf(3)?.Stat, Is.EqualTo(Stats.ExcellentDamageBonus));
        });
    }

    // The total damage of a hit which lands (a missed hit is tried again).
    private static async Task<long> HitAsync(Player attacker, IAttackable defender)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var hit = await attacker.CalculateDamageAsync(defender, null, false).ConfigureAwait(false);
            if (hit.HealthDamage + hit.ShieldDamage > 0)
            {
                return hit.HealthDamage + hit.ShieldDamage;
            }
        }

        Assert.Fail("No hit landed in 200 tries.");
        return 0;
    }

    private async Task<(Player Attacker, Monster Monster, Player Player)> CreateAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var mapInit = new MapInitializer(this._configuration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(this._configuration, provider, mapInit, new NullLoggerFactory(), new PlugInManager(null, new NullLoggerFactory(), null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInit.PlugInManager = gameContext.PlugInManager;
        mapInit.PathFinderPool = gameContext.PathFinderPool;

        async Task<Player> PlayerAsync()
        {
            var character = PlayerTestHelper.CreateCharacter(gameContext);
            var player = new TestPlayer(gameContext);
            await PlayerTestHelper.EnterWorldAsync(player, character).ConfigureAwait(false);
            player.Attributes![Stats.Level] = 100;
            player.Attributes[Stats.CriticalDamageChance] = 0;
            player.Attributes[Stats.DefenseIgnoreChance] = 0;
            player.Attributes.AddElement(new ConstantElement(1), Stats.ExcellentDamageChance);
            player.Attributes.AddElement(new ConstantElement(1000), Stats.MinimumPhysBaseDmg);
            player.Attributes.AddElement(new ConstantElement(1000), Stats.MaximumPhysBaseDmg);
            player.Attributes[Stats.AttackRatePvm] = 100_000;
            player.Attributes[Stats.AttackRatePvp] = 100_000;
            player.Attributes[Stats.DefenseRatePvp] = 0;
            return player;
        }

        var attacker = await PlayerAsync().ConfigureAwait(false);
        var defender = await PlayerAsync().ConfigureAwait(false);
        defender.Attributes![Stats.MaximumShield] = 0;

        var map = new GameMap(this._configuration.Maps.First(), TimeSpan.FromMinutes(1), 8);
        var monsterDefinition = new MonsterDefinition { Id = Guid.NewGuid(), Number = 2, ObjectKind = NpcObjectKind.Monster };
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = 1 });
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 1_000_000 });
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.DefenseBase, Value = 0 });
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.DefenseRatePvm, Value = 0 });
        var spawnArea = new MonsterSpawnArea { MonsterDefinition = monsterDefinition, GameMap = map.Definition, X1 = 100, X2 = 100, Y1 = 100, Y2 = 100, Quantity = 1 };
        var monster = new Monster(spawnArea, monsterDefinition, map, NullDropGenerator.Instance, new Mock<INpcIntelligence>().Object, gameContext.PlugInManager, gameContext.PathFinderPool);
        monster.Initialize();
        monster.Attributes[Stats.CurrentHealth] = 1_000_000;
        return (attacker, monster, defender);
    }

    private sealed class TestPlayer : Player
    {
        public TestPlayer(IGameContext gameContext)
            : base(gameContext)
        {
        }

        protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer() => new MockViewPlugInContainer();
    }
}
