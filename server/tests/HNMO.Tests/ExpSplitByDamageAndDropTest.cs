// <copyright file="ExpSplitByDamageAndDropTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Pathfinding;
using Moq;
using MonsterAttribute = HNMO.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests EXP split by damage share, top-damage-dealer drop ownership, party EXP preservation, and off-state fallback.
/// </summary>
[TestFixture]
public class ExpSplitByDamageAndDropTest
{
    private static async Task<(IGameContext Context, GameMap Map, Monster Monster, Player P1, Player P2, TaskCompletionSource<DroppedItem> DropTcs)> CreateSetupAsync(MonsterAiConfiguration? config)
    {
        var context = GameContextTestHelper.CreateGameContext(maximumLevel: 400);
        context.Configuration.ExperienceRate = 1.0f;
        if (config is not null)
        {
            var plugIn = context.PlugInManager.GetActivePlugInsOf<HNMO.GameLogic.IFeaturePlugIn>().OfType<MonsterAiPlugIn>().Single();
            plugIn.Configuration = config;
        }
        else
        {
            context.PlugInManager.DeactivatePlugIn<MonsterAiPlugIn>();
        }

        var map = (await context.GetMapAsync(0).ConfigureAwait(false))!;
        map.Definition.ExpMultiplier = 1;
        var monsterDef = new MonsterDefinition
        {
            Number = 1,
            Designation = "Hound",
            ViewRange = 5,
            AttackRange = 1,
            MoveRange = 3,
            AttackDelay = TimeSpan.FromMilliseconds(1500),
            MoveDelay = TimeSpan.FromMilliseconds(400),
        };
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = 50 });
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 1000 });
        monsterDef.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.DefensePvm, Value = 0 });

        var dropItemDef = new ItemDefinition { Group = 14, Number = 13, Name = "Jewel of Bless", Width = 1, Height = 1, Durability = 1 };
        var dropGenerator = new Mock<IDropGenerator>();
        dropGenerator
            .Setup(g => g.GenerateItemDropsAsync(It.IsAny<MonsterDefinition>(), It.IsAny<int>(), It.IsAny<Player>()))
            .ReturnsAsync((new[] { new HNMO.DataModel.Entities.Item { Definition = dropItemDef, Durability = 1 } }, (uint?)null));

        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = monsterDef,
            GameMap = map.Definition,
            X1 = 180,
            Y1 = 130,
            X2 = 180,
            Y2 = 130,
            Quantity = 1,
            MaximumHealthOverride = 1000,
        };

        var monster = new Monster(
            spawnArea,
            monsterDef,
            map,
            dropGenerator.Object,
            new NullMonsterIntelligence(),
            context.PlugInManager,
            context.PathFinderPool);
        monster.Initialize();
        monster.OnSpawn();
        await map.AddAsync(monster).ConfigureAwait(false);

        var dropTcs = new TaskCompletionSource<DroppedItem>(TaskCreationOptions.RunContinuationsAsynchronously);
        var p1 = await CreateReadyPlayerAsync(context, map, new Point(181, 130)).ConfigureAwait(false);
        var p2 = await CreateReadyPlayerAsync(context, map, new Point(181, 131)).ConfigureAwait(false);
        await p1.AddObserverAsync(p2).ConfigureAwait(false);
        await p2.AddObserverAsync(p1).ConfigureAwait(false);

        map.ObjectAdded += args =>
        {
            if (args.Object is DroppedItem di)
            {
                dropTcs.TrySetResult(di);
            }

            return ValueTask.CompletedTask;
        };

        return (context, map, monster, p1, p2, dropTcs);
    }

    private static async Task<Player> CreateReadyPlayerAsync(IGameContext context, GameMap map, Point pos)
    {
        var p = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        p.Position = pos;
        p.Attributes![Stats.Level] = 10;
        p.Attributes[Stats.MasterLevel] = 0;
        p.Attributes[Stats.PointsPerLevelUp] = 5;
        p.Attributes.AddElement(new SimpleElement(1.0f, AggregateType.AddRaw), Stats.ExperienceRate);
        p.Attributes.AddElement(new SimpleElement(10.0f, AggregateType.AddRaw), Stats.TotalLevel);
        p.SelectedCharacter!.Experience = 0;
        await map.AddAsync(p).ConfigureAwait(false);
        return p;
    }

    private static async Task HitMonsterAsync(Monster monster, Player attacker, uint damage)
    {
        var method = typeof(AttackableNpcBase).GetMethod("HitAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var hit = new HitInfo(damage, 0, DamageAttributes.Undefined);
        var vt = (ValueTask)method.Invoke(monster, [hit, attacker, null, null])!;
        await vt.ConfigureAwait(false);
    }

    /// <summary>
    /// Two solo players dealing 750 and 250 damage split EXP 75% / 25%, and the top damage dealer owns the drop even when the other lands the killing blow.
    /// </summary>
    [Test]
    public async Task TwoPlayersSplitExpByDamageAndTopDamagerGetsDropAsync()
    {
        var (_, _, monster, p1, p2, dropTcs) = await CreateSetupAsync(new MonsterAiConfiguration { SplitExpByDamage = true, DropToTopDamageDealer = true }).ConfigureAwait(false);

        await HitMonsterAsync(monster, p1, 750).ConfigureAwait(false);
        await HitMonsterAsync(monster, p2, 250).ConfigureAwait(false);
        var dropped = await dropTcs.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        var gained1 = p1.SelectedCharacter!.Experience;
        var gained2 = p2.SelectedCharacter!.Experience;

        Assert.That(gained1, Is.GreaterThan(0));
        Assert.That(gained2, Is.GreaterThan(0));
        Assert.That((double)gained1 / gained2, Is.EqualTo(3.0).Within(0.1), "750 vs 250 damage should split EXP 3:1.");
        Assert.That(dropped.IsPlayerAnOwner(p1), Is.True, "Top damage dealer (P1) must own the drop.");
        Assert.That(dropped.IsPlayerAnOwner(p2), Is.False, "Last hitter (P2) must not steal P1's drop.");
    }

    /// <summary>
    /// Party members still share EXP via existing party rules when fighting alongside another solo player.
    /// </summary>
    [Test]
    public async Task PartyPreservesPartyExpRulesWhenSplittingByDamageAsync()
    {
        var (context, map, monster, p1, p2, _) = await CreateSetupAsync(new MonsterAiConfiguration { SplitExpByDamage = true, DropToTopDamageDealer = true }).ConfigureAwait(false);
        var p3 = await CreateReadyPlayerAsync(context, map, new Point(182, 130)).ConfigureAwait(false);
        await p1.AddObserverAsync(p3).ConfigureAwait(false);
        await p3.AddObserverAsync(p1).ConfigureAwait(false);

        var partyManager = new PartyManager(5, new Microsoft.Extensions.Logging.Abstractions.NullLogger<Party>());
        var party = partyManager.CreateParty();
        await party.AddAsync(p1).ConfigureAwait(false);
        await party.AddAsync(p3).ConfigureAwait(false);

        // Party (P1) deals 500, Solo (P2) deals 500.
        await HitMonsterAsync(monster, p1, 500).ConfigureAwait(false);
        await HitMonsterAsync(monster, p2, 500).ConfigureAwait(false);

        Assert.That(p1.SelectedCharacter!.Experience, Is.GreaterThan(0));
        Assert.That(p3.SelectedCharacter!.Experience, Is.GreaterThan(0), "Party member P3 must receive party share.");
        Assert.That(p2.SelectedCharacter!.Experience, Is.GreaterThan(0));
    }

    /// <summary>
    /// When MonsterAiPlugIn is switched off (or SplitExpByDamage/DropToTopDamageDealer are false), EXP and drop go to the last hitter as before.
    /// </summary>
    [Test]
    public async Task WhenSwitchedOffLastHitterGetsAllExpAndDropAsync()
    {
        var (_, _, monster, p1, p2, dropTcs) = await CreateSetupAsync(config: null).ConfigureAwait(false);

        await HitMonsterAsync(monster, p1, 750).ConfigureAwait(false);
        await HitMonsterAsync(monster, p2, 250).ConfigureAwait(false);
        var dropped = await dropTcs.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.That(p1.SelectedCharacter!.Experience, Is.EqualTo(0), "P1 should get 0 EXP when off.");
        Assert.That(p2.SelectedCharacter!.Experience, Is.GreaterThan(0), "Last hitter P2 gets 100% EXP when off.");
        Assert.That(dropped.IsPlayerAnOwner(p2), Is.True, "Last hitter P2 owns drop when off.");
    }

    /// <summary>
    /// Verifies non-default admin toggles (SplitExpByDamage = false with DropToTopDamageDealer = true) and that full heal clears earlier pull damage.
    /// </summary>
    [Test]
    public async Task NonDefaultAdminSettingsAndFullHealResetAreRespectedAsync()
    {
        var (_, _, monster, p1, p2, dropTcs) = await CreateSetupAsync(new MonsterAiConfiguration
        {
            SplitExpByDamage = false,
            DropToTopDamageDealer = true,
        }).ConfigureAwait(false);

        // P1 hits for 900, then monster heals to full (e.g. leashed home), resetting damage table.
        await HitMonsterAsync(monster, p1, 900).ConfigureAwait(false);
        monster.RestoreHealth(int.MaxValue);

        // Now P1 hits for 700, P2 finishes with 300. Since SplitExpByDamage = false, P2 gets 100% EXP, while P1 (700 > 300) owns the drop.
        await HitMonsterAsync(monster, p1, 700).ConfigureAwait(false);
        await HitMonsterAsync(monster, p2, 300).ConfigureAwait(false);
        var dropped = await dropTcs.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.That(p1.SelectedCharacter!.Experience, Is.EqualTo(0), "With SplitExpByDamage = false, P1 gets 0 EXP.");
        Assert.That(p2.SelectedCharacter!.Experience, Is.GreaterThan(0), "With SplitExpByDamage = false, last hitter P2 gets 100% EXP.");
        Assert.That(dropped.IsPlayerAnOwner(p1), Is.True, "With DropToTopDamageDealer = true, P1 owns the drop.");
    }
}
