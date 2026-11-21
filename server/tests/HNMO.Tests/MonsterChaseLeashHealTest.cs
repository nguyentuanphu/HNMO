// <copyright file="MonsterChaseLeashHealTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Reflection;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.Pathfinding;
using MonsterAttribute = HNMO.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests that monsters chase attackers from beyond view range, leash back to spawn area when pulled too far, and heal to full.
/// </summary>
[TestFixture]
public class MonsterChaseLeashHealTest
{
    private static async Task InvokeTickAsync(BasicMonsterIntelligence ai)
    {
        var method = typeof(BasicMonsterIntelligence).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        var vt = (ValueTask)method.Invoke(ai, null)!;
        await vt.ConfigureAwait(false);
    }

    private static async Task<(IGameContext Context, GameMap Map, Monster Monster, BasicMonsterIntelligence Ai, Player Player)> CreateSetupAsync(Point spawnPos, Point playerPos)
    {
        var context = GameContextTestHelper.CreateGameContext();
        var map = (await context.GetMapAsync(0).ConfigureAwait(false))!;

        var monsterDef = new MonsterDefinition
        {
            Number = 0,
            Designation = "Bull Fighter",
            ViewRange = 4,
            AttackRange = 1,
            MoveRange = 5,
            AttackDelay = TimeSpan.FromMilliseconds(1500),
            MoveDelay = TimeSpan.FromMilliseconds(400),
        };
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 1000 });
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MinimumPhysBaseDmg, Value = 10 });
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumPhysBaseDmg, Value = 15 });
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.DefensePvm, Value = 5 });
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.AttackRatePvm, Value = 50 });

        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = monsterDef,
            GameMap = map.Definition,
            X1 = spawnPos.X,
            Y1 = spawnPos.Y,
            X2 = spawnPos.X,
            Y2 = spawnPos.Y,
            Quantity = 1,
        };

        var ai = new BasicMonsterIntelligence();
        var monster = new Monster(
            spawnArea,
            monsterDef,
            map,
            NullDropGenerator.Instance,
            ai,
            context.PlugInManager,
            context.PathFinderPool);
        monster.Initialize();
        monster.OnSpawn();
        await map.AddAsync(monster).ConfigureAwait(false);

        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Position = playerPos;
        await map.AddAsync(player).ConfigureAwait(false);
        await monster.AddObserverAsync(player).ConfigureAwait(false);

        return (context, map, monster, ai, player);
    }

    /// <summary>
    /// A monster hit from outside its ViewRange (6 tiles > ViewRange 4) must keep chasing the attacker instead of dropping target.
    /// </summary>
    [Test]
    public async Task MonsterChasesAttackerFromBeyondViewRangeAsync()
    {
        var (_, _, monster, ai, player) = await CreateSetupAsync(new Point(180, 130), new Point(186, 130)).ConfigureAwait(false);

        ai.RegisterHit(player);
        await InvokeTickAsync(ai).ConfigureAwait(false);

        Assert.That(ai.IsTargetingPlayer(player), Is.True, "Monster should keep targeting an attacker outside ViewRange.");
        Assert.That(monster.Position.X > 180 || monster.IsWalking, Is.True, "Monster should walk toward the attacker.");
    }

    /// <summary>
    /// A monster pulled beyond its spawn area + MoveRange + LeashExtraRange drops its target, heads back toward spawn, and heals to full.
    /// </summary>
    [Test]
    public async Task MonsterLeashesHomeAndHealsWhenPulledOutOfAreaAsync()
    {
        var (_, _, monster, ai, player) = await CreateSetupAsync(new Point(180, 130), new Point(195, 130)).ConfigureAwait(false);

        // Simulate monster having been pulled 12 tiles away from spawn (180 -> 192) and damaged to 300/1000 HP.
        monster.Position = new Point(192, 130);
        monster.Health = 300;
        ai.RegisterHit(player);

        await InvokeTickAsync(ai).ConfigureAwait(false);

        Assert.That(ai.IsTargetingPlayer(player), Is.False, "Leashed monster must drop its target.");
        Assert.That(monster.Health, Is.EqualTo(1000), "Leashed monster must heal to full.");
        Assert.That(monster.IsWalking || monster.Position.X < 192, Is.True, "Leashed monster must walk back toward spawn area.");
    }
}
