# Audit Findings — `server/src/GameLogic/PlugIns/CombiningLeftovers`

- **Unit**: `server/src/GameLogic/PlugIns/CombiningLeftovers` (11 files, 1,252 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `BagItemDescriptor.cs`, `CombiningLeftoversConfiguration.cs`, `CombiningLeftoversEngine.cs`, `CombiningLeftoversPlugIn.cs`
  - `CombiningLeftoversStateWriter.cs`, `CombiningLeftoversStore.cs`, `CombiningLeftoversStoreData.cs`
  - `LeftoverExchangeOffer.cs`, `LeftoverIngredient.cs`, `LeftoverRecipe.cs`, `LeftoversLogEntry.cs`

## Rules & Behavior
- Custom plug-in (`IDisabledByDefault`, `F10` or NPC `546`):
  - Default upgrade recipes for Box of Kundun `+1..+4` (`14:11` levels `8..11`, 2x box + 1x JoL `14:16` + 1x JoC `14:22` + `500k/1M/2M/3M` Zen) at `100%/90%/85%/80%` success rate, gated by `SeasonPlan` `MaxBoxLevel` (`5` max).
  - Default exchange offers (`5 × Rena 14:21 lv0 -> 25 GP`, `5 × Sign of Lord 14:21 lv3 -> 40 GP`).

## Real Bugs Found
1. **Critical Bug — `CombiningLeftoversEngine.cs:107-155` & `CombiningLeftoversPlugIn.cs:297-316` (Stacked items destroyed / uncounted)**:
   - `BagItemDescriptor` omits `Durability`, and `RemoveBagSlotsAsync` (`line 312-313`) always deletes the entire `Item` (`RemoveItemAsync` + `DeleteAsync`) rather than decrementing `Durability` by the needed count.
   - Because `ItemStackPlugIn` (enabled by default) allows accounts to stack `JoL`/`JoC` (`jewels`) and `Box of Kundun`/`Rena`/`Sign of Lord` (`boxes`), combining consumes **an entire stack** of up to 255 jewels/boxes for a `Count = 1` ingredient, while a single stacked slot of 5 Rena (`Durability = 5`) fails `PlanExchange` as having count `1 < 5`.
2. **NPC Override Note — `CombiningLeftoversPlugIn.cs:72-86`**:
   - When active with default `ExchangeNpcNumber = 546`, talking to NPC `546` marks `HasBeenHandled = true` and opens Combining Leftovers instead of Lahap's vanilla jewel mix/unmix dialog.

## Re-check 2026-11-27 (changes 8e98a00d..a108e846)

Commits read (every changed file read in full):

- 19e08d15 Economy write order: the bag is saved before the Jewel Bank is credited or a leftovers reward is kept; the stack pickup test checks the real cap
- 1e0121c8 Economy guards: equipment counts as one piece in combining leftovers; a Jewel Bank withdrawal gives one piece
- 478f9cf3 fix(plugins): fix stack deposits/consumption, receipt rollback, buff id collision, and partypass

Evidence:

- Engine: ingredients counted by stack pieces across slots (BagItemDescriptor.Quantity), equipment counts as one piece (PieceCount) - fixed.
- Plug-in: the live Season Plan box cap is used for planning and for the state - fixed.
- Plug-in: materials are saved before a waiting reward is written. OPEN: a failed save is ignored and the reward (coins through MissionCoinGranter in its own context, owed reward in the JSON file) still goes out, so a crash before the next good save gives the materials back and keeps the reward. Fixed by PR #423 (open): coins as a grant of the player's own context and waiting rewards as AssetOperation rows in the same save.
