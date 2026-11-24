# Audit Unit: `server/src/GameLogic/PlugIns/AccountSafety`

- **Status**: Checked (on commit `4e6f9c9d`, 2026-11-24)
- **Files**: 13 files read in full (`AccountSafetyConfiguration.cs`, `AccountSafetyEngine.cs`, `AccountSafetyPlugIn.cs`, `AccountSafetyStore.cs`, `AccountSafetyStoreData.cs`, `ItemLockResult.cs`, `PinCheck.cs`, `PinCheckResult.cs`, `SafetyAccountRecord.cs`, `SafetyAction.cs`, `SafetyItemLock.cs`, `SafetyLoginEntry.cs`, `SafetyTradeEntry.cs`).
- **Rule Category**: Custom HNMO feature — Second password (4-digit vault PIN), item locks, login/trade history.

## Findings

1. **PIN verification & lockout**: `AccountSafetyEngine.CheckPin` verifies BCrypt `$2` hashes (with legacy plaintext fallback), enforces `MaxWrongTries` (5) and `LockoutMinutes` (30) under `Store.Lock`, and `NormalizePin` normalizes 4-digit strings identically to numeric client PINs.
2. **Item locks & history bounds**: `Lock`/`Unlock` enforce `MaxLockedItems` (100) and `UnlockWaitHours` (24h). `RecordLogin` ignores unknown accounts on failed logins and caps histories (`MaxLoginsKept = 500`, `MaxTradesKept = 1000`). Note: `AccountSafetyEngine` calls `Store.Changed()` (`MarkFullScanPending`) rather than `MarkAccountDirty(login)` (functional, slightly more work per save). No defects found.
