---
title: Player State & Archival
description: Per-account and per-character row persistence in PostgreSQL and automated archival of finished data.
---

# Player State & Archival

Large per-account, per-character, and per-guild plug-in state (Missions, Gift Code claims, Top Races entries, Account Safety logs/locks, Onboarding progress, Guild progress/quests, Reset counters, Community entries, Offline whispers, and World Boss owed rewards) is stored row-by-row in PostgreSQL (`hnmo.player_feature_state`) instead of serializing a single huge `save/hnmo-state.json`.

- **Automatic migration**: On first start with an existing `save/hnmo-state.json`, per-player dictionaries are automatically copied into PostgreSQL rows and removed from `save/hnmo-state.json`. Only compact server-wide metadata remains in JSON.
- **Individual row updates**: Saving changes to one account or character only upserts/deletes the changed row(s) in `hnmo.player_feature_state`.
- **Automated archival**: Configure **Archive finished data after (days)** (default `30`) on **Rules & Events → Player state & archival** (`/state-retention`), or run **Archive expired rows now** to move expired gift-code claims and finished race/season rows into `hnmo.archived_feature_state`.
