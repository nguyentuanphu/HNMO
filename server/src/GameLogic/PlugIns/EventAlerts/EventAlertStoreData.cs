// <copyright file="EventAlertStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.EventAlerts;

/// <summary>
/// Persistent server state for Web Push VAPID keys, subscriber endpoints, and sent alert deduplication.
/// </summary>
public sealed class EventAlertStoreData
{
    /// <summary>Gets or sets the host-generated VAPID public key (Base64URL).</summary>
    public string VapidPublicKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the host-generated VAPID private key (Base64URL).</summary>
    public string VapidPrivateKey { get; set; } = string.Empty;

    /// <summary>Gets or sets active browser push subscriptions keyed by endpoint.</summary>
    public Dictionary<string, EventAlertSubscription> Subscriptions { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets recent dispatched alert log entries.</summary>
    public List<EventAlertSentLog> Logs { get; set; } = [];
}
