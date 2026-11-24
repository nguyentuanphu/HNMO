# Audit Findings: `server/src/GameServer` (39 Units)

Checked on: `2026-11-24`

## Units Covered
- `server/src/GameServer (files 1)` (24 files)
- `server/src/GameServer/MessageHandler (files 1)` (96 files) + 17 `MessageHandler/*` subdirectory units (`CashShop`, `CastleSiege`, `Character`, `Duel`, `Gens`, `Guild`, `Items`, `Login`, `Messenger`, `MiniGames`, `MuHelper`, `Party`, `Pet`, `PlayerShop`, `Quests`, `Trade`, `Vault`)
- `server/src/GameServer/RemoteView (files 1)` (54 files) + 19 `RemoteView/*` subdirectory units (`CashShop`, `CastleSiege`, `Character`, `Duel`, `Gens`, `Guild`, `Inventory`, `Login`, `Messenger`, `MiniGames`, `MuHelper`, `NPC`, `Party`, `Pet`, `PlayerShop`, `Quest`, `Trade`, `Vault`, `World`)

## Key Rules & Protocol Invariants Verified
- **Server & Context (`GameServer.cs`, `GameServerContext.cs`, `ClientVersionResolver.cs`, `DirectionExtensions.cs`, `GameServer.WebsiteAccounts.cs`)**:
  - `DirectionExtensions`: Maps `Direction` enum (`1`-based) to `0`-based packet bytes (`ToPacketByte`, `ParseAsDirection`).
  - `ClientVersionResolver`: Resolves 5-byte version headers to `ClientVersion` (default `6.3 English`).
  - `GameServer.WebsiteAccounts.cs`: Delegates website mission read/claim requests to `WebsiteMissionActions`.
- **Incoming Packet Handlers (`MessageHandler/*`)**:
  - `PacketHandlerPlugInContainer`: Dispatches by packet type byte (`span[0] % 2 == 1 ? 2 : 3`) to version-matched `IPacketHandlerPlugIn` / `ISubPacketHandlerPlugIn` implementations, delegating gameplay logic to `GameLogic` actions.
  - `HnmoPacketRateLimiter`: Token bucket (`Burst = 40`, `PerSecond = 20`) per player for group `0xF5` custom packets.
  - `CastleSiegeCommandHandlerPlugIn`: Bounds-checks `Team < CommandGroupCount` (7) and rejects unmapped command bytes above `Wait (2)` to prevent out-of-bounds writes on client buffers.
  - `CharacterWalkBaseHandlerPlugIn`: Distinguishes step-walking packets from zero-step stop-walk packets (`StopWalkAtAsync`) when a player attacks or casts mid-walk.
  - `AreaSkillAttackHandlerPlugIn`: Registers animation counters for `AreaSkillExplicitHits` and schedules 5x 1-second follow-up ticks for Pollution (`225`).
- **Outgoing View Serialization (`RemoteView/*`)**:
  - `ShowMessagePlugIn`: Splits UTF-8 messages at `241` bytes; routes `SlideNotice` to `SendSlideNoticeAsync` on Season > 0 (falling back to `GoldenCenter` on Season 0); prepends `"000000000"` on Season > 0.
  - `ItemSerializer`: 12-byte item wire encoding (`ItemCode`, `Options`, `Durability`, `Excellent/OptionHigh/512Bit/Fenrir`, `Ancient`, `Kind/380/Harmony`, `Socket1..5`), using `item.Durability()` (ceiling).
  - `AppearanceSerializer`: 18-byte preview charset encoding with `ConcurrentDictionary<IAppearanceData, byte[]>` cache invalidated via `AppearanceChanged`.
- **Verdict**: All 39 `GameServer` units are clean protocol adapters/serializers; no silent game rule regressions found.
