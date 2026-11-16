// <copyright file="EventAlertsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.EventAlerts;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;

/// <summary>
/// Configuration for <see cref="EventAlertsPlugIn"/> (OFF by default via <see cref="IDisabledByDefault"/>):
/// browser Web Push notifications with host-generated VAPID keys plus optional Discord webhook channel posts.
/// </summary>
public sealed class EventAlertsConfiguration
{
    /// <summary>Gets or sets a value indicating whether event alerts are active.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the store file name/key under the configured state folder.</summary>
    [Display(Name = "State File")]
    public string FileName { get; set; } = "event-alerts-state.json";

    /// <summary>Gets or sets how many minutes before an event start/spawn an upcoming alert is sent (<c>0..30</c>).</summary>
    [Display(Name = "Lead Time (Minutes)")]
    public int LeadTimeMinutes { get; set; } = 5;

    /// <summary>Gets or sets a value indicating whether an alert is also dispatched right when an event opens or a boss spawns.</summary>
    [Display(Name = "Alert at Start")]
    public bool RemindAtStart { get; set; } = true;

    /// <summary>Gets or sets the VAPID subject URI (<c>mailto:...</c> or <c>https://...</c>).</summary>
    [Display(Name = "VAPID Subject")]
    public string VapidSubject { get; set; } = "mailto:admin@hnmo.local";

    /// <summary>Gets or sets the auto-generated URL-safe Base64 ECDSA P-256 uncompressed VAPID public key.</summary>
    [Display(Name = "VAPID Public Key")]
    public string VapidPublicKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the auto-generated URL-safe Base64 ECDSA P-256 private scalar (never exposed publicly).</summary>
    [Display(Name = "VAPID Private Key")]
    public string VapidPrivateKey { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional Discord webhook URL for posting upcoming/opening event alerts (kept secret).</summary>
    [Display(Name = "Discord Webhook URL")]
    public string DiscordWebhookUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the language posted to Discord (<c>"en"</c>, <c>"vi"</c>, or <c>"both"</c>).</summary>
    [Display(Name = "Discord Language")]
    public string DiscordLanguage { get; set; } = "both";

    /// <summary>Gets or sets the default selectable event category keys offered to subscribers.</summary>
    [Display(Name = "Available Alert Categories")]
    public List<string> AvailableCategories { get; set; } =
    [
        "blood_castle",
        "devil_square",
        "chaos_castle",
        "world_boss",
        "invasion",
    ];
}
