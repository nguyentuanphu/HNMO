// <copyright file="MonsterAmountSelectorTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Components;

using System.Globalization;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.Shared.Components.MapEditor;

/// <summary>
/// Tests for the <see cref="MonsterAmountSelector"/> of the map editor.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class MonsterAmountSelectorTests
{
    private CultureInfo _previousUiCulture = null!;
    private BunitContext _context = null!;
    private GameConfiguration _config = null!;
    private GameMapDefinition _map = null!;
    private Mock<IContext> _persistence = null!;

    /// <summary>
    /// Sets up a configuration with one map.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        this._config = new HNMO.Persistence.BasicModel.GameConfiguration();
        this._map = new HNMO.Persistence.BasicModel.GameMapDefinition { Number = 3, Name = "Noria" };
        this._config.Maps.Add(this._map);

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._config);
        this._persistence = new Mock<IContext>();
        this._persistence.Setup(c => c.CreateNew<PlugInConfiguration>(It.IsAny<object?[]>())).Returns(() => new HNMO.Persistence.BasicModel.PlugInConfiguration());
        this._persistence.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        source.Setup(s => s.GetContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(this._persistence.Object);

        this._context = new BunitContext();
        this._context.Services.AddSingleton(source.Object);
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
    /// The selector shows the choices and the current multiplier of the map.
    /// </summary>
    [Test]
    public void ShowsChoicesAndCurrentMultiplier()
    {
        var amounts = new MonsterAmountConfiguration();
        amounts.SetMultiplier(3, 2);
        this.AddPlugInConfiguration(amounts);

        var component = this.Render();

        Assert.That(component.FindAll("option").Select(o => o.TextContent), Is.EqualTo(new[] { "×0.5", "×1", "×2", "×3", "×4", "×5" }));
        Assert.That(component.Find("[data-testid=monster-amount-current]").TextContent, Is.EqualTo("Now: ×2"));
        Assert.That(component.Find("select").GetAttribute("value"), Is.EqualTo("2"));
    }

    /// <summary>
    /// Saving stores the multiplier in the plug-in's configuration and activates it; the map's spawn areas stay as they are.
    /// </summary>
    [Test]
    public void SaveStoresMultiplierInPlugInConfiguration()
    {
        var component = this.Render();
        Assert.That(component.Find("[data-testid=monster-amount-current]").TextContent, Is.EqualTo("Now: ×1"));

        component.Find("select").Change("3");
        component.Find("button").Click();

        var configuration = this._config.PlugInConfigurations.Single(c => c.TypeId == typeof(MonsterAmountPlugIn).GUID);
        Assert.That(configuration.IsActive, Is.True);
        Assert.That(configuration.GetConfiguration<MonsterAmountConfiguration>(null)!.GetMultiplier(3), Is.EqualTo(3));
        Assert.That(component.Find("[data-testid=monster-amount-current]").TextContent, Is.EqualTo("Now: ×3"));
        Assert.That(component.Find("[role=status]").TextContent, Does.Contain("Saved"));
        this._persistence.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // ×1 removes the map's entry again.
        component.Find("select").Change("1");
        component.Find("button").Click();
        Assert.That(configuration.GetConfiguration<MonsterAmountConfiguration>(null)!.Maps, Is.Empty);
    }

    /// <summary>
    /// With unsaved changes of the map editor, nothing is saved, so that they aren't saved by accident.
    /// </summary>
    [Test]
    public void UnsavedMapChangesBlockTheSave()
    {
        this._persistence.Setup(c => c.HasChanges).Returns(true);
        var component = this.Render();

        component.Find("select").Change("4");
        component.Find("button").Click();

        Assert.That(this._config.PlugInConfigurations, Is.Empty);
        Assert.That(component.Find("[role=status]").TextContent, Does.Contain("Save or undo"));
        this._persistence.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private IRenderedComponent<MonsterAmountSelector> Render()
    {
        return this._context.Render<MonsterAmountSelector>(parameters => parameters
            .Add(p => p.Map, this._map)
            .AddCascadingValue(this._persistence.Object));
    }

    private void AddPlugInConfiguration(MonsterAmountConfiguration amounts)
    {
        var configuration = new HNMO.Persistence.BasicModel.PlugInConfiguration { TypeId = typeof(MonsterAmountPlugIn).GUID, IsActive = true };
        configuration.SetConfiguration(amounts, null);
        this._config.PlugInConfigurations.Add(configuration);
    }
}
