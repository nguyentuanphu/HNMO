// <copyright file="CharacterToolsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.Threading;
using Bunit;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Tests.AdminAuth;
using HNMO.Web.Tests.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>bUnit UI tests for <see cref="CharacterTools"/> (ROADMAP 14.15).</summary>
[TestFixture]
[NonParallelizable]
public sealed class CharacterToolsPageTests
{
    /// <summary>Verifies bilingual EN/VI toggle, help disclosure, hover hints, and character lookup warning when online.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task RendersHelpHintsAndOnlineRefusalBannerInEnglishAndVietnameseAsync()
    {
        var prev = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var provider = new InMemoryPersistenceContextProvider();
            using var ctx = provider.CreateNewContext();
            var cfg = ctx.CreateNew<GameConfiguration>();
            cfg.MaximumLevel = 400;
            var map = ctx.CreateNew<GameMapDefinition>();
            map.Number = 0;
            map.Name = "Lorencia";
            map.IsActive = true;
            cfg.Maps.Add(map);
            var cc = ctx.CreateNew<CharacterClass>();
            cc.Number = 0;
            cc.Name = "Dark Wizard";
            cfg.CharacterClasses.Add(cc);
            await ctx.SaveChangesAsync().ConfigureAwait(false);

            using var pctx = provider.CreateNewPlayerContext(cfg);
            var acc = pctx.CreateNew<Account>();
            acc.LoginName = "ann";
            var ch = pctx.CreateNew<Character>();
            ch.Name = "AnnWiz";
            ch.CurrentMap = map;
            ch.CharacterClass = cc;
            ch.Inventory = pctx.CreateNew<ItemStorage>();
            var lvl = pctx.CreateNew<StatAttribute>(Stats.Level, 50f);
            ch.Attributes.Add(lvl);
            acc.Characters.Add(ch);
            await pctx.SaveChangesAsync().ConfigureAwait(false);

            var cfgSource = new Mock<IDataSource<GameConfiguration>>();
            cfgSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(cfg);
            var login = new Mock<ILoginServer>();
            login.Setup(s => s.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte> { ["ann"] = 0 });
            var servers = new Mock<IServerProvider>();
            servers.SetupGet(s => s.Servers).Returns(new List<IManageableServer>());

            await using var ui = new BunitContext();
            ui.AddAdministratorAuthorization();
            ui.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
            ui.Services.AddSingleton(new CharacterToolsOperations(provider, cfgSource.Object, login.Object, servers.Object, AdminOperationTestServices.CreateExecutor(), new AdminWriteSettings { ReadOnly = false }));

            var cut = ui.Render<CharacterTools>();
            cut.Find(".page-help button").Click();
            Assert.That(cut.Find(".page-help .alert-info").TextContent, Does.Contain("offline"));

            cut.Find("#charToolsLookupName").Change("AnnWiz");
            cut.FindAll("button").First(b => b.TextContent.Contains("Load character", StringComparison.Ordinal)).Click();
            cut.WaitForState(() => cut.Markup.Contains("Unsafe edit refused", StringComparison.Ordinal) || cut.Markup.Contains("currently online", StringComparison.Ordinal), TimeSpan.FromSeconds(3));
            Assert.That(cut.FindAll("button").All(b => !string.IsNullOrWhiteSpace(b.GetAttribute("title"))), Is.True);

            cut.FindAll("button").First(b => b.TextContent.Trim() == "VI").Click();
            Assert.That(cut.Markup, Does.Contain("Công cụ nhân vật"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = prev;
        }
    }
}
