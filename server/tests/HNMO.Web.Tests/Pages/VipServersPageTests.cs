// <copyright file="VipServersPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Components;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Tests.AdminAuth;
using HNMO.Web.Shared.Components;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// Operations and bUnit page tests for <see cref="ServerSettingsOperations"/> and plain-word <see cref="Servers"/> page (15.6).
/// </summary>
[TestFixture]
public class VipServersPageTests
{
    /// <summary>Verifies saving toggles <c>Stats.IsVip</c> requirement and updates Drop &amp; Zen rates.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task SaveTogglesVipRequirementAndServerRates()
    {
        var rateStore = GameServerRateStore.OpenDefault();
        var prev = rateStore.GetRates(88);
        try
        {
            var provider = new InMemoryPersistenceContextProvider();
            using (var setup = provider.CreateNewContext())
            {
                var gc = setup.CreateNew<GameConfiguration>();
                var sc = setup.CreateNew<GameServerConfiguration>();
                sc.MaximumPlayers = 100;
                var def = setup.CreateNew<GameServerDefinition>();
                def.ServerID = 88;
                def.Description = "Sub 1";
                def.ExperienceRate = 1.0f;
                def.PvpEnabled = true;
                def.ServerConfiguration = sc;
                def.GameConfiguration = gc;
                await setup.SaveChangesAsync().ConfigureAwait(false);
            }

            var serverProvider = new Mock<IServerProvider>();
            serverProvider.SetupGet(s => s.Servers).Returns([]);

            var ops = new ServerSettingsOperations(provider, serverProvider.Object, AdminOperationTestServices.CreateExecutor());
            var list = await ops.LoadAsync(default).ConfigureAwait(false);
            var item = list.Single(s => s.ServerId == 88);
            Assert.That(item.VipOnly, Is.False);

            item.VipOnly = true;
            item.ExperienceRate = 5.0f;
            item.DropRate = 2.5f;
            item.ZenRate = 3.0f;
            item.PvpEnabled = false;
            item.MaximumPlayers = 250;

            var saved = await ops.SaveAsync(item, default).ConfigureAwait(false);
            Assert.That(saved.VipOnly, Is.True);
            Assert.That(saved.ExperienceRate, Is.EqualTo(5.0f));
            Assert.That(saved.DropRate, Is.EqualTo(2.5f));
            Assert.That(saved.ZenRate, Is.EqualTo(3.0f));
            Assert.That(saved.PvpEnabled, Is.False);
            Assert.That(saved.MaximumPlayers, Is.EqualTo(250));

            saved.VipOnly = false;
            var cleared = await ops.SaveAsync(saved, default).ConfigureAwait(false);
            Assert.That(cleared.VipOnly, Is.False);
        }
        finally
        {
            rateStore.SetRates(88, prev.DropRate, prev.ZenRate);
        }
    }

    /// <summary>Verifies <see cref="Servers"/> renders plain-word VIP &amp; rate controls and help panel.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task ServersPageRendersPlainRateAndVipControlsAsync()
    {
        var prevCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        var rateStore = GameServerRateStore.OpenDefault();
        var prev = rateStore.GetRates(89);
        try
        {
            var provider = new InMemoryPersistenceContextProvider();
            using (var setup = provider.CreateNewContext())
            {
                var gc = setup.CreateNew<GameConfiguration>();
                var sc = setup.CreateNew<GameServerConfiguration>();
                sc.MaximumPlayers = 120;
                var def = setup.CreateNew<GameServerDefinition>();
                def.ServerID = 89;
                def.Description = "VIP Arena";
                def.ExperienceRate = 3.0f;
                def.PvpEnabled = true;
                def.ServerConfiguration = sc;
                def.GameConfiguration = gc;
                await setup.SaveChangesAsync().ConfigureAwait(false);
            }

            var serverProvider = new Mock<IServerProvider>();
            serverProvider.SetupGet(s => s.Servers).Returns([]);
            var ops = new ServerSettingsOperations(provider, serverProvider.Object, AdminOperationTestServices.CreateExecutor());

            using var ctx = new BunitContext();
            ctx.AddAdministratorAuthorization();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.Services.AddSingleton<IPersistenceContextProvider>(provider);
            ctx.Services.AddSingleton(serverProvider.Object);
            ctx.Services.AddSingleton(Mock.Of<IGameServerInstanceManager>());
            ctx.Services.AddSingleton(ops);
            ctx.Services.AddSingleton(Mock.Of<IToastService>());
            ctx.ComponentFactories.AddStub<Breadcrumb>();
            ctx.ComponentFactories.AddStub<TotalOnlineCounter>();
            ctx.ComponentFactories.AddStub<ServerItem>();

            var cut = ctx.Render<Servers>();
            cut.WaitForState(() => cut.FindAll("#servers-plain-table tbody tr").Count == 1, TimeSpan.FromSeconds(5));

            cut.Find("#servers-help-toggle").Click();
            Assert.That(cut.FindAll("#servers-help").Count, Is.EqualTo(1));

            cut.Find("tr[data-server-id='89'] .server-vip-toggle").Change(true);
            cut.Find("tr[data-server-id='89'] .server-drop-input").Change("2.0");
            cut.Find("tr[data-server-id='89'] .server-zen-input").Change("4.0");
            cut.Find("tr[data-server-id='89'] .server-save-btn").Click();

            cut.WaitForState(() => rateStore.GetRates(89).DropRate == 2.0f, TimeSpan.FromSeconds(5));
            Assert.That(rateStore.GetRates(89), Is.EqualTo((2.0f, 4.0f)));
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevCulture;
            rateStore.SetRates(89, prev.DropRate, prev.ZenRate);
        }
    }
}
