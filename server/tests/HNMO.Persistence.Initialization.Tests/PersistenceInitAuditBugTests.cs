// <copyright file="PersistenceInitAuditBugTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;

/// <summary>
/// Regression tests for Season 6 and 0.75 data initialization bugs found during the audit.
/// </summary>
[TestFixture]
internal class PersistenceInitAuditBugTests
{
    /// <summary>Kantata ancient set must configure +10% (0.10) Excellent Damage Chance, not 1000% (10.0).</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task KantataAncientSetHasTenPercentExcellentDamageChanceAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        var kantata = gameConfiguration.ItemSetGroups
            .First(s => s.Name.ValueInNeutralLanguage.Contains("Kantata", StringComparison.OrdinalIgnoreCase));
        var excChance = kantata.Options!.PossibleOptions
            .Single(o => o.PowerUpDefinition?.TargetAttribute == Stats.ExcellentDamageChance);

        Assert.That(excChance.PowerUpDefinition!.Boost!.ConstantValue.Value, Is.EqualTo(0.10f).Within(0.0001f));
    }

    /// <summary>Blue Chocolate Box (14:34 +0) and Deep Blue Candy Box (14:34 +1) must register their normal item drop groups.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task BlueChocolateAndDeepBlueCandyBoxesRegisterNormalDropGroupsAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        var box = gameConfiguration.Items.Single(i => i.Group == 14 && i.Number == 34);
        Assert.Multiple(() =>
        {
            Assert.That(box.DropItems.Any(d => d.SourceItemLevel == 0 && d.ItemType == SpecialItemType.RandomItem), Is.True, "Blue Chocolate Box (+0) normal pool");
            Assert.That(box.DropItems.Any(d => d.SourceItemLevel == 1 && d.ItemType == SpecialItemType.RandomItem), Is.True, "Deep Blue Candy Box (+1) normal pool");
        });
    }

    /// <summary>Guardian Level 380 Boots option grants +700 Maximum Shield (not +200).</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task GuardianLv380BootsOptionGrantsSevenHundredMaximumShieldAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        var guardianBoots = gameConfiguration.ItemOptions
            .Where(o => o.Name.ValueInNeutralLanguage.Contains("Guardian", StringComparison.OrdinalIgnoreCase)
                     && o.Name.ValueInNeutralLanguage.Contains("Boots", StringComparison.OrdinalIgnoreCase))
            .SelectMany(o => o.PossibleOptions)
            .Single(o => o.PowerUpDefinition?.TargetAttribute == Stats.MaximumShield);

        Assert.That(guardianBoots.PowerUpDefinition!.Boost!.ConstantValue.Value, Is.EqualTo(700f).Within(0.01f));
    }

    /// <summary>GM test account Dark Lord must not place two pendants in the same inventory slot.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task GameMasterDarkLordAccountHasNoDuplicateInventorySlotsAsync()
    {
        var (context, _) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        var accounts = await context.GetAsync<Account>().ConfigureAwait(false);
        var gmAccount = accounts.Single(a => a.LoginName == "testgm");

        Assert.Multiple(() =>
        {
            foreach (var character in gmAccount.Characters.Where(c => c.Inventory is not null))
            {
                var slots = character.Inventory!.Items.Select(i => i.ItemSlot).ToList();
                Assert.That(slots, Is.Unique, $"Duplicate slot on {character.Name}");
            }
        });
    }

    /// <summary>Hanzo the Blacksmith (#251) must sell Mace (2:0) and have unique item slots in 0.75 and Season 6.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HanzoMerchantStoresUseMaceAndUniqueWeaponSlotsAsync()
    {
        var (ctx075, cfg075) = await TestGameConfiguration.Create075Async().ConfigureAwait(false);
        var (ctxS6, cfgS6) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = ctx075;
        using var __ = ctxS6;

        Assert.Multiple(() =>
        {
            foreach (var (label, cfg) in new[] { ("0.75", cfg075), ("S6", cfgS6) })
            {
                var hanzo = cfg.Monsters.Single(m => m.Number == 251).MerchantStore!;
                var slots = hanzo.Items.Select(i => i.ItemSlot).ToList();
                Assert.That(slots, Is.Unique, $"{label} Hanzo slots must be unique");
                Assert.That(hanzo.Items.Any(i => i.Definition!.Group == 2 && i.Definition.Number == 0), Is.True, $"{label} Hanzo must sell Mace (2:0)");
            }
        });
    }
}
