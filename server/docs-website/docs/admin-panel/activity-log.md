---
title: Player Activity Log
---

# Player Activity Log

The **Player Activity Log** page (`/activity-log`) lets administrators investigate in-game item and currency flows backed by individual PostgreSQL rows (`hnmo.player_feature_state` under feature `activityLog.entry`).

## Recorded Events

- **Trade**: Direct player-to-player trades with both accounts, characters, items, item GUIDs, and zen exchange.
- **Drop / Pickup**: Valuable floor drops and pickups (Excellent, Ancient, Socket, Jewels, Wings, Boxes, or items at or above `Min level`, configurable in settings).
- **Chaos Mix**: Chaos Machine / NPC crafting attempts and resulting items.
- **NPC Buy / NPC Sell**: Merchant purchases and sales.
- **Personal Store**: Player shop transactions (zen or custom item/coin currency).
- **Item Market**: Companion Item Market transactions recorded through `ActivityLogPlugIn.RecordExternal`.

## Filtering & Retention

- Filter by **Account** (matches both primary and partner accounts), **Character**, **Item name or GUID**, **Activity type**, and **Sydney time range**.
- Entries older than `Retention (days)` are automatically archived into `hnmo.archived_feature_state` on schedule.
