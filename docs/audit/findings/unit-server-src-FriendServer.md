# Audit Findings — `server/src/FriendServer`

- **Unit**: `server/src/FriendServer` (4 source files + `.csproj`, 690 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `FriendNotifierToGameServer.cs`, `FriendServer.cs`, `IFriendNotifier.cs`, `OnlineFriend.cs`

## Behavior Summary
- Tracks online players in `ConcurrentDictionary<string, OnlineFriend>` with bidirectional observer subscriptions (`Subscribe` / `Unsubscribe` guarded by `ReaderWriterLockSlim`).
- Supports `OfflineServerId` and `InvisibleServerId` (invisible is reported to friends as `OfflineServerId`, `OnlineFriend.cs:122`).
- Handles friend requests, mutual acceptance/deletion, letter forwarding, and 1:1 / invite chat room creation via `IChatServer`.

## Observations
1. **Blocking I/O under Read Lock — `OnlineFriend.cs:118-148`**:
   - `ChangeServer` holds `ReaderWriterLockSlim` read lock across `friend.OnNext(this)`, which synchronously blocks via `.AsTask().WaitWithoutException()` on `_gameServer.FriendOnlineStateChangedAsync(...)` (`line 124`). A slow publisher stalls concurrent `Subscribe`/`Unsubscribe` writers on that friend.
2. **Thread-safety of `_subscriptions` (`OnlineFriend.cs:91-97`)**: `_subscriptions` is a plain `List<Unsubscriber>` mutated during `AddSubscriptions` and read during `OnCompleted`.
