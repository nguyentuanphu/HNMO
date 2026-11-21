// <copyright file="SafezoneLateDamageTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.Pathfinding;

/// <summary>
/// Tests that damage which arrives after the attack itself (poison and bleeding ticks, the delayed
/// reflected hit) does nothing to a player who stands in a safe zone by then. Classic MU: no damage
/// in the safe zone.
/// </summary>
[TestFixture]
public class SafezoneLateDamageTest
{
    private const uint Damage = 10;

    /// <summary>
    /// A poison tick does no damage to a player in the safe zone.
    /// </summary>
    [Test]
    public async ValueTask PoisonTickInSafezoneDoesNothingAsync()
    {
        var (victim, attacker) = await CreatePlayersAsync(safezone: true).ConfigureAwait(false);
        var before = GetLife(victim);

        await victim.ApplyPoisonDamageAsync(attacker, Damage).ConfigureAwait(false);

        Assert.That(GetLife(victim), Is.EqualTo(before));
    }

    /// <summary>
    /// A bleeding tick does no damage to a player in the safe zone.
    /// </summary>
    [Test]
    public async ValueTask BleedingTickInSafezoneDoesNothingAsync()
    {
        var (victim, attacker) = await CreatePlayersAsync(safezone: true).ConfigureAwait(false);
        var before = GetLife(victim);

        await victim.ApplyBleedingDamageAsync(attacker, Damage).ConfigureAwait(false);

        Assert.That(GetLife(victim), Is.EqualTo(before));
    }

    /// <summary>
    /// The reflected hit, which lands 500 ms after the attack, does no damage to a player who stepped
    /// into the safe zone meanwhile.
    /// </summary>
    [Test]
    public async ValueTask ReflectedHitInSafezoneDoesNothingAsync()
    {
        var (victim, attacker) = await CreatePlayersAsync(safezone: true).ConfigureAwait(false);
        var before = GetLife(victim);

        await victim.ReflectDamageAsync(attacker, Damage).ConfigureAwait(false);

        Assert.That(GetLife(victim), Is.EqualTo(before));
    }

    /// <summary>
    /// Outside of the safe zone, poison, bleeding and reflected damage still hit.
    /// </summary>
    [Test]
    public async ValueTask LateDamageOutsideOfSafezoneHitsAsync()
    {
        var (victim, attacker) = await CreatePlayersAsync(safezone: false).ConfigureAwait(false);
        var before = GetLife(victim);

        await victim.ApplyPoisonDamageAsync(attacker, Damage).ConfigureAwait(false);
        var afterPoison = GetLife(victim);
        await victim.ApplyBleedingDamageAsync(attacker, Damage).ConfigureAwait(false);
        var afterBleeding = GetLife(victim);
        await victim.ReflectDamageAsync(attacker, Damage).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(afterPoison, Is.LessThan(before));
            Assert.That(afterBleeding, Is.LessThan(afterPoison));
            Assert.That(GetLife(victim), Is.LessThan(afterBleeding));
        });
    }

    /// <summary>
    /// Damage a player deals to itself (e.g. the shrinking zone of an arena match, which also hits
    /// players hiding on safe zone tiles) still applies.
    /// </summary>
    [Test]
    public async ValueTask SelfInflictedDamageInSafezoneStillHitsAsync()
    {
        var (victim, _) = await CreatePlayersAsync(safezone: true).ConfigureAwait(false);
        var before = GetLife(victim);

        await victim.ApplyPoisonDamageAsync(victim, Damage).ConfigureAwait(false);

        Assert.That(GetLife(victim), Is.LessThan(before));
    }

    private static float GetLife(Player player)
    {
        return player.Attributes![Stats.CurrentHealth] + player.Attributes[Stats.CurrentShield];
    }

    private static async ValueTask<(Player Victim, Player Attacker)> CreatePlayersAsync(bool safezone)
    {
        var victim = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var attacker = await PlayerTestHelper.CreatePlayerAsync(victim.GameContext).ConfigureAwait(false);

        // Terrain attribute 0 is walkable ground, 1 is walkable safezone.
        var terrain = new byte[ushort.MaxValue + 3];
        Array.Fill(terrain, safezone ? (byte)1 : (byte)0, 3, ushort.MaxValue);
        var mapDefinition = new HNMO.Persistence.BasicModel.GameMapDefinition
        {
            Id = Guid.NewGuid(),
            Number = 0,
            TerrainData = terrain,
        };
        var map = new GameMap(mapDefinition, TimeSpan.FromMinutes(1), 8);

        victim.SetCurrentMapSilently(map);
        victim.Position = new Point(10, 10);
        attacker.SetCurrentMapSilently(map);
        attacker.Position = new Point(12, 10);

        victim.Attributes![Stats.CurrentHealth] = victim.Attributes[Stats.MaximumHealth];
        Assert.That(victim.IsAtSafezone(), Is.EqualTo(safezone), "test setup");
        return (victim, attacker);
    }
}
