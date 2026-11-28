// <copyright file="LicencePickupService.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Startup;

using System.IO;
using System.Threading;
using HNMO.GameLogic;
using HNMO.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Picks up a bought licence: while one is awaited, the main program asks the hub on the backoff of
/// <see cref="DeveloperHub.PurchaseBackoff"/> (about 8 calls in 30 minutes; this loop ticks every 20 seconds and asks
/// only when a step is due). Every program (Season 9 and the Arena too) puts a changed licence file to use without a
/// restart; that check reads the file only.
/// </summary>
internal sealed class LicencePickupService : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(20);

    private readonly IHostLicenseProvider _licences;
    private readonly ILogger<LicencePickupService> _logger;
    private DateTime? _seenWriteUtc;

    /// <summary>Initializes a new instance of the <see cref="LicencePickupService"/> class.</summary>
    /// <param name="licences">The licence provider.</param>
    /// <param name="logger">The logger.</param>
    public LicencePickupService(IHostLicenseProvider licences, ILogger<LicencePickupService> logger)
    {
        this._licences = licences;
        this._logger = logger;
    }

    /// <summary>One round: the hub when a licence is awaited (main program), then a changed licence file.</summary>
    /// <param name="nowUtc">The time now.</param>
    /// <returns>The task.</returns>
    internal async Task RunOnceAsync(DateTime nowUtc)
    {
        if (!HnmoSeasonInstance.IsSecondary && DeveloperHub.Url.Length > 0 && DeveloperHub.ReadState().Purchase is not null)
        {
            var licence = await DeveloperHub.PollPurchaseAsync(nowUtc).ConfigureAwait(false);
            if (licence is not null)
            {
                await HubLicenceInstaller.InstallAsync(licence, this._licences, this._logger).ConfigureAwait(false);
            }
        }

        var settings = HostSettingsStore.TryLoad(HostSettingsStore.ResolveDefaultPath(AppContext.BaseDirectory));
        var path = this._licences.GetStatus(settings?.LicenseFilePath).ResolvedPath;
        DateTime? written = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        if (this._seenWriteUtc is null)
        {
            this._seenWriteUtc = written ?? DateTime.MinValue;
        }
        else if (written != this._seenWriteUtc)
        {
            this._seenWriteUtc = written ?? DateTime.MinValue;
            var applied = this._licences.Apply(settings?.LicenseFilePath);
            this._logger.LogInformation("HNMO licence: the licence file changed; up to {Players} players now.", applied.MaximumPlayers);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await this.RunOnceAsync(DateTime.UtcNow).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException)
                {
                    this._logger.LogDebug(ex, "HNMO licence: the licence couldn't be checked this round.");
                }

                await Task.Delay(Tick, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // stopping
        }
    }
}
