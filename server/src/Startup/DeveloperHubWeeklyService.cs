// <copyright file="DeveloperHubWeeklyService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Startup;

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.BugReports;
using HNMO.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Sends the server's weekly report to the HNMO developer's hub (owner's choice, 2026-11-20): once a week, with the
/// bug reports the server's owner forwarded, the licence, version, system, uptime, players (the most at once against the
/// limit, accounts and characters), crashes, the file check and the features on. Never player accounts, e-mails or IPs.
/// It also fetches a licence the developer issued for this server's request, and goes back to the free limit when the
/// developer withdrew the licence. Once, at the first setup, it sends the first-setup report (<see cref="HostSetupReport"/>).
/// </summary>
internal sealed class DeveloperHubWeeklyService : BackgroundService
{
    private static readonly TimeSpan FirstWait = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Every = TimeSpan.FromHours(1);

    private readonly IServerProvider _servers;
    private readonly IHostLicenseProvider _licences;
    private readonly ILogger<DeveloperHubWeeklyService> _logger;
    private readonly DateTime _startedUtc = DateTime.UtcNow;

    /// <summary>Initializes a new instance of the <see cref="DeveloperHubWeeklyService"/> class.</summary>
    /// <param name="servers">The servers of this program.</param>
    /// <param name="licences">The licence provider.</param>
    /// <param name="logger">The logger.</param>
    public DeveloperHubWeeklyService(IServerProvider servers, IHostLicenseProvider licences, ILogger<DeveloperHubWeeklyService> logger)
    {
        this._servers = servers;
        this._licences = licences;
        this._logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Only the main program sends (Season 9 and the Arena run in the same folder with the same licence).
        if (HnmoSeasonInstance.IsSecondary || DeveloperHub.Url.Length == 0)
        {
            return;
        }

        try
        {
            await Task.Delay(FirstWait, stoppingToken).ConfigureAwait(false);
            while (!stoppingToken.IsCancellationRequested)
            {
                await this.RunOnceAsync().ConfigureAwait(false);
                await Task.Delay(Every, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // stopping
        }
    }

    private static DateTime? TryStartTime()
    {
        try
        {
            return Process.GetCurrentProcess().StartTime.ToUniversalTime();
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private async Task RunOnceAsync()
    {
        try
        {
            await this.FetchLicenceAsync().ConfigureAwait(false);
            var state = DeveloperHub.ReadState();
            var now = DateTime.UtcNow;
            if (state.SetupReportedUtc is null && (state.SetupTriedUtc is not { } setupTried || now - setupTried >= TimeSpan.FromHours(6)))
            {
                await this.SendSetupAsync().ConfigureAwait(false);
            }

            var due = state.LastWeeklyUtc is not { } last || now - last >= TimeSpan.FromDays(DeveloperHub.WeeklyDays);
            var triedLately = state.LastWeeklyTryUtc is { } tried && now - tried < TimeSpan.FromHours(6);
            if (due && !triedLately)
            {
                await this.SendWeeklyAsync(state.LastWeeklyUtc).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "HNMO developer hub: the weekly report wasn't sent.");
        }
    }

    private async Task FetchLicenceAsync()
    {
        var (status, licence) = await DeveloperHub.FetchLicenceAsync().ConfigureAwait(false);
        if (status != "issued" || licence is null)
        {
            return;
        }

        var check = this._licences.Check(System.Text.Encoding.UTF8.GetBytes(licence));
        if (!check.IsValid)
        {
            this._logger.LogWarning("HNMO developer hub: the issued licence can't be used here: {Message}", check.Message);
            return;
        }

        var settings = HostSettingsStore.TryLoad(HostSettingsStore.ResolveDefaultPath(AppContext.BaseDirectory));
        var path = this._licences.GetStatus(settings?.LicenseFilePath).ResolvedPath;
        await File.WriteAllTextAsync(path, licence).ConfigureAwait(false);
        var applied = this._licences.Apply(settings?.LicenseFilePath);
        this._logger.LogInformation("HNMO developer hub: the licence for {Licensee} arrived and is in use (up to {Players} players).", applied.Licensee, applied.MaximumPlayers);
    }

    private async Task SendSetupAsync()
    {
        var contexts = this._servers.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context).ToList();
        var settings = HostSettingsStore.TryLoad(HostSettingsStore.ResolveDefaultPath(AppContext.BaseDirectory));
        var saveDir = Path.GetDirectoryName(HostSettingsStore.ResolveDefaultPath(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory;
        var (ok, error) = await DeveloperHub.SendSetupAsync(HostSetupReport.Build(contexts, settings, saveDir)).ConfigureAwait(false);
        if (ok)
        {
            this._logger.LogInformation("HNMO developer hub: the first-setup report was sent.");
        }
        else
        {
            this._logger.LogInformation("HNMO developer hub: the first-setup report wasn't taken ({Error}); it is tried again later.", error);
        }
    }

    private async Task SendWeeklyAsync(DateTime? since)
    {
        var contexts = this._servers.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context).ToList();
        var first = contexts.FirstOrDefault();
        var settings = HostSettingsStore.TryLoad(HostSettingsStore.ResolveDefaultPath(AppContext.BaseDirectory));
        var saveDir = Path.GetDirectoryName(HostSettingsStore.ResolveDefaultPath(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory;

        int accounts = 0, characters = 0;
        if (first is not null)
        {
            try
            {
                using var context = first.PersistenceContextProvider.CreateNewPlayerContext(first.Configuration);
                var statistics = await context.GetAccountStatisticsAsync(DateTime.UtcNow.AddDays(-7)).ConfigureAwait(false);
                (accounts, characters) = (statistics.AccountCount, statistics.CharacterCount);
            }
            catch (Exception ex)
            {
                this._logger.LogDebug(ex, "HNMO developer hub: the account numbers couldn't be read.");
            }
        }

        var crashes = CrashWatchStore.ListIncidents(saveDir).Count(i => since is null || i.OccurredAtUtc.UtcDateTime > since);
        var online = LicensedPlayerLimit.TotalCount;
        var plugins = new JsonArray();
        if (first is not null)
        {
            var known = first.PlugInManager.KnownPlugInTypes.ToDictionary(t => t.GUID, t => t.Name);
            foreach (var name in first.Configuration.PlugInConfigurations.Where(c => c.IsActive).Select(c => known.GetValueOrDefault(c.TypeId)).OfType<string>().Order())
            {
                plugins.Add(name);
            }
        }

        var bugReports = first is null ? null : BugReportsPlugIn.Get(first);
        var queued = bugReports?.QueuedForDeveloper() ?? [];
        var bugs = new JsonArray();
        foreach (var (_, message) in queued)
        {
            bugs.Add(message);
        }

        var weekly = new JsonObject
        {
            ["os"] = RuntimeInformation.OSDescription,
            ["uptimeHours"] = (int)(DateTime.UtcNow - (TryStartTime() ?? this._startedUtc)).TotalHours,
            ["players"] = new JsonObject
            {
                ["peak"] = DeveloperHub.TakePeak(online),
                ["limit"] = LicensedPlayerLimit.Maximum,
                ["online"] = online,
                ["accounts"] = accounts,
                ["characters"] = characters,
                ["graceStarted"] = DeveloperHub.ReadState().GraceStartedUtc?.ToString("O"),
            },
            ["health"] = new JsonObject { ["crashes"] = crashes, ["errors"] = DeveloperHub.TakeErrors(), ["integrity"] = DeveloperHub.Integrity },
            ["features"] = new JsonObject
            {
                ["season9"] = settings?.SeasonNineEnabled == true,
                ["arena"] = settings?.ArenaEnabled == true,
                ["expRate"] = first is null ? 0 : (int)Math.Round(first.Configuration.ExperienceRate),
                ["dropRate"] = 0,
                ["plugins"] = plugins,
            },
            ["bugs"] = bugs,
        };
        var (ok, withdrawn, error) = await DeveloperHub.SendWeeklyAsync(weekly).ConfigureAwait(false);
        if (!ok)
        {
            this._logger.LogInformation("HNMO developer hub: the weekly report wasn't taken ({Error}); it is tried again later.", error);
            return;
        }

        bugReports?.MarkSentToDeveloper(queued.Select(q => q.Id).ToList());
        this._logger.LogInformation("HNMO developer hub: the weekly report was sent ({Bugs} bug reports).", queued.Count);
        if (withdrawn)
        {
            LicensedPlayerLimit.Maximum = LicensedPlayerLimit.DefaultMaximum;
            LicensedPlayerLimit.Licensee = null;
            this._logger.LogWarning("HNMO developer hub: the developer withdrew this server's licence; up to {Players} players at once now.", LicensedPlayerLimit.DefaultMaximum);
        }
    }
}
