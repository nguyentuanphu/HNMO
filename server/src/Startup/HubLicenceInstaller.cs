// <copyright file="HubLicenceInstaller.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Startup;

using System.IO;
using HNMO.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>Saves a licence the hub issued and puts it to use at once (no restart).</summary>
internal static class HubLicenceInstaller
{
    /// <summary>Checks, saves and applies an issued licence.</summary>
    /// <param name="licence">The licence file's text.</param>
    /// <param name="licences">The licence provider.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The licence status in use, or null when it can't be used here.</returns>
    public static async Task<LicenseStatusInfo?> InstallAsync(string licence, IHostLicenseProvider licences, ILogger logger)
    {
        var check = licences.Check(System.Text.Encoding.UTF8.GetBytes(licence));
        if (!check.IsValid)
        {
            logger.LogWarning("HNMO developer hub: the issued licence can't be used here: {Message}", check.Message);
            return null;
        }

        var settings = HostSettingsStore.TryLoad(HostSettingsStore.ResolveDefaultPath(AppContext.BaseDirectory));
        var path = licences.GetStatus(settings?.LicenseFilePath).ResolvedPath;
        await File.WriteAllTextAsync(path, licence).ConfigureAwait(false);
        var applied = licences.Apply(settings?.LicenseFilePath);
        logger.LogInformation("HNMO developer hub: the licence for {Licensee} arrived and is in use (up to {Players} players).", applied.Licensee, applied.MaximumPlayers);
        return applied;
    }
}
