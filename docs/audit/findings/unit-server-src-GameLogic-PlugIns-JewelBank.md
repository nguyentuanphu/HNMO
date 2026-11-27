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

## Re-check 2026-11-27 (changes 8e98a00d..a108e846)

Commits read (every changed file read in full):

- 19e08d15 Economy write order: the bag is saved before the Jewel Bank is credited or a leftovers reward is kept; the stack pickup test checks the real cap
- 1e0121c8 Economy guards: equipment counts as one piece in combining leftovers; a Jewel Bank withdrawal gives one piece
- 478f9cf3 fix(plugins): fix stack deposits/consumption, receipt rollback, buff id collision, and partypass

Evidence:

- Deposit counts the whole stack (durability) within the capacity; the rest stays in the bag; withdrawal gives one piece per jewel (Durability 1) - fixed.
- Clear master: a failed save puts coins, receipt and Zen back - fixed.
- OPEN (HIGH): a failed bag save before the deposit is only logged and the bank is credited anyway; a crash before the next good save leaves the jewels in the saved bag AND in the bank (duplicate). A stop between the bag save and the JSON write loses the deposit. Fixed by PR #423 (open): the bank in the database, credited in the same transaction as the bag (SIGKILL-tested on PostgreSQL).
