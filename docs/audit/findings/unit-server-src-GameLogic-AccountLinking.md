# Unit Audit: `server/src/GameLogic/AccountLinking`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (4 files read in full)

## Summary

- `AccountLinkService`: 8-char code (`CodeAlphabet` without ambiguous `0/O/1/I`), `CodeValidity = 10 min`, SHA-256 hashed (`HashCode`), normalized uppercase alphanumeric (`NormalizeCode`), short-lived `IPlayerContext` per call, publishes `AccountUnlinkedEvent` on unlink.
- `AccountNotificationKeywords`: `login`, `letter`, `friend`.
- No defects found.
