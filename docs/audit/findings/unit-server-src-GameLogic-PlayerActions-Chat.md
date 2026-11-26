# Audit Findings: `server/src/GameLogic/PlayerActions/Chat`

**Status**: Checked (all 12 files read in full, 583 lines)
**References**: [`common-server-and-character-rules-s6-s9.md`](../references/common-server-and-character-rules-s6-s9.md), [`guild-and-alliance-s6-s9.md`](../references/guild-and-alliance-s6-s9.md), [`ledger-combat-items-events-2026-11-22.md`](ledger-combat-items-events-2026-11-22.md)

## Scope Covered
- `ChatMessageAction.cs` (106 lines), `BannableChatMessageBaseProcessor.cs` (80 lines), `ChatMessageCommandProcessor.cs` (88 lines), `ChatMessageWhisperProcessor.cs` (53 lines), `ChatMessageGlobalNotificationProcessor.cs` (42 lines), `ChatMessageGensProcessor.cs` (41 lines), `ChatMessageAllianceProcessor.cs` (35 lines), `ChatMessageGuildProcessor.cs` (34 lines), `ChatMessagePartyProcessor.cs` (32 lines), `ChatMessageSentExtensions.cs` (29 lines), `ChatMessageNormalProcessor.cs` (21 lines), `IChatMessageProcessor.cs` (19 lines).

## Rule & Suite Comparison

| File & Line | Rule / Behavior | Suite / Reference Value | Verdict |
|---|---|---|---|
| `ChatMessageAction.cs:22-30,77-94` | Reverse-sorted prefix matching (`@@` before `@`): `~` Party, `@` Guild, `@@` Alliance, `$` Gens, `!` GlobalNotification, `/` Command; `whisper` flag overrides prefix; else Normal. | Matches classic MU client chat prefixes (`~`, `@`, `@@`, `$`, `!`, `/`) in S6/S9. | Same |
| `ChatMessageAction.cs:63-66` | Logs warning when non-whisper packet `playerName` differs from `SelectedCharacter.Name`; downstream processors emit `sender.SelectedCharacter.Name` anyway. | Prevents impersonation even if a tampered packet changes header name. | Sound |
| `BannableChatMessageBaseProcessor.cs:24-36,54-79` | Enforces active moderation mute (`BanManagerService.FindActiveChatMute`), account `ChatBanUntil` (formatted in Sydney time, EN/VI), `ChatSpamGuardPlugIn.CheckAsync`, and `BannedWordsPlugIn.FilterChatAsync` before subclass delivery. | S6/S9 rely on GM chat block (`ChatBanTime`) without unified spam/banned-word pipeline; HNMO adds mute notice packet, spam throttling, and profanity filter. | HNMO custom (superset of S6/S9 chat ban) |
| `ChatMessageCommandProcessor.cs:37-87` | Looks up `IChatCommandPlugIn`, enforces `MinCharacterStatusRequirement`, blocks player commands in Arena (`ArenaRules.Action.Command`), blocks player commands when `BackupInventory != null` (trade/chaos mix open), and enforces per-command GM RBAC (`GmPermissionStore.AuthorizeAndAudit`). | S6/S9 allow standard commands unless restricted; HNMO adds trade/crafting dupe guard, Arena lock, and granular GM command auditing. | HNMO custom safeguards |
| `ChatMessageWhisperProcessor.cs:21-51` | Resolves receiver on local game server -> cross-server `OnlineCharacters` -> `CrossSeasonFriendsPlugIn` (S6/S9/Arena bridge) -> `OfflineWhisperPlugIn` storage; records `ChatLogPlugIn` and notifies `IChatMessageSentPlugIn` (which records local chat log). | S6/S9 only whisper online characters on local/linked GS; offline queue & cross-season whisper are HNMO additions. | Same + HNMO extensions |
| `ChatMessageAllianceProcessor.cs:26-33` & `ChatMessageGuildProcessor.cs:26-32` | Require `sender.GuildStatus != null` and `EventPublisher`; broadcast across cluster via `GuildServer`. | Matches S6/S9 `ServerGroupGuildChatting = 1`, `ServerGroupUnionChatting = 1`. | Same |
| `ChatMessageGensProcessor.cs:21-39` | Requires non-`None` Gens membership and active `GensFeaturePlugIn` config; broadcasts to same-Gens players on the game server. | Matches S6/S9 same-Gens chat (`$` prefix). | Same |
| `ChatMessageGlobalNotificationProcessor.cs:20-41` | Requires `CharacterStatus >= CharacterStatus.GameMaster`; filters via `BannedWordsPlugIn` and broadcasts `SendGlobalNotificationAsync`. | Matches S6/S9 `!` GM notice channel. | Same |
| `ChatMessageNormalProcessor.cs:17-20` & `ChatMessagePartyProcessor.cs:26-30` | Normal broadcasts to world observers; Party broadcasts to `sender.Party` members. | Matches S6/S9 local & party chat. | Same |

## Bugs / Open Issues
- None critical (`#306` already fixed `/post` missing from `ChatLogPlugIn`). Note that `BannableChatMessageBaseProcessor` orders mute/chat-ban before `ChatSpamGuardPlugIn`, preventing muted players from tripping spam counters.
