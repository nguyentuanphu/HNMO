// <copyright file="Program.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using HNMO.AdminPanel.Host;
using HNMO.Dapr.Common;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.EntityFramework;
using HNMO.Persistence.EntityFramework.AdminAuth;
using HNMO.PlugIns;
using HNMO.ServerClients;
using HNMO.Telemetry;
using HNMO.Web.AdminPanel;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Mcp;

var builder = DaprService.CreateBuilder("AdminPanel", args);

var plugInConfigurations = new List<PlugInConfiguration>();

var services = builder.Services;

services.AddPeristenceProvider(true)
    .AddPlugInManager(plugInConfigurations)
    .AddManageableServerRegistry()
    .AddTransient<IDictionary<int, IGameServer>>(s => s.GetRequiredService<IServerProvider>().Servers.OfType<IGameServer>().ToDictionary(server => server.Id))
    .AddSingleton<ILoginServer, LoginServer>()

    // E.g. to notify the Discord bot when an administrator removes the link of an account.
    .AddSingleton<IEventPublisher, EventPublisher>()
    .AddSingleton<IGameServerInstanceManager, DockerGameServerInstanceManager>()
    .AddSingleton<IConnectServerInstanceManager, DockerConnectServerInstanceManager>()
    .AddSingleton<IBackupService, BackupService>()
    .AddSingleton<IDatabaseSnapshotService, DatabaseSnapshotService>()
    .AddAdminUserRepository();

builder.AddAdminPanel();
builder.Services.AddAdminMcp(builder.Configuration);

var metricsRegistry = new MetricsRegistry();

// todo: add some meaningful metrics
builder.AddOpenTelemetryMetrics(metricsRegistry);

var app = builder.BuildAndConfigure(false);
app.UseStaticFiles();
app.UseRouting();
app.UseAdminMcpProtection();
app.UseAdminPanelAuth();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<HNMO.Web.AdminPanel.Components.App>()
    .AddInteractiveServerRenderMode();
app.MapAdminPanelAuthEndpoints();
app.MapAdminMcp();

await app.WaitForDatabaseConnectionInitializationAsync().ConfigureAwait(false);

await app.Services.TryLoadPlugInConfigurationsAsync(plugInConfigurations).ConfigureAwait(false);

app.Run();
