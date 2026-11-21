// <copyright file="WebsiteMailServices.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.IO;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers account mail in both embedded and separate admin hosts.</summary>
internal static class WebsiteMailServices
{
    /// <summary>Uses the unified save file and existing player-state backend.</summary>
    /// <param name="services">The admin host services.</param>
    internal static void AddWebsiteAccountMail(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => new WebsiteMailSettingsStore(
            HnmoStateStore.Open(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName))!,
            provider.GetRequiredService<IDataProtectionProvider>()));
        services.TryAddSingleton(provider => new WebsiteEmailChallengeStore(
            HnmoPlayerStateRowStoreRegistry.GetBackend(provider.GetRequiredService<WebsiteMailSettingsStore>().StatePath),
            provider.GetRequiredService<IDataProtectionProvider>()));
        services.TryAddSingleton<IWebsiteConfirmationMail, SmtpWebsiteConfirmationMail>();
    }
}
