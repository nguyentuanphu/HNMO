// <copyright file="EventAlertsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.EventAlerts;

using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging;

/// <summary>
/// Event alerts plug-in (OFF by default): dispatches browser Web Push notifications using host-generated
/// VAPID keys and posts scheduled event/boss reminders to an admin-configured Discord webhook URL.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Event Alerts (Web Push & Discord)", Description = "Sends browser Web Push alerts with host-generated VAPID keys and optional Discord channel posts for events/bosses (OFF by default).")]
[Guid("F18B3A27-4C9D-46E0-9B51-7D2C8E6A4F90")]
public sealed class EventAlertsPlugIn :
    IPlayerStateChangedPlugIn,
    IPeriodicTaskPlugIn,
    ISupportCustomConfiguration<EventAlertsConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(10) };
    private DateTime _lastTickUtc = DateTime.MinValue;

    /// <inheritdoc />
    public EventAlertsConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="EventAlertsPlugIn"/> for <paramref name="gameContext"/>, or <c>null</c> when disabled.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static EventAlertsPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<EventAlertsPlugIn>().FirstOrDefault();

    /// <summary>Dispatches a single <see cref="EventAlertCandidate"/> to Web Push subscribers and optional Discord webhook.</summary>
    /// <param name="config">Active configuration.</param>
    /// <param name="store">Subscription and log store.</param>
    /// <param name="candidate">Alert content.</param>
    /// <param name="logger">Optional logger (never logs webhook URLs or private keys).</param>
    /// <param name="httpClient">Optional HttpClient override for tests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recorded <see cref="EventAlertSentLog"/>.</returns>
    public static async Task<EventAlertSentLog> DispatchCandidateAsync(
        EventAlertsConfiguration config,
        EventAlertStore store,
        EventAlertCandidate candidate,
        ILogger? logger = null,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        var http = httpClient ?? SharedHttp;
        var (pub, priv) = store.EnsureVapidKeys(config.VapidPublicKey, config.VapidPrivateKey);

        var okCount = 0;
        var failCount = 0;
        foreach (var sub in store.ListSubscriptions())
        {
            if (!EventAlertsEngine.MatchesSubscription(sub, candidate.CategoryKey))
            {
                continue;
            }

            var isVi = string.Equals(sub.Language, "vi", StringComparison.OrdinalIgnoreCase);
            var payloadJson = JsonSerializer.Serialize(new
            {
                title = isVi ? candidate.TitleVi : candidate.TitleEn,
                body = isVi ? candidate.BodyVi : candidate.BodyEn,
                tag = candidate.CategoryKey,
                url = "/home.html#schedule",
            });

            try
            {
                var (ok, gone) = await WebPushCrypto.SendAsync(
                    http,
                    sub,
                    payloadJson,
                    config.VapidSubject,
                    pub,
                    priv,
                    cancellationToken).ConfigureAwait(false);
                if (ok)
                {
                    okCount++;
                }
                else
                {
                    failCount++;
                    if (gone)
                    {
                        store.RemoveSubscription(sub.Endpoint);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Security.Cryptography.CryptographicException)
            {
                failCount++;
                logger?.LogDebug("Web Push delivery skipped/failed for subscriber batch ({Reason}).", ex.GetType().Name);
            }
        }

        var discordOk = false;
        if (!string.IsNullOrWhiteSpace(config.DiscordWebhookUrl)
            && Uri.TryCreate(config.DiscordWebhookUrl.Trim(), UriKind.Absolute, out var webhookUri)
            && string.Equals(webhookUri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var content = FormatDiscordContent(config.DiscordLanguage, candidate);
                using var body = new StringContent(JsonSerializer.Serialize(new { content }), Encoding.UTF8, "application/json");
                using var resp = await http.PostAsync(webhookUri, body, cancellationToken).ConfigureAwait(false);
                discordOk = resp.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                logger?.LogWarning("Event alert Discord webhook post failed ({Reason}).", ex.GetType().Name);
            }
        }

        var log = new EventAlertSentLog
        {
            SentKey = candidate.SentKey,
            TitleEn = candidate.TitleEn,
            TitleVi = candidate.TitleVi,
            SentAtUtc = DateTime.UtcNow,
            PushSucceeded = okCount,
            PushFailed = failCount,
            DiscordPosted = discordOk,
        };
        store.RecordSent(log);
        return log;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new EventAlertsConfiguration();

    /// <inheritdoc />
    public void ForceStart() => this._lastTickUtc = DateTime.MinValue;

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var utcNow = DateTime.UtcNow;
        if (utcNow - this._lastTickUtc < TimeSpan.FromSeconds(30))
        {
            return;
        }

        this._lastTickUtc = utcNow;
        var cfg = this.Configuration ?? new EventAlertsConfiguration();
        if (!cfg.Enabled)
        {
            return;
        }

        var ttCfg = TimetablePlugIn.Get(gameContext)?.Configuration ?? new TimetableConfiguration();
        var sources = TimetableSourceCollector.CollectFromContext(gameContext, ttCfg, utcNow);
        var snapshot = TimetableEngine.BuildSnapshot(ttCfg, enabled: true, utcNow, sources, []);
        var store = EventAlertStore.Open(cfg.FileName);
        var due = EventAlertsEngine.CollectDueAlerts(cfg, store, snapshot, utcNow);
        if (due.Count == 0)
        {
            return;
        }

        var logger = gameContext.LoggerFactory.CreateLogger<EventAlertsPlugIn>();
        foreach (var candidate in due)
        {
            await DispatchCandidateAsync(cfg, store, candidate, logger).ConfigureAwait(false);
        }
    }

    private static string FormatDiscordContent(string? lang, EventAlertCandidate candidate)
    {
        var mode = (lang ?? "both").Trim().ToLowerInvariant();
        return mode switch
        {
            "en" => $"**{candidate.TitleEn}**\n{candidate.BodyEn}",
            "vi" => $"**{candidate.TitleVi}**\n{candidate.BodyVi}",
            _ => $"**{candidate.TitleEn}** / **{candidate.TitleVi}**\n{candidate.BodyEn}\n{candidate.BodyVi}",
        };
    }
}
