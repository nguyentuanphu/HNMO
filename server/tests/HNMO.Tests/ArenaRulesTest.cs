// <copyright file="ArenaRulesTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns.Arena;

/// <summary>
/// The HNMO Arena's rules apply only in the Arena program; its default arena characters cover every class.
/// </summary>
[TestFixture]
public sealed class ArenaRulesTest
{
    /// <summary>Outside the Arena (this test program is Season 6.3) nothing is refused and every HNMO message is handled.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NothingIsRefusedOutsideTheArenaAsync()
    {
        Assert.That(ArenaRules.Active, Is.False);
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        foreach (var action in Enum.GetValues<ArenaRules.Action>())
        {
            Assert.That(await ArenaRules.RefuseAsync(player, action).ConfigureAwait(false), Is.False, action.ToString());
        }

        Assert.That(ArenaRules.AllowsHnmoMessage(0x38), Is.True, "the market outside the Arena");
        Assert.That(ArenaRules.AllowsHnmoMessage(ArenaRules.ArenaTravelSubCode), Is.True);
    }

    /// <summary>Every class family (0-6) has one default arena character with a weapon and armour, level 300.</summary>
    [Test]
    public void EveryClassHasADefaultArenaCharacter()
    {
        var templates = ArenaConfiguration.DefaultTemplates();
        Assert.That(templates.Select(t => t.ClassFamily), Is.EquivalentTo(Enumerable.Range(0, 7)));
        foreach (var template in templates)
        {
            Assert.That(template.Level, Is.EqualTo(300), template.Name);
            Assert.That(template.Items.Any(i => i.Slot <= 1), Is.True, $"{template.Name} has a weapon");
            Assert.That(template.Items.Any(i => i.Slot == 3), Is.True, $"{template.Name} has armour");
            Assert.That(template.Items.Select(i => i.Slot).Distinct().Count(), Is.EqualTo(template.Items.Count), $"{template.Name}: one item per slot");
        }

        var plugIn = new ArenaPlugIn();
        Assert.That(plugIn.TemplateOf(5)?.Name, Is.EqualTo("Dark Knight"), "class 5 (Blade Knight) plays the Dark Knight's");
        Assert.That(plugIn.TemplateOf(24)?.Name, Is.EqualTo("Rage Fighter"));
    }
}
