// <copyright file="WebsiteServerGuideTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.MuHelper;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.API;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;

/// <summary>Public configuration projection, response bounds, locale metadata and offline privacy checks.</summary>
[TestFixture]
public sealed class WebsiteServerGuideTests
{
    /// <summary>Checks current settings are read and inactive classes are excluded, including cyclic promotion data.</summary>
    [Test]
    public void ReadsCurrentClassFamilyAndStorageConfiguration()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>();
        configuration.MaximumInventoryMoney = 1234;
        var baseClass = context.CreateNew<CharacterClass>();
        baseClass.Name = new("Wizard"); baseClass.CanGetCreated = true; baseClass.LevelRequirementByCreation = 90;
        baseClass.BaseAttributeValues.Add(new ConstValueAttribute(100, Stats.MaximumShield));
        var masterClass = context.CreateNew<CharacterClass>();
        baseClass.NextGenerationClass = masterClass; masterClass.NextGenerationClass = baseClass;
        var skill = context.CreateNew<Skill>(); skill.MasterDefinition = context.CreateNew<MasterSkillDefinition>();
        skill.MasterDefinition.Rank = 8; skill.QualifiedCharacters.Add(masterClass);
        configuration.CharacterClasses.Add(baseClass); configuration.Skills.Add(skill);
        var inactive = context.CreateNew<CharacterClass>(); inactive.IsActive = false; inactive.CanGetCreated = true;
        configuration.CharacterClasses.Add(inactive);
        var helper = new MuHelperConfiguration { MinLevel = 90 };
        var first = Project(configuration, helper);
        Assert.That(first["classes"]!.AsArray(), Has.Count.EqualTo(1));
        Assert.That(first["classes"]![0]!["maximumMasterTier"]!.GetValue<int>(), Is.EqualTo(8));
        Assert.That(first["classes"]![0]!["shieldConfigured"]!.GetValue<bool>(), Is.True);
        Assert.That(first["maximumInventoryMoney"]!.GetValue<int>(), Is.EqualTo(1234));
        helper.MinLevel = 120;
        Assert.That(Project(configuration, helper)["helper"]!["minLevel"]!.GetValue<int>(), Is.EqualTo(120));
    }

    /// <summary>Checks exact enabled party overrides and unchanged fallback table entries.</summary>
    [Test]
    public void PartyUsesActiveOverridesAndGameDefaults()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>(); configuration.MaximumPartySize = 3;
        var rates = new RateTuningConfiguration { Enabled = true, PartyExpBonuses = [new() { PartySize = 2, ExpPercent = 175 }] };
        var data = Project(configuration, rates: rates)["partyExperience"]!.AsArray();
        Assert.That(data, Has.Count.EqualTo(2));
        Assert.That(data[0]!["percent"]!.GetValue<int>(), Is.EqualTo(175));
        Assert.That(data[1]!["percent"]!.GetValue<int>(), Is.EqualTo(RateTuningPlugIn.DefaultPartyExpBonuses.Single(value => value.PartySize == 3).ExpPercent));
        rates.Enabled = false;
        Assert.That(Project(configuration, rates: rates)["partyExperience"]![0]!["percent"]!.GetValue<int>(), Is.EqualTo(160));
    }

    /// <summary>Checks disabled VIP does not publish default offers and changes use the current active price.</summary>
    [Test]
    public void VipPublishesOnlyEnabledCurrentOffersWithoutPlayerState()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>(); var vip = new VipConfiguration();
        Assert.That(Project(configuration, vip: vip)["vip"]!.AsArray(), Is.Empty);
        vip.Enabled = true; vip.Levels[0].Offers = [new(7, 123, CashShopCoinType.WCoinC)];
        var data = Project(configuration, vip: vip);
        Assert.That(data["vip"]![0]!["offers"]![0]!["price"]!.GetValue<int>(), Is.EqualTo(123));
        foreach (var secret in new[] { "memberships", "payments", "wallet", "password", "account", "fileName" })
        {
            Assert.That(data.ToJsonString(), Does.Not.Contain(secret).IgnoreCase);
        }
    }

    /// <summary>Checks sale/active filters, bundle prices and the bounded catalog without identifiers.</summary>
    [Test]
    public void CatalogUsesBundlePriceAndBoundsCurrentProducts()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>(); configuration.CashShopConfiguration = context.CreateNew<CashShopConfiguration>();
        var package = context.CreateNew<CashShopPackage>(); package.IsForSale = true; package.IsBundle = true; package.Price = 77;
        for (var index = 0; index < 102; index++)
        {
            var product = context.CreateNew<CashShopProduct>(); product.Price = 999; product.ItemDefinition = context.CreateNew<ItemDefinition>();
            product.ItemDefinition.Name = new("Potion"); package.Products.Add(product);
        }

        configuration.CashShopConfiguration.Packages.Add(package);
        var data = Project(configuration)["catalog"]!;
        Assert.That(data["products"]!.AsArray(), Has.Count.EqualTo(100));
        Assert.That(data["truncated"]!.GetValue<bool>(), Is.True);
        Assert.That(data["products"]![0]!["price"]!.GetValue<int>(), Is.EqualTo(77));
        Assert.That(data["products"]![0]!.ToJsonString(), Does.Not.Contain("Sequence"));
        package.IsForSale = false;
        Assert.That(Project(configuration)["catalog"]!["products"]!.AsArray(), Is.Empty);
    }

    /// <summary>Checks old plug-in JSON gains empty public fields and validation/captions exist in every host language.</summary>
    /// <param name="language">The host language.</param>
    [TestCase("en")]
    [TestCase("vi")]
    [TestCase("de")]
    [TestCase("zh-CN")]
    public void ExistingSettingsAndFourLanguageMetadataArePreserved(string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            var config = JsonSerializer.Deserialize<WebsiteConfiguration>("{\"AdminPath\":\"host-secret\",\"Theme\":2}")!;
            Assert.That(config.AdminPath, Is.EqualTo("host-secret")); Assert.That(config.ServerClientRulesEn, Is.Empty);
            Assert.That(WebsiteServerPageLabels.ClientRulesEn, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.ClientRulesEn)));
            Assert.That(WebsiteServerPageLabels.NotForSaleVi, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.NotForSaleVi)));
            config.ServerClientRulesEn = new string('x', 4001);
            var results = new List<ValidationResult>();
            Assert.That(Validator.TryValidateObject(config, new ValidationContext(config), results, true), Is.False);
            Assert.That(results.Any(result => result.ErrorMessage?.Contains("4000", StringComparison.Ordinal) == true), Is.True);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>Checks unavailable servers clear caching and do not query any account/configuration repository.</summary>
    [Test]
    public async Task OfflineServerReturnsUnavailableWithoutAccessingPersistenceAsync()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var persistence = new Mock<IPersistenceContextProvider>(MockBehavior.Strict);
        var controller = new WebsiteServerGuideController(new Dictionary<int, HNMO.Interfaces.IGameServer>(), persistence.Object, cache)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() },
        };
        var result = await controller.GetAsync().ConfigureAwait(false);
        Assert.That(result, Is.TypeOf<StatusCodeResult>());
        Assert.That(((StatusCodeResult)result).StatusCode, Is.EqualTo(503));
        Assert.That(controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
        persistence.VerifyNoOtherCalls();
    }

    private static JsonNode Project(GameConfiguration configuration, MuHelperConfiguration? helper = null, VipConfiguration? vip = null, RateTuningConfiguration? rates = null)
        => JsonSerializer.SerializeToNode(WebsiteServerGuideProjection.Build(configuration, helper, vip, null, rates, null), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
