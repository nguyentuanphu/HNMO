# Audit Findings — `server/src/GameLogic/Seasons`

- **Unit**: `server/src/GameLogic/Seasons` (1 file, 104 lines, checked on `2026-11-24`)
- **Files reviewed**: `SeasonSharedAccounts.cs`

## Rules & Behavior
- **`MirrorAsync` (`lines 35-72`)**: In Season 9 (`store != null`), mirrors login credentials, `SecurityCode`, `EMail`, `State`, `LanguageIsoCode`, `ChatBanUntil`, and `CoinBalances` from the shared Season 6.3 account store into the Season 9 DB before login verification, keeping per-season `Vault` and characters isolated. Records baseline `Baselines[shared.LoginName] = shared.Coins`.
- **`SyncCoinsAsync` (`lines 82-100`)**: On save, computes `current - baseline`, pushes delta to `store.AddCoinsAsync`, applies returned shared balances back to `account`, and updates `Baselines[account.LoginName]`. Bots and templates are skipped.

## Findings
1. **Concurrency/Race Risk — `SeasonSharedAccounts.cs:89-99`**:
   - Between reading `current = CoinBalances.Of(account)` (`line 89`), awaiting `store.AddCoinsAsync(...)` (`line 91`), and calling `shared.ApplyTo(account)` (`line 97`), if an in-game action modifies `account` coin fields concurrently with a background save, `shared.ApplyTo(account)` overwrites the in-flight in-game coin change and `Baselines` stores the pre-change total (losing that coin delta permanently). Needs either per-account serialization or re-applying the delta earned during `await`.
2. **Stale Baseline Edge Case — `SeasonSharedAccounts.cs:25,70,90`**:
   - If the Season 9 process restarts or a player reconnects without `MirrorAsync` re-running (or `MirrorAsync` runs twice on two concurrent logins), `Baselines` relies on in-process static dictionary state; safe as long as every login goes through `MirrorAsync` sequentially per account.

## Re-check 2026-11-27 (changes 4c299903..a108e846)

Commits read (every changed file read in full):

- 004b40c3 Coins: take back only what the shared store really took
- b6bbfd9d fix(economy): take the shared coin change back when the local save fails
- 39443f93 fix(gamelogic): fix anticheat speed/time, season coin race, party master exp gate, chaos jewel count, and gens quest reward

Evidence:

- SeasonSharedAccounts: per-login SemaphoreSlim, the sync reports a receipt with the delta the shared store really applied (GREATEST(0,..) clamps), an undo gives back only that and restores the baseline - fixed (#393, #407). Open: the receipt lives in memory only (a process stop between the shared and the local commit) - 2PC coordinator, Antigravity task B.10.
