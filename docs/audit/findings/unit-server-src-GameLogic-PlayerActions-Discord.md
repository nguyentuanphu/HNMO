# Unit Audit: `server/src/GameLogic/PlayerActions/Discord`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (1 file read in full)

## Summary

- `DiscordIntegrationAction`: 30-second per-player link code cooldown (`LinkCodeCooldown`), `UnlinkAsync`, `ShowIntegrationInfoAsync`, `RequestLinkCodeAsync`, `GetIntegrationInfoAsync` (checks guild/alliance bindings and world chat `GameEventPublisherPlugIn`).
- No defects found.
