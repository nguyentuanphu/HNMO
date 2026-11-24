# Unit Audit: `server/src/GameLogic/PlugIns/Escrow`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (28 files read in full; HNMO-custom escrow, Direct VND, Host VND, and Trade Credit system — no Season 6 / Season 9 legacy counterpart)

## Files Audited

- `ContractChatCommandPlugIn.cs`
- `EscrowConfiguration.cs`
- `EscrowContract.cs`
- `EscrowContractsPlugIn.cs`
- `EscrowContractStatus.cs`
- `EscrowCurrency.cs`
- `EscrowDirectVndDetails.cs`
- `EscrowDirectVndEngine.cs`
- `EscrowEngine.cs`
- `EscrowFeeBreakdown.cs`
- `EscrowFeeLedgerEntry.cs`
- `EscrowFeePayer.cs`
- `EscrowHostVndDetails.cs`
- `EscrowHostVndEngine.cs`
- `EscrowItemSnapshot.cs`
- `EscrowJewelLine.cs`
- `EscrowOwedDelivery.cs`
- `EscrowReputationEntry.cs`
- `EscrowStore.cs`
- `EscrowStoreData.cs`
- `EscrowTradeCooldownEntry.cs`
- `EscrowTradeCreditEngine.cs`
- `EscrowWebhookNonceEntry.cs`
- `TradeCreditAccountState.cs`
- `TradeCreditCashOutRequest.cs`
- `TradeCreditLedgerEntry.cs`
- `TradeCreditProceedLot.cs`
- `TradeCreditReconciliationReport.cs`

## Summary of Rules & Behavior

1. **In-game Escrow (`EscrowEngine`, `EscrowContractsPlugIn`, `ContractChatCommandPlugIn`)**:
   - Fee calculation (`ComputeFee`) clamps percentage at `25%` (`MaxFeePercent`), supports `Seller` / `Buyer` / `Split50_50` fee payers, and tracks host fees in `FeeLedger` (capped at 500 entries).
   - Enforces `MinAccountAgeDays = 3`, `PostEscrowTradeCooldownHours = 24`, banned item keys (defaults `13:20`), high-value confirmation thresholds, atomic `AcceptContractAtomic`, `ExpireDueContracts`, and `TakeOwedDelivery` / `RestoreOwedDelivery`.
   - `EscrowContractsPlugIn` validates bag items and held items via `HeldItems.HoldAsync` / `GiveAsync` and persists progress on contract creation (`player.SaveProgressAsync()`).
2. **Direct VND (`EscrowDirectVndEngine`)**:
   - Gated by `DirectVndComplianceAcknowledged`.
   - Redacts sensitive bank/webhook fields (`ProjectForViewer`, `WithoutOtherLogins`), validates PNG/JPG/WEBP receipt magic bytes, verifies HMAC-SHA256 webhooks with `CryptographicOperations.FixedTimeEquals`, `300s` timestamp skew, and replay protection (`MaxStoredNonces = 1000`).
3. **Host VND (`EscrowHostVndEngine`)**:
   - Gated by `HostVndComplianceAcknowledged`, uses `HNMO-XXXX` reference matching, idempotent `ConfirmHostPaymentReceived` via `PaymentTxId` & status check, `MarkPayoutPaid`, and `ResolveHostDispute`.
4. **Trade Credit (`EscrowTradeCreditEngine`)**:
   - Gated by `TradeCreditEnabled && TradeCreditComplianceAcknowledged`.
   - Separates non-cashoutable `GeneralUnits` (top-ups, 1-way coin conversion) from cash-sale `ProceedLots` subject to holding period (`TradeCreditCashOutHoldHours`), one-time verified bank binding (`RegisterVerifiedBankOnce`), daily/monthly cash-out caps, cash-out fees, and FIFO lot deduction.

## Findings / Observations

1. **Minor persistence omission on coin debit (`EscrowContractsPlugIn`)**: `TryDebitAccountCoins` in `AcceptContractFromPlayerAsync` and `ReserveDirectVndFromPlayerAsync` mutates in-memory account coins without calling `player.SaveProgressAsync()`, unlike contract creation.
2. **Ledger truncation vs reconciliation (`EscrowTradeCreditEngine.Reconcile`)**: `AddLedgerLocked` caps `data.TradeCreditLedger` at `MaxLedgerEntries = 5000` (dropping oldest entries), whereas `Reconcile` sums `TopUp`, `ConvertToCoins`, `CashOutPaid`, and `Fee` across `data.TradeCreditLedger`. Once more than 5000 ledger entries are created, `ExpectedCirculatingUnits` will drift from `ActualCirculatingUnits`.
3. **Store persistence check**: `EscrowStore` uses `HnmoStateStore` under `_store.SyncRoot`; verify `HnmoFeatureRowSync` dirty tracking when auditing `server/src/Persistence`.
