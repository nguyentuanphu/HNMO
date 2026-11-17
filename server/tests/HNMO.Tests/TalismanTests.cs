// <copyright file="TalismanTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Globalization;
using System.Text.Json;
using HNMO.AttributeSystem;
using HNMO.DataModel;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.ItemConsumeActions;
using HNMO.GameLogic.PlugIns.Talismans;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;
using Moq;

/// <summary>
/// Unit and integration tests for <see cref="TalismanEngine"/> and <see cref="TalismanPlugIn"/>.
/// </summary>
[TestFixture]
public class TalismanTests
{
    /// <summary>Verifies bag bonus calculation ignores equipped/shop slots, respects MinItemLevel, and caps StackCopies.</summary>
    [Test]
    public void EngineEvaluatesBagTalismansWithSlotAndStackRules()
    {
        var cfg = new TalismanConfiguration
        {
            Talismans =
            [
                new TalismanDefinitionConfig
                {
                    ItemGroup = 13,
                    ItemNumber = 81,
                    MinItemLevel = 3,
                    BonusDefense = 50,
                    BonusExpPercent = 10,
                    StackCopies = true,
                    MaxStacks = 2,
                    GrantedMagicEffectNumber = 7,
                },
            ],
        };

        var def = new HNMO.Persistence.BasicModel.ItemDefinition { Group = 13, Number = 81, Width = 1, Height = 1 };
        var items = new List<Item>
        {
            new HNMO.Persistence.BasicModel.Item { Definition = def, ItemSlot = 5, Level = 5 }, // equipped (< 12) -> ignored
            new HNMO.Persistence.BasicModel.Item { Definition = def, ItemSlot = InventoryConstants.FirstStoreItemSlotIndex, Level = 5 }, // shop -> ignored
            new HNMO.Persistence.BasicModel.Item { Definition = def, ItemSlot = 12, Level = 1 }, // below MinItemLevel -> ignored
            new HNMO.Persistence.BasicModel.Item { Definition = def, ItemSlot = 13, Level = 3 }, // count 1
            new HNMO.Persistence.BasicModel.Item { Definition = def, ItemSlot = 14, Level = 5 }, // count 2
            new HNMO.Persistence.BasicModel.Item { Definition = def, ItemSlot = 15, Level = 7 }, // capped at MaxStacks=2
        };

        var summary = TalismanEngine.EvaluateBagBonuses(items, cfg);
        Assert.That(summary.ActiveCount, Is.EqualTo(2));
        Assert.That(summary.BonusDefense, Is.EqualTo(100));
        Assert.That(summary.BonusExpPercent, Is.EqualTo(20));
        Assert.That(summary.GrantedEffectNumbers, Is.EqualTo(new short[] { 7 }));
    }

    /// <summary>Verifies <see cref="TalismanEngine.EvaluateStatConsume"/> clamps to cap and enforces Command gate.</summary>
    [Test]
    public void EngineEvaluatesStatConsumableCapAndCommandGate()
    {
        var cfg = new TalismanConfiguration
        {
            GlobalStatPointsCap = 50,
            StatConsumables =
            [
                new StatConsumableDefinitionConfig { ItemGroup = 14, ItemNumber = 91, PointsPerUse = 15, TargetBucket = "levelUpPoints" },
                new StatConsumableDefinitionConfig { ItemGroup = 14, ItemNumber = 92, PointsPerUse = 10, TargetBucket = "cmd", MaxCapOverride = 100 },
            ],
        };

        var scroll = new HNMO.Persistence.BasicModel.Item
        {
            Definition = new HNMO.Persistence.BasicModel.ItemDefinition { Group = 14, Number = 91 },
            Durability = 1,
        };

        var partial = TalismanEngine.EvaluateStatConsume(cfg, scroll, currentClaimed: 42, allowsCommand: false);
        Assert.That(partial.IsConfiguredConsumable, Is.True);
        Assert.That(partial.Allowed, Is.True);
        Assert.That(partial.PointsGranted, Is.EqualTo(8)); // 50 - 42 = 8

        var capped = TalismanEngine.EvaluateStatConsume(cfg, scroll, currentClaimed: 50, allowsCommand: false);
        Assert.That(capped.IsConfiguredConsumable, Is.True);
        Assert.That(capped.Allowed, Is.False);
        Assert.That(capped.DenialReason, Is.EqualTo("cap_reached"));

        var cmdScroll = new HNMO.Persistence.BasicModel.Item
        {
            Definition = new HNMO.Persistence.BasicModel.ItemDefinition { Group = 14, Number = 92 },
            Durability = 1,
        };
        var noCmd = TalismanEngine.EvaluateStatConsume(cfg, cmdScroll, currentClaimed: 0, allowsCommand: false);
        Assert.That(noCmd.Allowed, Is.False);
        Assert.That(noCmd.DenialReason, Is.EqualTo("no_command"));
    }

