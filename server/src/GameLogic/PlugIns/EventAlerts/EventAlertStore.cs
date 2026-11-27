// <copyright file="EventAlertStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.EventAlerts;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// Thread-safe server store for Web Push subscriptions, host-generated VAPID keys, and alert logs.
/// </summary>
public sealed class EventAlertStore
{
    /// <summary>Section key inside <see cref="HnmoStateStore"/>.</summary>
    public const string SectionKey = "eventAlerts";

    /// <summary>The most subscriptions kept; anyone can subscribe, so the list has an end.</summary>
    public const int MaximumSubscriptions = 20000;

    private const int MaximumEndpointLength = 1024;

    private const int MaximumKeyLength = 256;

    private const int MaximumEventsPerSubscription = 32;

    private const int MaxLogs = 100;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private static readonly ConcurrentDictionary<string, EventAlertStore> Shared = new(StringComparer.Ordinal);

    /// <summary>
    /// The browsers' push services. Notifications are only ever sent to these: a subscription is sent in by
    /// anyone, and the server must not be made to call other addresses with it.
    /// </summary>
    private static readonly string[] PushServiceHosts =
    [
        "fcm.googleapis.com",
        "android.googleapis.com",
        "updates.push.services.mozilla.com",
        "web.push.apple.com",
    ];

    private static readonly string[] PushServiceHostSuffixes =
    [
        ".push.services.mozilla.com",
        ".notify.windows.com",
        ".push.apple.com",
    ];

    private readonly object _gate = new();

    private readonly HnmoStateStore? _stateStore;

    private readonly EventAlertStoreData _data;

