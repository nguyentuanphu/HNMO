// <copyright file="StateRetentionPageTests.cs" company="HNMO">
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
using HNMO.GameLogic.PlugIns;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// bUnit tests for the admin panel's Player state and archival page (<c>/state-retention</c>).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class StateRetentionPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _configuration = null!;
    private string _file = null!;

    /// <summary>Sets up the admin page context and test state store.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-state-ret-{Guid.NewGuid():N}.json");
        this._provider = new InMemoryPersistenceContextProvider();
        using (var setup = this._provider.CreateNewContext())
        {
            this._configuration = setup.CreateNew<GameConfiguration>();
            this._plugin = setup.CreateNew<PlugInConfiguration>();
            this._plugin.TypeId = typeof(HnmoStateRetentionPlugIn).GUID;
            this._plugin.IsActive = true;
            this._plugin.CustomConfiguration = JsonSerializer.Serialize(new HnmoStateRetentionConfiguration
            {
                FileName = this._file,
                ArchiveFinishedDataAfterDays = 30,
            });
            this._configuration.PlugInConfigurations.Add(this._plugin);
            await setup.SaveChangesAsync().ConfigureAwait(false);
        }

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
        this._context.Services.AddSingleton(Mock.Of<ILogger<StateRetention>>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Disposes bUnit and temporary state files.</summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
        if (File.Exists(this._file))
        {
            File.Delete(this._file);
        }
    }

    /// <summary>Verifies saving retention days and running immediate archival updates active/archived counts.</summary>
    [Test]
    public void EditsRetentionDaysAndRunsArchival()
    {
        var backend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._file);
        backend.UpsertBatch(
            [
                new HnmoFeatureRow("giftCodes.claims", "old", "[\"a:ann\"]", DateTime.UtcNow.AddDays(-45), DateTime.UtcNow.AddDays(-40)),
                new HnmoFeatureRow("missions.account", "ann", "{}", DateTime.UtcNow),
            ],
            []);

        var cut = this._context.Render<StateRetention>();
        cut.WaitForState(() => cut.FindAll("#sr-save").Count == 1, LoadTimeout);
        Assert.That(cut.Find("#sr-stats").TextContent, Does.Contain("Active DB rows: 2"));

        cut.Find("#sr-days").Change("15");
        cut.Find("#sr-save").Click();
        cut.WaitForState(() => Saved()?.ArchiveFinishedDataAfterDays == 15, LoadTimeout);

        cut.Find("#sr-archive").Click();
        cut.WaitForState(() => cut.Find("#sr-stats").TextContent.Contains("Archived DB rows: 1"), LoadTimeout);
        Assert.That(cut.Find("#sr-stats").TextContent, Does.Contain("Active DB rows: 1"));
    }

    /// <summary>Verifies English and Vietnamese localization and help toggle.</summary>
    /// <param name="culture">Culture code.</param>
    /// <param name="heading">Expected heading.</param>
    [TestCase("en", "Player state & archival")]
    [TestCase("vi", "Lưu trữ & Dọn dữ liệu")]
    public void RendersEnglishAndVietnameseWithHelpBox(string culture, string heading)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var cut = this._context.Render<StateRetention>();
        cut.WaitForState(() => cut.FindAll("#sr-save").Count == 1, LoadTimeout);
        Assert.That(cut.Find("h1").TextContent, Does.Contain(heading));

        cut.Find("button.btn-outline-info").Click();
        Assert.That(cut.FindAll("#state-retention-help").Count, Is.EqualTo(1));
    }

    private HnmoStateRetentionConfiguration? Saved() =>
        this._plugin.CustomConfiguration is { } json ? JsonSerializer.Deserialize<HnmoStateRetentionConfiguration>(json) : null;
}
