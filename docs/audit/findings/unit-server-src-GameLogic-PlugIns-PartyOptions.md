# Audit Findings — `server/src/GameLogic/PlugIns/PartyOptions`

- **Unit**: `server/src/GameLogic/PlugIns/PartyOptions` (8 files, 908 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `PartyAdSnapshot.cs`, `PartyAdvertisement.cs`, `PartyOptionsConfiguration.cs`, `PartyOptionsEngine.cs`
  - `PartyOptionsPlugIn.cs`, `PartyOptionsStateWriter.cs`, `PartyOptionsStore.cs`, `PartyOptionsStoreData.cs`

## Rules & Comparison
| Rule | HNMO Implementation | Season 6 / 9 Suite Reference | Status |
| --- | --- | --- | --- |
| Auto-party / password join | `PartyOptionsPlugIn`: `/partypass PASSWORD` + spot finder drawer (`MaxPasswordLength` default `16`, `MaxSize = MaximumPartySize`, default `5`), denies across Guild/Gens/event rules via `PartyRequestAction.IsPartyDeniedAsync` | Season 6/9 suite `Common` INI: `AutoParty = 1` works by whispering party password to leader | Differs (HNMO uses `/partypass` + Spot Finder UI instead of whisper) |
| Spot & password privacy | `PartyOptionsStateWriter.WritePublic`: exposes `hasPassword` boolean without leaking password plaintext; strips `< > & " ' \`` control chars (`PartyOptionsPlugIn.cs:192`) | N/A (custom web/drawer listing) | Custom |

## Real Bugs Found
1. **Bug — `PartyOptionsPlugIn.cs:144-147, 254` (`AllowPasswordJoin` blocked when `AllowAutoSpotParty` is false)**:
   - `HandlePartyPassCommandAsync` (`line 144`) validates via `engine.FindByPassword(...)` (which checks `AllowPasswordJoin`), then delegates to `JoinLeaderPartyAsync` (`line 147`).
   - `JoinLeaderPartyAsync` (`line 254`) calls `engine.PlanSpotJoin(...)`, which rejects immediately if `AllowAutoSpotParty == false` (`PartyOptionsEngine.cs:161`).
   - Consequence: if an admin enables password party joins (`AllowPasswordJoin = true`) while disabling the hunting spot finder (`AllowAutoSpotParty = false`), every `/partypass` join attempt fails with `"Party Options are currently disabled."`.

## Re-check 2026-11-27 (changes cf8c32b8..a108e846)

Commits read (every changed file read in full):

- 478f9cf3 fix(plugins): fix stack deposits/consumption, receipt rollback, buff id collision, and partypass

Evidence:

- PartyOptionsEngine.PlanSpotJoin: a join with a password needs AllowPasswordJoin, one without needs AllowAutoSpotParty (the password join was blocked by the auto-spot switch) - fixed.
