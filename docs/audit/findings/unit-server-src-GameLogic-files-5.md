# Audit Findings: `server/src/GameLogic (files 5)`

**Status**: Checked (all 13 files read in full)
**References**: Core engine mechanics (`SkillHitValidator`, `SkillList`, `Storage`, `SoccerGameMap`, `Walker`, `StateMachine`)

## Scope & Summary
Audited all 13 files (2,372 lines): `SkillHitValidator.cs`, `SkillList.cs`, `SoccerGameMap.cs`, `SocketSubOptionType.cs`, `State.cs`, `StateMachine.cs`, `Storage.cs`, `TemporaryItem.cs`, `TemporaryItemStorage.cs`, `TimeSpanExtensions.cs`, `Walker.cs`, `WalkingStep.cs`, `WalkingStepsExtensions.cs`.

## Rule & Bug Findings

1. **`Storage.TryTakeAllAsync` rollback can delete unrelated items in destination storage (`Storage.cs:222-251`)**:
   - Before attempting `this.AddItemAsync(item.Item)`, `item.Item.ItemSlot` holds the slot from `anotherStorage`.
   - When `this.AddItemAsync(item.Item)` returns `false` (inventory full), `item.Item.ItemSlot` is NOT cleared or changed — it still contains the old slot index.
   - In the `finally` rollback loop (lines 242–246), `await this.RemoveItemAsync(item.Item)` runs for *every* item in `itemsWithSlots`, including items that were never added to `this`!
   - `RemoveItemAsync` checks `this.ContainsSlot(item.ItemSlot)` and sets `this.ItemArray[slot] = null` and `SetItemUsedSlots(..., false)` unconditionally without checking `ReferenceEquals(this.ItemArray[slot], item)`, which can evict an unrelated existing item already sitting at that slot in `this`!

2. **`SkillList.SkillCount` byte truncation (`SkillList.cs:68`)**:
   - `public byte SkillCount => (byte)this._availableSkills.Count;` casts unchecked to `byte`. Safe in standard builds (<256 total skills), wraps if configured >255.

3. **Verified Core Mechanics**:
   - `SoccerGameMap.cs:96-112`: Battle Soccer awards `20` points per goal when `SoccerBall` crosses into `LeftGoal` / `RightGoal` and respawns the ball.
   - `SkillHitValidator.cs:20-21, 139-277`: Enforces `0x01..0x32` animation counter sequence, replay window (`SkillHitCounterReplayWindowMs`), 10s max animation-to-hit delay, and area-center slack validation via `ISkillAnimationCounterStrategy`.
   - `Walker.cs:114-160, 265-276, 302-345`: Prevents walk-restart speed exploits (`_lastStepTimestamp`, `_paceFirstStepAcrossWalks`), avoids cross-walk cancellation races (`StopOwnWalkAsync`), and compensates `Task.Delay` timer drift.

## Re-check 2026-11-27 (changes 96a69cae..a108e846)

Commits read (every changed file read in full):

- 49b959ac fix(gamelogic): fix Fenrir skill check order, spawn gate, drop stack lock, and storage rollback

Evidence:

- Storage.TryTakeAllAsync: on failure only the items actually added are removed again before every item goes back to the other storage; RemoveItemAsync ignores a slot that holds another item - fixed (an item could be removed from the wrong slot / lost on rollback).
