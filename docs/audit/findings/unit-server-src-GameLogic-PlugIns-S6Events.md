# Unit Audit Findings — `server/src/GameLogic/PlugIns/S6Events`

- **Unit**: `server/src/GameLogic/PlugIns/S6Events` (12 files, 1,522 lines)
- **Checked on**: 2026-11-24
- **Reference**: [invasions-and-s6-events-s6-s9.md](../references/invasions-and-s6-events-s6-s9.md)

## Scope Covered

- `BlossomChatCommandPlugIn.cs`, `LmsChatCommandPlugIn.cs`, `LuckyCoinChatCommandPlugIn.cs`, `S6EventsConfiguration.cs`, `S6EventsEngine.cs`, `S6EventsLmsWinEntry.cs`, `S6EventsOwedReward.cs`, `S6EventsPlugIn.cs`, `S6EventsState.cs`, `S6EventsStore.cs`, `S6ExchangeRewardEntry.cs`, `S6ExchangeTier.cs`

## Findings & Bugs

1. **Non-winner LMS survivors left stranded in arena on round timeout (`S6EventsPlugIn.cs:468-506`)**:
   - When `timedOut == true` with multiple survivors (`survivors.Length > 1`), `CheckLmsCompletionAsync` selects the highest-kill winner and calls `WarpBackToSafezoneAsync(winner, cfg)` (`line 506`), but never warps the remaining eliminated/runner-up `survivors` back to safezone.
2. **No safe escape / arena-leave disqualification or Registration PvP protection (`S6EventsPlugIn.cs:183-206, 297-302`)**:
   - Players warped to `LmsArenaMapNumber` (`64`, Duel Arena) during `LmsPhase.Registration` are already in a PvP zone before `Fighting` begins; kills during Registration do not eliminate contestants, and a player who warps out of the arena via town portal or move remains in `_lmsContestants` until timeout.
3. **Incorrect `Participants` count recorded for LMS history (`S6EventsPlugIn.cs:496`)**:
   - `Participants = Math.Max(1, survivors.Length + kills)` adds only the *winner's* kill count rather than the initial registered contestant count (`initialContestants`), undercounting whenever other players scored kills or left.
4. **Inventory scan does not filter equippable slots (`S6EventsEngine.cs:34-36, 117-120`)**:
   - `CountStackedItems` and `TryConsumeStackedItemsAsync` scan `inventory.Items` without `i.ItemSlot > InventoryConstants.LastEquippableItemSlotIndex` (harmless for `14:90` / `14:100` defaults, risky if configurable to equippable items).
5. **Intentional HNMO adaptations (OK)**:
   - Cherry Blossom (`/blossom`, NPC `450`, item `14:90`, tiers `10/50/255`) and Lucky Coin (`/luckycoin`, NPC `478`, item `14:100`, tiers `10/20/30`) replace complex S6 ItemBags with configurable weighted Zen/Coin/Item tiers and atomic `S6EventsOwedReward` fallback when inventory/Zen is full.
