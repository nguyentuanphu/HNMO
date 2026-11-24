# Audit Unit: `server/src/GameLogic/PlugIns/UnlockCharacterClass`

- **Status**: Checked (on commit `f9b96fc8`, 2026-11-24)
- **Files**: 5 files read in full (`UnlockCharacterAtLevelBase.cs`, `UnlockDarkLordAtLevel250.cs`, `UnlockMagicGladiatorAtLevel220.cs`, `UnlockRageFighterAtLevel150.cs`, `UnlockSummonerAtLevel1.cs`).
- **Rule Category**: Character class unlock level thresholds (S6/S9 parity).

## Findings

1. **S6/S9 parity**: Thresholds match Season 6 / Season 9 server configuration (`common-server-and-character-rules-s6-s9.md`):
   - Magic Gladiator (`12`): level `220`
   - Dark Lord (`16`): level `250`
   - Rage Fighter (`24`): level `150`
   - Summoner (`20`): level `1`
2. **Concurrency guard**: `UnlockCharacterAtLevelBase` skips `account.IsBot == true` to avoid duplicate `PK_AccountCharacterClass` inserts across concurrent bot characters on the same account. No defects found.
