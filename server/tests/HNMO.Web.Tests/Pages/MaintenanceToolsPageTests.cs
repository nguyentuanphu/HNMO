// <copyright file="MaintenanceToolsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;
using Account = HNMO.DataModel.Entities.Account;
using Character = HNMO.DataModel.Entities.Character;

/// <summary>
/// bUnit tests for the admin panel Maintenance Tools page (<c>/maintenance-tools</c>).
/// </summary>
[TestFixture]
public sealed class MaintenanceToolsPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private InMemoryAdminAuditRepository _audit = null!;

    /// <summary>Sets up bUnit context with seeded maintenance issues.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        var provider = new InMemoryPersistenceContextProvider();
        GameConfiguration config;
        using (var setup = provider.CreateNewContext())
        {
            config = setup.CreateNew<GameConfiguration>();
            config.MaximumLevel = 400;
            var levelAttr = setup.CreateNew<AttributeDefinition>();
            levelAttr.Id = Stats.Level.Id;
            levelAttr.Designation = "Level";
            config.Attributes.Add(levelAttr);
            await setup.SaveChangesAsync().ConfigureAwait(false);

            using var pctx = provider.CreateNewPlayerContext(config);
            var old = pctx.CreateNew<Account>();
            old.LoginName = "staleuser";
            old.RegistrationDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var acc = pctx.CreateNew<Account>();
            acc.LoginName = "player1";
            acc.RegistrationDate = DateTime.UtcNow;
            var ch = pctx.CreateNew<Character>();
            ch.Name = "HeroOne";
            ch.Attributes.Add(pctx.CreateNew<StatAttribute>(levelAttr, 5f));
            ch.Experience = -50;
            acc.Characters.Add(ch);
            await pctx.SaveChangesAsync().ConfigureAwait(false);
        }

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);
        this._audit = new InMemoryAdminAuditRepository();
        var ops = new MaintenanceToolsOperations(provider, source.Object, AdminOperationTestServices.CreateExecutor(audit: this._audit), TimeProvider.System);

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton(ops);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<MaintenanceTools>>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Restores culture and disposes bUnit.</summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
    }

    /// <summary>Verifies help toggle, dry-run preview, audited apply, and backup link.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task PreviewsAppliesFixesAndShowsHelpAsync()
    {
        var cut = this._context.Render<MaintenanceTools>();
        cut.Find("#mt-help-btn").Click();
        Assert.That(cut.Find("#maintenance-tools-help").TextContent, Contains.Substring("Dry-run first"));
        Assert.That(cut.Markup, Contains.Substring("href=\"/control\""));

        cut.Find("#mt-inactive-preview").Click();
        cut.WaitForAssertion(() => Assert.That(cut.Find("#mt-inactive-table").TextContent, Contains.Substring("staleuser")), LoadTimeout);
        cut.Find("#mt-inactive-apply").Click();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("#mt-inactive-none"), Has.Count.EqualTo(1)), LoadTimeout);

        cut.Find("#mt-exp-preview").Click();
        cut.WaitForAssertion(() => Assert.That(cut.Find("#mt-exp-table").TextContent, Contains.Substring("HeroOne")), LoadTimeout);
        cut.Find("#mt-exp-apply").Click();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("#mt-exp-none"), Has.Count.EqualTo(1)), LoadTimeout);

        var (entries, _) = await this._audit.ReadAsync(0, 50).ConfigureAwait(false);
        Assert.That(entries, Has.Count.EqualTo(2));
    }

    /// <summary>Verifies Vietnamese localization renders properly.</summary>
    [Test]
    public void RendersVietnameseLabels()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
        var cut = this._context.Render<MaintenanceTools>();
        Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Công cụ bảo trì"));
    }
}
