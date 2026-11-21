// <copyright file="BerserkerProficiencyTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Views;
using HNMO.Pathfinding;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GameMap = HNMO.GameLogic.GameMap;
using MonsterAttribute = HNMO.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;
using MonsterSpawnArea = HNMO.Persistence.BasicModel.MonsterSpawnArea;

/// <summary>
/// Verifies Berserker Proficiency (<c>470</c>) applies once through Summoner's
/// <see cref="Stats.BerserkerWizardryMultiplier"/> on wizardry skills only, matching the Season 6 server suite
/// (never a second time as a final multiplier and never on curse skills).
/// </summary>
[TestFixture]
public class BerserkerProficiencyTest
{
    private GameConfiguration _seasonSixConfig = null!;

    /// <summary>Creates the Season 6 configuration once for all tests.</summary>
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

    /// <summary>Wizardry skills receive Berserker Proficiency once via base wizardry bonus, not a second final multiplier.</summary>
    [Test]
    public async ValueTask WizardryAppliesBerserkerProficiencyOnceAsync()
    {
        var (attacker, defender) = await this.CreateSummonerAndTargetAsync().ConfigureAwait(false);
        var wizardrySkill = CreateSkill(DamageType.Wizardry, 0);

        attacker.Attributes!.AddElement(new ConstValueAttribute(0.5f, Stats.BerserkerProficiencyMultiplier), Stats.BerserkerProficiencyMultiplier);

        // With 180 energy on Summoner: statsMinWiz = 20, statsMaxWiz = 45, SkillDamageBonus = 4000 -> base 4020..4045.
        // Single BerserkerProficiency (+50% of 20..45 = +10..+22) => 4030..4067.
        // Double application also multiplied final damage by 1.5x => 6045..6100.
        var hit = await attacker.CalculateDamageAsync(defender, wizardrySkill, false).ConfigureAwait(false);

        Assert.That(hit.HealthDamage, Is.InRange(4030U, 4070U));
    }

    /// <summary>Curse skills are unaffected by Berserker Proficiency (neither via base multiplier nor final multiplier).</summary>
    [Test]
    public async ValueTask CurseDoesNotApplyBerserkerProficiencyAsync()
    {
        var (attacker, defender) = await this.CreateSummonerAndTargetAsync().ConfigureAwait(false);
        var curseSkill = CreateSkill(DamageType.Curse, 0);

        attacker.Attributes!.AddElement(new ConstValueAttribute(0.5f, Stats.BerserkerProficiencyMultiplier), Stats.BerserkerProficiencyMultiplier);

        // With 180 energy: statsMinCurse = 20, statsMaxCurse = 45, SkillDamageBonus = 4000 -> 4020..4045.
        // With the old final-damage multiplier, 0.5f boosted this to 6030..6067.
        var hit = await attacker.CalculateDamageAsync(defender, curseSkill, false).ConfigureAwait(false);

        Assert.That(hit.HealthDamage, Is.InRange(4020U, 4045U));
    }

    private async ValueTask<(Player Attacker, Monster Defender)> CreateSummonerAndTargetAsync()
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
            character.Attributes.Add(new HNMO.Persistence.BasicModel.StatAttribute(def.Attribute!, def.BaseValue));
        }

        var attacker = new TestPlayer(gameContext);
        await PlayerTestHelper.EnterWorldAsync(attacker, character).ConfigureAwait(false);
        attacker.Attributes![Stats.Level] = 100;
        attacker.Attributes[Stats.BaseAgility] = 1000;
        attacker.Attributes[Stats.BaseEnergy] = 180;
        attacker.Attributes.AddElement(new ConstValueAttribute(4000, Stats.SkillDamageBonus), Stats.SkillDamageBonus);
        attacker.Attributes[Stats.CriticalDamageChance] = 0;
        attacker.Attributes[Stats.ExcellentDamageChance] = 0;

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

        return (attacker, defender);
    }

    private static SkillEntry CreateSkill(DamageType damageType, int attackDamage)
    {
        var skillMock = new Mock<Skill>();
        skillMock.SetupAllProperties();
        skillMock.Setup(s => s.AttributeRelationships).Returns(new List<AttributeRelationship>());
        skillMock.Setup(s => s.Requirements).Returns(new List<AttributeRequirement>());
        skillMock.Setup(s => s.ConsumeRequirements).Returns(new List<AttributeRequirement>());
        skillMock.Object.Number = 230;
        skillMock.Object.DamageType = damageType;
        skillMock.Object.AttackDamage = attackDamage;
        return new SkillEntry { Skill = skillMock.Object };
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
