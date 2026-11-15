// <copyright file="GiftCodesPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.TopRaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// Tests for the admin panel's "Gift codes" page.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class GiftCodesPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;
    private string _file = null!;
    private GiftCodeStore _store = null!;

    /// <summary>
    /// Sets up the page with a store in its own file.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-gift-codes-page-{Guid.NewGuid():N}.json");
        this._store = GiftCodeStore.Open(this._file);
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var config = setup.CreateNew<GameConfiguration>();
        this._plugin = setup.CreateNew<PlugInConfiguration>();
        this._plugin.TypeId = typeof(GiftCodesPlugIn).GUID;
        this._plugin.IsActive = true;
        this._plugin.CustomConfiguration = JsonSerializer.Serialize(new GiftCodesConfiguration { FileName = this._file });
        config.PlugInConfigurations.Add(this._plugin);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IPlayerStateChangedPlugIn, GiftCodesPlugIn>();
        var writes = new PluginWriteOperations(provider, source.Object, manager, AdminOperationTestServices.CreateExecutor(), new AdminChangeConfirmation(TimeProvider.System), new AdminWriteSettings { ReadOnly = false });

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(provider);
        this._context.Services.AddSingleton(source.Object);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<GiftCodes>>());
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
    /// A new code with every field is saved into the store (normalized, Sydney dates, items with options, classes); it
    /// can be edited, switched off and deleted after a second click.
    /// </summary>
    [Test]
    public void ACodeIsMadeEditedSwitchedOffAndDeleted()
    {
        var cut = this._context.Render<GiftCodes>();
        cut.WaitForState(() => cut.FindAll("#gift-codes-new").Count == 1, LoadTimeout);
        Assert.That(cut.Find("#gift-codes-none").TextContent, Is.EqualTo(Resources.GiftCodesNone));

        cut.Find("#gift-codes-new").Click();
        cut.Find("#gift-code-text").Change(" xmas 2026 ");
        cut.Find("#gift-code-note").Change("Christmas");
        cut.Find("#gift-code-public").Change(true);
        cut.Find("#gift-code-coins").Change("250");
        cut.Find("#gift-code-coin-type").Change(nameof(CashShopCoinType.WCoinP));
        cut.Find("#gift-code-zen").Change("100000");
        cut.Find("#gift-code-add-item").Click();
        cut.Find(".gift-code-item .item-group").Change("0");
        cut.Find(".gift-code-item .item-number").Change("5");
        cut.Find(".gift-code-item .item-level").Change("9");
        cut.Find(".gift-code-item .item-luck").Change(true);
        cut.Find(".gift-code-item .item-option").Change("4");
        cut.Find(".gift-code-item .item-excellent").Change("3");
        cut.Find("#gift-code-uses").Change("100");
        cut.Find("#gift-code-limit").Change(nameof(GiftCodeLimit.OncePerCharacter));
        cut.Find("#gift-code-start").Change("2026-12-24T18:00");
        cut.Find("#gift-code-end").Change("2027-01-01 00:00");
        cut.Find("#gift-code-min-level").Change("50");
        cut.Find("#gift-code-max-resets").Change("0");
        cut.Find($".gift-code-class[data-class='{nameof(TopRaceClass.FairyElf)}']").Change(true);
        cut.Find("#gift-code-save").Click();

        cut.WaitForState(() => cut.FindAll(".gift-code-row").Count == 1, LoadTimeout);
        var code = this.Codes().Single();
        Assert.That((code.Code, code.Note, code.Public, code.Enabled), Is.EqualTo(("XMAS2026", "Christmas", true, true)));
        Assert.That((code.Reward.Coins, code.Reward.CoinType, code.Reward.Zen), Is.EqualTo((250, CashShopCoinType.WCoinP, 100000)));
        var item = code.Reward.Items.Single();
        Assert.That((item.Group, item.Number, item.Level, item.Luck, item.OptionLevel, item.ExcellentOptions), Is.EqualTo(((short)0, (short)5, (byte)9, true, (byte)4, (byte)3)));
        Assert.That((code.MaxUses, code.Limit, code.Start, code.End), Is.EqualTo((100, GiftCodeLimit.OncePerCharacter, "2026-12-24 18:00", "2027-01-01 00:00")));
        Assert.That((code.MinLevel, code.MaxResets, code.Classes.Single()), Is.EqualTo((50, 0, TopRaceClass.FairyElf)));
        Assert.That(File.ReadAllText(this._file), Does.Contain("XMAS2026"), "Saved in the file.");
        Assert.That(cut.Find(".gift-code-row .gift-code-uses").TextContent, Is.EqualTo("0 / 100"));

        cut.Find(".gift-code-row .gift-code-edit").Click();
        cut.Find("#gift-code-note").Change("Merry Christmas");
        cut.Find("#gift-code-save").Click();
        cut.WaitForState(() => this.Codes().Single().Note == "Merry Christmas", LoadTimeout);
        Assert.That(this.Codes().Single().Id, Is.EqualTo(code.Id));

        cut.Find(".gift-code-row .gift-code-enabled").Change(false);
        Assert.That(this.Codes().Single().Enabled, Is.False);

        cut.Find(".gift-code-row .gift-code-delete").Click();
        Assert.That(this.Codes(), Has.Count.EqualTo(1), "The first click asks.");
        cut.Find(".gift-code-row .gift-code-delete").Click();
        Assert.That(this.Codes(), Is.Empty);
    }

    /// <summary>
    /// A batch of single-use codes is made with its prefix, unique, and offered as a CSV download.
    /// </summary>
    [Test]
    public void ABatchIsMadeAndDownloadedAsCsv()
    {
        var cut = this._context.Render<GiftCodes>();
        cut.WaitForState(() => cut.FindAll("#gift-codes-new-batch").Count == 1, LoadTimeout);
        cut.Find("#gift-codes-new-batch").Click();
        cut.Find("#gift-code-prefix").Change("tet");
        cut.Find("#gift-code-count").Change("25");
        cut.Find("#gift-code-length").Change("6");
        cut.Find("#gift-code-save").Click();

        cut.WaitForState(() => cut.FindAll("#gift-codes-csv-link").Count == 1, LoadTimeout);
        var batch = this.Codes().Single();
        Assert.That(batch.BatchCodes, Has.Count.EqualTo(25));
        Assert.That(batch.BatchCodes.Distinct().Count(), Is.EqualTo(25));
        Assert.That(batch.BatchCodes, Has.All.Match("^TET[A-Z2-9]{6}$"));
        var link = cut.Find("#gift-codes-csv-link");
        Assert.That(link.GetAttribute("download"), Does.StartWith("gift-codes-tet-"));
        var href = link.GetAttribute("href")!;
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(href[(href.IndexOf(',') + 1)..]));
        Assert.That(csv, Does.Contain("code,used,account,character,used_at_sydney"));
        Assert.That(batch.BatchCodes.All(c => csv.Contains(c + ",no", StringComparison.Ordinal)), Is.True);
        Assert.That(cut.Find(".gift-code-row code").TextContent, Is.EqualTo("TET… (25)"));
    }

    /// <summary>
    /// Invalid entries are listed and nothing is saved: a code that exists (also as a batch code), bad dates, an empty
    /// reward, limits the wrong way round.
    /// </summary>
    [Test]
    public void InvalidEntriesAreNotSaved()
    {
        lock (this._store.Lock)
        {
            this._store.Data.Codes.Add(new GiftCodeDefinition { Id = "old", Code = "TAKEN", Reward = new GiftCodeReward { Coins = 1 } });
            this._store.Data.Codes.Add(new GiftCodeDefinition { Id = "batch", BatchCodes = ["BATCHAAAA"], BatchPrefix = "BATCH", Reward = new GiftCodeReward { Coins = 1 } });
            this._store.CodesChanged();
        }

        var cut = this._context.Render<GiftCodes>();
        cut.WaitForState(() => cut.FindAll(".gift-code-row").Count == 2, LoadTimeout);
        cut.Find("#gift-codes-new").Click();
        cut.Find("#gift-code-text").Change("taken");
        cut.Find("#gift-code-coins").Change("0");
        cut.Find("#gift-code-start").Change("2027-01-02 00:00");
        cut.Find("#gift-code-end").Change("2027-01-01 00:00");
        cut.Find("#gift-code-min-level").Change("80");
        cut.Find("#gift-code-max-level").Change("20");
        cut.Find("#gift-code-save").Click();
        var errors = cut.Find("#gift-codes-errors").TextContent;
        Assert.That(errors, Does.Contain(string.Format(CultureInfo.CurrentCulture, Resources.GiftCodesErrorDuplicate, "TAKEN")));
        Assert.That(errors, Does.Contain(Resources.GiftCodesErrorDates));
        Assert.That(errors, Does.Contain(Resources.GiftCodesErrorReward));
        Assert.That(errors, Does.Contain(Resources.GiftCodesErrorLimits));

        cut.Find("#gift-code-text").Change("batchaaaa");
        cut.Find("#gift-code-save").Click();
        Assert.That(cut.Find("#gift-codes-errors").TextContent, Does.Contain(string.Format(CultureInfo.CurrentCulture, Resources.GiftCodesErrorDuplicate, "BATCHAAAA")));
        Assert.That(this.Codes(), Has.Count.EqualTo(2));
    }

    /// <summary>
    /// The settings are saved into the plug-in's configuration; the uses and the log show who used a code (with a filter);
    /// the page speaks Vietnamese.
    /// </summary>
    [Test]
    public void SettingsLogAndVietnamese()
    {
        lock (this._store.Lock)
        {
            this._store.Data.Codes.Add(new GiftCodeDefinition { Id = "w", Code = "WELCOME", Reward = new GiftCodeReward { Coins = 5 } });
            this._store.CodesChanged();
        }

        var engine = new GiftCodeEngine(new GiftCodesConfiguration(), this._store);
        engine.Redeem("welcome", new GiftCodeRedeemer("ann", "c-1", "AnnKnight", 10, 0, 4), new DateTime(2026, 11, 15, 5, 0, 0, DateTimeKind.Utc), "website");
        engine.Redeem("welcome", new GiftCodeRedeemer("bob", "c-2", "Bobby", 10, 0, 0), new DateTime(2026, 11, 15, 6, 0, 0, DateTimeKind.Utc), "game");

        var cut = this._context.Render<GiftCodes>();
        cut.WaitForState(() => cut.FindAll(".gift-code-log-row").Count == 2, LoadTimeout);
        Assert.That(cut.Find(".gift-code-row .gift-code-uses").TextContent, Is.EqualTo("2"));
        Assert.That(cut.FindAll(".gift-code-log-row")[1].TextContent, Does.Contain("AnnKnight").And.Contain(Resources.GiftCodesSourceWebsite).And.Contain("16:00"));
        cut.Find("#gift-codes-log-filter").Input("bob");
        Assert.That(cut.FindAll(".gift-code-log-row"), Has.Count.EqualTo(1));

        cut.Find("#gift-codes-tries").Change("3");
        cut.Find("#gift-codes-website").Change(false);
        cut.Find("#gift-codes-save-settings").Click();
        cut.WaitForState(() => this._plugin.CustomConfiguration is { } json && JsonSerializer.Deserialize<GiftCodesConfiguration>(json)?.WrongTriesLimit == 3, LoadTimeout);
        var saved = JsonSerializer.Deserialize<GiftCodesConfiguration>(this._plugin.CustomConfiguration!)!;
        Assert.That((saved.WrongTriesLimit, saved.AllowWebsite, saved.FileName), Is.EqualTo((3, false, this._file)));

        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
        var vietnamese = this._context.Render<GiftCodes>();
        vietnamese.WaitForState(() => vietnamese.FindAll(".gift-code-row").Count == 1, LoadTimeout);
        Assert.That(vietnamese.Find("h1").TextContent, Is.EqualTo("Gift code"));
        Assert.That(vietnamese.Markup, Does.Contain("Lịch sử nhập mã").And.Contain("Một lần mỗi tài khoản"));
    }

    private List<GiftCodeDefinition> Codes()
    {
        lock (this._store.Lock)
        {
            return this._store.Data.Codes.ToList();
        }
    }
}
