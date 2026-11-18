---
title: Economy Dashboard
---

# Economy Dashboard

The **Economy Dashboard** (`/economy-dashboard`) summarizes daily server wealth and item flows in Sydney calendar days backed by `hnmo.player_feature_state` (`economy.dailySnapshot`):

- **Server Totals & History Chart**: Daily totals of **Zen**, **Coins** (`WCoinC + WCoinP + GoblinPoints`), **Jewels** (including bundles), and valuable items across inventories, vaults, and optional `IEconomyExternalAssetProvider` hooks (Jewel Bank / Item Market).
- **Daily Drop vs. Sink Rates**: Aggregates valuable pickups, Chaos mixes, NPC Zen sinks, and NPC Zen faucets from the **Player Activity Log** (`/activity-log`).
- **Top Holders**: Ranks the top accounts with one-click links into `/activity-log?account=...`.
- **Anomaly Detection**: Automatically flags sudden day-over-day spikes in Global or per-account Zen, Coins, or Jewels exceeding configurable `%` and minimum delta thresholds.
