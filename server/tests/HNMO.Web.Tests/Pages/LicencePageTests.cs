// <copyright file="LicencePageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.IO;
using Bunit;
using HNMO.GameLogic;
using HNMO.Interfaces;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Services;
using HNMO.Web.Tests.AdminAuth;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>
/// The License page (owner, 2026-11-28): no shop when the hub is off; the packages with a button per payment way; a
/// pending bank transfer shown with its VietQR. The package list comes from the server's 6-hour cache, so no hub call.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class LicencePageTests
{
    private const string Catalogue = """
        {"packages":[
          {"id":"trial-14","kind":"trial","name":{"en":"Trial","vi":"Dùng thử"},"description":{"en":"14 days, 100 players","vi":"14 ngày, 100 người"},"maxPlayers":100,"seasons":["s6","s9","arena"],"billing":"once","durationDays":14,"prices":{},"order":1},
          {"id":"standard-year","kind":"plan","name":{"en":"Standard","vi":"Tiêu chuẩn"},"description":{"en":"For most servers","vi":"Cho hầu hết server"},"maxPlayers":200,"seasons":["s6","s9"],"billing":"year","durationDays":365,"prices":{"vnd":4990000,"aud":29900},"highlight":true,"order":2},
          {"id":"standard-month","kind":"plan","name":{"en":"Standard","vi":"Tiêu chuẩn"},"description":{"en":"Monthly"},"maxPlayers":200,"seasons":["s6","s9"],"billing":"month","durationDays":31,"prices":{"vnd":499000},"order":3},
          {"id":"players-100","kind":"addon","addon":"players","name":{"en":"+100 players"},"description":{"en":"More players"},"addPlayers":100,"billing":"once","prices":{"vnd":990000},"order":4}
        ],"providers":{"stripe":true,"payos":false,"bank":true}}
        """;

    private string? _oldHub;
    private string? _oldState;
    private DirectoryInfo _dir = null!;
    private CultureInfo _oldCulture = null!;
    private BunitContext _context = null!;

    /// <summary>A state file of its own and the page's services.</summary>
    [SetUp]
    public void SetUp()
    {
        this._oldHub = Environment.GetEnvironmentVariable("HNMO_DEVELOPER_HUB");
        this._oldState = DeveloperHub.StateFile;
        this._oldCulture = CultureInfo.CurrentUICulture;
        this._dir = Directory.CreateTempSubdirectory();
        DeveloperHub.StateFile = Path.Combine(this._dir.FullName, "developer-hub.json");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        var settings = new Mock<IHostSettingsService>();
        settings.Setup(s => s.Load()).Returns(new HostSettings());
        settings.Setup(s => s.GetLicenseStatus(It.IsAny<string?>())).Returns(new LicenseStatusInfo(10, "No license: up to 10 players at once.", "1234567890abcdef", "license.hnmo", false));
        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddLogging();
        this._context.Services.AddToasts();
        this._context.Services.AddSingleton(settings.Object);
        this._context.AddAdministratorAuthorization();
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Gives the test run its settings back.</summary>
    /// <returns>The task.</returns>
    [TearDown]
    public async Task TearDownAsync()
    {
        await this._context.DisposeAsync().ConfigureAwait(false);
        Environment.SetEnvironmentVariable("HNMO_DEVELOPER_HUB", this._oldHub);
        DeveloperHub.StateFile = this._oldState;
        CultureInfo.CurrentUICulture = this._oldCulture;
        this._dir.Delete(recursive: true);
    }

    /// <summary>With the hub off there is no shop, only how to get a licence file.</summary>
    [Test]
    public void HubOffShowsNoShop()
    {
        Environment.SetEnvironmentVariable("HNMO_DEVELOPER_HUB", "off");
        var page = this._context.Render<Licence>();
        Assert.That(page.FindAll("#licence-hub-off"), Has.Count.EqualTo(1));
        Assert.That(page.FindAll(".licence-buy-card, .licence-buy-bank, #licence-trial-start"), Is.Empty);
        Assert.That(page.Find("#licence-current").TextContent, Does.Contain("10 players"));
    }

    /// <summary>The yearly plans with a card and a bank button (PayOS off), the trial, and add-ons kept for licensed servers.</summary>
    [Test]
    public void PackagesShowAButtonPerPaymentWay()
    {
        this.UseHubWithCachedPackages();
        var page = this._context.Render<Licence>();
        page.WaitForAssertion(() => Assert.That(page.FindAll(".licence-package"), Has.Count.EqualTo(1)));
        var card = page.Find(".licence-package");
        Assert.That(card.GetAttribute("data-package"), Is.EqualTo("standard-year"));
        Assert.That(card.TextContent, Does.Contain("Suggested").And.Contain("200 players").And.Contain("Season 6.3 + Season 9").And.Contain("₫"));
        Assert.That(page.FindAll(".licence-buy-card"), Has.Count.EqualTo(1));
        Assert.That(page.FindAll(".licence-buy-bank"), Has.Count.EqualTo(1));
        Assert.That(page.FindAll(".licence-buy-payos"), Is.Empty);
        Assert.That(page.FindAll("#licence-trial-start"), Has.Count.EqualTo(1));
        Assert.That(page.FindAll("#licence-addons-need"), Has.Count.EqualTo(1), "add-ons need a licence");
        Assert.That(page.FindAll("button,select,input").All(b => !string.IsNullOrWhiteSpace(b.GetAttribute("title"))), Is.True);

        page.FindAll("#licence-billing button")[0].Click();
        Assert.That(page.Find(".licence-package").GetAttribute("data-package"), Is.EqualTo("standard-month"));
    }

    /// <summary>A bank transfer that waits is shown again with its VietQR, account, amount and note.</summary>
    [Test]
    public void PendingBankTransferShowsItsQr()
    {
        this.UseHubWithCachedPackages();
        DeveloperHub.ChangeState(s => s.Purchase = new DeveloperHubPurchase
        {
            PackageId = "standard-year",
            Provider = "bank",
            StartedUtc = DateTime.UtcNow,
            NextCheckUtc = DateTime.UtcNow.AddHours(1),
            LastCheckUtc = DateTime.UtcNow,
            Bank = new HubBankTransfer("970436", "Vietcombank", "0011001234567", "NGUYEN VAN A", 4_990_000, "HNMO O1A2B3", "O1A2B3"),
        });
        var page = this._context.Render<Licence>();
        page.WaitForAssertion(() => Assert.That(page.FindAll("#licence-bank"), Has.Count.EqualTo(1)));
        Assert.That(page.Find("#bank-account").TextContent, Is.EqualTo("0011001234567"));
        Assert.That(page.Find("#bank-note").TextContent, Is.EqualTo("HNMO O1A2B3"));
        Assert.That(page.Find("#bank-amount").TextContent, Does.Contain("4.990.000"));
        Assert.That(page.Find("#licence-bank").InnerHtml, Does.Contain("<svg"));
        Assert.That(page.Find("#licence-waiting").TextContent, Does.Contain("confirms the transfer"));
    }

    private void UseHubWithCachedPackages()
    {
        // A loopback hub nobody answers: the fresh cache keeps the page from calling it.
        Environment.SetEnvironmentVariable("HNMO_DEVELOPER_HUB", "http://127.0.0.1:9");
        DeveloperHub.ChangeState(s =>
        {
            s.PackagesJson = Catalogue;
            s.PackagesFetchedUtc = DateTime.UtcNow;
        });
    }
}
