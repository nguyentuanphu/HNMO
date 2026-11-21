// <copyright file="ReflectedDamageTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.AttributeSystem;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;

/// <summary>
/// Tests that reflected damage is not reflected again (classic MU: a returned hit only lowers the attacker's life).
/// </summary>
[TestFixture]
public class ReflectedDamageTest
{
    /// <summary>
    /// Two players who both reflect damage: the reflected hit stops at the first player and does not bounce back.
    /// </summary>
    [Test]
    public async ValueTask ReflectedDamageDoesNotBounceBackAsync()
    {
        var gameContext = (await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false)).GameContext;
        var attacker = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var reflector = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        foreach (var player in new[] { attacker, reflector })
        {
            player.Attributes![Stats.CurrentHealth] = 100_000;
            player.Attributes.AddElement(new SimpleElement(0.5f, AggregateType.AddRaw), Stats.DamageReflection);
        }

        // The reflector returns 1000 damage to the attacker.
        await attacker.ReflectDamageAsync(reflector, 1000).ConfigureAwait(false);

        // A second reflection would arrive after the reflect delay of 500 ms.
        await Task.Delay(1500).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(attacker.Attributes![Stats.CurrentHealth], Is.EqualTo(99_000));
            Assert.That(reflector.Attributes![Stats.CurrentHealth], Is.EqualTo(100_000), "The reflected hit was reflected back.");
        });
    }
}