    /// <summary>Initializes a new instance of the <see cref="EventAlertStore"/> class.</summary>
    /// <param name="filePath">Optional state file path or key.</param>
    public EventAlertStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._data = this.Load();
    }

    /// <summary>Opens or returns the shared store for <paramref name="fileName"/>.</summary>
    /// <param name="fileName">Configured state file name or path.</param>
    /// <returns>The store instance.</returns>
    public static EventAlertStore Open(string? fileName)
    {
        var key = string.IsNullOrWhiteSpace(fileName) ? string.Empty : fileName.Trim();
        var resolved = key.Length == 0
            ? string.Empty
            : (HnmoStateStore.ResolveStateFilePath(key) ?? Path.GetFullPath(key, AppContext.BaseDirectory));
        return Shared.GetOrAdd(resolved, p => new EventAlertStore(p.Length == 0 ? null : p));
    }

    /// <summary>Tells whether an address belongs to one of the browsers' push services.</summary>
    /// <param name="endpoint">The subscription's endpoint.</param>
    /// <returns><c>true</c>, if notifications may be sent to it.</returns>
    public static bool IsPushService(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var host = endpoint.IdnHost;
        return endpoint.IsDefaultPort
            && (PushServiceHosts.Contains(host, StringComparer.OrdinalIgnoreCase)
                || PushServiceHostSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Ensures a valid VAPID P-256 keypair exists, generating one when missing or empty.</summary>
    /// <param name="configuredPublic">Configured public key from plug-in settings.</param>
    /// <param name="configuredPrivate">Configured private key from plug-in settings.</param>
    /// <returns>The active public and private keys (Base64URL).</returns>
    public (string PublicKey, string PrivateKey) EnsureVapidKeys(string? configuredPublic = null, string? configuredPrivate = null)
    {
        lock (this._stateStore?.SyncRoot ?? this._gate)
        {
            if (!string.IsNullOrWhiteSpace(configuredPublic) && !string.IsNullOrWhiteSpace(configuredPrivate))
            {
                if (!string.Equals(this._data.VapidPublicKey, configuredPublic, StringComparison.Ordinal)
                    || !string.Equals(this._data.VapidPrivateKey, configuredPrivate, StringComparison.Ordinal))
                {
                    this._data.VapidPublicKey = configuredPublic.Trim();
                    this._data.VapidPrivateKey = configuredPrivate.Trim();
                    this.SaveLocked();
                }

                return (this._data.VapidPublicKey, this._data.VapidPrivateKey);
            }

            if (string.IsNullOrWhiteSpace(this._data.VapidPublicKey) || string.IsNullOrWhiteSpace(this._data.VapidPrivateKey))
            {
                var (pub, priv) = WebPushCrypto.GenerateVapidKeyPair();
                this._data.VapidPublicKey = pub;
                this._data.VapidPrivateKey = priv;
                this.SaveLocked();
            }

            return (this._data.VapidPublicKey, this._data.VapidPrivateKey);
        }
    }

    /// <summary>Forces regeneration of a fresh host VAPID P-256 keypair.</summary>
    /// <returns>The newly generated public and private keys.</returns>
    public (string PublicKey, string PrivateKey) RegenerateVapidKeys()
    {
        lock (this._stateStore?.SyncRoot ?? this._gate)
        {
            var (pub, priv) = WebPushCrypto.GenerateVapidKeyPair();
            this._data.VapidPublicKey = pub;
            this._data.VapidPrivateKey = priv;
            this.SaveLocked();
            return (pub, priv);
        }
    }

    /// <summary>Adds or updates a Web Push browser subscription.</summary>
    /// <param name="subscription">The subscription details.</param>
    /// <returns><c>true</c> if valid and stored.</returns>
    public bool UpsertSubscription(EventAlertSubscription subscription)
    {
        if (string.IsNullOrWhiteSpace(subscription.Endpoint)
            || string.IsNullOrWhiteSpace(subscription.P256dh)
            || string.IsNullOrWhiteSpace(subscription.Auth)
            || !Uri.TryCreate(subscription.Endpoint, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            || !IsPushService(uri)
            || subscription.Endpoint.Length > MaximumEndpointLength
            || subscription.P256dh.Length > MaximumKeyLength
            || subscription.Auth.Length > MaximumKeyLength)
        {
            return false;
        }

        lock (this._stateStore?.SyncRoot ?? this._gate)
        {
            if (this._data.Subscriptions.Count >= MaximumSubscriptions && !this._data.Subscriptions.ContainsKey(uri.AbsoluteUri))
            {
                return false;
            }

            this._data.Subscriptions[uri.AbsoluteUri] = new EventAlertSubscription
            {
                Endpoint = uri.AbsoluteUri,
                P256dh = subscription.P256dh.Trim(),
                Auth = subscription.Auth.Trim(),
                Language = string.Equals(subscription.Language, "vi", StringComparison.OrdinalIgnoreCase) ? "vi" : "en",
                Events = (subscription.Events ?? [])
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal)
                    .Take(MaximumEventsPerSubscription)
                    .ToList(),
                UpdatedAtUtc = DateTime.UtcNow,
                LastSentUtc = subscription.LastSentUtc,
            };
            this.SaveLocked();
            return true;
        }
    }

    /// <summary>Removes a Web Push browser subscription by endpoint.</summary>
    /// <param name="endpoint">Endpoint URL.</param>
    /// <returns><c>true</c> if removed.</returns>
    public bool RemoveSubscription(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        lock (this._stateStore?.SyncRoot ?? this._gate)
        {
            var removed = this._data.Subscriptions.Remove(endpoint.Trim());
            if (removed)
            {
                this.SaveLocked();
            }

            return removed;
        }
    }

    /// <summary>Returns a thread-safe snapshot of active push subscriptions.</summary>
    /// <returns>Subscription list.</returns>
    public IReadOnlyList<EventAlertSubscription> ListSubscriptions()
    {
        lock (this._stateStore?.SyncRoot ?? this._gate)
        {
            return this._data.Subscriptions.Values
                .Select(s => new EventAlertSubscription
                {
                    Endpoint = s.Endpoint,
                    P256dh = s.P256dh,
                    Auth = s.Auth,
                    Language = s.Language,
                    Events = [.. s.Events],
                    UpdatedAtUtc = s.UpdatedAtUtc,
                    LastSentUtc = s.LastSentUtc,
                })
                .ToList();
        }
    }

    /// <summary>Checks whether <paramref name="sentKey"/> has already been dispatched.</summary>
    /// <param name="sentKey">Deduplication key.</param>
    /// <returns><c>true</c> if already logged.</returns>
    public bool HasSent(string sentKey)
    {
        lock (this._stateStore?.SyncRoot ?? this._gate)
        {
            return this._data.Logs.Any(l => string.Equals(l.SentKey, sentKey, StringComparison.Ordinal));
        }
    }

    /// <summary>Appends a dispatch audit entry and trims history to <see cref="MaxLogs"/>.</summary>
    /// <param name="entry">Log entry.</param>
    public void RecordSent(EventAlertSentLog entry)
    {
        lock (this._stateStore?.SyncRoot ?? this._gate)
        {
            this._data.Logs.Insert(0, entry);
            if (this._data.Logs.Count > MaxLogs)
            {
                this._data.Logs.RemoveRange(MaxLogs, this._data.Logs.Count - MaxLogs);
            }

            this.SaveLocked();
        }
    }

    /// <summary>Returns recent dispatch audit logs (newest first).</summary>
    /// <returns>Log entries.</returns>
    public IReadOnlyList<EventAlertSentLog> ListLogs()
    {
        lock (this._stateStore?.SyncRoot ?? this._gate)
        {
            return [.. this._data.Logs];
        }
    }

    private EventAlertStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new EventAlertStoreData();
        }

        var loaded = this._stateStore.ReadSection<EventAlertStoreData>(SectionKey, JsonOptions) ?? new EventAlertStoreData();
        loaded.Subscriptions ??= new Dictionary<string, EventAlertSubscription>(StringComparer.Ordinal);
        loaded.Logs ??= [];
        return loaded;
    }

    private void SaveLocked()
    {
        this._stateStore?.WriteSection(SectionKey, this._data, JsonOptions);
    }
}
