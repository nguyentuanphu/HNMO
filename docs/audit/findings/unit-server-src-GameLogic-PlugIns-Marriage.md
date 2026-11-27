# Audit Findings — `server/src/GameLogic/PlugIns/Marriage`

- **Unit**: `server/src/GameLogic/PlugIns/Marriage` (10 files, 1,461 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `MarriageChatCommandArgs.cs`, `MarriageChatCommandPlugIn.cs`, `MarriageConfiguration.cs`, `MarriageCoupleEntry.cs`
  - `MarriageEngine.cs`, `MarriagePlugIn.cs`, `MarriageStateWriter.cs`, `MarriageStore.cs`
  - `MarriageStoreData.cs`, `PendingMarriageProposal.cs`

## Rules & Behavior
- Custom `/marry` feature (`IDisabledByDefault`): `MinimumLevel = 100`, ceremony at Devias (`map 2`, `(210, 23)`, radius `18`, `1,000,000` Zen), divorce `2,000,000` Zen with `24h` remarry cooldown (cleaned on character delete via `ForgetCharacter`), couple party bonus (`+10%` EXP, `+15` Def Final), and spouse warp enforcing map level (`GetSpouseWarpLevel`), `HnmoWarpGuard`, and `WarpGateRules`.

## Real Bugs Found
1. **Buff ID Collision Bug — `MarriagePlugIn.cs:32` vs `MountChainPlugIn.cs:36`**:
   - Both `MarriagePlugIn` and `MountChainPlugIn` use `public const short BuffEffectNumber = 242`.
   - When both plugins are active, `MountChainPlugIn.ApplyActiveBuffAsync` (`line 330`) disposes effect `242` (removing the marriage couple buff), and every 15s `MarriagePlugIn.RefreshCoupleBonusAsync` overwrites `MountChainPlugIn`'s aura effect `242` with the 2-minute couple buff.
2. **Stale Buff Window — `MarriagePlugIn.cs:504-507`**:
   - When spouses separate (leave party or change maps), `RefreshCoupleBonusAsync` returns early without disposing existing buff `242`, leaving the couple buff active for up to 2 minutes until `BuffDuration` expires.

## Re-check 2026-11-27 (changes ba8c04f4..a108e846)

Commits read (every changed file read in full):

- 478f9cf3 fix(plugins): fix stack deposits/consumption, receipt rollback, buff id collision, and partypass

Evidence:

- MarriagePlugIn: buff effect number 241 (was 242, the MountChain buff); 240 Guilds, 242 MountChain, 243 Talismans - no collision left. The couple buff is removed when the couple bonus no longer applies - fixed.
