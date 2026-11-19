// <copyright file="ShopEditorTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.Threading;
using Bunit;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Components;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Services;
using HNMO.Web.Shared.Components.ItemPicker;
using HNMO.Web.Tests.Mcp;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Exercises the real virtualized catalogue, graphical draft, grid and audited save without timing sleeps.</summary>
[TestFixture]
[NonParallelizable]
public sealed class ShopEditorTests
{
    private BunitContext _ui = null!;
    private IContext _context = null!;
    private InMemoryPersistenceContextProvider _provider = null!;
    private Mock<IDataSource<GameConfiguration>> _source = null!;
    private GameConfiguration _configuration = null!;
    private MonsterDefinition _merchant = null!;
    private MonsterDefinition _other = null!;
    private List<ItemDefinition> _definitions = null!;
    private List<CharacterClass> _classes = null!;
    private ShopEditorOperations _operations = null!;
    private CultureInfo _culture = null!;
    private CultureInfo _uiCulture = null!;

    /// <summary>Creates isolated catalogue/shop data and a real audited operation executor.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._culture = CultureInfo.CurrentCulture;
        this._uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._provider = new InMemoryPersistenceContextProvider();
        this._context = this._provider.CreateNewContext();
        this._configuration = this._context.CreateNew<GameConfiguration>();
        var knight = this._context.CreateNew<CharacterClass>();
        knight.Number = 4;
        knight.Name = "Knight";
        var elf = this._context.CreateNew<CharacterClass>();
        elf.Number = 8;
        elf.Name = "Elf";
        this._classes = [knight, elf];
        var sword = this.Definition(0, 1, "Bronze Sword", 2, 2, 40, "Kiếm đồng");
        sword.QualifiedCharacters.Add(knight);
        var blade = this.Definition(0, 2, "Short Sword", 1, 3, 10, "Kiếm ngắn");
        blade.QualifiedCharacters.Add(knight);
        var bow = this.Definition(4, 0, "Bow", 2, 3, 30, "Cung");
        bow.QualifiedCharacters.Add(elf);
        this._definitions = [sword, blade, bow, this.Definition(6, 0, "Shield", 2, 3, 0, "Khiên"), this.Definition(14, 0, "Healing Potion", 1, 1, 0, "Bình máu")];
        this._merchant = this.Merchant("Hannah");
        this._other = this.Merchant("Zora");
        var map = this._context.CreateNew<GameMapDefinition>();
        map.Number = 0;
        map.Name = "Town";
        var spawn = this._context.CreateNew<MonsterSpawnArea>();
        spawn.MonsterDefinition = this._merchant;
        spawn.GameMap = map;
        spawn.X1 = 12;
        spawn.X2 = 12;
        spawn.Y1 = 34;
        spawn.Y2 = 34;
        await this._context.SaveChangesAsync().ConfigureAwait(false);
        this._source = new Mock<IDataSource<GameConfiguration>>();
        this._source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._configuration);
        this._source.Setup(s => s.GetContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(this._context);
        this._source.Setup(s => s.GetAll<ItemDefinition>()).Returns(this._definitions);
        this._source.Setup(s => s.GetAll<MonsterDefinition>()).Returns([this._merchant, this._other]);
        this._source.Setup(s => s.GetAll<CharacterClass>()).Returns(this._classes);
        this._source.Setup(s => s.GetAll<MonsterSpawnArea>()).Returns([spawn]);
        this._operations = new ShopEditorOperations(this._source.Object, AdminOperationTestServices.CreateExecutor(), new AdminChangeConfirmation(TimeProvider.System), NullLogger<ShopEditorOperations>.Instance);
        this._ui = new BunitContext();
        this._ui.JSInterop.Mode = JSRuntimeMode.Loose;
        this._ui.JSInterop.Setup<bool>("window.confirm", _ => true).SetResult(true);
        this._ui.Services.AddSingleton(this._source.Object);
        this._ui.Services.AddSingleton<IPersistenceContextProvider>(this._provider);
        this._ui.Services.AddSingleton(this._operations);
        this._ui.Services.AddSingleton<IChangeNotificationService, ChangeNotificationService>();
        this._ui.Services.AddSingleton(Mock.Of<ILookupController>());
        this._ui.Services.AddSingleton<ILogger<MerchantShops>>(NullLogger<MerchantShops>.Instance);
        this._ui.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Disposes only this fixture's resources and restores both cultures.</summary>
    [TearDown]
    public void TearDown()
    {
        this._ui.Dispose();
        this._context.Dispose();
        CultureInfo.CurrentCulture = this._culture;
        CultureInfo.CurrentUICulture = this._uiCulture;
    }

    /// <summary>Search and every filter operate through actual change/input events.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CatalogueSearchFiltersAndSortAsync()
    {
        var cut = this._ui.Render<ItemPicker>(p => p.Add(c => c.Definitions, this._definitions).Add(c => c.Classes, this._classes).Add(c => c.Stocked, new HashSet<(byte, short)> { (0, 1) }));
        await cut.Find(".picker-search").InputAsync(new ChangeEventArgs { Value = "kiếm" });
        cut.WaitForAssertion(() => Assert.That(cut.Instance.Matches.Count, Is.EqualTo(2)));
        await cut.Find(".picker-search").InputAsync(new ChangeEventArgs { Value = "0:1" });
        cut.WaitForAssertion(() => Assert.That(cut.Instance.Matches.Single(), Is.SameAs(this._definitions[0])));
        await cut.Find(".picker-search").InputAsync(new ChangeEventArgs { Value = "" });
        await cut.Find(".picker-category").ChangeAsync(new ChangeEventArgs { Value = "4" });
        cut.WaitForAssertion(() => Assert.That(cut.Instance.Matches.Single().Name.ValueInNeutralLanguage, Is.EqualTo("Bow")));
        await cut.Find(".picker-category").ChangeAsync(new ChangeEventArgs { Value = "-1" });
        await cut.Find(".picker-class").ChangeAsync(new ChangeEventArgs { Value = "4" });
        cut.WaitForAssertion(() => Assert.That(cut.Instance.Matches, Does.Not.Contain(this._definitions[2])));
        await cut.Find(".picker-min").ChangeAsync(new ChangeEventArgs { Value = "20" });
        cut.WaitForAssertion(() => Assert.That(cut.Instance.Matches.Single(), Is.SameAs(this._definitions[0])));
        await cut.Find(".picker-max").ChangeAsync(new ChangeEventArgs { Value = "35" });
        cut.WaitForAssertion(() => Assert.That(cut.Instance.Matches, Is.Empty));
        await cut.Find(".picker-min").ChangeAsync(new ChangeEventArgs { Value = "0" });
        await cut.Find(".picker-max").ChangeAsync(new ChangeEventArgs { Value = "1000" });
        await cut.Find(".picker-missing").ChangeAsync(new ChangeEventArgs { Value = true });
        cut.WaitForAssertion(() => Assert.That(cut.Instance.Matches, Does.Not.Contain(this._definitions[0])));
        await cut.Find(".picker-sort").ChangeAsync(new ChangeEventArgs { Value = "group" });
        cut.WaitForAssertion(() => Assert.That(cut.Instance.Matches.First(), Is.SameAs(this._definitions[1])));
        await cut.Find(".picker-sort").ChangeAsync(new ChangeEventArgs { Value = "level" });
        cut.WaitForAssertion(() => Assert.That(ItemCatalogueFilter.RequiredLevel(cut.Instance.Matches.First()), Is.Zero));
    }

    /// <summary>A catalogue click uses the first fitting rectangle and never changes the source before save.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ClickAddsAtFirstFreeSpaceAsync()
    {
        this.Stock(this._merchant, this._definitions[4], 0);
        var draft = new ShopDraft(this._merchant.MerchantStore!);
        var cut = this.Workspace(draft);
        await AddAsync(cut, "0:1").ConfigureAwait(false);
        cut.WaitForAssertion(() => Assert.That(draft.Items.Single(i => i.Definition == this._definitions[0]).ItemSlot, Is.EqualTo(1)));
        Assert.That(draft.IsValid(), Is.True);
        Assert.That(this._merchant.MerchantStore!.Items.Count, Is.EqualTo(1));
        Assert.That(cut.Find(".shop-price").TextContent, Does.Contain("Base purchase price:"));
    }

    /// <summary>A rejected addition explains the failure and neither leaks an item nor adds an undo step.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task FullShopShowsDoesNotFitAsync()
    {
        for (byte slot = 0; slot < 120; slot++)
        {
            this.Stock(this._merchant, this._definitions[4], slot);
        }

        var draft = new ShopDraft(this._merchant.MerchantStore!);
        var cut = this.Workspace(draft);
        await AddAsync(cut, "0:1").ConfigureAwait(false);
        cut.WaitForAssertion(() => Assert.That(cut.Find(".shop-feedback").TextContent, Does.Contain("does not fit")));
        Assert.That(draft.Items.Count(), Is.EqualTo(120));
        Assert.That(draft.CanUndo, Is.False);
    }

    /// <summary>Largest-first packing is bounded and overlap-free; it is undoable as one action.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ArrangeUndoAndRedoAsync()
    {
        this.Stock(this._merchant, this._definitions[3], 82);
        this.Stock(this._merchant, this._definitions[4], 7);
        var draft = new ShopDraft(this._merchant.MerchantStore!);
        var cut = this.Workspace(draft);
        await cut.Find(".shop-arrange").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(draft.Items.Single(i => i.Definition == this._definitions[3]).ItemSlot, Is.Zero));
        Assert.That(draft.IsValid(), Is.True);
        await cut.Find(".shop-undo").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(draft.Items.Single(i => i.Definition == this._definitions[3]).ItemSlot, Is.EqualTo(82)));
        await cut.Find(".shop-redo").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(draft.IsValid() && draft.Items.Single(i => i.Definition == this._definitions[3]).ItemSlot == 0, Is.True));
    }

    /// <summary>Whole-category preview tells how many fit and bulk insertion is one undo step.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task WholeCategoryAndMultiSelectionAsync()
    {
        var draft = new ShopDraft(this._merchant.MerchantStore!);
        var cut = this.Workspace(draft);
        await cut.Find(".picker-category").ChangeAsync(new ChangeEventArgs { Value = "0" });
        await cut.Find(".shop-add-category").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(draft.Items.Count(), Is.EqualTo(2)));
        Assert.That(this._ui.JSInterop.Invocations.Last(i => i.Identifier == "window.confirm").Arguments[0]?.ToString(), Does.Contain("2 fit"));
        Assert.That(draft.IsValid(), Is.True);
        await cut.Find(".shop-undo").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(draft.Items, Is.Empty));
        await cut.Find(".picker-entry[data-item='0:1'] input").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find(".picker-entry[data-item='0:2'] input").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find(".shop-add-selected").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(draft.Items.Count(), Is.EqualTo(2)));
    }

    /// <summary>The list shows actual NPC spawn data, save shows counts and commits only the confirmed draft.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ListSaveSummaryAndDiscardAsync()
    {
        var cut = this._ui.Render<MerchantShops>();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("Town (0) · 12,34")));
        await cut.Find(".shop-open").ClickAsync(new MouseEventArgs());
        await AddAsync(cut, "0:1").ConfigureAwait(false);
        cut.WaitForAssertion(() => Assert.That(cut.Find(".shop-summary").TextContent, Does.Contain("Added 1")));
        Assert.That(this._merchant.MerchantStore!.Items, Is.Empty);
        await cut.Find(".shop-save").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(this._merchant.MerchantStore.Items.Count, Is.EqualTo(1)));
        cut.WaitForAssertion(() => Assert.That(cut.Find(".shop-notice").TextContent, Does.Contain("Shop saved. Added 1")));
        Assert.That(this._ui.JSInterop.Invocations.Last(i => i.Identifier == "window.confirm").Arguments[0]?.ToString(), Does.Contain("Save this shop? Added 1"));
        await AddAsync(cut, "0:2").ConfigureAwait(false);
        await cut.Find(".shop-discard").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(cut.Find(".shop-summary").TextContent, Does.Contain("Added 0")));
        Assert.That(this._merchant.MerchantStore.Items.Count, Is.EqualTo(1));
    }

    /// <summary>A failed reload keeps its localized error visible instead of silently hiding it.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ReloadFailureRemainsVisibleAsync()
    {
        var cut = this._ui.Render<MerchantShops>();
        cut.WaitForState(() => cut.FindAll(".shop-open").Count == 2);
        await cut.Find(".shop-open").ClickAsync(new MouseEventArgs());
        this._source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Unavailable fixture source."));
        await cut.Find(".shop-reload").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(cut.Find(".shop-error").TextContent, Is.EqualTo(HNMO.Web.AdminPanel.Properties.Resources.ShopEditorLoadingFailed)));
    }

    /// <summary>Copying opens an independent destination draft, never overwrites the source and stays undoable.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CopyShopCreatesIndependentDraftAsync()
    {
        this.Stock(this._merchant, this._definitions[0], 0);
        var cut = this._ui.Render<MerchantShops>();
        cut.WaitForState(() => cut.FindAll(".shop-copy-start").Count == 2);
        await cut.Find(".shop-copy-start").ClickAsync(new MouseEventArgs());
        await cut.Find("#shop-copy-target").ChangeAsync(new ChangeEventArgs { Value = this._other.GetId().ToString() });
        await cut.Find(".shop-copy button").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(cut.Find(".shop-summary").TextContent, Does.Contain("Added 1")));
        Assert.That(this._other.MerchantStore!.Items, Is.Empty);
        await cut.Find(".shop-undo").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(cut.Find(".shop-summary").TextContent, Does.Contain("Added 0")));
        Assert.That(this._merchant.MerchantStore!.Items.Count, Is.EqualTo(1));
    }

    /// <summary>Concurrent source changes and invalid layouts cannot be saved over another administrator's work.</summary>
    [Test]
    public void SaveRejectsConflictAndOverlap()
    {
        var draft = new ShopDraft(this._merchant.MerchantStore!);
        this.Stock(this._merchant, this._definitions[4], 0);
        var conflict = Assert.ThrowsAsync<AdminOperationException>(async () => await this._operations.SaveAsync(this._merchant.GetId(), draft).ConfigureAwait(false));
        Assert.That(conflict!.Kind, Is.EqualTo(AdminOperationErrorKind.Conflict));
        draft = new ShopDraft(this._merchant.MerchantStore!);
        draft.Storage.Items.Add(new ShopItemDraft { Definition = this._definitions[0], ItemSlot = 0 });
        draft.Record();
        var invalid = Assert.ThrowsAsync<AdminOperationException>(async () => await this._operations.SaveAsync(this._merchant.GetId(), draft).ConfigureAwait(false));
        Assert.That(invalid!.Kind, Is.EqualTo(AdminOperationErrorKind.InvalidInput));
    }

    /// <summary>Fresh definition dimensions are validated after references are resolved into the save context.</summary>
    [Test]
    public void SaveRechecksChangedDefinitionDimensions()
    {
        this.Stock(this._merchant, this._definitions[0], 0);
        var draft = new ShopDraft(this._merchant.MerchantStore!);
        draft.Items.Single().Definition = new HNMO.Persistence.BasicModel.ItemDefinition
        {
            Id = this._definitions[0].GetId(), Group = 0, Number = 1, Width = 2, Height = 2, MaximumItemLevel = 15,
        };
        this._definitions[0].Width = 8;
        draft.Storage.Items.Add(new ShopItemDraft { Definition = this._definitions[4], ItemSlot = 2 });
        draft.Record();
        Assert.That(draft.IsValid(), Is.True);
        var error = Assert.ThrowsAsync<AdminOperationException>(async () => await this._operations.SaveAsync(this._merchant.GetId(), draft).ConfigureAwait(false));
        Assert.That(error!.Kind, Is.EqualTo(AdminOperationErrorKind.Conflict));
        Assert.That(this._merchant.MerchantStore!.Items.Count, Is.EqualTo(1));
    }

    /// <summary>The help toggle and main controls have English and Vietnamese resources.</summary>
    /// <param name="language">The interface language.</param>
    /// <param name="help">The expected button.</param>
    /// <returns>The task.</returns>
    [TestCase("en", "How to use this page")]
    [TestCase("vi", "Hướng dẫn sử dụng trang này")]
    public async Task HelpIsLocalizedAsync(string language, string help)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        var cut = this._ui.Render<MerchantShops>();
        cut.WaitForState(() => cut.FindAll(".shop-help").Count == 1);
        Assert.That(cut.Find(".shop-help").TextContent, Does.Contain(help));
        await cut.Find(".shop-help").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.That(cut.Find("#shop-help-box").TextContent.Length, Is.GreaterThan(100)));
        Assert.That(cut.Find(".shop-help").GetAttribute("title"), Is.EqualTo(help));
    }

    private IRenderedComponent<ShopWorkspace> Workspace(ShopDraft draft) => this._ui.Render<ShopWorkspace>(p => p.Add(c => c.Draft, draft).Add(c => c.Definitions, this._definitions).Add(c => c.Classes, this._classes).Add(c => c.LookupContext, this._context));

    private static async Task AddAsync<TComponent>(IRenderedComponent<TComponent> cut, string key)
        where TComponent : IComponent
    {
        cut.WaitForState(() => cut.FindAll($".picker-entry[data-item='{key}'] .picker-add").Count == 1);
        await cut.Find($".picker-entry[data-item='{key}'] .picker-add").ClickAsync(new MouseEventArgs());
    }

    private ItemDefinition Definition(byte group, short number, string name, byte width, byte height, int required, string vietnamese)
    {
        var definition = this._context.CreateNew<ItemDefinition>();
        definition.Group = group;
        definition.Number = number;
        definition.Width = width;
        definition.Height = height;
        definition.MaximumItemLevel = 15;
        definition.Name = new LocalizedString(name).WithTranslation(CultureInfo.GetCultureInfo("vi"), vietnamese);
        var requirement = this._context.CreateNew<AttributeRequirement>();
        requirement.Attribute = Stats.Level;
        requirement.MinimumValue = required;
        definition.Requirements.Add(requirement);
        this._configuration.Items.Add(definition);
        return definition;
    }

    private MonsterDefinition Merchant(string name)
    {
        var merchant = this._context.CreateNew<MonsterDefinition>();
        merchant.Designation = name;
        merchant.ObjectKind = NpcObjectKind.PassiveNpc;
        merchant.MerchantStore = this._context.CreateNew<ItemStorage>();
        this._configuration.Monsters.Add(merchant);
        return merchant;
    }

    private void Stock(MonsterDefinition merchant, ItemDefinition definition, byte slot)
    {
        var item = this._context.CreateNew<Item>();
        item.Definition = definition;
        item.ItemSlot = slot;
        merchant.MerchantStore!.Items.Add(item);
    }
}
