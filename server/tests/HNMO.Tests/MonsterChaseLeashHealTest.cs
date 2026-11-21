// <copyright file="MonsterChaseLeashHealTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Reflection;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.GameplayRules;
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
        // Full = the monster's maximum health now (other game rules, e.g. a health multiplier, may raise the definition's 1000).
        Assert.That(monster.Health, Is.EqualTo((int)monster.Attributes[Stats.MaximumHealth]).And.GreaterThanOrEqualTo(1000), "Leashed monster must heal to full.");
        Assert.That(monster.IsWalking || monster.Position.X < 192, Is.True, "Leashed monster must walk back toward spawn area.");
    }

    /// <summary>
    /// The admin's settings are what the monsters do: with the leash switched off, a monster pulled far away keeps its
    /// target and isn't healed.
    /// </summary>
    [Test]
    public async Task TheLeashSettingIsReadFromThePlugInAsync()
    {
        var (context, _, monster, ai, player) = await CreateSetupAsync(new Point(180, 130), new Point(195, 130)).ConfigureAwait(false);
        var plugIn = context.PlugInManager.GetActivePlugInsOf<IFeaturePlugIn>().OfType<MonsterAiPlugIn>().Single();
        plugIn.Configuration = new MonsterAiConfiguration { LeashToSpawnArea = false };

        monster.Position = new Point(192, 130);
        monster.Health = 300;
        ai.RegisterHit(player);
        await InvokeTickAsync(ai).ConfigureAwait(false);

        Assert.That(ai.IsTargetingPlayer(player), Is.True, "Without the leash the monster keeps its target.");
        Assert.That(monster.Health, Is.EqualTo(300), "Without the leash the monster isn't healed.");
    }

    /// <summary>The plug-in switched off: the rules before it, so a monster pulled far away isn't leashed or healed.</summary>
    [Test]
    public async Task WithThePlugInOffThereIsNoLeashAsync()
    {
        var (context, _, monster, ai, player) = await CreateSetupAsync(new Point(180, 130), new Point(195, 130)).ConfigureAwait(false);
        context.PlugInManager.DeactivatePlugIn<MonsterAiPlugIn>();

        monster.Position = new Point(192, 130);
        monster.Health = 300;
        ai.RegisterHit(player);
        await InvokeTickAsync(ai).ConfigureAwait(false);

        Assert.That(monster.Health, Is.EqualTo(300));
        Assert.That(typeof(BasicMonsterIntelligence).GetField("_isReturningHome", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ai), Is.False);
    }

    /// <summary>A leashed monster which can't walk home is put back into its spawn area, and fights again.</summary>
    [Test]
    public async Task ALeashedMonsterWithoutAWayHomeIsPutBackAsync()
    {
        var (_, _, monster, ai, player) = await CreateSetupAsync(new Point(180, 130), new Point(195, 130)).ConfigureAwait(false);
        monster.Position = new Point(192, 130);
        ai.RegisterHit(player);
        await InvokeTickAsync(ai).ConfigureAwait(false);

        // Twenty seconds later it still isn't home (it was held, or the way is blocked).
        typeof(BasicMonsterIntelligence).GetField("_returningHomeSince", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(ai, DateTime.UtcNow.AddSeconds(-21));
        await monster.MoveAsync(new Point(192, 130)).ConfigureAwait(false);
        await InvokeTickAsync(ai).ConfigureAwait(false);

        Assert.That(monster.Position.X, Is.InRange(179, 181), "Back in its spawn area.");
        Assert.That(typeof(BasicMonsterIntelligence).GetField("_isReturningHome", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ai), Is.False);
    }

    /// <summary>Verifies that MonsterAiPlugIn display name and description are localized via PlugInResources in en, vi, de, and zh-CN.</summary>
    [Test]
    public void PlugInDisplayTextsExistInAllLanguages()
    {
        var display = typeof(MonsterAiPlugIn).GetCustomAttribute<System.ComponentModel.DataAnnotations.DisplayAttribute>();
        Assert.That(display, Is.Not.Null);
        Assert.That(display!.ResourceType, Is.EqualTo(typeof(HNMO.GameLogic.Properties.PlugInResources)));

        foreach (var cultureName in new[] { "en", "vi", "de", "zh-CN" })
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
            var name = HNMO.GameLogic.Properties.PlugInResources.ResourceManager.GetString(display.Name!, culture);
            var desc = HNMO.GameLogic.Properties.PlugInResources.ResourceManager.GetString(display.Description!, culture);
            Assert.That(name, Is.Not.Null.And.Not.Empty, $"Missing MonsterAiPlugIn name for {cultureName}");
            Assert.That(desc, Is.Not.Null.And.Not.Empty, $"Missing MonsterAiPlugIn description for {cultureName}");
        }
    }
}
