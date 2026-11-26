// <copyright file="GameLogicCoreAuditBugTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Views;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BasicModel = HNMO.Persistence.BasicModel;
using GameMap = HNMO.GameLogic.GameMap;
using MonsterAttribute = HNMO.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;
using MonsterSpawnArea = HNMO.Persistence.BasicModel.MonsterSpawnArea;

/// <summary>
/// Regression tests for core <see cref="GameLogic"/> audit bugs.
/// </summary>
[TestFixture]
public class GameLogicCoreAuditBugTests
{
    private GameConfiguration _seasonSixConfig = null!;

    /// <summary>Loads the Season 6 configuration once per fixture.</summary>
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new Persistence.Initialization.VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory())
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        this._seasonSixConfig = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>
    /// Summoner casting a Fenrir skill (<see cref="DamageType.Fenrir"/>) must not receive
    /// Summoner wizardry/curse or Berserker skill damage bonuses.
    /// </summary>
    [Test]
    public async ValueTask SummonerFenrirSkillUsesFenrirDamageWithoutSummonerBonusesAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var mapInit = new MapInitializer(this._seasonSixConfig, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(
            this._seasonSixConfig,
            provider,
            mapInit,
            new NullLoggerFactory(),
            new PlugInManager(null, new NullLoggerFactory(), null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInit.PlugInManager = gameContext.PlugInManager;
        mapInit.PathFinderPool = gameContext.PathFinderPool;

        var summonerClass = this._seasonSixConfig.CharacterClasses.First(c => c.Number == 20);
        var character = PlayerTestHelper.CreateCharacter(gameContext);
        character.CharacterClass = summonerClass;
        character.Attributes.Clear();
        foreach (var def in summonerClass.StatAttributes)
        {
            character.Attributes.Add(new BasicModel.StatAttribute(def.Attribute!, def.BaseValue));
        }

        var attacker = new TestPlayer(gameContext);
        await PlayerTestHelper.EnterWorldAsync(attacker, character).ConfigureAwait(false);
        attacker.Attributes![Stats.Level] = 100;
        attacker.Attributes[Stats.BaseAgility] = 1000;
        attacker.Attributes[Stats.BaseEnergy] = 180;
        attacker.Attributes[Stats.CriticalDamageChance] = 0;
        attacker.Attributes[Stats.ExcellentDamageChance] = 0;
        attacker.Attributes.AddElement(new ConstValueAttribute(100, Stats.FenrirBaseDmg), Stats.FenrirBaseDmg);

        var map = new GameMap(this._seasonSixConfig.Maps.First(), TimeSpan.FromMinutes(1), 8);
        var monsterDef = new MonsterDefinition { Id = Guid.NewGuid(), Number = 2, ObjectKind = NpcObjectKind.Monster };
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = 1 });
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 100000 });
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.DefenseBase, Value = 0 });
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.DefenseRatePvm, Value = 0 });
        var spawnArea = new MonsterSpawnArea { MonsterDefinition = monsterDef, GameMap = map.Definition, X1 = 100, X2 = 100, Y1 = 100, Y2 = 100, Quantity = 1 };
        var defender = new Monster(spawnArea, monsterDef, map, NullDropGenerator.Instance, new Mock<INpcIntelligence>().Object, gameContext.PlugInManager, gameContext.PathFinderPool);
        defender.Initialize();
        defender.Attributes[Stats.CurrentHealth] = 100000;

        var skillMock = new Mock<Skill>();
        skillMock.SetupAllProperties();
        skillMock.Setup(s => s.AttributeRelationships).Returns(new List<AttributeRelationship>());
        skillMock.Setup(s => s.Requirements).Returns(new List<AttributeRequirement>());
        skillMock.Setup(s => s.ConsumeRequirements).Returns(new List<AttributeRequirement>());
        skillMock.Object.Number = 76;
        skillMock.Object.DamageType = DamageType.Fenrir;
        skillMock.Object.AttackDamage = 50;
        var fenrirSkill = new SkillEntry { Skill = skillMock.Object };

        // Fenrir damage for a Summoner must remain immune to Summoner Wizardry/Curse, Berserker, and SkillDamageBonus.
        var baseline = await attacker.CalculateDamageAsync(defender, fenrirSkill, false).ConfigureAwait(false);
        attacker.Attributes.AddElement(new ConstValueAttribute(200, Stats.SkillDamageBonus), Stats.SkillDamageBonus);
        attacker.Attributes.AddElement(new ConstValueAttribute(1000, Stats.WizardryAndCurseBaseDmgBonus), Stats.WizardryAndCurseBaseDmgBonus);
        attacker.Attributes.AddElement(new ConstValueAttribute(500, Stats.MinWizardryAndCurseDmgBonus), Stats.MinWizardryAndCurseDmgBonus);
        attacker.Attributes.AddElement(new ConstValueAttribute(500, Stats.BerserkerMinCurseDmgBonus), Stats.BerserkerMinCurseDmgBonus);
        attacker.Attributes.AddElement(new ConstValueAttribute(500, Stats.BerserkerMaxCurseDmgBonus), Stats.BerserkerMaxCurseDmgBonus);

        var boosted = await attacker.CalculateDamageAsync(defender, fenrirSkill, false).ConfigureAwait(false);
        Assert.That(boosted.HealthDamage, Is.LessThan(baseline.HealthDamage + 100U));
        Assert.That(boosted.HealthDamage, Is.InRange(350U, 550U));
    }

    /// <summary>
    /// A non-owner must not bypass drop ownership protection before
    /// <c>TimeUntilDropIsFree</c> expires even if they already
    /// hold a stackable item of the same type in their inventory.
    /// </summary>
    [Test]
    public async ValueTask NonOwnerCannotBypassDropOwnershipWhenStackingAsync()
    {
        var stackDef = new BasicModel.ItemDefinition
        {
            Group = 14,
            Number = 13,
            Name = "Jewel of Bless",
            Width = 1,
            Height = 1,
            Durability = 5,
            MaximumItemLevel = 0,
            IsBoundToCharacter = false,
        };

        var gameContext = GameContextTestHelper.CreateGameContext();
        var owner = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var stranger = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var existing = new BasicModel.Item { Definition = stackDef, Durability = 1 };
        await stranger.Inventory!.AddItemAsync(12, existing).ConfigureAwait(false);

        var dropItem = new BasicModel.Item { Definition = stackDef, Durability = 1 };
        var drop = new DroppedItem(dropItem, owner.Position, owner.CurrentMap!, owner, new object[] { owner });
        await owner.CurrentMap!.AddAsync(drop).ConfigureAwait(false);

        var (success, _) = await drop.TryPickUpByAsync(stranger).ConfigureAwait(false);

        Assert.That(success, Is.False);
        Assert.That(existing.Durability, Is.EqualTo(1));
    }

    /// <summary>
    /// Picking up two stackable dropped items concurrently must never push an existing
    /// inventory stack above its maximum stack size.
    /// </summary>
    [Test]
    public async ValueTask ConcurrentStackablePickupsDoNotExceedMaximumStackSizeAsync()
    {
        var stackDef = new BasicModel.ItemDefinition
        {
            Group = 14,
            Number = 13,
            Name = "Jewel of Bless",
            Width = 1,
            Height = 1,
            Durability = 5,
            MaximumItemLevel = 0,
            IsBoundToCharacter = false,
        };

        var stackLimit = HNMO.DataModel.ItemExtensions.StackLimit;
        try
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                var gameContext = GameContextTestHelper.CreateGameContext();
                var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);

                // The game context's item stack plug-in sets the static rule (stacks up to 255): check against the
                // definition's own cap of 5, or an overflow could never be seen.
                HNMO.DataModel.ItemExtensions.StackLimit = null;
                var existing = new BasicModel.Item { Definition = stackDef, Durability = 3 };
                await player.Inventory!.AddItemAsync(12, existing).ConfigureAwait(false);

                var drop1Item = new BasicModel.Item { Definition = stackDef, Durability = 2 };
                var drop2Item = new BasicModel.Item { Definition = stackDef, Durability = 2 };
                var map = player.CurrentMap!;
                var drop1 = new DroppedItem(drop1Item, player.Position, map, player);
                var drop2 = new DroppedItem(drop2Item, player.Position, map, player);
                await map.AddAsync(drop1).ConfigureAwait(false);
                await map.AddAsync(drop2).ConfigureAwait(false);

                using var barrier = new Barrier(2);
                var t1 = Task.Factory.StartNew(() =>
                {
                    barrier.SignalAndWait();
                    return drop1.TryPickUpByAsync(player).AsTask().GetAwaiter().GetResult();
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                var t2 = Task.Factory.StartNew(() =>
                {
                    barrier.SignalAndWait();
                    return drop2.TryPickUpByAsync(player).AsTask().GetAwaiter().GetResult();
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                await Task.WhenAll(t1, t2).ConfigureAwait(false);

                Assert.That(existing.MaximumStackSize(), Is.EqualTo(5));
                Assert.That(existing.Durability, Is.LessThanOrEqualTo(existing.MaximumStackSize()), $"Overflow on attempt {attempt}: Durability={existing.Durability}");
            }
        }
        finally
        {
            HNMO.DataModel.ItemExtensions.StackLimit = stackLimit;
        }
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
