# Audit Findings — `server/src/GameLogic/PlugIns/OfflineTrade`

- **Unit**: `server/src/GameLogic/PlugIns/OfflineTrade` (5 files, 374 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `OfflineTradeConfiguration.cs`, `OfflineTradeDecision.cs`, `OfflineTradeDenialReason.cs`
  - `OfflineTradeEngine.cs`, `OfflineTradePlugIn.cs`

## Rules & Behavior
- Custom `/offtrade` feature (`IDisabledByDefault`): requires alive character, not in mini-game, no active offline session, personal store open with `>= 1` item, safe zone (`SafeZoneOnly = true`), optional allowed map list, `MinimumLevel = 6` (matching personal shop unlock level), optional non-Zen coin/jewel currency gate (`AllowCoinCurrencies = true`), optional `OpenFeeZen = 0` (refunded if `StartOffTradeAsync` fails), `MaxDurationHours = 48`, and `StopWhenSoldOut = true`.
- Clean validation & rollback; no defects found.
