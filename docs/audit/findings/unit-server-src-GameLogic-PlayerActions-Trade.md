# Unit: server/src/GameLogic/PlayerActions/Trade

Checked against: the Season 6 suite's common settings, server config, and commands config, and the Season 9 suite's common settings and commands config.

Scope: `server/src/GameLogic/PlayerActions/Trade` (`6` files, `681` lines).

## Summary

- Core player-to-player trade validation matches the Season 6 and Season 9 suites: `/requests off` toggle (`TradeRequestAction.cs:39`), `IsTradable` item check (`TradeButtonAction.cs:258`), inventory free-space check (`TradeButtonAction.cs:162`), and inventory money cap (`MaximumInventoryMoney`, `TradeButtonAction.cs:84-94,152`) preventing Zen overflow.
- HNMO adds Account Safety locked-item checks, 2nd-password trade gate, and Escrow cooldown item checks (`TradeButtonAction.cs:232-256`, `TradeRequestAction.cs:58`), plus two-phase `TradeContext` persistence before mutating player Zen (`TradeButtonAction.cs:175-186`).

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `TradeButtonAction.cs:84-94,152` | Cancels trade if `(long)receiver.Money + receivedMoney > MaximumInventoryMoney` (`int.MaxValue` fallback) | Suite player inventory Zen cap (`2,000,000,000`) | Same |
| `TradeButtonAction.cs:258` | Cancels trade if any offered item has `Definition.IsTradable == false` | Season 6 & Season 9 suites' common settings trade-block flags | Same |
| `TradeButtonAction.cs:162-168` | Fails trade with `FailedByFullInventory` if either partner lacks inventory space for offered items | Suite trade window space check | Same |
| `TradeMoneyAction.cs:32-40` | Validates `(long)player.Money + player.TradingMoney >= moneyAmount` before updating escrowed trade Zen | Suite trade Zen escrow | Same |
| `TradeRequestAction.cs:39` | Honors `/requests off` to refuse incoming trade requests | Season 6 & Season 9 suites' commands config (`/requests <on|off>`) | Same |
| `TradeButtonAction.cs:196-197,232-256`, `TradeRequestAction.cs:58` | Refuses locked items (`AccountSafetyPlugIn`), escrow cooldown items (`EscrowContractsPlugIn`), and partners needing 2nd-password verification; logs trade to safety & activity logs | `HNMO custom` account protection & audit extensions | HNMO custom |
| `BaseTradeAction.cs:48,76`, `TradeAcceptAction.cs:80`, `TradeCancelAction.cs` | Cancelling restores `BackupInventory.Money` and zeroes `TradingMoney` | Rollback anti-dupe guard | Same |
