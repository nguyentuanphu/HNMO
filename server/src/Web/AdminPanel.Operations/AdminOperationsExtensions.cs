// <copyright file="AdminOperationsExtensions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.IO;
using HNMO.Persistence.AdminAuth;
using HNMO.Persistence.Worlds;
using HNMO.Web.AdminPanel.Operations.ItemExchange;
using HNMO.Web.AdminPanel.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers UI independent admin read operations.</summary>
public static class AdminOperationsExtensions
{
    /// <summary>Adds the read operations to the application.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection for further registration.</returns>
    public static IServiceCollection AddAdminReadOperations(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AdminChangeConfirmation>();
        services.AddScoped<ConfigurationWriteOperations>();
        services.AddScoped<ShopEditorOperations>();
        services.AddScoped<PluginWriteOperations>();
        services.AddScoped<VipAdminOperations>();
        services.AddScoped<ContentActivationOperations>();
        services.AddScoped<ConfigurationTaskOperations>();
        services.AddScoped<CharacterWriteOperations>();
        services.AddScoped<CharacterToolsOperations>();
        services.AddScoped<GmManagerOperations>();
        services.AddScoped<AccountReadOperations>();
        services.AddScoped<ServerReadOperations>();
        services.AddScoped<GuildReadOperations>();
        services.AddScoped<ConfigurationReadOperations>();
        services.AddScoped<PluginReadOperations>();
        services.AddScoped<MiniGameReadOperations>();
        services.AddScoped<ItemExchangeOperations>();
        services.TryAddScoped<IAdminOperationContext, UnconfiguredAdminOperationContext>();
        services.TryAddSingleton<IAdminAuditRepository, UnconfiguredAdminAuditRepository>();
        services.TryAddSingleton<AdminWriteSettings>();
        services.TryAddSingleton<AdminAuditSettings>();
        services.TryAddSingleton<AdminLogAccess>();
        services.TryAddSingleton(new LoggingConfigurationService(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json")));
        services.AddScoped<AdminOperationExecutor>();
        services.AddScoped<ServerWriteOperations>();
        services.AddScoped<PlayerWriteOperations>();
        services.AddScoped<LiveGameOperations>();
        services.AddScoped<PluginAdminOperations>();
        services.AddScoped<AccountWriteOperations>();
        services.AddScoped<CashShopOperations>();
        services.AddScoped<CastleSiegeOperations>();
        services.AddScoped<LogOperations>();
        services.AddScoped<AuditReadOperations>();
        services.TryAddSingleton(_ => new WorldRegistryService(ResolveWorldsFile()));
        services.AddScoped<WorldsOperations>();
        services.AddScoped<CashShopCoinService>();
        services.AddScoped<CashShopCoinDelivery>();
        services.AddScoped<HnmoSkillService>();
        services.AddScoped<BankTopUpService>();
        services.AddScoped<CastleSiegeManagementService>();
        services.AddScoped<MaintenanceToolsOperations>();
        services.AddScoped<JewelBankOperations>();
        services.AddScoped<EconomyDashboardOperations>();
        services.AddScoped<EventRewardsOperations>();
        services.AddScoped<ServerSettingsOperations>();
        services.AddScoped<GiftBoxAdminOperations>();
        services.AddScoped<BanAdminOperations>();
        services.AddScoped<NewsAdminOperations>();
        return services;
    }

    /// <summary>
    /// The worlds list lives with the other saved state (the working folder, the package's <c>save</c>), not in
    /// the program folder, which a new version of the package replaces. A list an older version wrote into the
    /// program folder is taken along once.
    /// </summary>
    private static string ResolveWorldsFile()
    {
        const string fileName = "hnmo-worlds.json";
        var saved = Path.GetFullPath(fileName);
        var old = Path.Combine(AppContext.BaseDirectory, fileName);
        try
        {
            if (!File.Exists(saved) && File.Exists(old) && !string.Equals(saved, old, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(old, saved);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The old list stays where it is and is used from there.
            return old;
        }

        return saved;
    }
}
