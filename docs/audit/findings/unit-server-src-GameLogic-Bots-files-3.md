# Audit Findings: `server/src/GameLogic/Bots (files 3)`

**Status**: Checked (all 4 files read in full, 408 lines)
**References**: Owner design (server-side bot simulation), [`common-server-and-character-rules-s6-s9.md`](../references/common-server-and-character-rules-s6-s9.md)

## Scope Covered
- `BotWingHandler.cs` (263 lines), `HuntingSpot.cs` (18 lines), `HuntingSpotOccupancy.cs` (110 lines), `PendingPartyInvite.cs` (13 lines).

## Rule & Design Comparison

| File & Line | Rule / Behavior | Suite / Reference Value | Verdict |
|---|---|---|---|
| `BotWingHandler.cs:28-55,109-162` | Grants wings to bots directly into `InventoryConstants.WingsSlot` at level milestones: `180` (`+0`, luck, `+12` op), `280` (`+9`, luck, `+16` op), `400` (`+15`, luck, `+16` op). Walks down from earned tier to highest `QualifiedCharacters`-eligible wing (so un-evolved classes keep Tier 2; DL/RF re-receive Cape of Lord `13:30` / Fighter `12:49` as `+9` at Lv 280). | Classic equip milestones are Lv 180 (1st wings), Lv 215/220 (2nd wings), Lv 400 master (3rd wings); `280` is a bot-progression pacing rule, and `QualifiedCharacters` enforces S6/S9 class eligibility. | HNMO custom (bot milestone) + Same item class rules |
| `BotWingHandler.cs:69-101` | Destroys outgrown wings (`DestroyInventoryItemAsync`) rather than dropping them; persists immediately (`SaveProgressAsync`); deletes created item on failed add. | Prevents fabricated item leakage into player economy. | Sound |
| `BotWingHandler.cs:149-150,210-234` | Chooses highest-scoring wing option: `WizardryBaseDmg`/`CurseBaseDmg` (`3`) when `TotalEnergy > TotalStrength`, else `PhysicalBaseDmg` (`3`). | Matches MG dual-wing (`Wings of Heaven` vs `Wings of Satan`) build affinity. | Sound |
| `HuntingSpotOccupancy.cs:20-109`, `HuntingSpot.cs:17-18` | Deconflicts bot hunting spots using immutable reference `HuntingSpot(Point, MapNumber)` records (atomic ref swap); excludes party members; `SpotRange = 2 * BotMuHelperSettings.DefaultHuntingRange`; `IsMonsterClaimed` checks competitor bot claimed/current positions within `attackRange`. | HNMO custom multi-bot farm deconfliction. | Sound |
| `PendingPartyInvite.cs:12-13` | Record `(Requester, AcceptAtUtc)` staging delayed human-like acceptance of party invites. | HNMO custom bot social timing. | Sound |

## Bugs / Open Issues
- None found.
