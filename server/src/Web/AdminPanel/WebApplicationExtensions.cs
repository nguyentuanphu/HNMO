// <copyright file="WebApplicationExtensions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel;

using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.Network.Analyzer;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.Persistence.Initialization.Captions;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.Telemetry;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Components;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Modal;
using HNMO.Web.Shared.Models;
using HNMO.Web.Shared.Services;

/// <summary>
/// Extensions for the <see cref="WebApplicationBuilder"/>.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Adds the map application to the web app.
    /// When using the DaprService, call the BuildAndConfigure-Method with the parameter to add Blazor.
    /// </summary>
    /// <param name="builder">The web application builder which should be configured.</param>
    /// <param name="includeMapApp">If set to <c>true</c>, the map app is included.</param>
    /// <returns>
    /// The web application builder.
    /// </returns>
    public static WebApplicationBuilder AddAdminPanel(this WebApplicationBuilder builder, bool includeMapApp = false)
    {
        // Ensure that DataInitialization plugins will get collected - for the setup functionality.
        _ = DataInitialization.Id;

        var services = builder.Services;

        var supportedCultures = CultureHelper
            .GetAvailableCultures<Properties.Resources>()
            .Select(culture => culture.Name)
            .ToArray();
        services.AddLocalization()
            .Configure<RequestLocalizationOptions>(o =>
            {
                o.AddSupportedCultures(supportedCultures);
                o.AddSupportedUICultures(supportedCultures);
            });
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        if (includeMapApp)
        {
            AdminPanelEnvironment.IsHostingEmbedded = true;
            AdminPanelEnvironment.IsGrafanaAvailable = builder.Configuration.IsOtlpExportConfigured();
        }

        services.AddAdminPanelControllers();

        services.AddToasts();

        services.AddMemoryCache();

        services.AddScoped<ModalService>();
        services.AddScoped<IModalService>(sp => sp.GetRequiredService<ModalService>());
        services.AddScoped<ILookupController, PersistentObjectsLookupController>();
        services.AddScoped<CreationPanelService>();

        services.AddAdminPanelAuth(builder.Configuration);

        services.AddSingleton<IDataSource<GameConfiguration>, GameConfigurationDataSource>();
        services.AddSingleton<IDataSource<Account>, AccountDataSource>();
        services.AddSingleton<ConfigurationSearchIndexCache>();
        services.AddScoped<IAdminReadModel, AdminReadModel>();
        services.AddAdminReadOperations();
        services.AddSingleton(new AdminLogAccess { LocalFilesAvailable = includeMapApp });
        services.AddSingleton<SetupService>();
        services.AddSingleton(new LoggingConfigurationService(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json")));
        services.AddSingleton(provider => new DiscordConfigurationService(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), provider.GetRequiredService<IConfiguration>()));
        services.AddScoped<DataUpdateService>();
        services.AddScoped<ConfigurationCaptionService>();
        services.AddScoped<AccountService>();
        services.AddScoped<IDataService<Account>>(serviceProvider => serviceProvider.GetService<AccountService>()!);
        services.AddScoped<IGuildMemberEnricher, CharacterGuildMemberEnricher>();
        services.AddScoped<GuildService>();
        services.AddScoped<IGuildService>(serviceProvider => serviceProvider.GetRequiredService<GuildService>());
        services.AddScoped<IDataService<GuildListItem>>(serviceProvider => serviceProvider.GetRequiredService<GuildService>());
        services.AddScoped<PlugInController>();
        services.AddScoped<IDataService<PlugInConfigurationViewItem>>(serviceProvider => serviceProvider.GetService<PlugInController>()!);
        services.AddScoped<ChatCommandController>();
        services.AddScoped<IDataService<ChatCommandViewItem>>(serviceProvider => serviceProvider.GetService<ChatCommandController>()!);
        services.AddScoped<AdminUserManagementService>();
        services.AddScoped<CastleSiegeManagementService>();
        services.AddSingleton<OnlinePlayerHistory>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<OnlinePlayerHistory>());
        services.AddScoped<IChangeNotificationService, ChangeNotificationService>();

        // The analyzers are only created when the network analyzer page is actually used,
        // which requires an IPacketCaptureService - that's only registered in the all-in-one
        // deployment.
        services.AddSingleton<PacketAnalyzerProvider>();
        services.AddScoped<NavigationHistory>();
        services.AddScoped<LoggedInAccountService>();
        services.AddScoped<LoadingOverlayService>();
        services.AddScoped<IDataService<LoggedInAccount>>(serviceProvider => serviceProvider.GetService<LoggedInAccountService>()!);
        services.AddScoped<OfflineAccountService>();
        services.AddScoped<IDataService<OfflineAccount>>(serviceProvider => serviceProvider.GetService<OfflineAccountService>()!);
        services.AddScoped<BotAccountService>();
        services.AddScoped<IDataService<BotAccount>>(serviceProvider => serviceProvider.GetService<BotAccountService>()!);

        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        return builder;
    }

    /// <summary>
    /// Configures the admin panel.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The configured web application.</returns>
    public static WebApplication ConfigureAdminPanel(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        app.UseStaticFiles();

        app.UseRouting();
        app.UseAdminPanelAuth();

        // The log files may contain sensitive information, so they are only served to authorized users.
        app.UseAuthorizedPath("/logs", AdminPolicies.Administrator);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "logs")),
            RequestPath = "/logs",
        });

        app.UseAntiforgery();

        app.MapStaticAssets();

        app.UseRequestLocalization();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.MapControllers().RequireAuthorization();
        app.MapAdminPanelAuthEndpoints();

        AdminPanelEnvironment.IsHostingEmbedded = true;

        return app;
    }

    /// <summary>
    /// Adds the controllers of the admin panel, e.g. its API and the downloads, with their conventions.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The builder of the controllers.</returns>
    internal static IMvcBuilder AddAdminPanelControllers(this IServiceCollection services)
    {
        return services.AddControllers(options =>
            {
                options.Filters.Add<HNMO.Web.AdminPanel.API.AdminOperationExceptionFilter>();
                options.Conventions.Add(new HNMO.Web.AdminPanel.API.AccountDownloadAuthorizationConvention());
            })
            .ConfigureApplicationPartManager(setup =>
                setup.FeatureProviders.Add(new GenericControllerFeatureProvider()));
    }
}
