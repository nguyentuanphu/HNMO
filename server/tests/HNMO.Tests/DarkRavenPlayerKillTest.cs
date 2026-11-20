// <copyright file="DarkRavenPlayerKillTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Pet;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// The dark raven of a dark lord attacks for its owner: a player it hits may defend itself,
/// and a player it kills makes its owner an outlaw, like a kill by the owner itself.
/// </summary>
[TestFixture]
public class DarkRavenPlayerKillTest
{
    /// <summary>A player killed by a raven makes the owner of the raven an outlaw.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RavenKillMakesItsOwnerAnOutlawAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var owner = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var victim = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        owner.SelectedCharacter!.State = HeroState.Normal;
        victim.SelectedCharacter!.State = HeroState.Normal;
        victim.Attributes![Stats.CurrentHealth] = victim.Attributes[Stats.MaximumHealth];
        var raven = new AttackerSurrogate(owner, owner.Attributes!);

        await victim.ApplyBleedingDamageAsync(raven, 100_000).ConfigureAwait(false);

        Assert.That(victim.IsAlive, Is.False);
        Assert.That(owner.SelectedCharacter.State, Is.EqualTo(HeroState.PlayerKillWarning));
        Assert.That(owner.SelectedCharacter.PlayerKillCount, Is.EqualTo(1));
    }

    /// <summary>A player hit by a raven may defend itself against the owner of the raven.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RavenHitStartsTheSelfDefenseAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var owner = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var victim = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var raven = new AttackerSurrogate(owner, owner.Attributes!);

        new SelfDefensePlugIn().AttackableGotHit(victim, raven, new HitInfo(10, 0, DamageAttributes.Undefined));

        Assert.That(victim.IsSelfDefenseActive(owner), Is.True);
    }
}
