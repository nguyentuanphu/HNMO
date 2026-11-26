// <copyright file="ShieldBurnSkillPlugInTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Skills;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// The Season 9 skill Shield-Burn (621) cast through its skill plug-in, by a character who has learned it: it burns a
/// part of the target's shield, never its health, and doesn't cast (nor spend anything) when the Season 9 skills are off,
/// outside a Season 9 realm, in a safe zone or at a monster.
/// </summary>
/// <remarks>
/// No learning source for 618-621 exists in HNMO's data yet (no skill item, master tree row or level reward), so the
/// test learns the skill the way every such source does: <see cref="ISkillList.AddLearnedSkillAsync"/>.
/// </remarks>
[TestFixture]
public class ShieldBurnSkillPlugInTests
{
    /// <summary>A cast burns 25 % of the target's shield; the health stays, the caster pays 30 mana.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ABurnTakesShieldAndNeverHealthAsync()
    {
        var (caster, target) = await CreateAsync(skillsEnabled: true, seasonNine: true).ConfigureAwait(false);

        await new ShieldBurnSkillPlugIn().PerformSkillAsync(caster, target, 621).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(target.Attributes![Stats.CurrentShield], Is.EqualTo(750));
            Assert.That(target.Attributes[Stats.CurrentHealth], Is.EqualTo(500));
            Assert.That(caster.Attributes![Stats.CurrentMana], Is.EqualTo(70));
        });
    }

    /// <summary>With the Season 9 skills switched off it doesn't cast and spends nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SwitchedOffItDoesNotCastAsync()
    {
        var (caster, target) = await CreateAsync(skillsEnabled: false, seasonNine: true).ConfigureAwait(false);

        await new ShieldBurnSkillPlugIn().PerformSkillAsync(caster, target, 621).ConfigureAwait(false);

        AssertNothingHappened(caster, target);
    }

    /// <summary>Outside a Season 9 realm (Season 6.3, Arena) it doesn't cast.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutsideASeasonNineRealmItDoesNotCastAsync()
    {
        var (caster, target) = await CreateAsync(skillsEnabled: true, seasonNine: false).ConfigureAwait(false);

        await new ShieldBurnSkillPlugIn().PerformSkillAsync(caster, target, 621).ConfigureAwait(false);

        AssertNothingHappened(caster, target);
    }

    /// <summary>A target in a safe zone can't be burned; nothing is spent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ATargetInASafeZoneIsNotBurnedAsync()
    {
        var (caster, target) = await CreateAsync(skillsEnabled: true, seasonNine: true).ConfigureAwait(false);
        var terrain = target.CurrentMap!.Terrain;
        terrain.SafezoneMap[target.Position.X, target.Position.Y] = true;

        await new ShieldBurnSkillPlugIn().PerformSkillAsync(caster, target, 621).ConfigureAwait(false);

        AssertNothingHappened(caster, target);
    }

    /// <summary>The game's targeted skill handling finds this plug-in for 621 (and the default one for the other S9 skills).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheSkillHandlingFindsThePlugInAsync()
    {
        var (caster, _) = await CreateAsync(skillsEnabled: true, seasonNine: true).ConfigureAwait(false);
        var plugInManager = caster.GameContext.PlugInManager;

        Assert.That(plugInManager.GetStrategy<short, ITargetedSkillPlugin>(621), Is.InstanceOf<ShieldBurnSkillPlugIn>());
        Assert.That(plugInManager.GetStrategy<short, ITargetedSkillPlugin>(618), Is.Not.InstanceOf<ShieldBurnSkillPlugIn>());
    }

    private static void AssertNothingHappened(Player caster, Player target)
    {
        Assert.Multiple(() =>
        {
            Assert.That(target.Attributes![Stats.CurrentShield], Is.EqualTo(1000));
            Assert.That(target.Attributes[Stats.CurrentHealth], Is.EqualTo(500));
            Assert.That(caster.Attributes![Stats.CurrentMana], Is.EqualTo(100));
        });
    }

    private static async Task<(Player Caster, Player Target)> CreateAsync(bool skillsEnabled, bool seasonNine)
    {
        var configuration = new PlugInConfiguration
        {
            TypeId = typeof(SeasonNineSystemsPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new SeasonNineSystemsConfiguration { Enabled = true, SeasonNineSkillsEnabled = skillsEnabled, ShieldBurnSdPercent = 25 }),
        };
        var gameContext = GameContextTestHelper.CreateGameContext([configuration]);
        SeasonNineSystemsPlugIn.Get(gameContext)!.IsSeasonNineRealm = () => seasonNine;

        var shieldBurn = new HNMO.Persistence.BasicModel.Skill
        {
            Number = ShieldBurnSkillPlugIn.ShieldBurnSkillNumber,
            Name = "Shield-Burn",
            Range = 3,
            SkillType = SkillType.Other,
            Target = SkillTarget.Explicit,
        };
        shieldBurn.ConsumeRequirements.Add(new HNMO.Persistence.BasicModel.AttributeRequirement { Attribute = Stats.CurrentMana, MinimumValue = 30 });
        gameContext.Configuration.Skills.Add(shieldBurn);

        var caster = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var target = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        caster.CurrentMap!.Definition.TerrainData ??= new byte[ushort.MaxValue + 3];
        caster.Position = new Point(10, 10);
        target.Position = new Point(11, 10);
        await caster.SkillList!.AddLearnedSkillAsync(shieldBurn).ConfigureAwait(false);
        caster.Attributes![Stats.CurrentMana] = 100;
        target.Attributes![Stats.CurrentShield] = 1000;
        target.Attributes[Stats.CurrentHealth] = 500;
        return (caster, target);
    }
}
