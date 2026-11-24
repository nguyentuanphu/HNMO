# Unit Audit: `server/src/GameLogic/PlugIns/Vip`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (34 files read in full)

## Summary of Rules & Behavior

- **VIP Rules & Wallet (`VipRules`, `VipWallet`, `VipAdministration`, `VipDailyCoins`, `VipGameActions`, `VipPlugIn`, `VipStateStore`)**:
  - Disabled by default (`VipConfiguration.Enabled = false`), with Bronze/Đồng (5% XP/Zen, 10 daily coins), Silver/Bạc (10%, 20), Gold/Vàng (15%, 30), and 7/30/90-day offers in `WCoinC`.
  - `VipWallet`: serializes per-account operations via `AccountGates`, settles pending coin grants first (`VipPendingCoins.ApplyAsync`), persists `WalletIntents` before account mutation, commits debit + 3 owned `AccountAttributeGrant`s (`Stats.IsVip`, `Stats.BonusExperienceRate`, `Stats.MoneyAmountRate`) + `CashShopCoinGrant` receipt (`vip:{accountId:N}:{operationId:N}`) in one `save()` boundary, and supports cold DB reconstruction (`ReadActiveOwnedAsync`).
  - `VipDailyCoins`: idempotent per Sydney calendar day (`vipdaily:{accountId:N}:{yyyy-MM-dd}`), guards against `int.MaxValue` balance overflow.
  - `VipChat.Mark`: prepends `[VIP] ` after channel control prefixes (`@@`, `@`, `$`) when `Stats.IsVip > 0`.
- **Server Rates (`GameServerRateStore`)**:
  - Clamps per-server Drop & Zen multipliers to `[0.1f, 50.0f]` (default `1.0f`) and persists via `HnmoFeatureRowSync<ServerRatesEntry>` (`MarkDirty` + `SaveChanges`).

No game-rule defects found.
