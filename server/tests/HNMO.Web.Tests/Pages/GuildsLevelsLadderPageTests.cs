// <copyright file="GuildsLevelsLadderPageTests.cs" company="HNMO">
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
using HNMO.GameLogic.PlugIns.Guilds;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// Tests for the admin panel's "Guilds: levels and ladder" page.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class GuildsLevelsLadderPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;
    private Mock<IDataSource<GameConfiguration>> _source = null!;
    private GameConfiguration _config = null!;

    /// <summary>
    /// Sets up the page with the default guilds configuration.
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
        this._plugin.TypeId = typeof(GuildsPlugIn).GUID;
        this._plugin.IsActive = true;
        this._plugin.CustomConfiguration = JsonSerializer.Serialize(new GuildsConfiguration { FileName = string.Empty });
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
        manager.RegisterPlugIn<IPlayerStateChangedPlugIn, GuildsPlugIn>();
        var writes = new PluginWriteOperations(provider, this._source.Object, manager, AdminOperationTestServices.CreateExecutor(), new AdminChangeConfirmation(TimeProvider.System), new AdminWriteSettings { ReadOnly = false });

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(provider);
        this._context.Services.AddSingleton(this._source.Object);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<GuildsLevelsLadder>>());
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
    /// The page lists the sources, levels and ladder rewards; edits, a new level and a new reward are saved into the
    /// plug-in's configuration.
    /// </summary>
    [Test]
    public void EditsAreSavedIntoThePlugInConfiguration()
    {
        var cut = this._context.Render<GuildsLevelsLadder>();
        cut.WaitForState(() => cut.FindAll("#guilds-levels tbody tr").Count == 10, LoadTimeout);
        Assert.That(cut.FindAll("#guilds-sources tbody tr"), Has.Count.EqualTo(Enum.GetValues<GuildXpSource>().Length));
        Assert.That(cut.FindAll("#guilds-rewards tbody tr"), Has.Count.EqualTo(4));
        Assert.That(cut.Markup, Does.Contain(Resources.GuildsSourceWorldBossDamage));

        cut.Find("#guilds-cap").Change("777");
        cut.Find("tr[data-source='MonsterKill'] .source-xp").Change("3");
        cut.Find("tr[data-source='OnlineTime'] .source-on").Change(false);
        cut.Find("tr[data-level='5'] .level-defense").Change("12");
        cut.Find("#guilds-buffs").Change(false);
        cut.Find("#guilds-add-level").Click();
        cut.Find("#guilds-add-reward").Click();
        var last = cut.FindAll("#guilds-rewards tbody tr").Last();
        last.QuerySelector(".reward-coins")!.Change("9");
        cut.FindAll("#guilds-rewards tbody tr").Last().QuerySelector(".reward-items")!.Change("14 13 0 1");
        cut.Find("#guilds-save").Click();

        cut.WaitForState(() => this._plugin.CustomConfiguration?.Contains("777", StringComparison.Ordinal) is true, LoadTimeout);
        var saved = JsonSerializer.Deserialize<GuildsConfiguration>(this._plugin.CustomConfiguration!)!;
        Assert.That(saved.DailyCapPerMember, Is.EqualTo(777));
        Assert.That(saved.MonsterKill.Xp, Is.EqualTo(3));
        Assert.That(saved.OnlineTime.Enabled, Is.False);
        Assert.That(saved.BuffsEnabled, Is.False);
        Assert.That(saved.Levels, Has.Count.EqualTo(11));
        Assert.That(saved.Levels.Single(l => l.Level == 5).Defense, Is.EqualTo(12));
        Assert.That(saved.Levels.Last().Level, Is.EqualTo(11));
        var reward = saved.LadderRewards.Last();
        Assert.That((reward.RankFrom, reward.CoinsPerMember), Is.EqualTo((6, 9)));
        Assert.That(reward.Items.Single().Number, Is.EqualTo(13));
    }

    /// <summary>
    /// Invalid entries are listed and nothing is saved.
    /// </summary>
    [Test]
    public void InvalidEntriesAreNotSaved()
    {
        var before = this._plugin.CustomConfiguration;
        var cut = this._context.Render<GuildsLevelsLadder>();
        cut.WaitForState(() => cut.FindAll("#guilds-levels tbody tr").Count > 0, LoadTimeout);
        cut.Find("tr[data-level='3'] .level-xp").Change("1");
        cut.Find("tr[data-rank='4'] .reward-from").Change("3");
        cut.Find("#guilds-save").Click();
        cut.WaitForState(() => cut.FindAll("#guilds-errors li").Count > 0, LoadTimeout);
        var errors = cut.Find("#guilds-errors").TextContent;
        Assert.That(errors, Does.Contain(Resources.GuildsErrorLevels));
        Assert.That(errors, Does.Contain(string.Format(Resources.GuildsErrorRanks, 3, 5)));
        Assert.That(this._plugin.CustomConfiguration, Is.EqualTo(before));
    }

    /// <summary>
    /// The page reads the plug-in's row from the database, not from the panel's cached configuration, which can miss a
    /// row the game server added at its first start; and it names the page in Vietnamese.
    /// </summary>
    [Test]
    public void TheSettingsAreReadFromTheDatabaseInBothLanguages()
    {
        this._source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HNMO.Persistence.BasicModel.GameConfiguration());
        var cut = this._context.Render<GuildsLevelsLadder>();
        cut.WaitForState(() => cut.FindAll("#guilds-levels tbody tr").Count > 0, LoadTimeout);
        Assert.That(cut.Markup, Does.Not.Contain(Resources.GuildsNotInstalled));
        Assert.That(cut.Find("#guilds-ladder-empty").TextContent, Is.EqualTo(Resources.GuildsNoPoints));

        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
        var vietnamese = this._context.Render<GuildsLevelsLadder>();
        vietnamese.WaitForState(() => vietnamese.FindAll("#guilds-levels tbody tr").Count > 0, LoadTimeout);
        Assert.That(vietnamese.Find("h1").TextContent, Is.EqualTo("Bang hội: cấp và bảng xếp hạng"));
    }
}
