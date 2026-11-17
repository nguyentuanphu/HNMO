// <copyright file="WebsiteVipControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.Globalization;
using System.Text.Json;
using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Checks authenticated website purchases, ownership reservations and actual coin/reward persistence.</summary>
[TestFixture]
public class WebsiteVipControllerTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 1, 0, 0, DateTimeKind.Utc);
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _game = null!;
    private Account _account = null!;
    private PlugInConfiguration _plugin = null!;
    private Mock<ILoginServer> _login = null!;
    private WebsiteVipController _controller = null!;

    /// <summary>Creates an enabled isolated configuration and a real persisted account.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._provider = new();
        using var setup = this._provider.CreateNewContext();
        this._game = setup.CreateNew<GameConfiguration>();
        foreach (var attribute in new[] { Stats.IsVip, Stats.BonusExperienceRate, Stats.MoneyAmountRate })
        {
            this._game.Attributes.Add(setup.CreateNew<AttributeDefinition>(attribute.Id, attribute.Designation, attribute.Description));
        }

        this._plugin = setup.CreateNew<PlugInConfiguration>();
        this._plugin.TypeId = typeof(VipPlugIn).GUID;
        this._plugin.IsActive = true;
        this._plugin.CustomConfiguration = JsonSerializer.Serialize(new VipConfiguration { Enabled = true });
        await setup.SaveChangesAsync().ConfigureAwait(false);
        using var players = this._provider.CreateNewPlayerContext(this._game);
        this._account = players.CreateNew<Account>();
        this._account.LoginName = "ann";
        this._account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4);
        this._account.EMail = "private@example.test";
        this._account.VaultPassword = "private-pin";
        this._account.WCoinC = 1000;
        await players.SaveChangesAsync().ConfigureAwait(false);
        var configurations = new Mock<IDataSource<GameConfiguration>>();
        configurations.Setup(source => source.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._game);
        this._login = new();
        this._login.Setup(login => login.TryReserveAccountAsync("ann", It.IsAny<int>())).ReturnsAsync(true);
        this._login.Setup(login => login.ReleaseAccountReservationAsync("ann", It.IsAny<int>())).Returns(ValueTask.CompletedTask);
        this._controller = new(this._provider, configurations.Object, this._login.Object, NullLogger<WebsiteVipController>.Instance, VipStateStore.CreateInMemory(), new FixedClock())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    /// <summary>The bridge policy applies to all three endpoints and safe state never exposes private account fields.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task StateRequiresBridgePolicyAndExposesOnlySafeFieldsAsync()
    {
        var policy = typeof(WebsiteVipController).GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single();
        Assert.That(policy.Policy, Is.EqualTo(AdminPolicies.AuthenticatedOperator));
        Assert.That(policy.AuthenticationSchemes, Is.EqualTo(ApiKeyAuthenticationDefaults.ApiSchemes));
        var result = Content(await this._controller.StateAsync(new("ann", "secret")).ConfigureAwait(false));
        Assert.That(result.GetProperty("enabled").GetBoolean(), Is.True);
        Assert.That(result.GetProperty("offers").GetArrayLength(), Is.EqualTo(9));
        var raw = result.GetRawText();
        Assert.That(raw, Does.Not.Contain("private@example.test").And.Not.Contain("private-pin").And.Not.Contain(this._account.PasswordHash).And.Not.Contain(this._account.GetId().ToString()));
        Assert.That(this._controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
    }

    /// <summary>Repeated requests with the same reviewed offer save exactly one debit and one entitlement interval.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PurchaseAndRetryHaveOneDebitAsync()
    {
        var request = await this.PurchaseRequestAsync().ConfigureAwait(false);
        var first = Content(await this._controller.PurchaseAsync(request).ConfigureAwait(false));
        var second = Content(await this._controller.PurchaseAsync(request).ConfigureAwait(false));
        Assert.That(first.GetProperty("status").GetString(), Is.EqualTo("Applied"));
        Assert.That(second.GetProperty("status").GetString(), Is.EqualTo("Applied"));
        Assert.That(first.GetProperty("active").GetProperty("untilUtc").GetDateTime(), Is.EqualTo(Now.AddDays(7)));
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        using var players = this._provider.CreateNewPlayerContext(this._game);
        Assert.That(await players.GetCashShopCoinHistoryAsync(this._account.GetId(), 0, 20).ConfigureAwait(false), Has.Count.EqualTo(1));
        this._login.Verify(login => login.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Exactly(2));
    }

    /// <summary>Bad credentials or a banned account cannot acquire a mutation reservation.</summary>
    /// <param name="banned">Whether the authenticated account is banned.</param>
    /// <returns>The test task.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task RejectsUnauthorizedAccountsBeforeReservationAsync(bool banned)
    {
        this._account.State = banned ? AccountState.Banned : AccountState.Normal;
        var request = new WebsiteVipRequest("ann", banned ? "secret" : "wrong", Guid.NewGuid(), 1, 7, new string('a', 64));
        Assert.That(await this._controller.PurchaseAsync(request).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        this._login.Verify(login => login.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
    }

    /// <summary>A password changed before reservation ownership is obtained invalidates the original authorization.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ReauthenticatesAfterObtainingReservationAsync()
    {
        var request = await this.PurchaseRequestAsync().ConfigureAwait(false);
        this._login.Setup(login => login.TryReserveAccountAsync("ann", It.IsAny<int>()))
            .Callback(() => this._account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("replacement", 4)).ReturnsAsync(true);
        Assert.That(await this._controller.PurchaseAsync(request).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        Assert.That(this._account.AttributeGrants, Is.Empty);
        this._login.Verify(login => login.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Once);
    }

    /// <summary>An online account is directed to the game window without an offline balance write.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RefusesAnAccountOwnedByTheGameAsync()
    {
        var request = await this.PurchaseRequestAsync().ConfigureAwait(false);
        this._login.Setup(login => login.TryReserveAccountAsync("ann", It.IsAny<int>())).ReturnsAsync(false);
        var result = Content(await this._controller.PurchaseAsync(request).ConfigureAwait(false));
        Assert.That(result.GetProperty("status").GetString(), Is.EqualTo("Online"));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        Assert.That(this._account.AttributeGrants, Is.Empty);
    }

    /// <summary>Pending top-up credits settle before the reviewed VIP price is charged.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task SettlesPendingCoinsBeforePurchaseAsync()
    {
        this._account.WCoinC = 50;
        using var players = this._provider.CreateNewPlayerContext(this._game);
        var credit = players.CreateNew<CashShopCoinGrant>();
        credit.AccountId = this._account.GetId();
        credit.CoinType = CashShopCoinType.WCoinC;
        credit.Amount = 100;
        credit.CreatedAt = Now;
        await players.SaveChangesAsync().ConfigureAwait(false);
        var request = await this.PurchaseRequestAsync().ConfigureAwait(false);
        Assert.That(Content(await this._controller.PurchaseAsync(request).ConfigureAwait(false)).GetProperty("status").GetString(), Is.EqualTo("Applied"));
        Assert.That(this._account.WCoinC, Is.EqualTo(50));
        Assert.That(credit.AppliedAt, Is.Not.Null);
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
    }

    /// <summary>A reviewed version mismatch never spends coins.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ChangedOfferRequiresFreshReviewAsync()
    {
        var request = (await this.PurchaseRequestAsync().ConfigureAwait(false)) with { OfferToken = new string('0', 64) };
        var result = Content(await this._controller.PurchaseAsync(request).ConfigureAwait(false));
        Assert.That(result.GetProperty("status").GetString(), Is.EqualTo("PriceChanged"));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        Assert.That(this._account.AttributeGrants, Is.Empty);
    }

    /// <summary>Daily reward records apply through normal coin grants and repeated requests cannot add another bonus.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task DailyCoinsAreRecordedAndAppliedOnceAsync()
    {
        await this._controller.PurchaseAsync(await this.PurchaseRequestAsync().ConfigureAwait(false)).ConfigureAwait(false);
        var first = Content(await this._controller.DailyAsync(new("ann", "secret")).ConfigureAwait(false));
        var second = Content(await this._controller.DailyAsync(new("ann", "secret")).ConfigureAwait(false));
        Assert.That(first.GetProperty("status").GetString(), Is.EqualTo("DailyApplied"));
        Assert.That(second.GetProperty("status").GetString(), Is.EqualTo("AlreadyClaimed"));
        Assert.That(second.GetProperty("daily").GetProperty("claimed").GetBoolean(), Is.True);
        Assert.That(this._account.GoblinPoints, Is.EqualTo(10));
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
    }

    /// <summary>A stored but deactivated feature cannot start purchases.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task StoredInactivePluginDoesNotAllowPurchasesAsync()
    {
        var request = await this.PurchaseRequestAsync().ConfigureAwait(false);
        this._plugin.IsActive = false;
        var result = Content(await this._controller.PurchaseAsync(request).ConfigureAwait(false));
        Assert.That(result.GetProperty("enabled").GetBoolean(), Is.False);
        Assert.That(result.GetProperty("rule").GetString(), Is.EqualTo("Disabled"));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
    }

    /// <summary>Real purchase and daily receipts are presented in the admin's language rather than as implementation JSON.</summary>
    /// <param name="language">The admin language.</param>
    /// <returns>The test task.</returns>
    [TestCase("en")]
    [TestCase("vi")]
    [NonParallelizable]
    public async Task ReceiptHistoryUsesBilingualDescriptionsAsync(string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        var previousResource = Resources.Culture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            Resources.Culture = CultureInfo.CurrentUICulture;
            await this._controller.PurchaseAsync(await this.PurchaseRequestAsync().ConfigureAwait(false)).ConfigureAwait(false);
            await this._controller.DailyAsync(new("ann", "secret")).ConfigureAwait(false);
            using var players = this._provider.CreateNewPlayerContext(this._game);
            var rows = await players.GetCashShopCoinHistoryAsync(this._account.GetId(), 0, 20).ConfigureAwait(false);
            var purchase = rows.Single(row => VipWallet.ReadReceipt(row.Reason) is not null);
            var daily = rows.Single(row => VipDailyCoins.ReadReceipt(row.Reason) is not null);
            Assert.That(CashShopHistoryText.Format(purchase.Reason), Is.EqualTo(language == "vi" ? "VIP Đồng: 7 ngày" : "VIP Bronze: 7 days"));
            Assert.That(CashShopHistoryText.Format(daily.Reason), Is.EqualTo(language == "vi" ? "Quà VIP hằng ngày: Đồng" : "VIP daily reward: Bronze"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            Resources.Culture = previousResource;
        }
    }

    private static JsonElement Content(IActionResult result)
    {
        Assert.That(result, Is.InstanceOf<ContentResult>());
        using var json = JsonDocument.Parse(((ContentResult)result).Content!);
        return json.RootElement.Clone();
    }

    private async Task<WebsiteVipRequest> PurchaseRequestAsync()
    {
        var state = Content(await this._controller.StateAsync(new("ann", "secret")).ConfigureAwait(false));
        var offer = state.GetProperty("offers")[0];
        return new("ann", "secret", Guid.NewGuid(), offer.GetProperty("levelId").GetInt32(), offer.GetProperty("days").GetInt32(), offer.GetProperty("offerToken").GetString());
    }

    private sealed class FixedClock : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
