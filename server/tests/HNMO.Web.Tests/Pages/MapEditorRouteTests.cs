// <copyright file="MapEditorRouteTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Threading;
using Bunit;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Components;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Components;
using HNMO.Web.Shared.Components.MapEditor;
using HNMO.Web.Shared.Components.Modal;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>
/// Tests the map editor when it is entered from a bookmarked route.
/// </summary>
[TestFixture]
public class MapEditorRouteTests
{
    /// <summary>
    /// A stale bookmark shows a message and does not open the editor for another map.
    /// </summary>
    [Test]
    public void MissingMapShowsAnError()
    {
        using var context = CreateContext();

        var page = context.Render<EditMap>(p => p.Add(c => c.SelectedMapId, Guid.NewGuid()));

        page.WaitForState(() => page.FindAll("[role=alert]").Count == 1);
        Assert.That(page.FindAll(".editor-fixture"), Is.Empty);
    }

    /// <summary>
    /// The map editor landing route still opens when no explicit map was requested.
    /// </summary>
    [Test]
    public void DefaultRouteOpensTheEditor()
    {
        using var context = CreateContext();

        var page = context.Render<EditMap>();

        page.WaitForState(() => page.FindAll(".editor-fixture").Count == 1);
        Assert.That(page.FindAll("[role=alert]"), Is.Empty);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var configuration = setup.CreateNew<GameConfiguration>();
        configuration.Maps.Add(setup.CreateNew<GameMapDefinition>());
        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(data => data.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(configuration);
        source.Setup(data => data.GetContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IContext>());
        context.Services.AddSingleton(source.Object);
        context.Services.AddSingleton(new LoadingOverlayService());
        context.Services.AddSingleton(Mock.Of<IModalService>());
        context.Services.AddSingleton(Mock.Of<IToastService>());
        context.Services.AddSingleton(new AdminPolicyGuard(null!, null!));
        context.ComponentFactories.AddStub<PageHelp>();
        context.ComponentFactories.AddStub<Breadcrumb>();
        context.ComponentFactories.AddStub<MapEditor>(builder => builder.AddMarkupContent(0, "<div class=\"editor-fixture\"></div>"));
        return context;
    }
}
