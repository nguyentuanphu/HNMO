# Unit Audit: `server/src/GameLogic/Discord`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (6 files read in full)

## Summary

- `DiscordIntegrationFeaturePlugIn` & `DiscordIntegrationConfiguration`: public invite URL and Rich Presence IDs/keys (no secrets).
- `BridgedChatSender`: `@` prefix reservation (`IsAllowedInCharacterName`, `IsBridged`, `GetName`).
- `DiscordIntegrationInfo`, `DiscordLinkCodeResult`, `ExternalChatScope`.
- No defects found.