    /// <summary>Verifies carrying and removing a talisman toggles passive defense/buffs dynamically and stat consumables stop at cap.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async ValueTask CarryingTalismanGrantsAndRemovesBonusAndConsumableEnforcesCapAsync()
    {
        var cfg = new TalismanConfiguration
        {
            FileName = string.Empty, // isolate in-memory store
            GlobalStatPointsCap = 15,
            Talismans =
            [
                new TalismanDefinitionConfig
                {
                    ItemGroup = 13,
                    ItemNumber = 81,
                    NameEn = "Guardian Charm",
                    NameVi = "Bùa Hộ Mệnh",
                    BonusDefense = 75,
                    BonusDamage = 30,
                    GrantedMagicEffectNumber = 2,
                },
            ],
            StatConsumables =
            [
                new StatConsumableDefinitionConfig
                {
                    ItemGroup = 14,
                    ItemNumber = 91,
                    NameEn = "Stat Scroll",
                    NameVi = "Sách Tiềm Năng",
                    PointsPerUse = 10,
                    TargetBucket = "levelUpPoints",
                },
            ],
        };

        var pluginCfg = new PlugInConfiguration
        {
            TypeId = typeof(TalismanPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(cfg),
        };
        var ctx = GameContextTestHelper.CreateGameContext([pluginCfg]);

        // Add MagicEffect #2 (Soul Barrier / bonus effect) to context configuration
        ctx.Configuration.MagicEffects.Add(new HNMO.Persistence.BasicModel.MagicEffectDefinition
        {
            Number = 2,
            InformObservers = false,
            Duration = new HNMO.Persistence.BasicModel.PowerUpDefinitionValue { ConstantValue = { Value = 60, AggregateType = AggregateType.AddRaw } },
            PowerUpDefinitions =
            {
                new HNMO.Persistence.BasicModel.PowerUpDefinition
                {
                    TargetAttribute = Stats.DefenseRatePvm,
                    Boost = new HNMO.Persistence.BasicModel.PowerUpDefinitionValue { ConstantValue = { Value = 20, AggregateType = AggregateType.AddRaw } },
                },
            },
        });

        var player = await PlayerTestHelper.CreatePlayerAsync(ctx).ConfigureAwait(false);
        player.Culture = CultureInfo.GetCultureInfo("vi-VN");
        var defBase = player.Attributes![Stats.DefenseFinal];

        // 1. Add talisman to bag slot 12 -> bonuses + MagicEffect #2 apply immediately
        var talismanDef = new HNMO.Persistence.BasicModel.ItemDefinition { Group = 13, Number = 81, Width = 1, Height = 1, Durability = 1 };
        var talismanItem = new HNMO.Persistence.BasicModel.Item { Id = Guid.NewGuid(), Definition = talismanDef, Durability = 1 };
        Assert.That(await player.Inventory!.AddItemAsync(12, talismanItem).ConfigureAwait(false), Is.True);

        Assert.That(player.MagicEffectList.ContainsEffect(TalismanPlugIn.BuffEffectNumber), Is.True);
        Assert.That(player.MagicEffectList.ContainsEffect(2), Is.True);
        Assert.That(player.Attributes[Stats.DefenseFinal], Is.EqualTo(defBase + 75));

        // 2. Remove talisman -> passive bonuses and granted buff clear immediately
        await player.Inventory.RemoveItemAsync(talismanItem).ConfigureAwait(false);
        Assert.That(player.MagicEffectList.ContainsEffect(TalismanPlugIn.BuffEffectNumber), Is.False);
        Assert.That(player.MagicEffectList.ContainsEffect(2), Is.False);
        Assert.That(player.Attributes[Stats.DefenseFinal], Is.EqualTo(defBase));

        // 3. Consume two scrolls (+10 each, cap 15) -> 1st grants +10, 2nd grants remaining +5, 3rd refused without consuming!
        var scrollDef = new HNMO.Persistence.BasicModel.ItemDefinition { Group = 14, Number = 91, Width = 1, Height = 1, Durability = 1 };
        var s1 = new HNMO.Persistence.BasicModel.Item { Id = Guid.NewGuid(), Definition = scrollDef, Durability = 1 };
        var s2 = new HNMO.Persistence.BasicModel.Item { Id = Guid.NewGuid(), Definition = scrollDef, Durability = 1 };
        var s3 = new HNMO.Persistence.BasicModel.Item { Id = Guid.NewGuid(), Definition = scrollDef, Durability = 1 };
        Assert.That(await player.Inventory.AddItemAsync(14, s1).ConfigureAwait(false), Is.True);
        Assert.That(await player.Inventory.AddItemAsync(15, s2).ConfigureAwait(false), Is.True);
        Assert.That(await player.Inventory.AddItemAsync(16, s3).ConfigureAwait(false), Is.True);

        var initialPts = player.SelectedCharacter!.LevelUpPoints;
        var consumeAction = new ItemConsumeAction();
        await consumeAction.HandleConsumeRequestAsync(player, 14, 0, default).ConfigureAwait(false);
        Assert.That(player.SelectedCharacter.LevelUpPoints, Is.EqualTo(initialPts + 10));

        await consumeAction.HandleConsumeRequestAsync(player, 15, 0, default).ConfigureAwait(false);
        Assert.That(player.SelectedCharacter.LevelUpPoints, Is.EqualTo(initialPts + 15));

        await consumeAction.HandleConsumeRequestAsync(player, 16, 0, default).ConfigureAwait(false);
        Assert.That(player.SelectedCharacter.LevelUpPoints, Is.EqualTo(initialPts + 15));
        Assert.That(player.Inventory.GetItem(16), Is.Not.Null); // 3rd scroll kept intact!
        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Verify(v => v.ShowMessageAsync(It.Is<string>(m => m.Contains("đạt giới hạn") && m.Contains("15/15")), MessageType.BlueNormal), Times.Once);
    }
}
