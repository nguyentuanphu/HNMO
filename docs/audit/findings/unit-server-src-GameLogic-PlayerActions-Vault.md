# Unit: server/src/GameLogic/PlayerActions/Vault

Checked against: Season 6 and Season 9 suite vault lock role (`Warehouse` PIN protection).

Scope: `server/src/GameLogic/PlayerActions/Vault` (`3` files, `116` lines).

## Summary

- Implements vault PIN unlock, set, and remove actions.
- Standard MU checks a plain warehouse password against the account password on set/remove; HNMO routes unlock/set through `AccountSafetyPlugIn` (hashed PIN + wrong-attempt lockout tracking) when enabled, falling back to `AccountSafetyEngine.PinMatches` and `BCrypt.Net.BCrypt.Verify`.

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `UnlockVaultAction.cs:22-38` | Delegates unlock to `AccountSafetyPlugIn.EnterPinAsync` (enforcing wrong-try limits & hashed 2nd password), falling back to `AccountSafetyEngine.PinMatches` | Suite warehouse PIN unlock (plaintext account `bloc_code` / warehouse code without attempt limit in stock suite) | Differs (HNMO custom security & attempt limit) |
| `SetVaultPinAction.cs:22-36` | Requires unlocked vault & BCrypt-verified account password; hashes numeric PIN via `AccountSafetyEngine.HashPin` and marks `PinSetInGame` | Suite verifies character SSN / account password and sets plain PIN | Differs (HNMO custom hashed PIN) |
| `RemoveVaultPinAction.cs:19-26` | Clears `VaultPassword` and unlocks vault (`IsVaultLocked = false`) if `BCrypt.Verify` on account password passes | Suite removes warehouse lock with password/SSN | Same role (BCrypt password check) |
