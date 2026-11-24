# Audit Findings — `server/src/GameLogic/PlugIns/JewelBank`

- **Unit**: `server/src/GameLogic/PlugIns/JewelBank` (11 files, 1,238 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `ClearMasterArguments.cs`, `ClearMasterCommandPlugIn.cs`, `JewelBankConfiguration.cs`, `JewelBankEngine.cs`
  - `JewelBankEntryConfig.cs`, `JewelBankLogEntry.cs`, `JewelBankPendingWithdrawal.cs`, `JewelBankPlugIn.cs`
  - `JewelBankStateWriter.cs`, `JewelBankStore.cs`, `JewelBankStoreData.cs`

## Rules & Behavior
- Account-scoped jewel bank (`IDisabledByDefault`) for 7 default jewels (`bless`, `soul`, `life`, `creation`, `chaos`, `harmony`, `guardian`, default `MaxCapacity = 10_000`), respecting `AccountSafetyPlugIn` item locks, optional `SafeZoneOnly` (`true`), `WithdrawZenFee`/`DepositZenFee` (`0`), withdrawal clamp `1..50` per call, pending website withdrawals claimed on `EnteredWorld`, and `/clearmaster` (`AllowClearMasterSkills = true`, `ClearMasterZen = 5_000_000`, `ClearMasterCoins = 0` `GoblinPoints`).

## Bugs Found
1. **Critical Bug — `JewelBankPlugIn.cs:188-198` (Stacked jewels lose up to 254 pieces on deposit)**:
   - `DepositAsync` always calls `engine.Deposit(login, cfg.Key, 1, "game", DateTime.UtcNow)` (hardcoded quantity `1`) regardless of `item.Durability`, and then deletes the entire item instance (`RemoveItemAsync` + `DeleteAsync`).
   - When `ItemStackPlugIn` (which is **enabled by default**) allows an unlocked account to stack jewels (`14:13-14`, `14:16`, `14:22`, `14:31`, `12:15` up to `255` per slot), depositing a stack of N jewels credits only `1` jewel to Jewel Bank and destroys the remaining `N - 1` jewels.
2. **Bug — `JewelBankPlugIn.cs:354-355` (ClearMaster does not verify `SaveProgressAsync` result)**:
   - `ClearMasterSkillsAsync` calls `await player.SaveProgressAsync()` without checking its boolean return, then unconditionally logs `engine.RecordClearMaster(...)` (`line 355`) and notifies success (`line 356`). Also any `CashShopBalanceReceipt` (`line 342`) is not rolled back if save fails.
