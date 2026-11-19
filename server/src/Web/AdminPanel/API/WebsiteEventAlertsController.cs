// <copyright file="WebsiteEventAlertsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic.PlugIns.EventAlerts;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Public website endpoints for opting in/out of browser Web Push event alerts (<c>api/website/event-alerts</c>).
/// Never exposes private VAPID keys or secret Discord webhook URLs.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/event-alerts")]
public sealed class WebsiteEventAlertsController(IPersistenceContextProvider persistence) : ControllerBase
{
    /// <summary>Gets public event alert settings including the VAPID public key.</summary>
    /// <returns>JSON configuration snapshot without secrets.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<EventAlertsConfiguration>(
            persistence,
            typeof(EventAlertsPlugIn),
            this.HttpContext.RequestAborted).ConfigureAwait(false);

        var cfg = saved ?? new EventAlertsConfiguration();
        if (!isActive || !cfg.Enabled)
        {
            return this.Ok(new
            {
                off = true,
                leadTimeMinutes = cfg.LeadTimeMinutes,
                categories = cfg.AvailableCategories ?? [],
                vapidPublicKey = string.Empty,
                subscriberCount = 0,
            });
        }

        var store = EventAlertStore.Open(cfg.FileName);
        var (pub, _) = store.EnsureVapidKeys(cfg.VapidPublicKey, cfg.VapidPrivateKey);
        return this.Ok(new
        {
            off = false,
            leadTimeMinutes = cfg.LeadTimeMinutes,
            remindAtStart = cfg.RemindAtStart,
            categories = cfg.AvailableCategories ?? [],
            vapidPublicKey = pub,
            subscriberCount = store.ListSubscriptions().Count,
        });
    }

    /// <summary>Registers or updates a browser Web Push subscription.</summary>
    /// <param name="request">Subscription payload from <c>PushManager.subscribe</c>.</param>
    /// <returns>Success or validation status.</returns>
    [HttpPost("subscribe")]
    public async Task<IActionResult> SubscribeAsync([FromBody] SubscribeAlertRequest? request)
    {
        var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<EventAlertsConfiguration>(
            persistence,
            typeof(EventAlertsPlugIn),
            this.HttpContext.RequestAborted).ConfigureAwait(false);
        var cfg = saved ?? new EventAlertsConfiguration();
        if (!isActive || !cfg.Enabled)
        {
            return this.StatusCode(503, new { error = "Event alerts are currently disabled." });
        }

        if (request is null
            || string.IsNullOrWhiteSpace(request.Endpoint)
            || string.IsNullOrWhiteSpace(request.P256dh)
            || string.IsNullOrWhiteSpace(request.Auth))
        {
            return this.BadRequest(new { error = "Invalid Web Push subscription payload." });
        }

        var store = EventAlertStore.Open(cfg.FileName);
        var stored = store.UpsertSubscription(new EventAlertSubscription
        {
            Endpoint = request.Endpoint,
            P256dh = request.P256dh,
            Auth = request.Auth,
            Language = request.Language ?? "en",
            Events = request.Events ?? [],
        });

        if (!stored)
        {
            return this.BadRequest(new { error = "Push endpoint must be a valid HTTPS URL." });
        }

        return this.Ok(new { ok = true, subscriberCount = store.ListSubscriptions().Count });
    }

    /// <summary>Unsubscribes a browser Web Push endpoint.</summary>
    /// <param name="request">Endpoint removal payload.</param>
    /// <returns>Success status.</returns>
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> UnsubscribeAsync([FromBody] UnsubscribeAlertRequest? request)
    {
        var (_, _, saved) = await PlugInSettings.ReadStoredAsync<EventAlertsConfiguration>(
            persistence,
            typeof(EventAlertsPlugIn),
            this.HttpContext.RequestAborted).ConfigureAwait(false);
        var cfg = saved ?? new EventAlertsConfiguration();
        var store = EventAlertStore.Open(cfg.FileName);
        var removed = store.RemoveSubscription(request?.Endpoint);
        return this.Ok(new { ok = true, removed });
    }

    /// <summary>Request payload for browser Web Push subscription registration.</summary>
    public sealed record SubscribeAlertRequest(
        string Endpoint,
        string P256dh,
        string Auth,
        string? Language,
        List<string>? Events);

    /// <summary>Request payload for browser Web Push endpoint removal.</summary>
    public sealed record UnsubscribeAlertRequest(string? Endpoint);
}
