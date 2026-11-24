# Audit: `server/src/GameLogic/PlayerActions/MiniGames`

- **Unit**: `server/src/GameLogic/PlayerActions/MiniGames`
- **Files read in full**: 7 of 7 (784 lines total)
  - `server/src/GameLogic/PlayerActions/MiniGames/EnterDoppelgangerAction.cs` (106 lines)
  - `server/src/GameLogic/PlayerActions/MiniGames/EnterImperialGuardianAction.cs` (125 lines)
  - `server/src/GameLogic/PlayerActions/MiniGames/EnterMiniGameAction.cs` (330 lines)
  - `server/src/GameLogic/PlayerActions/MiniGames/EnterResult.cs` (51 lines)
  - `server/src/GameLogic/PlayerActions/MiniGames/FreeTicketItems.cs` (46 lines)
  - `server/src/GameLogic/PlayerActions/MiniGames/MiniGameDefinitionExtensions.cs` (66 lines)
  - `server/src/GameLogic/PlayerActions/MiniGames/MiniGameOpeningStateRequestAction.cs` (57 lines)
- **Suite references**:
  - Season 6 suite: `IGCData/Events/IGC_BloodCastle.ini`, `IGCData/Events/DoppelGanger/IGC_DoppelGanger_Config.ini`, `IGCData/Events/ImperialGuardian/IGC_ImperialGuardian.ini`
  - Season 9 suite: `IGCData/Events/ImperialGuardian/IGC_ImperialGuardian.ini`

## Rules & Comparison

| Rule / Behaviour | HNMO | Season 6 / Season 9 Suite | Match |
|---|---|---|---|
| Entry busy/dialog state (`IsBusy`) | Blocks dead, duelling, or non-`EnteredWorld` except when `PlayerState.NpcDialogOpened` for the matching event entry NPC (`BloodCastle`, `DevilSquare`, `IllusionTemple`, `LugardDoppelgangerEntry`, `JerintGaionEvententry`, `Kanturu`) | Blocks entry while dead, trading, or in unrelated NPC dialogs | Same |
| PK state check | Blocks `HeroState >= PlayerKillWarning` unless `ArePlayerKillersAllowedToEnter` | Outlaws (including PK Warning) blocked from Blood Castle / Devil Square / Chaos Castle / Illusion Temple | Same |
| Level & Master class check | Uses `MinimumSpecialCharacterLevel`/`MaximumSpecialCharacterLevel` for MG/DL/RF and `MinimumCharacterLevel`/`MaximumCharacterLevel` otherwise; checks `RequiresMasterClass` | Same special-class level bracket offset & Master requirement | Same |
| Ticket & Free Ticket priority | Prefers explicit slot if valid, else regular `TicketItem` matching `TicketItemLevel` (`Durability > 0`), else `FreeTicketItem`; decrements durability by 1 on entry | Regular ticket consumed before CashShop free ticket; durability = remaining entries | Same |
| Doppelganger tickets | Accepts `(14, 111)` Mirror of Dimensions or `(13, 125)` Doppelganger Free Ticket; prefers Mirror of Dimensions | Same item IDs `(14,111)` and `(13,125)` | Same |
| Imperial Guardian tickets & party | Mon-Sat requires `(14, 102)` Gaion's Order; Sunday requires `(14, 109)` Complete Secromicon; enforces `IsPartyRequired` | Same item IDs `(14,102)` / `(14,109)` and party requirement | Same |
| On-entry cleanup | Deducts `EntranceFee`, consumes ticket, leaves party if `!AllowParty`, closes personal store, removes summon, clears death-cleared effects (called twice at lines 151 & 160 — harmless duplicate), warps to `Entrance` | Same cleanup on event warp | Same |
