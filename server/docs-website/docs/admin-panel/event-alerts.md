# Event Alerts: Web Push & Discord (`/event-alerts`)

Allows players to opt in to browser notifications for chosen events and world bosses (Web Push, no app installation needed), while optionally posting scheduled reminders to a Discord channel webhook.

- **Plug-in**: `EventAlertsPlugIn` (**OFF by default** via `IDisabledByDefault`)
- **Web Push Crypto (`WebPushCrypto`)**: Pure .NET implementation of RFC 8292 (VAPID ES256 P-256 JWT signing with host-generated EC keypair) and RFC 8291 (`aes128gcm` payload encryption). Private VAPID key and Discord webhook URL stay server-side and are never exposed publicly.
- **Website & In-Game Opt-In**:
  - Service Worker at `client/web/public/sw-alerts.js`
  - UI widget `client/web/src/ui/eventAlertsWidget.ts` opened from the Timetable (`H`) "Browser Alerts" button, `hnmo-event-alerts-open` event, or `mountEventAlertsWidget(container)` on `account.html`/`home.html`.
  - Website endpoints: `GET /website/event-alerts`, `POST /website/event-alerts/subscribe`, `POST /website/event-alerts/unsubscribe`.
