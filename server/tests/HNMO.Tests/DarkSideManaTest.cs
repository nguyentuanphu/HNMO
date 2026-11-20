// <copyright file="DarkSideManaTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Skills;
using HNMO.Pathfinding;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Tests that the Rage Fighter's Dark Side pays its mana (70 in the Season 6/9 server suites).
/// </summary>
[TestFixture]
public class DarkSideManaTest
{
    private const short DarkSideNumber = 263;
    private const int ManaCost = 70;

    /// <summary>
    /// Dark Side at a target in reach costs its mana.
    /// </summary>
    [Test]
    public async ValueTask DarkSideCostsManaAsync()
    {
        var fighter = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var target = await PlayerTestHelper.CreatePlayerAsync(fighter.GameContext).ConfigureAwait(false);
        fighter.Position = new Point(100, 100);
        target.Position = new Point(102, 100);
        var skill = new BasicModel.Skill { Number = DarkSideNumber, Range = 4 };
        skill.ConsumeRequirements.Add(new BasicModel.AttributeRequirement { Attribute = Stats.CurrentMana, MinimumValue = ManaCost });
        await fighter.SkillList!.AddLearnedSkillAsync(skill).ConfigureAwait(false);
        fighter.Attributes![Stats.CurrentMana] = ManaCost * 2;

        await new RageSkillAttackAction().AttackAsync(fighter, target.Id, (ushort)DarkSideNumber).ConfigureAwait(false);

        Assert.That(fighter.Attributes[Stats.CurrentMana], Is.EqualTo(ManaCost));
    }

    /// <summary>
    /// Without enough mana Dark Side doesn't attack.
    /// </summary>
    [Test]
    public async ValueTask DarkSideWithoutManaDoesNothingAsync()
    {
        var fighter = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var target = await PlayerTestHelper.CreatePlayerAsync(fighter.GameContext).ConfigureAwait(false);
        fighter.Position = new Point(100, 100);
        target.Position = new Point(102, 100);
        var skill = new BasicModel.Skill { Number = DarkSideNumber, Range = 4 };
        skill.ConsumeRequirements.Add(new BasicModel.AttributeRequirement { Attribute = Stats.CurrentMana, MinimumValue = ManaCost });
        await fighter.SkillList!.AddLearnedSkillAsync(skill).ConfigureAwait(false);
        fighter.Attributes![Stats.CurrentMana] = ManaCost - 1;
        var targetHealth = target.Attributes![Stats.CurrentHealth];

        await new RageSkillAttackAction().AttackAsync(fighter, target.Id, (ushort)DarkSideNumber).ConfigureAwait(false);
        await Task.Delay(1500).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(fighter.Attributes[Stats.CurrentMana], Is.EqualTo(ManaCost - 1));
            Assert.That(target.Attributes[Stats.CurrentHealth], Is.EqualTo(targetHealth));
        });
    }
}
