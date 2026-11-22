// <copyright file="ChainLightningSkillPlugInTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions.Skills;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.Views.World;
using HNMO.Pathfinding;
using Moq;
using GameMap = HNMO.GameLogic.GameMap;
using MonsterAttribute = HNMO.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;
using MonsterSpawnArea = HNMO.Persistence.BasicModel.MonsterSpawnArea;

/// <summary>
/// Verifies <see cref="ChainLightningSkillPlugIn"/> target chaining matches the Season 6 and Season 9 server suites:
/// <c>[T1, T2, T2]</c> for two targets, <c>[T1, T2, T3]</c> for three, and <c>[T1, T1, T1]</c> for one.
/// </summary>
[TestFixture]
public class ChainLightningSkillPlugInTest
{
    /// <summary>With two alive monsters in range, Chain Lightning chains T1 -> T2 -> T2 (never T1 twice when T2 is in range).</summary>
    [Test]
    public async ValueTask TwoTargetsChainToSecondThenSecondAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Position = new Point(98, 100);
        var map = player.CurrentMap!;
        var def = CreateMonsterDefinition();
        var m1 = await CreateMonsterAsync(player.GameContext, map, def, new Point(100, 100)).ConfigureAwait(false);
        var m2 = await CreateMonsterAsync(player.GameContext, map, def, new Point(101, 100)).ConfigureAwait(false);

        var sentTargets = await CastAndCaptureAsync(player, m1).ConfigureAwait(false);
        Assert.That(sentTargets, Is.EqualTo(new IAttackable[] { m1, m2, m2 }));
    }

    /// <summary>With three alive monsters in range, Chain Lightning chains T1 -> T2 -> T3.</summary>
    [Test]
    public async ValueTask ThreeTargetsChainToThreeDistinctTargetsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Position = new Point(98, 100);
        var map = player.CurrentMap!;
        var def = CreateMonsterDefinition();
        var m1 = await CreateMonsterAsync(player.GameContext, map, def, new Point(100, 100)).ConfigureAwait(false);
        var m2 = await CreateMonsterAsync(player.GameContext, map, def, new Point(101, 100)).ConfigureAwait(false);
        var m3 = await CreateMonsterAsync(player.GameContext, map, def, new Point(102, 100)).ConfigureAwait(false);

        var sentTargets = await CastAndCaptureAsync(player, m1).ConfigureAwait(false);
        Assert.That(sentTargets, Is.EqualTo(new IAttackable[] { m1, m2, m3 }));
    }

    /// <summary>With only one monster in range, Chain Lightning repeats [T1, T1, T1] like the Season 6/9 suites.</summary>
    [Test]
    public async ValueTask SingleTargetRepeatsTargetThreeTimesAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Position = new Point(98, 100);
        var map = player.CurrentMap!;
        var m1 = await CreateMonsterAsync(player.GameContext, map, CreateMonsterDefinition(), new Point(100, 100)).ConfigureAwait(false);

        var sentTargets = await CastAndCaptureAsync(player, m1).ConfigureAwait(false);
        Assert.That(sentTargets, Is.EqualTo(new IAttackable[] { m1, m1, m1 }));
    }

    private static async ValueTask<List<IAttackable>> CastAndCaptureAsync(Player player, Monster primary)
    {
        List<IAttackable>? sentTargets = null;
        var view = player.ViewPlugIns.GetPlugIn<IShowChainLightningPlugIn>()!;
        Mock.Get(view)
            .Setup(v => v.ShowLightningChainAnimationAsync(player, It.IsAny<Skill>(), It.IsAny<IReadOnlyCollection<IAttackable>>()))
            .Callback<IAttacker, Skill, IReadOnlyCollection<IAttackable>>((_, _, targets) => sentTargets = targets.ToList())
            .Returns(ValueTask.CompletedTask);

        await new ChainLightningSkillPlugIn()
            .AfterTargetGotAttackedAsync(player, primary, CreateSkillEntry(), primary.Position, null)
            .ConfigureAwait(false);

        Assert.That(sentTargets, Is.Not.Null);
        return sentTargets!;
    }

    private static SkillEntry CreateSkillEntry()
    {
        var skill = new Mock<Skill>();
        skill.SetupAllProperties();
        skill.Object.Number = 215;
        skill.Object.DamageType = DamageType.Wizardry;
        return new SkillEntry { Skill = skill.Object };
    }

    private static MonsterDefinition CreateMonsterDefinition()
    {
        var def = new MonsterDefinition
        {
            Id = Guid.NewGuid(),
            Number = 2,
            ObjectKind = NpcObjectKind.Monster,
        };
        def.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = 1 });
        def.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 1000 });
        def.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.DefenseBase, Value = 0 });
        return def;
    }

    private static async ValueTask<Monster> CreateMonsterAsync(IGameContext gameContext, GameMap map, MonsterDefinition def, Point position)
    {
        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = def,
            GameMap = map.Definition,
            X1 = position.X,
            X2 = position.X,
            Y1 = position.Y,
            Y2 = position.Y,
            Quantity = 1,
        };
        var monster = new Monster(spawnArea, def, map, NullDropGenerator.Instance, new Mock<INpcIntelligence>().Object, gameContext.PlugInManager, gameContext.PathFinderPool);
        monster.Initialize();
        monster.Attributes[Stats.CurrentHealth] = 1000;
        await map.AddAsync(monster).ConfigureAwait(false);
        return monster;
    }
}
