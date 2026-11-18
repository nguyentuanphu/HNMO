// <copyright file="ActivityLogPageTests.cs" company="HNMO">
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
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// bUnit tests for the admin panel's Player activity log page (<c>/activity-log</c>).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ActivityLogPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _configuration = null!;
    private string _file = null!;

    /// <summary>Sets up the admin page context and test activity store.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        ActivityLogStore.ResetForTests();
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-act-page-{Guid.NewGuid():N}.json");
        this._provider = new InMemoryPersistenceContextProvider();
        using (var setup = this._provider.CreateNewContext())
        {
            this._configuration = setup.CreateNew<GameConfiguration>();
            this._plugin = setup.CreateNew<PlugInConfiguration>();
            this._plugin.TypeId = typeof(ActivityLogPlugIn).GUID;
            this._plugin.IsActive = true;
            this._plugin.CustomConfiguration = JsonSerializer.Serialize(new ActivityLogConfiguration
            {
                FileName = this._file,
                Enabled = true,
                OnlyValuableDropsAndPickups = true,
                MinValuableDropLevel = 7,
                RetentionDays = 30,
            });
            this._configuration.PlugInConfigurations.Add(this._plugin);
            await setup.SaveChangesAsync().ConfigureAwait(false);
        }

        var store = ActivityLogStore.Open(this._file);
        store.Append(new ActivityLogEntry
        {
            TimestampUtc = new DateTime(2026, 11, 16, 1, 30, 0, DateTimeKind.Utc), // 12:30 AEDT
            Kind = ActivityKind.Trade,
            Account = "alice",
            Character = "AliceBM",
            PartnerAccount = "bob",
            PartnerCharacter = "BobDW",
            ItemSummary = "Excellent Bone Blade +9",
            ItemIds = "item-guid-111",
            ZenDelta = 500000,
            Details = "ZenGave=0;ZenGot=500000",
        });
        store.Append(new ActivityLogEntry
        {
            TimestampUtc = new DateTime(2026, 11, 16, 2, 0, 0, DateTimeKind.Utc), // 13:00 AEDT
            Kind = ActivityKind.ChaosMix,
            Account = "charlie",
            Character = "CharlieElf",
            PartnerCharacter = "Chaos Goblin",
            ItemSummary = "Wings of Dragon +0",
            Details = "Result=Success",
        });

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._configuration);
        var manager = new PlugInManager([], Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, null, null);
        var writes = new PluginWriteOperations(
            this._provider,
            source.Object,
            manager,
            AdminOperationTestServices.CreateExecutor(),
            new AdminChangeConfirmation(TimeProvider.System),
            new AdminWriteSettings { ReadOnly = false });

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(this._provider);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<ActivityLog>>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Disposes bUnit and temporary files.</summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        ActivityLogStore.ResetForTests();
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
        if (File.Exists(this._file))
        {
            File.Delete(this._file);
        }
    }

    /// <summary>Verifies rendering, Sydney timestamps, help toggle, filtering, and saving settings.</summary>
    [Test]
    public void FiltersEntriesTogglesHelpAndSavesSettings()
    {
        var cut = this._context.Render<ActivityLog>();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("#al-table tbody tr"), Has.Count.EqualTo(2)), LoadTimeout);

        Assert.That(cut.Markup, Contains.Substring("2026-11-16 12:30:00"));
        Assert.That(cut.Markup, Contains.Substring("Excellent Bone Blade +9"));

        cut.Find("#al-help-btn").Click();
        Assert.That(cut.Find("#activity-log-help").TextContent, Contains.Substring("Sydney date/time"));

        cut.Find("#al-item").Change("item-guid-111");
        cut.Find("#al-search").Click();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("#al-table tbody tr"), Has.Count.EqualTo(1)), LoadTimeout);
        Assert.That(cut.Find("#al-table").TextContent, Contains.Substring("alice"));

        cut.Find("#al-clear").Click();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("#al-table tbody tr"), Has.Count.EqualTo(2)), LoadTimeout);

        cut.Find("#al-min-level").Change("9");
        cut.Find("#al-save").Click();
        cut.WaitForAssertion(() => Assert.That(this.Saved()?.MinValuableDropLevel, Is.EqualTo(9)), LoadTimeout);
    }

    /// <summary>Verifies Vietnamese localization renders cleanly.</summary>
    [Test]
    public void RendersVietnameseLabelsWhenCultureIsVietnamese()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
        var cut = this._context.Render<ActivityLog>();
        cut.WaitForAssertion(() => Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Nhật ký hoạt động")), LoadTimeout);
        Assert.That(cut.Find("#al-search").TextContent, Is.EqualTo("Tìm kiếm"));
    }

    private ActivityLogConfiguration? Saved() =>
        this._plugin.CustomConfiguration is { } json ? JsonSerializer.Deserialize<ActivityLogConfiguration>(json) : null;
}
