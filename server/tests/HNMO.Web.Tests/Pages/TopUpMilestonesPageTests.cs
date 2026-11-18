// <copyright file="TopUpMilestonesPageTests.cs" company="HNMO">
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
using HNMO.GameLogic.PlugIns.TopUpMilestones;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// bUnit tests for <see cref="TopUpMilestones"/> admin page.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class TopUpMilestonesPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private string _file = null!;

    /// <summary>Sets up the bUnit test context and in-memory persistence.</summary>
    /// <returns>A task representing initialization.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-topup-milestones-page-{Guid.NewGuid():N}.json");

        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var config = setup.CreateNew<GameConfiguration>();
        var plugin = setup.CreateNew<PlugInConfiguration>();
        plugin.TypeId = typeof(TopUpMilestonesPlugIn).GUID;
        plugin.IsActive = true;
        plugin.CustomConfiguration = JsonSerializer.Serialize(new TopUpMilestonesConfiguration { FileName = this._file });
        config.PlugInConfigurations.Add(plugin);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IPlayerStateChangedPlugIn, TopUpMilestonesPlugIn>();
        var writes = new PluginWriteOperations(
            provider,
            source.Object,
            manager,
            AdminOperationTestServices.CreateExecutor(),
            new AdminChangeConfirmation(TimeProvider.System),
            new AdminWriteSettings { ReadOnly = false });

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(provider);
        this._context.Services.AddSingleton(source.Object);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<TopUpMilestones>>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Cleans up temporary resources.</summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
        File.Delete(this._file);
        File.Delete(this._file + ".bak");
    }

    /// <summary>Verifies rendering, help toggle, step list, and manual VND confirmation.</summary>
    [Test]
    public void RendersSettingsHelpAndRecordsManualTopUp()
    {
        var cut = this._context.Render<TopUpMilestones>();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("#topup-milestones-steps tbody tr"), Has.Count.EqualTo(5)), LoadTimeout);

        Assert.That(cut.FindAll("#topup-milestones-help"), Is.Empty);
        cut.Find("#topup-milestones-help-toggle").Click();
        Assert.That(cut.FindAll("#topup-milestones-help"), Has.Count.EqualTo(1));

        cut.Find("#manual-login").Change("alice");
        cut.Find("#manual-vnd").Change("250000");
        cut.Find("#manual-ref").Change("TX-MANUAL-1");
        cut.Find("#manual-record-btn").Click();

        cut.WaitForAssertion(() => Assert.That(cut.Find("#topup-milestones-accounts").TextContent, Does.Contain("alice")), LoadTimeout);
        Assert.That(cut.Find("#topup-milestones-accounts").TextContent, Does.Contain("250,000"));
    }

    /// <summary>Verifies step validation catches duplicate IDs and invalid item syntax.</summary>
    [Test]
    public void ValidatesStepDrafts()
    {
        var drafts = new[]
        {
            new TopUpMilestones.StepDraft { Id = "s1", ThresholdVnd = 100_000, ItemsText = "14:13:0:1" },
            new TopUpMilestones.StepDraft { Id = "s1", ThresholdVnd = 200_000 },
            new TopUpMilestones.StepDraft { Id = "s2", ThresholdVnd = -5 },
            new TopUpMilestones.StepDraft { Id = "s3", ThresholdVnd = 300_000, ItemsText = "bad-syntax" },
        };

        var errors = TopUpMilestones.ValidateAndBuildSteps(drafts, out var built);
        Assert.That(errors, Has.Count.EqualTo(3));
        Assert.That(built, Has.Count.EqualTo(1));
    }
}
