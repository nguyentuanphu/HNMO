// <copyright file="ResetsRelifePageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// Tests for the admin panel's "Resets and relife" page.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ResetsRelifePageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;
    private string _file = null!;
    private ResetStore _store = null!;

    /// <summary>
    /// Sets up the page with a store in its own file.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-resets-page-{Guid.NewGuid():N}.json");
        this._store = ResetStore.Open(this._file);
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var config = setup.CreateNew<GameConfiguration>();
        this._plugin = setup.CreateNew<PlugInConfiguration>();
        this._plugin.TypeId = typeof(ResetOptionsPlugIn).GUID;
        this._plugin.IsActive = true;
        this._plugin.CustomConfiguration = JsonSerializer.Serialize(new ResetOptionsConfiguration { FileName = this._file });
        config.PlugInConfigurations.Add(this._plugin);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IPlayerStateChangedPlugIn, ResetOptionsPlugIn>();
        var writes = new PluginWriteOperations(provider, source.Object, manager, AdminOperationTestServices.CreateExecutor(), new AdminChangeConfirmation(TimeProvider.System), new AdminWriteSettings { ReadOnly = false });

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(provider);
        this._context.Services.AddSingleton(source.Object);
        this._context.Services.AddSingleton(Mock.Of<ILoginServer>());
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<ResetsRelife>>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>
    /// Restores the culture, disposes the test context and deletes the file.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
        File.Delete(this._file);
    }

    /// <summary>
    /// The default tiers are shown; an edited tier, a new tier with items, the VIP, relife, delegated and website options
    /// are saved into the plug-in's settings.
    /// </summary>
    [Test]
    public void TheOptionsAreEditedAndSaved()
    {
        var cut = this._context.Render<ResetsRelife>();
        cut.WaitForState(() => cut.FindAll("#resets-save").Count == 1, LoadTimeout);
        Assert.That(cut.FindAll(".resets-tier"), Has.Count.EqualTo(3));

        cut.FindAll(".resets-tier .tier-zen")[0].Change("1234");
        cut.FindAll(".resets-tier .tier-daily")[0].Change("7");
        cut.Find("#resets-add-tier").Click();
        Assert.That(cut.FindAll(".resets-tier"), Has.Count.EqualTo(4));
        cut.FindAll(".resets-tier .tier-items")[3].Change("14:13:0:2; 14:22");
        cut.FindAll(".resets-tier .tier-mode")[3].Change(nameof(ResetPointsMode.Add));
        cut.Find("#resets-vip-level").Change("33");
        cut.Find("#resets-relife-resets").Change("80");
        cut.Find("#resets-relife-items").Change("14:22:0:1");
        cut.Find("#resets-delegated-zen").Change("999");
        cut.Find("#resets-web-clear").Change(false);
        cut.Find("#resets-save").Click();

        cut.WaitForState(() => Saved()?.Vip.LevelReduction == 33, LoadTimeout);
        var saved = Saved()!;
        Assert.That((saved.Tiers[0].Zen, saved.Tiers[0].DailyLimit), Is.EqualTo((1234, 7)));
        Assert.That(saved.Tiers, Has.Count.EqualTo(4));
        Assert.That(saved.Tiers[2].ToResets, Is.GreaterThanOrEqualTo(saved.Tiers[2].FromResets), "The open tier got an end.");
        Assert.That(saved.Tiers[3].FromResets, Is.EqualTo(saved.Tiers[2].ToResets + 1));
        Assert.That(saved.Tiers[3].PointsMode, Is.EqualTo(ResetPointsMode.Add));
        Assert.That(saved.Tiers[3].Items.Select(i => (i.Group, i.Number, i.Count)), Is.EqualTo(new[] { ((short)14, (short)13, 2), ((short)14, (short)22, 1) }));
        Assert.That((saved.Relife.ResetsNeeded, saved.Relife.Items.Single().Number, saved.Delegated.ExtraZen, saved.Website.AllowClearStats), Is.EqualTo((80, (short)22, 999, false)));
        Assert.That(saved.FileName, Is.EqualTo(this._file));
    }

    /// <summary>
    /// Wrong items and overlapping tiers are refused with their errors; nothing is saved.
    /// </summary>
    [Test]
    public void WrongItemsAndOverlapsAreRefused()
    {
        var configuration = new ResetOptionsConfiguration();
        var errors = ResetsRelife.Validate(
            configuration,
            [(new ResetTier { FromResets = 0, ToResets = 10 }, "14:13:0:1"), (new ResetTier { FromResets = 5, ToResets = -1 }, "abc")],
            "99:1");
        Assert.That(errors, Has.Count.EqualTo(3));
        Assert.That(errors, Has.Some.Contains("overlap"));
        Assert.That(ResetsRelife.TryParseItems("14:13:2:3", out var items), Is.True);
        Assert.That((items.Single().MinimumLevel, items.Single().Count), Is.EqualTo(((byte)2, 3)));
        Assert.That(ResetsRelife.ItemsText(items), Is.EqualTo("14:13:2:3"));

        var cut = this._context.Render<ResetsRelife>();
        cut.WaitForState(() => cut.FindAll("#resets-save").Count == 1, LoadTimeout);
        cut.FindAll(".resets-tier .tier-items")[0].Change("nope");
        cut.Find("#resets-save").Click();
        Assert.That(cut.Find("#resets-errors").TextContent, Does.Contain("Tier 1"));
        Assert.That(JsonSerializer.Deserialize<ResetOptionsConfiguration>(this._plugin.CustomConfiguration!)!.Tiers[0].Items, Is.Empty);
    }

    /// <summary>
    /// The rankings and the log come from the store; the page is in Vietnamese with a Vietnamese culture.
    /// </summary>
    [Test]
    public void TheRankingsAndTheLogAreShownInBothLanguages()
    {
        var engine = new ResetEngine(new ResetOptionsConfiguration { FileName = this._file }, this._store);
        var subject = new ResetSubject { CharacterId = "c1", Name = "AnnKnight", Login = "ann", Level = 400, Resets = 0, Zen = 100_000_000 };
        var plan = engine.PlanReset(subject, DateTime.UtcNow, ResetSource.Delegated);
        engine.Record(subject, plan, DateTime.UtcNow, ResetSource.Delegated);
        engine.SetDelegated(subject, true);

        var cut = this._context.Render<ResetsRelife>();
        cut.WaitForState(() => cut.FindAll("#resets-log").Count == 1, LoadTimeout);
        Assert.That(cut.Find("#resets-monthly").TextContent, Does.Contain("AnnKnight: 1"));
        Assert.That(cut.Find("#resets-log").TextContent, Does.Contain("AnnKnight").And.Contain("Delegated"));
        Assert.That(cut.Find("#resets-delegated-list").TextContent, Does.Contain("AnnKnight (ann)"));

        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
        var vietnamese = this._context.Render<ResetsRelife>();
        vietnamese.WaitForState(() => vietnamese.FindAll("#resets-log").Count == 1, LoadTimeout);
        Assert.That(vietnamese.Find("h1").TextContent, Is.EqualTo("Reset và relife"));
        Assert.That(vietnamese.Markup, Does.Contain("Các mức reset").And.Contain("Ủy thác"));
    }

    private ResetOptionsConfiguration? Saved() =>
        this._plugin.CustomConfiguration is { } json ? JsonSerializer.Deserialize<ResetOptionsConfiguration>(json) : null;
}
