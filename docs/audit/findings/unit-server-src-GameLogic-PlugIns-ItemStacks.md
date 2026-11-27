# Audit Findings — `server/src/GameLogic/PlugIns/ItemStacks`

- **Unit**: `server/src/GameLogic/PlugIns/ItemStacks` (4 files, 494 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `ItemStackConfiguration.cs`, `ItemStackKind.cs`, `ItemStackPlugIn.cs`, `ItemStackRules.cs`

## Rules & Comparison
- **Custom stacking override** (extends vanilla S6/S9 per-item durability stack caps to `255` per slot in both bag and vault):
  - **Free (`Limit = 255`)**: Potions/scrolls (`14:0-10`, `14:35-40`, `14:94`) and event tickets/keys (`14:17-19`, `13:16-18`, `13:29`, `13:49-51`, `14:28-29`, `14:101-111`).
  - **Coin-unlockable (`500` WCoin (C) each, `Limit = 255`)**: Jewels (`14:13-14`, `14:16`, `14:22`, `14:31`, `12:15`, `14:41-44`), Boxes/quest items (`14:11-12`, `14:20-21`, `14:23-26`, `12:32-34`, `14:32-34`, `14:45-52`, `14:54-59`, `14:85-93`, `14:95-100`), Materials (`13:14`, `13:32-36`, `13:52-53`, `12:26`, `14:53`).
- `ItemStackRules.Resolve`: stacks already having `item.Durability > 1` retain the stack limit even outside an explicit player context (`ItemStackRules.cs:125`).
- `SplitAsync` validates trade/store state, checks free inventory/vault space, and rolls back `Durability` cleanly if `SaveProgressAsync()` fails (`ItemStackPlugIn.cs:67-126`).

## Bugs Found
1. **Bug — `ItemStackPlugIn.cs:200-207` (Orphaned `CashShopBalanceReceipt` on failed unlock save)**:
   - In `UnlockAsync`, `CashShopBalanceReceipt.Record(...)` (`line 201`) stages a negative `StackUnlock` ledger entry before `player.SaveProgressAsync()`. If `SaveProgressAsync()` returns `false`, line 205 refunds `account`'s coin field, **but does not remove the staged `CashShopBalanceReceipt`** (unlike `AccountCoinShopCurrencyExchange.cs:72` which calls `RemoveAsync`). On the next successful save, a phantom deduction receipt persists without a matching coin deduction.

## Re-check 2026-11-27 (changes cf8c32b8..a108e846)

Commits read (every changed file read in full):

- 478f9cf3 fix(plugins): fix stack deposits/consumption, receipt rollback, buff id collision, and partypass

Evidence:

- ItemStackPlugIn unlock: same receipt rollback on a failed save - fixed.
