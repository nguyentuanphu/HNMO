// <copyright file="OnboardingStarterKitPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.Text.Json;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Onboarding;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// Tests for the admin panel's "Onboarding and starter kit" page.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class OnboardingStarterKitPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;
    private Mock<IDataSource<GameConfiguration>> _source = null!;
    private GameConfiguration _config = null!;

    /// <summary>
    /// Sets up the page with the default onboarding configuration.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        this._config = setup.CreateNew<GameConfiguration>();
        this._plugin = setup.CreateNew<PlugInConfiguration>();
        this._plugin.TypeId = typeof(OnboardingPlugIn).GUID;
        this._plugin.IsActive = true;
        this._plugin.CustomConfiguration = JsonSerializer.Serialize(new OnboardingConfiguration());
        this._config.PlugInConfigurations.Add(this._plugin);
        var knight = setup.CreateNew<CharacterClass>();
        knight.Number = 4;
        knight.Name = "Dark Knight";
        knight.CanGetCreated = true;
        this._config.CharacterClasses.Add(knight);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        this._source = new Mock<IDataSource<GameConfiguration>>();
        this._source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._config);
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IPlayerStateChangedPlugIn, OnboardingPlugIn>();
        var writes = new PluginWriteOperations(provider, this._source.Object, manager, AdminOperationTestServices.CreateExecutor(), new AdminChangeConfirmation(TimeProvider.System), new AdminWriteSettings { ReadOnly = false });

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(provider);
        this._context.Services.AddSingleton(this._source.Object);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<OnboardingStarterKit>>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>
    /// Restores the culture and disposes the test context.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
    }

    /// <summary>
    /// The page lists the steps and the kit; edits, a moved step, a new step and a new class item are saved into the
    /// plug-in's configuration.
    /// </summary>
    [Test]
    public void EditsAreSavedIntoThePlugInConfiguration()
    {
        var defaultSteps = new OnboardingConfiguration().Steps.Count;
        var cut = this._context.Render<OnboardingStarterKit>();
        cut.WaitForState(() => cut.FindAll("#onboarding-steps tbody tr").Count == defaultSteps, LoadTimeout);
        Assert.That(cut.Markup, Does.Contain("Talk to a merchant").And.Contain("Nói chuyện với một thương nhân"));
        Assert.That(cut.FindAll("#kit-items tbody tr"), Has.Count.EqualTo(new StarterKitConfiguration().Items.Count));
        Assert.That(cut.Find("#kit-items .kit-class").InnerHtml, Does.Contain("Dark Knight"), "The classes come from the game data.");

        cut.Find("#onboarding-level").Change("5");
        cut.Find("#kit-zen").Change("12345");
        cut.Find("#kit-account").Change(true);
        cut.Find("tr[data-step='kill-10'] .step-up").Click();
        cut.Find("#onboarding-add").Click();
        cut.Find("#kit-add").Click();
        cut.FindAll("#kit-items tbody tr").Last().QuerySelector(".kit-class")!.Change("4");
        cut.Find("#onboarding-save").Click();

        cut.WaitForState(() => this._plugin.CustomConfiguration?.Contains("12345", StringComparison.Ordinal) is true, LoadTimeout);
        var saved = JsonSerializer.Deserialize<OnboardingConfiguration>(this._plugin.CustomConfiguration!)!;
        Assert.That(saved.NewCharacterMaxLevel, Is.EqualTo(5));
        Assert.That(saved.StarterKit.Zen, Is.EqualTo(12345));
        Assert.That(saved.StarterKit.PerAccount, Is.True);
        Assert.That(saved.Steps, Has.Count.EqualTo(defaultSteps + 1));
        Assert.That(saved.Steps.First().Id, Is.EqualTo("kill-10"), "Moved up.");
        Assert.That(saved.StarterKit.Items.Last().ClassNumber, Is.EqualTo(4));
    }

    /// <summary>
    /// Invalid entries are listed and nothing is saved.
    /// </summary>
    [Test]
    public void InvalidEntriesAreNotSaved()
    {
        var before = this._plugin.CustomConfiguration;
        var cut = this._context.Render<OnboardingStarterKit>();
        cut.WaitForState(() => cut.FindAll("#onboarding-steps tbody tr").Count > 0, LoadTimeout);
        cut.Find("tr[data-step='kill-10'] .step-id").Change("talk-merchant");
        cut.Find("#onboarding-save").Click();
        cut.WaitForState(() => cut.FindAll("#onboarding-errors li").Count > 0, LoadTimeout);
        Assert.That(cut.Find("#onboarding-errors").TextContent, Does.Contain(string.Format(Resources.OnboardingErrorDuplicateId, "talk-merchant")));
        Assert.That(this._plugin.CustomConfiguration, Is.EqualTo(before));
    }

    /// <summary>
    /// The page reads the plug-in's row from the database, not from the panel's cached configuration, which can miss a
    /// row the game server added at its first start.
    /// </summary>
    [Test]
    public void TheSettingsAreReadFromTheDatabase()
    {
        this._source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HNMO.Persistence.BasicModel.GameConfiguration());
        var cut = this._context.Render<OnboardingStarterKit>();
        cut.WaitForState(() => cut.FindAll("#onboarding-steps tbody tr").Count > 0, LoadTimeout);
        Assert.That(cut.Markup, Does.Not.Contain(Resources.OnboardingNotInstalled));
    }
}
