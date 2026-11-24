# Audit Unit: `server/src/GameLogic/PlugIns/ChatCommands (files 2)`

- **Status**: Checked (on commit `28ceb59c`, 2026-11-24)
- **Files**: 9 files read in full (`UnBanCharChatCommandPlugIn.cs`, `UnHideChatCommandPlugIn.cs`, `UnlockChatCommandPlugIn.cs`, `UnlockItemChatCommandPlugIn.cs`, `ValidValuesAttribute.cs`, `ValueReferenceAttribute.cs`, `VaultChatCommandPlugIn.cs`, `WalkMonsterChatCommand.cs`, `WorldChatChatCommandPlugIn.cs`).
- **Rule Category**: Chat commands (GM moderation, account safety PIN/item unlock, multi-vault `/vault`, `/world` chat).

## Findings

1. **Access & spam guards**: GM commands enforce `CharacterStatus.GameMaster`. `/world` checks `Account.ChatBanUntil`, `ChatSpamGuardPlugIn.CheckAsync`, `BannedWordsPlugIn.FilterChatAsync`, and `IChatMessageReceivedPlugIn` before broadcasting. `/vault` converts 1-based user page numbers to 0-based `SwitchVaultAsync(player, index - 1)`. No defects found.
