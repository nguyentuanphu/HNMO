// <copyright file="ChaosCardCraftingTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.PlayerActions.Craftings;
using HNMO.GameLogic.PlayerActions.Items;

/// <summary>
/// Unit tests for <see cref="ChaosCardCrafting"/> (<c>MixType = 40</c>).
/// </summary>
[TestFixture]
public class ChaosCardCraftingTests
{
    /// <summary>
    /// Verifies that mixing a single Chaos Card produces a valid reward item from the configured pool,
    /// consumes the card, and enforces Level +0 on any Excellent reward.
    /// </summary>
    [Test]
    public async ValueTask SingleChaosCardMixConsumesCardAndProducesRewardAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var (npc, settings) = ConfigureChaosCardMaster(player);

        await new TalkNpcAction().TalkToNpcAsync(player, npc).ConfigureAwait(false);
        Assert.That(player.OpenedNpc, Is.SameAs(npc), "TalkNpcAction must leave NPC opened.");
        var cardDef = settings.RequiredItems.Single().PossibleItems.First();

        var action = new ItemCraftAction();
        for (int run = 0; run < 20; run++)
        {
            foreach (var existing in player.TemporaryStorage!.Items.ToList())
            {
                await player.TemporaryStorage.RemoveItemAsync(existing).ConfigureAwait(false);
            }

            var card = player.PersistenceContext.CreateNew<Item>();
            card.Definition = cardDef;
            card.Durability = 1;
            card.ItemSlot = 0;
            Assert.That(await player.TemporaryStorage.AddItemAsync(0, card).ConfigureAwait(false), Is.True);

            await action.MixItemsAsync(player, ChaosCardCrafting.MixNumber, 0).ConfigureAwait(false);

            var contents = player.TemporaryStorage.Items.ToList();
            Assert.That(contents, Has.Count.EqualTo(1));
            var reward = contents[0];
            Assert.That(reward, Is.Not.SameAs(card));
            if (reward.IsExcellent())
            {
                Assert.That(reward.Level, Is.Zero);
            }
        }
    }

    /// <summary>
    /// Verifies that placing two Chaos Cards in the Chaos Card Master slot is rejected as incorrect mix items.
    /// </summary>
    [Test]
    public async ValueTask MultipleChaosCardsAreRejectedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var (npc, settings) = ConfigureChaosCardMaster(player);
        var crafting = npc.Definition!.ItemCraftings.Single();
        var handler = new ChaosCardCrafting(crafting.SimpleCraftingSettings!);

        await new TalkNpcAction().TalkToNpcAsync(player, npc).ConfigureAwait(false);
        var cardDef = settings.RequiredItems.Single().PossibleItems.First();
        for (byte slot = 0; slot < 2; slot++)
        {
            var card = player.PersistenceContext.CreateNew<Item>();
            card.Definition = cardDef;
            card.Durability = 1;
            card.ItemSlot = slot;
            await player.TemporaryStorage!.AddItemAsync(slot, card).ConfigureAwait(false);
        }

        var error = handler.TryGetRequiredItems(player, out _, out _);
        Assert.That(error, Is.EqualTo(HNMO.GameLogic.Views.NPC.CraftingResult.TooManyItems));
    }

    private static (NonPlayerCharacter Npc, SimpleCraftingSettings Settings) ConfigureChaosCardMaster(Player player)
    {
        var context = player.PersistenceContext;
        var config = player.GameContext.Configuration;
        var card54 = context.CreateNew<ItemDefinition>();
        card54.Group = 14;
        card54.Number = 54;
        card54.Name = "Chaos Card";
        card54.Width = 1;
        card54.Height = 1;
        card54.Durability = 1;

        var kris = context.CreateNew<ItemDefinition>();
        kris.Group = 0;
        kris.Number = 0;
        kris.Name = "Kris";
        kris.Width = 1;
        kris.Height = 2;
        kris.Durability = 20;

        var bless = context.CreateNew<ItemDefinition>();
        bless.Group = 14;
        bless.Number = 13;
        bless.Name = "Jewel of Bless";
        bless.Width = 1;
        bless.Height = 1;
        bless.Durability = 1;

        config.Items.Add(card54);
        config.Items.Add(kris);
        config.Items.Add(bless);

        var crafting = context.CreateNew<ItemCrafting>();
        crafting.Number = ChaosCardCrafting.MixNumber;
        crafting.Name = "Chaos Card Combination";
        crafting.ItemCraftingHandlerClassName = typeof(ChaosCardCrafting).FullName!;

        var settings = context.CreateNew<SimpleCraftingSettings>();
        settings.SuccessPercent = 100;
        settings.Money = 0;

        var req = context.CreateNew<ItemCraftingRequiredItem>();
        req.MinimumAmount = 1;
        req.MaximumAmount = 1;
        req.SuccessResult = MixResult.Disappear;
        req.FailResult = MixResult.Disappear;
        req.PossibleItems.Add(card54);
        settings.RequiredItems.Add(req);

        var res1 = context.CreateNew<ItemCraftingResultItem>();
        res1.ItemDefinition = kris;
        var res2 = context.CreateNew<ItemCraftingResultItem>();
        res2.ItemDefinition = bless;
        settings.ResultItems.Add(res1);
        settings.ResultItems.Add(res2);
        crafting.SimpleCraftingSettings = settings;

        var npcDef = context.CreateNew<MonsterDefinition>();
        npcDef.Number = 375;
        npcDef.Designation = "Chaos Card Master";
        npcDef.ObjectKind = NpcObjectKind.PassiveNpc;
        npcDef.NpcWindow = NpcWindow.ChaosCardCombination;
        npcDef.ItemCraftings.Add(crafting);
        config.Monsters.Add(npcDef);

        return (new NonPlayerCharacter(null!, npcDef, null!), settings);
    }
}
