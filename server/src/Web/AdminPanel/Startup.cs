// <copyright file="Startup.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel;

using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using HNMO.DataModel.Entities;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Components;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared;
using HNMO.Web.Shared.Components.Modal;
using HNMO.Web.Shared.Models;
using HNMO.Web.Shared.Services;

/// <summary>
/// The startup class for the blazor app.
/// </summary>
/// <remarks>
/// This class is only used when running as all-in-one deployment.
/// </remarks>
public class Startup
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public Startup(IConfiguration configuration)
    {
        this.Configuration = configuration;
    }

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    /// <value>
    /// The configuration.
    /// </value>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// This method gets called by the runtime. Use this method to add services to the container.
    /// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new TimeSpanConverter()));

        services.AddAdminPanelControllers();

        services.AddToasts();
        services.AddScoped<ModalService>();
        services.AddScoped<IModalService>(sp => sp.GetRequiredService<ModalService>());

        services.AddSingleton<ILookupController, PersistentObjectsLookupController>();
        services.AddSingleton<ConfigurationSearchIndexCache>();
        services.AddScoped<IAdminReadModel, AdminReadModel>();
        services.AddAdminReadOperations();

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
        services.AddScoped<CreationPanelService>();

        services.AddScoped<IChangeNotificationService, ChangeNotificationService>();
        services.AddScoped<AdminUserManagementService>();
        services.AddScoped<CastleSiegeManagementService>();
        services.AddAdminPanelAuth(this.Configuration);
    }

    /// <summary>
    /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    /// </summary>
    /// <param name="app">The app builder.</param>
    /// <param name="env">The web host environment.</param>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);

            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
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

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();
            endpoints.MapControllers().RequireAuthorization();
            endpoints.MapAdminPanelAuthEndpoints();
        });
    }
}
