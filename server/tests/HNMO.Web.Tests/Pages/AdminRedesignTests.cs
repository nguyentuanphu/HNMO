// <copyright file="AdminRedesignTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.Reflection;
using System.Threading;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.Network.Analyzer;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.Persistence.InMemory;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Components.Layout;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components;
using HNMO.Web.Shared.Services;
using HNMO.Web.Tests.AdminAuth;
using HNMO.Web.Tests.NetworkAnalyzer;

/// <summary>
/// Verification suite for ROADMAP 15.4–15.5 (Admin Navigation Catalog, Setup Checklist,
/// Ctrl+K Global Search, Favourites/Recent/Basic-Advanced, and Searchable Admin Guide).
/// </summary>
[TestFixture]
public class AdminRedesignTests
{
    private static readonly HashSet<string> UtilityExcludedRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "login",
        "error",
        "access-denied",
        "not-found",
    };

    /// <summary>Verifies every catalog route is unique and has complete EN/VI metadata.</summary>
    [Test]
    public void NavCatalogCoversEveryAdminPageOnceWithBilingualMetadata()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in AdminNavCatalog.Entries)
        {
            Assert.That(seen.Add(entry.Route), Is.True, $"Duplicate catalog route '{entry.Route}'");
            Assert.That(entry.TitleEn, Is.Not.Empty, entry.Route);
            Assert.That(entry.TitleVi, Is.Not.Empty, entry.Route);
            Assert.That(entry.DescriptionEn, Is.Not.Empty, entry.Route);
            Assert.That(entry.DescriptionVi, Is.Not.Empty, entry.Route);
            Assert.That(entry.Icon, Does.StartWith("oi-"), entry.Route);
        }

        // Reflection check: every static parameterless @page route in AdminPanel (except auth/error/dynamic) is catalogued.
        var pageTypes = typeof(AdminGuide).Assembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t)
                && t.GetCustomAttributes<RouteAttribute>(inherit: false).Any());

        foreach (var type in pageTypes)
        {
            foreach (var attr in type.GetCustomAttributes<RouteAttribute>(inherit: false))
            {
                if (attr.Template.Contains('{', StringComparison.Ordinal))
                {
                    continue;
                }

                var key = AdminNavigationPreferences.NormalizeRoute(attr.Template);
                if (UtilityExcludedRoutes.Contains(key))
                {
                    continue;
                }

                Assert.That(
                    AdminNavCatalog.FindByRoute(key),
                    Is.Not.Null,
                    $"Parameterless AdminPanel page '{attr.Template}' ({type.Name}) is missing from AdminNavCatalog.");
            }
        }
    }

    /// <summary>Verifies <see cref="AdminNavigationPreferences"/> toggles favourites, recents, and view mode.</summary>
    [Test]
    public void PreferencesTrackFavouritesRecentsAndBasicMode()
    {
        var prefs = new AdminNavigationPreferences();
        Assert.That(prefs.AdvancedMode, Is.True);

        prefs.SetAdvancedMode(false);
        Assert.That(prefs.AdvancedMode, Is.False);

        prefs.ToggleFavourite("/servers");
        Assert.That(prefs.IsFavourite("servers"), Is.True);
        prefs.ToggleFavourite("servers");
        Assert.That(prefs.IsFavourite("/servers"), Is.False);

        for (var i = 0; i < 8; i++)
        {
            prefs.RecordVisit($"route-{i}");
        }

        Assert.That(prefs.Recent.Count, Is.EqualTo(6));
        Assert.That(prefs.Recent[0], Is.EqualTo("route-7"));
    }

    /// <summary>Verifies <see cref="AdminGuide"/> renders recipes/reference articles and filters by query.</summary>
    [Test]
    public void AdminGuideRendersAndFiltersRecipesAsync()
    {
        var prev = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        try
        {
            using var ctx = new BunitContext();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.AddAdministratorAuthorization();
            ctx.ComponentFactories.AddStub<Breadcrumb>();

            var cut = ctx.Render<AdminGuide>();
            Assert.That(cut.FindAll("article[data-guide-id]").Count, Is.EqualTo(AdminGuideCatalog.Articles.Count));

            cut.Find("#admin-guide-help-toggle").Click();
            Assert.That(cut.FindAll("#admin-guide-help").Count, Is.EqualTo(1));

            cut.Find("#admin-guide-search").Input("Weekend 2x");
            Assert.That(cut.FindAll("article[data-guide-id]").Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(cut.Markup, Does.Contain("Weekend 2x EXP"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = prev;
        }
    }

    /// <summary>Verifies global <see cref="ConfigurationSearch"/> matches pages &amp; guide articles in EN and VI.</summary>
    [Test]
    public void GlobalSearchMatchesPagesAndAdminGuide()
    {
        var prev = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
        try
        {
            using var ctx = new BunitContext();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            var provider = new Mock<IMigratableDatabaseContextProvider>();
            provider.Setup(p => p.CanConnectToDatabaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var plugInManager = new PlugInManager(null, NullLoggerFactory.Instance, null, null);
            var dataSource = new Mock<IDataSource<GameConfiguration>>();
            var setupService = new SetupService(provider.Object, plugInManager);
            var cache = new ConfigurationSearchIndexCache(
                provider.Object,
                dataSource.Object,
                NullLogger<ConfigurationSearchIndexCache>.Instance,
                setupService);

            ctx.Services.AddSingleton(cache);
            ctx.Services.AddSingleton(setupService);
            ctx.Services.AddSingleton<NavigationHistory>();
            ctx.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ConfigurationSearch>>(NullLogger<ConfigurationSearch>.Instance);
            ctx.Renderer.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));

            var cut = ctx.Render<ConfigurationSearch>();
            cut.Find("#admin-global-search-input").Focus();
            cut.Find("#admin-global-search-input").Input("Cẩm nang");
            cut.WaitForState(() => cut.FindAll(".configuration-search__result").Count > 0, TimeSpan.FromSeconds(5));

            Assert.That(cut.Markup, Does.Contain("Cẩm nang quản trị"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = prev;
        }
    }
}
