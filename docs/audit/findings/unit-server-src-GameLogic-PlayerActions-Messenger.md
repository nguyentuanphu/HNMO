# Unit Audit: `server/src/GameLogic/PlayerActions/Messenger`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (8 files read in full)

## Summary

- `AddFriendAction`, `AddResponseAction`, `ChangeOnlineStateAction`, `ChatRequestAction`, `DeleteFriendAction`, `LetterDeleteAction`, `LetterReadRequestAction`, `LetterSendAction`.
- `AddFriendAction` verifies `CharacterExistsAsync` before sending a friend request.
- `LetterSendAction` enforces `LetterSendPrice`, `MaximumLetters` (classic 50), and publishes `LetterReceivedEvent`.

## Findings / Defects

1. **`LetterReadRequestAction.ReadRequestAsync` off-by-one bounds check (`LetterReadRequestAction.cs:22`)**: Checks `Letters.Count < letterIndex` instead of `<=`, so requesting `letterIndex == Letters.Count` throws `ArgumentOutOfRangeException`.
2. **`LetterDeleteAction.DeleteLetterAsync` missing `-1` check (`LetterDeleteAction.cs:29-30`)**: `character.Letters.RemoveAt(letterIndex)` will throw `ArgumentOutOfRangeException` if `IndexOf(letter)` returns `-1`.
