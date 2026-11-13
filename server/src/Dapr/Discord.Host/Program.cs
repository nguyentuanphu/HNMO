// <copyright file="Program.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HNMO.Dapr.Common;
using HNMO.Discord;
using HNMO.Interfaces;
using HNMO.PlugIns;
using HNMO.ServerClients;

var builder = DaprService.CreateBuilder("Discord", args);

var plugInConfigurations = new List<PlugInConfiguration>();

builder.Services
    .AddPeristenceProvider()
    .AddPlugInManager(plugInConfigurations)
    .AddManageableServerRegistry()
    .AddSingleton<IFriendServer, FriendServer>()
    .AddSingleton<IGuildServer, GuildServer>()
    .AddSingleton<IEventPublisher, EventPublisher>()

    // The game servers of the distributed deployment run in UTC, too.
    .AddDiscordIntegration(builder.Configuration.GetSection(DiscordSettings.SectionName), () => TimeZoneInfo.Utc);

var app = builder.BuildAndConfigure();

await app.WaitForDatabaseConnectionInitializationAsync().ConfigureAwait(false);
await app.Services.TryLoadPlugInConfigurationsAsync(plugInConfigurations).ConfigureAwait(false);

app.Run();
