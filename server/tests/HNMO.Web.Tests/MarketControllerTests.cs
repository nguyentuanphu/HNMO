// <copyright file="MarketControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.ItemMarket;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Integration tests for <see cref="WebsiteMarketController"/>: public feed, disabled gate,
/// atomic coin purchase, insufficient balance rollback, and seller cancel.
/// </summary>
[TestFixture]
public class MarketControllerTests
{
    private string _tempDir = string.Empty;

    /// <summary>Creates an isolated state directory per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-mkt-web-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Cleans up temp files.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>Public catalog shows active listings and atomic website buy transfers coins & escrow delivery once.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task PublicGetAndAtomicCoinBuyFlow()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var gameConfig = new GameConfiguration();
        var configSource = new Mock<IDataSource<GameConfiguration>>();
        configSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(gameConfig);
        var serverProvider = new Mock<IServerProvider>();
        serverProvider.Setup(s => s.Servers).Returns([]);

        var marketCfg = new MarketConfiguration
        {
            FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
            FeePercent = 10,
            AllowWebsite = true,
            AllowWCoinC = true,
        };
        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(MarketPlugIn).GUID;
            row.IsActive = true;
            row.SetConfiguration(marketCfg, null);
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        using (var acctCtx = persistence.CreateNewPlayerContext(gameConfig))
        {
            var seller = acctCtx.CreateNew<Account>();
            seller.LoginName = "seller";
            seller.PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123");

            var buyer = acctCtx.CreateNew<Account>();
            buyer.LoginName = "buyer";
            buyer.PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123");
            buyer.WCoinC = 300;
            await acctCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var store = MarketStore.Open(marketCfg.FileName);
        var engine = new MarketEngine(marketCfg, store, enabled: true);
        engine.CreateListing("seller", "SellerChar", new MarketItemSnapshot { Group = 12, Number = 15, NameEn = "Jewel of Chaos", NameVi = "Ngọc Hỗn Nguyên" }, MarketCurrency.WCoinC, 500, DateTime.UtcNow, out var expensive);
        engine.CreateListing("seller", "SellerChar", new MarketItemSnapshot { Group = 14, Number = 13, NameEn = "Jewel of Bless", NameVi = "Ngọc Ước Nguyện" }, MarketCurrency.WCoinC, 200, DateTime.UtcNow, out var affordable);

        var controller = new WebsiteMarketController(persistence, configSource.Object, serverProvider.Object, NullLogger<WebsiteMarketController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        // Public feed shows 2 listings
        var pubResult = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(pubResult.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("listings").GetArrayLength(), Is.EqualTo(2));
        }

        // Without the market's one-off access fee paid, the website refuses and nothing is bought.
        var noAccess = await controller.BuyAsync(new MarketActionRequest("buyer", "pass123", affordable!.Id)).ConfigureAwait(false);
        Assert.That(noAccess, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)noAccess).StatusCode, Is.EqualTo(403));
        Assert.That(JsonSerializer.Serialize(((ObjectResult)noAccess).Value), Does.Contain("\"access\""));
        Assert.That(store.Read(d => d.Listings.Any(l => l.Id == affordable.Id)), Is.True);
        engine.GrantAccess("buyer");
        Assert.That(engine.HasAccess("BUYER"), Is.True, "login names are matched without case");

        // Buying expensive listing (500 > 300 WCoinC) fails & restores listing
        var insufResult = (ContentResult)await controller.BuyAsync(new MarketActionRequest("buyer", "pass123", expensive!.Id)).ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(insufResult.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("result").GetString(), Is.EqualTo("InsufficientFunds"));
            Assert.That(store.Read(d => d.Listings.Any(l => l.Id == expensive.Id)), Is.True);
        }

        // Buying affordable listing (200 WCoinC) succeeds once and queues delivery + seller grant
        var okResult = (ContentResult)await controller.BuyAsync(new MarketActionRequest("buyer", "pass123", affordable!.Id)).ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(okResult.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("result").GetString(), Is.EqualTo("Bought"));
            Assert.That(doc.RootElement.GetProperty("coins").GetProperty("wCoinC").GetInt32(), Is.EqualTo(100));
            Assert.That(store.Read(d => d.PendingDeliveries.Count(x => x.RecipientLogin == "buyer")), Is.EqualTo(1));
        }

        // Second buy of same listing refuses ("sold")
        var secondResult = (ContentResult)await controller.BuyAsync(new MarketActionRequest("buyer", "pass123", affordable.Id)).ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(secondResult.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("result").GetString(), Is.EqualTo("sold"));
        }

        // While the buyer plays (the login server can't hold the account), the website refuses: a purchase in the
        // game could spend the same coins at the same moment (audit 1.5). Nothing is bought, the listing stays.
        engine.CreateListing("seller", "SellerChar", new MarketItemSnapshot { Group = 14, Number = 14, NameEn = "Jewel of Soul", NameVi = "Ngọc Tâm Linh" }, MarketCurrency.WCoinC, 50, DateTime.UtcNow, out var whilePlaying);
        var playing = new Mock<ILoginServer>();
        playing.Setup(l => l.TryReserveAccountAsync("buyer", It.IsAny<int>())).ReturnsAsync(false);
        var guarded = new WebsiteMarketController(persistence, configSource.Object, serverProvider.Object, NullLogger<WebsiteMarketController>.Instance, playing.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        var refused = await guarded.BuyAsync(new MarketActionRequest("buyer", "pass123", whilePlaying!.Id)).ConfigureAwait(false);
        Assert.That(refused, Is.TypeOf<ConflictObjectResult>());
        Assert.That(store.Read(d => d.Listings.Any(l => l.Id == whilePlaying.Id)), Is.True);
        playing.Verify(l => l.ReleaseAccountReservationAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }
}
