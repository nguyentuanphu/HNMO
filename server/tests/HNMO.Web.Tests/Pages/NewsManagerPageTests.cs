// <copyright file="NewsManagerPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using Bunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.News;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Components;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.AdminAuth;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// Controller and bUnit page tests for <see cref="WebsiteNewsController"/> and <see cref="NewsManager"/>.
/// </summary>
[TestFixture]
public class NewsManagerPageTests
{
    /// <summary>Verifies the public website news endpoint filters future scheduled posts.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task WebsiteNewsControllerReturnsOnlyPublishedPosts()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"hnmo-news-ctrl-{Guid.NewGuid():N}.json");
        try
        {
            var provider = new InMemoryPersistenceContextProvider();
            using (var setup = provider.CreateNewContext())
            {
                var cfg = setup.CreateNew<GameConfiguration>();
                var plugin = setup.CreateNew<PlugInConfiguration>();
                plugin.TypeId = typeof(NewsPlugIn).GUID;
                plugin.IsActive = true;
                plugin.CustomConfiguration = JsonSerializer.Serialize(new NewsConfiguration
                {
                    AllowWebsite = true,
                    ShowLoginPanel = true,
                    FileName = statePath,
                });
                cfg.PlugInConfigurations.Add(plugin);
                await setup.SaveChangesAsync().ConfigureAwait(false);
            }

            var store = NewsStore.Open(statePath);
            var past = store.Upsert(new NewsPost
            {
                TitleEn = "Live Season Open",
                TitleVi = "Khai mo may chu",
                BodyEn = "Now open!",
                BodyVi = "Da mo cua!",
                PublishAtUtc = DateTime.UtcNow.AddMinutes(-10),
                IsPinned = true,
            });
            store.Upsert(new NewsPost
            {
                TitleEn = "Secret Future Post",
                PublishAtUtc = DateTime.UtcNow.AddDays(2),
            });

            var controller = new WebsiteNewsController(provider);
            var result = await controller.GetAsync() as ContentResult;
            Assert.That(result, Is.Not.Null);

            using var doc = JsonDocument.Parse(result!.Content!);
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
            var posts = doc.RootElement.GetProperty("posts");
            Assert.That(posts.GetArrayLength(), Is.EqualTo(1));
            Assert.That(posts[0].GetProperty("id").GetString(), Is.EqualTo(past.Id));
            Assert.That(posts[0].GetProperty("pinned").GetBoolean(), Is.True);
        }
        finally
        {
            if (File.Exists(statePath))
            {
                File.Delete(statePath);
            }
        }
    }

    /// <summary>Verifies <see cref="NewsManager"/> renders, toggles help, creates and deletes a post.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task NewsManagerRendersHelpAndManagesPostsAsync()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"hnmo-news-page-{Guid.NewGuid():N}.json");
        var prevCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        try
        {
            var provider = new InMemoryPersistenceContextProvider();
            GameConfiguration cfg;
            using (var setup = provider.CreateNewContext())
            {
                cfg = setup.CreateNew<GameConfiguration>();
                var plugin = setup.CreateNew<PlugInConfiguration>();
                plugin.TypeId = typeof(NewsPlugIn).GUID;
                plugin.IsActive = true;
                plugin.CustomConfiguration = JsonSerializer.Serialize(new NewsConfiguration
                {
                    FileName = statePath,
                });
                cfg.PlugInConfigurations.Add(plugin);
                await setup.SaveChangesAsync().ConfigureAwait(false);
            }

            using var ctx = new BunitContext();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.AddAdministratorAuthorization();

            var dataSource = new Mock<IDataSource<GameConfiguration>>();
            dataSource.Setup(d => d.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(cfg);

            var serverProvider = new Mock<IServerProvider>();
            serverProvider.SetupGet(s => s.Servers).Returns([]);

            var executor = AdminOperationTestServices.CreateExecutor();
            var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
            manager.RegisterPlugIn<IPlayerStateChangedPlugIn, NewsPlugIn>();
            var writes = new PluginWriteOperations(
                provider,
                dataSource.Object,
                manager,
                executor,
                new AdminChangeConfirmation(TimeProvider.System),
                new AdminWriteSettings { ReadOnly = false });

            ctx.Services.AddSingleton<IPersistenceContextProvider>(provider);
            ctx.Services.AddSingleton(dataSource.Object);
            ctx.Services.AddSingleton(serverProvider.Object);
            ctx.Services.AddSingleton(writes);
            ctx.Services.AddSingleton(new NewsAdminOperations(provider, serverProvider.Object, executor));
            ctx.Services.AddSingleton(Mock.Of<IToastService>());
            ctx.Services.AddSingleton<ILogger<NewsManager>>(NullLogger<NewsManager>.Instance);
            ctx.ComponentFactories.AddStub<Breadcrumb>();

            var cut = ctx.Render<NewsManager>();

            cut.WaitForState(() => cut.FindAll("#news-editor-card").Count > 0, TimeSpan.FromSeconds(5));
            cut.Find("button.btn-outline-info").Click();
            Assert.That(cut.FindAll("#news-help").Count, Is.EqualTo(1));

            cut.Find("#news-title-en").Change("Season Opening");
            cut.Find("#news-title-vi").Change("Khai Mo May Chu");
            cut.Find("#news-body-en").Change("Rates 5x");
            cut.Find("#news-body-vi").Change("Ti le x5");
            cut.Find("#news-save-post").Click();

            cut.WaitForState(() => cut.FindAll("#news-posts-table tbody tr").Count == 1, TimeSpan.FromSeconds(5));
            Assert.That(cut.Markup, Does.Contain("Season Opening"));

            cut.Find("#news-posts-table button.btn-outline-danger").Click();
            cut.WaitForState(() => cut.FindAll("#news-posts-table").Count == 0, TimeSpan.FromSeconds(5));
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevCulture;
            if (File.Exists(statePath))
            {
                File.Delete(statePath);
            }
        }
    }
}
