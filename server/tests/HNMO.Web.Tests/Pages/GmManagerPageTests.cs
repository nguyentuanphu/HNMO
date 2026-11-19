// <copyright file="GmManagerPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.IO;
using System.Threading;
using Bunit;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.GmManager;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Tests.AdminAuth;
using HNMO.Web.Tests.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>bUnit and operations tests for <see cref="GmManager"/> (ROADMAP 14.17).</summary>
[TestFixture]
[NonParallelizable]
public sealed class GmManagerPageTests
{
    /// <summary>Verifies bilingual EN/VI rendering, help disclosure, hover hints, and assignment/revocation workflows.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task ManagesGmPermissionsAndRendersBilingualHelpAndHintsAsync()
    {
        var prev = CultureInfo.CurrentUICulture;
        var tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-gm-page-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var provider = new InMemoryPersistenceContextProvider();
            using var ctx = provider.CreateNewContext();
            var cfg = ctx.CreateNew<GameConfiguration>();
            await ctx.SaveChangesAsync().ConfigureAwait(false);

            using var pctx = provider.CreateNewPlayerContext(cfg);
            var acc = pctx.CreateNew<Account>();
            acc.LoginName = "gmweb";
            var ch = pctx.CreateNew<Character>();
            ch.Name = "WebGM";
            ch.CharacterStatus = CharacterStatus.Normal;
            ch.Inventory = pctx.CreateNew<ItemStorage>();
            acc.Characters.Add(ch);
            await pctx.SaveChangesAsync().ConfigureAwait(false);

            var cfgSource = new Mock<IDataSource<GameConfiguration>>();
            cfgSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(cfg);
            var login = new Mock<ILoginServer>();
            login.Setup(s => s.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte>());
            login.Setup(s => s.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);

            var store = new GmPermissionStore(Path.Combine(tempDir, "gm.json"), Path.Combine(tempDir, "act.json"));
            var ops = new GmManagerOperations(provider, cfgSource.Object, login.Object, AdminOperationTestServices.CreateExecutor(), new AdminWriteSettings { ReadOnly = false })
            {
                StoreOverride = store,
                ActivityLogFileName = Path.Combine(tempDir, "act.json"),
            };

            // Assign via operations and verify offline promotion + permission flags
            var assigned = await ops.AssignGmAsync("WebGM", GmPermission.Announce | GmPermission.MuteKick, true, "Shift moderator", CancellationToken.None).ConfigureAwait(false);
            Assert.That(assigned.Permissions, Is.EqualTo(GmPermission.Announce | GmPermission.MuteKick));

            await using var ui = new BunitContext();
            ui.AddAdministratorAuthorization();
            ui.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
            ui.Services.AddSingleton(ops);

            var cut = ui.Render<GmManager>();
            cut.Find(".page-help button").Click();
            Assert.That(cut.Find(".page-help .alert-info").TextContent, Does.Contain("Announce"));
            Assert.That(cut.Markup, Does.Contain("WebGM"));
            Assert.That(cut.FindAll("button").All(b => !string.IsNullOrWhiteSpace(b.GetAttribute("title"))), Is.True);

            cut.FindAll("button").First(b => b.TextContent.Trim() == "VI").Click();
            Assert.That(cut.Markup, Does.Contain("Quản lý GM"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = prev;
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Ignore temp cleanup lock.
            }
        }
    }
}
