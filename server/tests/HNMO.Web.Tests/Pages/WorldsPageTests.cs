// <copyright file="WorldsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Persistence.Worlds;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// bUnit and API tests for the Admin Panel Worlds page (<c>/worlds</c>) and <see cref="WebsiteController.GetWorlds"/> (ROADMAP 15.7).
/// </summary>
[TestFixture]
public sealed class WorldsPageTests
{
    private BunitContext _context = null!;
    private CultureInfo _previousCulture = null!;
    private WorldRegistryService _registry = null!;

    /// <summary>Sets up bUnit services and world registry.</summary>
    [SetUp]
    public void SetUp()
    {
        this._previousCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._registry = new WorldRegistryService();
        this._registry.EnsureDefaultWorld([0]);
        var provider = new InMemoryPersistenceContextProvider();
        this._registry.RegisterWorldContextProvider("main", provider);

        var serverProvider = new Mock<IServerProvider>();
        serverProvider.SetupGet(p => p.Servers).Returns(Array.Empty<IManageableServer>());

        var ops = new WorldsOperations(
            this._registry,
            serverProvider.Object,
            provider,
            AdminOperationTestServices.CreateExecutor());

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton(ops);
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Restores culture and disposes bUnit context.</summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousCulture;
    }

    /// <summary>Verifies English and Vietnamese labels, help toggle, and world table rendering.</summary>
    [Test]
    public void RendersWorldsPageInEnAndViWithHelpToggle()
    {
        var cut = this._context.Render<Worlds>();
        Assert.That(cut.Markup, Does.Contain("Worlds &amp; Multi-Realm Databases"));
        Assert.That(cut.Markup, Does.Contain("hnmo_world_1"));

        cut.Find("button[title='Show or hide usage help']").Click();
        Assert.That(cut.Markup, Does.Contain("How to use this page:"));

        cut.Find("button[title='Chuyển giao diện sang tiếng Việt']").Click();
        Assert.That(cut.Markup, Does.Contain("Quản lý Thế giới"));
        Assert.That(cut.Markup, Does.Contain("Hướng dẫn sử dụng trang này:"));
    }

    /// <summary>Verifies <see cref="WebsiteController.GetWorlds"/> exposes grouped world snapshots.</summary>
    [Test]
    public void WebsiteControllerExposesGroupedWorlds()
    {
        var serverMock = new Mock<IGameServer>();
        serverMock.SetupGet(s => s.Id).Returns(0);
        serverMock.SetupGet(s => s.CurrentConnections).Returns(7);
        serverMock.SetupGet(s => s.MaximumConnections).Returns(100);
        serverMock.SetupGet(s => s.ServerState).Returns(ServerState.Started);

        var controller = new WebsiteController(
            new Dictionary<int, IGameServer> { [0] = serverMock.Object },
            this._registry);

        var result = controller.GetWorlds() as OkObjectResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));
    }
}
