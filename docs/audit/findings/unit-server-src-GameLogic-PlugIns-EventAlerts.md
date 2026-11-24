# Audit Unit: `server/src/GameLogic/PlugIns/EventAlerts`

- **Status**: Checked (on commit `4e6f9c9d`, 2026-11-24)
- **Files**: 9 files read in full (`EventAlertCandidate.cs`, `EventAlertsConfiguration.cs`, `EventAlertsEngine.cs`, `EventAlertSentLog.cs`, `EventAlertsPlugIn.cs`, `EventAlertStore.cs`, `EventAlertStoreData.cs`, `EventAlertSubscription.cs`, `WebPushCrypto.cs`).
- **Rule Category**: Custom HNMO feature — Web Push (RFC 8291/8292) & Discord event/boss alerts.

## Findings

1. **SSRF & resource bounds**: `EventAlertStore.UpsertSubscription` restricts endpoints to HTTPS default-port browser push services (`IsPushService`), caps subscriptions at `20000` and field lengths, and `EventAlertsPlugIn` prunes `404`/`410` endpoints.
2. **Deduplication & crypto**: `EventAlertsEngine.CollectDueAlerts` deduplicates by `SentKey` (`id:epochMinute:soon/start`), and `WebPushCrypto` implements P-256 VAPID JWT + `aes128gcm` encryption with zero external dependencies. No defects found.
