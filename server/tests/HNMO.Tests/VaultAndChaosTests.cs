// <copyright file="VaultAndChaosTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using Moq;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.Views.NPC;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Unit and game-context tests for Chaos Machine Rates (14.4) and Multi-Vault (14.5).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class VaultAndChaosTests
{
    private string _file = null!;

    /// <summary>Sets up an isolated row store path per test.</summary>
    [SetUp]
    public void SetUp()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-mv-{Guid.NewGuid():N}.json");
    }

    /// <summary>Cleans up temporary state.</summary>
    [TearDown]
    public void TearDown()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        if (File.Exists(this._file))
        {
            File.Delete(this._file);
        }
    }

    /// <summary>Verifies global multiplier, recipe override, and live player crafting preview percentage.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask ChaosRatesScaleAndOverrideBothRollAndPlayerPreviewAsync()
    {
        var chaosCfg = new PlugInConfiguration
        {
            TypeId = typeof(ChaosRatesPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new ChaosRatesConfiguration
            {
                Enabled = true,
                ServerWideMultiplier = 1.5,
                RecipeOverrides =
                [
                    new ChaosRecipeOverride
                    {
                        RecipeNumber = 3,
                        RecipeName = "Mix Wings",
                        SuccessRateOverridePercent = 60,
                        RateMultiplier = 1.0,
                    },
                ],
            }),
        };

        var context = GameContextTestHelper.CreateGameContext([chaosCfg]);
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = "crafter" };

        // Base rate 40 with no override -> 40 * 1.5 = 60%
        Assert.That(ChaosRatesPlugIn.AdjustSuccessRate(player, 1, 40), Is.EqualTo(60));
        // Override 60 with server-wide multiplier 1.5 -> 90%
        Assert.That(ChaosRatesPlugIn.AdjustSuccessRate(player, 3, 25), Is.EqualTo(90));

        // Verify live ItemCraftAction crafting preview shows the overridden rate
        var jewelDef = new HNMO.Persistence.BasicModel.ItemDefinition { Number = 15, Group = 12, Name = "Jewel of Chaos", Width = 1, Height = 1, Durability = 1 };
        var required = new HNMO.Persistence.BasicModel.ItemCraftingRequiredItem
        {
            MinimumAmount = 1,
            MaximumAmount = 1,
            MinimumItemLevel = 0,
            MaximumItemLevel = 15,
            NpcPriceDivisor = 0,
            Reference = 0,
        };
        required.PossibleItems.Add(jewelDef);

        var simple = new HNMO.Persistence.BasicModel.SimpleCraftingSettings
        {
            SuccessPercent = 25,
            Money = 10000,
        };
        simple.RequiredItems.Add(required);

        var crafting = new HNMO.Persistence.BasicModel.ItemCrafting
        {
            Number = 3,
            Name = "Mix Wings",
            SimpleCraftingSettings = simple,
        };

        var npcDef = new HNMO.Persistence.BasicModel.MonsterDefinition { NpcWindow = NpcWindow.ChaosMachine };
        npcDef.ItemCraftings.Add(crafting);
        var spawnArea = new HNMO.Persistence.BasicModel.MonsterSpawnArea { MonsterDefinition = npcDef };
        player.OpenedNpc = new NonPlayerCharacter(spawnArea, npcDef, player.CurrentMap!);

        var jewel = new HNMO.Persistence.BasicModel.Item { Definition = jewelDef, Durability = 1 };
        await player.TemporaryStorage!.AddItemAsync(0, jewel).ConfigureAwait(false);

        await new ItemCraftAction().ShowCraftingPreviewAsync(player).ConfigureAwait(false);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowItemCraftingPreviewPlugIn>()!)
            .Verify(v => v.ShowCraftingPreviewAsync(crafting, 90, 10000), Times.Once);
    }

    /// <summary>Verifies multi-vault unlocks (default + VIP + coin buy), item isolation across vaults, and row persistence.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask MultiVaultSwitchesPersistsItemsAndSupportsVipAndCoinPurchaseAsync()
    {
        var vaultCfg = new PlugInConfiguration
        {
            TypeId = typeof(MultiVaultPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new MultiVaultConfiguration
            {
                Enabled = true,
                DefaultVaultCount = 2,
                MaxVaultsPerAccount = 5,
                VipExtraVaults = 1,
                PurchaseExtraVaultCost = 50,
                PurchaseCoinType = CashShopCoinType.WCoinC,
                FileName = this._file,
            }),
        };

        var context = GameContextTestHelper.CreateGameContext([vaultCfg]);
        var swordDef = new HNMO.Persistence.BasicModel.ItemDefinition { Number = 0, Group = 0, Name = "Kris", Width = 1, Height = 2, Durability = 20 };
        context.Configuration.Items.Add(swordDef);

        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account = new HNMO.Persistence.BasicModel.Account
        {
            LoginName = "vaultuser",
            WCoinC = 120,
            Vault = new HNMO.Persistence.BasicModel.ItemStorage { Money = 5000 },
        };

        var sword = player.PersistenceContext.CreateNew<Item>();
        sword.Definition = swordDef;
        sword.Level = 7;
        sword.Durability = 20;
        sword.ItemSlot = 0;
        player.Account.Vault!.Items.Add(sword);

        var plugIn = MultiVaultPlugIn.Get(context)!;
        Assert.That(plugIn.GetUnlockedVaultCount(player), Is.EqualTo(2));

        // VIP gives +1 extra vault (total 3)
        player.Attributes!.AddElement(new SimpleElement(1, AggregateType.AddRaw), Stats.IsVip);
        Assert.That(plugIn.GetUnlockedVaultCount(player), Is.EqualTo(3));

        // Switch from Vault #0 -> Vault #1 (saves sword in #0, loads empty #1)
        var (ok1, _) = await plugIn.SwitchVaultAsync(player, 1).ConfigureAwait(false);
        Assert.That(ok1, Is.True);
        Assert.That(player.Account.Vault.Items, Is.Empty);
        Assert.That(player.Account.Vault.Money, Is.EqualTo(0));

        // Switch back to Vault #0 -> restores Kris +7 and 5000 zen
        var (ok0, _) = await plugIn.SwitchVaultAsync(player, 0).ConfigureAwait(false);
        Assert.That(ok0, Is.True);
        Assert.That(player.Account.Vault.Money, Is.EqualTo(5000));
        Assert.That(player.Account.Vault.Items.Single().Level, Is.EqualTo(7));

        // Attempting locked vault #3 fails before buying
        var (lockedOk, _) = await plugIn.SwitchVaultAsync(player, 3).ConfigureAwait(false);
        Assert.That(lockedOk, Is.False);

        // Buy extra vault (costs 50 WCoinC -> 70 left, unlocked becomes 4)
        var (bought, _) = await plugIn.BuyExtraVaultAsync(player).ConfigureAwait(false);
        Assert.That(bought, Is.True);
        Assert.That(player.Account.WCoinC, Is.EqualTo(70));
        Assert.That(plugIn.GetUnlockedVaultCount(player), Is.EqualTo(4));

        var (ok3, _) = await plugIn.SwitchVaultAsync(player, 3).ConfigureAwait(false);
        Assert.That(ok3, Is.True);
    }
}
