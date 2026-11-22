// <copyright file="WebsiteServerGuideProjection.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.MuHelper;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.PlugIns.Vip;

/// <summary>Allowlisted configuration facts for the public server overview; never receives player records.</summary>
public static class WebsiteServerGuideProjection
{
    // Public response bounds prevent a large host catalog from making an unbounded page.
    private const int MaximumCatalogProducts = 100;
    private const int MaximumClasses = 32;

    // The runtime only treats groups of at least three different classes as a set party.
    private const int MinimumSetPartySize = 3;
    private const int FullPercentage = 100;

    /// <summary>Projects current progression, helper, storage and paid offers without account or state-store data.</summary>
    /// <param name="configuration">The running server's configuration.</param>
    /// <param name="helper">The active helper configuration, if any.</param>
    /// <param name="vip">The active VIP configuration, if any.</param>
    /// <param name="vault">The active multi-vault configuration, if any.</param>
    /// <param name="rates">The active rate tuning configuration, if any.</param>
    /// <param name="inventory">The active inventory extension delivery configuration, if any.</param>
    /// <returns>An allowlisted public view.</returns>
    public static object Build(GameConfiguration configuration, MuHelperConfiguration? helper, VipConfiguration? vip, MultiVaultConfiguration? vault, RateTuningConfiguration? rates, InventoryExtensionDeliveryConfiguration? inventory)
        => new
        {
            configuration.MaximumInventoryMoney,
            configuration.MaximumVaultMoney,
            classes = Classes(configuration),
            helper = helper is null ? null : new { helper.MinLevel, vipMinLevel = helper.GetMinimumLevel(true), helper.MaxLevel, payIntervalSeconds = helper.PayInterval.TotalSeconds, helper.CostPerStage },
            inventory = inventory is null ? null : new { maximumExtensions = Math.Min(inventory.MaximumExtensions, HNMO.DataModel.InventoryConstants.MaximumNumberOfExtensions) },
            vault = vault is { Enabled: true } ? new { vault.DefaultVaultCount, vault.MaxVaultsPerAccount, vault.VipExtraVaults, vault.PurchaseExtraVaultCost, purchaseCoinType = vault.PurchaseCoinType.ToString() } : null,
            partyExperience = PartyBonuses(configuration.MaximumPartySize, rates),
            vip = VipOffers(vip),
            catalog = Catalog(configuration),
        };

    private static object[] Classes(GameConfiguration configuration)
        => configuration.CharacterClasses.Where(item => item.IsActive && item.CanGetCreated).OrderBy(item => item.Number).Take(MaximumClasses)
            .Select(item => (object)new
            {
                id = item.Number,
                nameEn = item.Name.GetTranslation(CultureInfo.GetCultureInfo("en")),
                nameVi = item.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")),
                requiredLevel = item.LevelRequirementByCreation,
                shieldConfigured = item.AttributeCombinations.Any(relationship => relationship.TargetAttribute == Stats.MaximumShield)
                    || item.BaseAttributeValues.Any(attribute => attribute.Definition == Stats.MaximumShield && attribute.Value > 0),
                maximumMasterTier = MasterTier(configuration, item),
            }).ToArray();

    private static int? MasterTier(GameConfiguration configuration, CharacterClass characterClass)
    {
        var family = new HashSet<CharacterClass>();
        for (var current = characterClass; current is not null && family.Count < MaximumClasses && family.Add(current); current = current.NextGenerationClass)
        {
        }

        return configuration.Skills.Where(skill => skill.MasterDefinition is not null && skill.QualifiedCharacters.Any(family.Contains))
            .Select(skill => (int?)skill.MasterDefinition!.Rank).Max();
    }

    private static object[] PartyBonuses(byte maximumPartySize, RateTuningConfiguration? rates)
    {
        var overrides = rates is { Enabled: true } ? rates.PartyExpBonuses : [];
        return Enumerable.Range(1, maximumPartySize).Skip(1).Select(size =>
        {
            var entry = overrides.FirstOrDefault(value => value.PartySize == size)
                ?? RateTuningPlugIn.DefaultPartyExpBonuses.Where(value => value.PartySize <= size).MaxBy(value => value.PartySize);
            return (object)new { size, percent = Math.Max(0, entry?.ExpPercent ?? FullPercentage), differentClassesPercent = size >= MinimumSetPartySize ? Math.Max(0, entry?.DifferentClassesExpPercent ?? 0) : 0 };
        }).ToArray();
    }

    private static object[] VipOffers(VipConfiguration? configuration)
        => configuration is not { Enabled: true } ? [] : configuration.Levels.Take(MaximumClasses).Select(level => (object)new
        {
            level.Id,
            level.NameEn,
            level.NameVi,
            level.ExperiencePercent,
            level.ZenPercent,
            level.DailyCoins,
            dailyCoinType = level.DailyCoinType.ToString(),
            offers = level.Offers.Take(MaximumClasses).Select(offer => new { offer.Days, offer.Price, coinType = offer.CoinType.ToString() }).ToArray(),
        }).ToArray();

    private static object Catalog(GameConfiguration configuration)
    {
        var products = configuration.CashShopConfiguration?.Packages.Where(package => package.IsForSale)
            .SelectMany(package => package.Products.Where(product => product.ItemDefinition is { IsActive: true })
                .Select(product => new
                {
                    nameEn = product.ItemDefinition!.Name.GetTranslation(CultureInfo.GetCultureInfo("en")),
                    nameVi = product.ItemDefinition!.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")),
                    product.ItemLevel,
                    product.Quantity,
                    durationSeconds = product.Duration.TotalSeconds,
                    price = package.IsBundle ? package.Price : product.Price,
                    coinType = package.CoinType.ToString(),
                    bundle = package.IsBundle,
                })).Take(MaximumCatalogProducts + 1).ToArray();
        return new { products = products?.Take(MaximumCatalogProducts).ToArray(), truncated = products?.Length > MaximumCatalogProducts };
    }
}
