// <copyright file="Program.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HNMO.Dapr.Common;
using HNMO.Dapr.Common.HealthChecks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameServer;
using HNMO.GameServer.Host;
using HNMO.Interfaces;
using HNMO.PlugIns;
using HNMO.ServerClients;
using HNMO.Telemetry;
using HNMO.Web.Map;
using GameServer = HNMO.GameServer.GameServer;

// Ensure GameLogic and GameServer Assemblies are loaded
_ = HNMO.GameLogic.Rand.NextInt(1, 2);
_ = HNMO.GameServer.ClientVersionResolver.DefaultVersion;

var gameServerId = GameServerIdResolver.Determine();
var serviceName = $"GameServer{gameServerId}";
var builder = DaprService.CreateBuilder(serviceName, args);
var plugInConfigurations = new List<PlugInConfiguration>();

// Add services to the container.
var services = builder.Services;
services.AddSingleton<GameServer>()
    .AddSingleton<DaprAppTokenAuthorizationFilter>()
    .AddSingleton<AdministrationDeliveries>()
    .AddSingleton<IGameServer>(s => s.GetService<GameServer>()!)
    .AddSingleton<IList<IManageableServer>>(s => new List<IManageableServer> { s.GetService<GameServer>()! })
    .AddSingleton(s => s.GetService<GameServer>()!.Context)
    .AddSingleton<GameServerStatePublisher>()
    .AddSingleton<ConfigurationChangeMediator>()
    .AddSingleton<IConfigurationChangeMediator>(s => s.GetRequiredService<ConfigurationChangeMediator>())
    .AddSingleton<IConfigurationChangeMediatorListener>(s => s.GetRequiredService<ConfigurationChangeMediator>())
    .AddSingleton<ILoginServer, LoginServer>()
    .AddSingleton<IGuildServer, GuildServer>()
    .AddSingleton<IEventPublisher, EventPublisher>()
    .AddSingleton<IFriendServer, FriendServer>()
    .AddSingleton<GameServerInitializer>()
    .AddSingleton<IObservableGameServer, ObservableGameServerAdapter>()
    .AddPersistentSingleton<GameServerDefinition>(def => def.ServerID == gameServerId)
    .AddPeristenceProvider()
    .AddDatabaseHealthCheck()
    .AddPlugInManager(plugInConfigurations)
    .AddIpResolver(args)
    .AddNetworkObservation()
    .AddHostedService<GameServerHostedServiceWrapper>()
    .PublishManageableServer<IGameServer>();

// On shutdown, the game server saves and disconnects all players, which may take longer than the
// default of 30 seconds. The container runtime should wait longer than that, e.g. 60 seconds.
services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(50));

builder.AddMapApp();

var metricsRegistry = new MetricsRegistry();
metricsRegistry.AddNetworkMeters();
metricsRegistry.AddMeters(HNMO.GameLogic.Metrics.Meters);
builder.AddOpenTelemetryMetrics(metricsRegistry);

var app = builder.BuildAndConfigure(false);
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<HNMO.GameServer.Host.App>()
    .AddInteractiveServerRenderMode();
await app.WaitForDatabaseConnectionInitializationAsync().ConfigureAwait(false);

app.Run();
