// <copyright file="EventAlertSubscription.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.EventAlerts;

/// <summary>
/// Persisted browser Web Push subscription opted in by a player.
/// </summary>
public sealed class EventAlertSubscription
{
    /// <summary>Gets or sets the push service endpoint URL.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the client P-256 ECDH public key (Base64URL).</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Gets or sets the client authentication secret (Base64URL).</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>Gets or sets the subscriber language (<c>"en"</c> or <c>"vi"</c>).</summary>
    public string Language { get; set; } = "en";

    /// <summary>Gets or sets selected event category/entry keys (empty list matches all).</summary>
    public List<string> Events { get; set; } = [];

    /// <summary>Gets or sets when the subscription was created/updated in UTC.</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Gets or sets when a push alert was last delivered to this endpoint.</summary>
    public DateTime? LastSentUtc { get; set; }
}
