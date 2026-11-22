// <copyright file="IncreaseBlockMasterTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verifies Rage Fighter's <see cref="SkillNumber.DefSuccessRateIncPowUp"/> adds a flat defense rate bonus
/// (independent of <see cref="Stats.DefenseRatePvm"/>) rather than scaling as a percentage of it.
/// </summary>
[TestFixture]
public class IncreaseBlockMasterTest
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

    /// <summary>Master Increase Block bonus does not scale with DefenseRatePvm; it adds a flat Formula502 value (~44.08 at level 20).</summary>
    [Test]
    public async ValueTask IncreaseBlockPowerUpAddsFlatDefenseRateRegardlessOfBaseDefenseRateAsync()
    {
        var lowAgilBonus = await this.MeasureIncreaseBlockBonusAsync(baseAgility: 100).ConfigureAwait(false);
        var highAgilBonus = await this.MeasureIncreaseBlockBonusAsync(baseAgility: 2000).ConfigureAwait(false);

        // With 80 Energy -> base 2 + 80/10 = 10. With level 20 DefSuccessRateIncPowUp -> Formula502(20) ~= 44.08.
        // Total flat IncreaseBlockBonus ~= 54.08 at both low and high DefenseRatePvm (was ~254.08 at high DefenseRatePvm when percentage).
        Assert.Multiple(() =>
        {
            Assert.That(lowAgilBonus, Is.EqualTo(54.08f).Within(0.25f));
            Assert.That(highAgilBonus, Is.EqualTo(lowAgilBonus).Within(0.01f));
        });
    }

    private async ValueTask<float> MeasureIncreaseBlockBonusAsync(int baseAgility)
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

        var fistMasterClass = this._seasonSixConfig.CharacterClasses.First(c => c.Number == 25);
        var character = PlayerTestHelper.CreateCharacter(gameContext);
        character.CharacterClass = fistMasterClass;
        character.Attributes.Clear();
        foreach (var def in fistMasterClass.StatAttributes)
        {
            character.Attributes.Add(new HNMO.Persistence.BasicModel.StatAttribute(def.Attribute!, def.BaseValue));
        }

        var baseSkill = this._seasonSixConfig.Skills.Single(s => s.Number == (short)SkillNumber.IncreaseBlock);
        var skill = this._seasonSixConfig.Skills.Single(s => s.Number == (short)SkillNumber.DefSuccessRateIncPowUp);
        character.LearnedSkills.Add(new HNMO.Persistence.BasicModel.SkillEntry { Skill = baseSkill, Level = 0 });
        character.LearnedSkills.Add(new HNMO.Persistence.BasicModel.SkillEntry { Skill = skill, Level = 20 });

        var player = new TestPlayer(gameContext);
        await PlayerTestHelper.EnterWorldAsync(player, character).ConfigureAwait(false);
        player.Attributes![Stats.BaseEnergy] = 80;
        player.Attributes[Stats.BaseAgility] = baseAgility;

        var skillEntry = player.SkillList!.GetSkill((ushort)SkillNumber.DefSuccessRateIncPowUp)!;
        player.CreateMagicEffectPowerUp(skillEntry);
        foreach (var (target, element) in skillEntry.PowerUps!)
        {
            player.Attributes.AddElement(element, target);
        }

        return player.Attributes[Stats.IncreaseBlockBonus];
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
