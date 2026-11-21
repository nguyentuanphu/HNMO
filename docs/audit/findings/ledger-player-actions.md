# Ledger: player actions and their message handlers

- Read at commit `cbf4593e` (origin/claude/hnmo-main-handover-54co2m, 21 Nov 2026).
- References: classic MU behaviour; Season 6/9 server suites `Data/Common.ini` (GuildCreateLevel 150), `Data/CommonServer.cfg`.
- How packets run: `RemotePlayer.PacketReceivedAsync` handles one player's packets one after another (`RunPersistenceExclusiveAsync`) and logs exceptions. Races are therefore only possible between two players (trade, store, party, guild, duel), or between a packet and a timer/background task.
- Status per unit: **Checked** = every file read in full. **Partly read** = the files named below were read; the rest is listed as not read with the reason. No unit of this assignment is fully Checked; the size (about 20,000 lines of PlayerActions and 14,000 lines of MessageHandler) did not fit into this pass. The read files are the ones where items, zen and state change hands.

## Findings

| # | Rule | HNMO did | Classic / suites | Result |
|---|---|---|---|---|
| 1 | Trade: both traders press at once | both finish the trade, each holding its own state lock and waiting for the other's: deadlock, both stuck in the trade state | trade finishes once | fixed #215 |
| 2 | Inventory changes while a trade or crafting window is open | drop, complete stack, personal store purchase and pickup were allowed; the cancel/disconnect rollback to the inventory backup then duplicated (dropped item on the ground and in the bag; 5+5 potions became 15; sold store item back to the seller; buyer's zen back) or lost (picked up item) | classic refuses drop and item use while a trade/interface is open | fixed #216 |
| 3 | Duel answer | answered any number of times and by anyone in the room: a repeated accept cancelled the starting duel (or took the fees again if the checks passed) | one answer, by the requested player | fixed #219 |
| 4 | Guild creation | `GuildCreateAction` checks only the player state: level 100 and "not in a guild" are only checked when the guild master is asked for the dialog, the creation packet can be sent without | GuildCreateLevel 150 (suites), not in a guild | fixed #221 (level 100 kept, the suites' 150 is owner question Q1) |
| 5 | Guild join answer | shows "busy" when the requester or the master isn't in EnteredWorld, then accepts anyway (missing return) | refused | fixed #221 |
| 6 | Talisman stat items (HNMO) | consumed without the EnteredWorld check of every other consume handler (dead, trading, NPC dialog) | | found, not fixed: the per-character cap and the durability check stop any duplication; Q4 |
| 7 | Entering a mini game (Blood Castle etc.) | no player state check (trade, NPC dialog, dead); `// TODO: Check Duel state`; ticket and fee are taken from the inventory, which a trade cancel then rolls back | classic: not during trade/duel | found, not fixed - Q5 |
| 8 | Repair all without NPC | logs "probably cheater" and repairs anyway (inventory price) | NPC only | found, not fixed (no gain: inventory price is higher) - Q6 |
| 9 | Party kick | `PartyList[index]` with the index from the packet, no bounds check (exception, logged) | | found, not fixed (only the sender's packet fails) |
| 10 | Quest completion | completed anywhere by packet, no NPC dialog check | classic: at the quest NPC | Q7 |
| 11 | Personal store open | no level check (price setting needs level 6), no state check | | low; Q8 |

## Unit: server/src/GameLogic/PlayerActions/Trade (6 files) - Checked
All read in full: BaseTradeAction, TradeAcceptAction, TradeButtonAction, TradeCancelAction, TradeMoneyAction, TradeRequestAction.
- Request: both state machines advance to TradeRequested; Arena, requests-off, second password, template players refused. Same.
- Money: offered zen moves out of the inventory, the already offered amount counts as available; a change resets both buttons. Same.
- Items into/out of the trade reset both confirmations (MoveItemAction). Same.
- Finish: both pressed; untradable/locked/escrow items cancel; maximum inventory money checked for both; items saved in one trade context before the zen moves. Same. Simultaneous presses: fixed #215.
- Cancel: inventory restored from the backup (see #216 for what the backup can't undo).

## Unit: server/src/GameLogic/PlayerActions/PlayerStore (5 files) - Checked
BuyRequestAction, CloseStoreAction, OpenStoreAction, SetItemPriceAction, StoreItemListRequestAction read in full.
- Price: level 6, negative removes, other currencies need the setting and their maximum. Zen price has no maximum (a price above the buyer's maximum money simply can't be paid).
- Buy: store open, slot range, sellable, locked item, currency support, funds; under the store lock the item, its price and the buyer's free slot are checked again before paying (race of #190). Same. Trading buyer/seller: fixed #216.
- Buyer and seller only need to be on the same map (handler `CurrentMap.GetObject`), no distance. Classic: the store is opened by clicking the seller, so in view range. Q8.

## Unit: server/src/GameLogic/PlayerActions/Items (25 files) - Partly read
Read in full: MoveItemAction, DropItemAction, PickupItemAction (first 160 lines: refusal paths, limits, quest items), SellItemToNpcAction, BuyNpcItemAction, ItemRepairAction, ItemStackAction (stack part), ItemCraftAction (first 120 lines), BaseItemCraftingHandler (first 140 lines).
Not read: CraftingRequiredItemLink, IItemCraftingHandler, ItemBoxDroppedPlugIn, ItemCraftingHandlerFactory, ItemRegistration/*, KalimaGate, LostMapDroppedPlugIn (audited in #186), OutlawNpcShopRule, PetInfoRequestAction, PetStorageLocation, SetPetBehaviourRequestAction, SimpleItemCraftingHandler (data-driven crafting: the data agent's craftings), rest of PickupItemAction/ItemStackAction/BaseItemCraftingHandler - time.
- Move: storage per state (inventory in world/NPC/trade, vault only with the vault NPC, trade only in trade, crafting storages only with their NPC), item rules (tradable, storable, store-sellable), frozen/locked items, equip requirements and hand conflicts, mini game equip rules, bound items, slot fit by width/height, stacking only inside the inventory. Same. Complete stack during a backup: fixed #216. A target slot below the store's first slot makes `IsTargetSlotBlocked` index a negative row (exception before any change) - harmless.
- Drop: walkable target, droppable flag, plug-ins, saved before it lands. No state check: fixed #216.
- Pickup: alive, distance 3, storage limit per character, quest items. During a backup: fixed #216.
- NPC sell: merchant open, outlaw rule, sellable flag, bound items, account safety, maximum money. NPC buy: merchant, outlaw, active item, plug-ins, stack or free slot before paying, castle tax. Same.
- Repair: pet slot only at an NPC; repair-all without NPC continues (finding 8). NPC price for any opened NPC (not only smiths) - Q6.
- Jewel bundling: NPC check, jewels counted, fee before removing. Same.
- Crafting: NPC crafting lookup, locked items, season gates, zen paid before the success roll, backup dropped after the mix. An exception in a handler is reported as "lacking items" after the zen may already be paid - noted.

## Unit: server/src/GameLogic/PlayerActions/ItemConsumeActions (63 files) - Partly read
Read in full: ItemConsumeAction, BaseConsumeHandlerPlugIn, SiegePotionConsumeHandlerPlugIn, and the precondition parts of Recover, Learnables, ComplexPotion; TalismanPlugIn stat consume (PlugIns/Talismans). Checked by grep: every handler derives from BaseConsumeHandlerPlugIn and goes through its EnteredWorld + durability check (13 overriding handlers call CheckPreconditions/base).
Not read: the other 55 handlers' effect code (potions, jewels, fruits, tickets): their numbers are data-driven and belong with the item data unit.
- Consume: item at slot, Harmony season gate, talisman path (finding 6), handler by item, plug-ins, durability -1, destroy at 0. Same.

## Unit: server/src/GameLogic/PlayerActions/Character (8 files) - Partly read
Read in full: IncreaseStatsAction, AddMasterPointAction (first 120 lines), DeleteCharacterAction. Not read: CreateCharacterAction, FocusCharacterAction, RequestCharacterListAction, SaveKeyConfigurationAction, SelectCharacterAction - time.
- Stats: points available, increasable by the class, maximum value capped. Same.
- Master points: points, master skill, class, rank and required skills, maximum level. Same.
- Delete: character selection state, own character, security code or password, not a guild member. Same.

## Unit: server/src/GameLogic/PlayerActions/Guild (11 files) - Partly read
Read in full: GuildCreateAction, GuildMasterAnswerAction, GuildRequestAction, GuildRequestAnswerAction, GuildKickPlayerAction; TalkNpcAction guild master part. Not read: GuildInfoRequestAction, GuildListRequestAction, GuildRelationshipChangeAction, GuildRoleAssignAction, GuildWarAnswerAction, GuildWarRequestAction - time.
- Create: findings 4. Join: level 6, not in a guild, master is a guild master on the same map, member limit plug-in, one pending request. Answer: finding 5 (both fixed #221). Kick: security code or password, guild master or self, castle owner can't disband. Same.

## Unit: server/src/GameLogic/PlayerActions/Duel (3 files) - Partly read
Read in full: DuelActions. Not read: EndDuelWhenLeavingDuelMapPlugIn, UpdateDuelScorePlugIn (audited in #182/#209).
- Request/answer checks: same map, no mini game, no PK2+, no guild war, no self-defense, no NPC dialog, EnteredWorld, level, fee. Same. Answer once: finding 3 (fixed #219).

## Unit: server/src/GameLogic/PlayerActions/Party (4 files) - Partly read
Read in full: PartyRequestAction, PartyResponseAction, PartyKickAction; Party.AddAsync/KickPlayerAsync. Not read: PartyListRequestAction (list only).
- Request: mini games, plug-ins (gens), requests off, already in party, only the master invites, bots. Response: state, still not in a party, checked again. Add respects the maximum party size. Same. Kick: finding 9.

## Unit: server/src/GameLogic/PlayerActions (top level, 16 files) - Partly read
Read in full: WarpAction, WarpGateAction, WizardTeleportAction, CloseNpcDialogAction, TalkNpcAction (lines 100-216). Not read: EnterBalgassRefugeeAction, EnterBarracksOfBalgassAction, EnterMarketPlaceAction, EnterQuestMapAction, GameMapDefinitionExtensions, HitAction, LoginAction (656 lines), LogoutAction, PingAction, TalkNpcAction075, WebLoginTicketStore, start of TalkNpcAction - time.
- Warp (M menu): Arena, warp guard (dead/duel/trade), plug-ins, level, map requirements, zen last (50x for outlaws). Same.
- Gate: only gates of the current map (handler), within 12 tiles, level, map requirements, warp guard. Same.
- Teleport: not from/into safe zone, walkable, skill range, stun/sleep/freeze effects, mana. Same.

## Other units read in full
- CashShop/CashShopActions (first 300 lines; Q: none): EnteredWorld, alive, safe zone; storage item delivery and deletion saved together. Same.
- Messenger/LetterSendAction: zen checked first and taken after the save. Same.
- Gens/GensActions (first 150 lines): NPC of the gens, join/leave/reward checks inside one exclusive operation. Same.
- Quests/QuestCompletionAction: items, kills, client action, inactive class; items destroyed, rewards, state cleared. No NPC check (Q7).
- MiniGames/EnterMiniGameAction (first 120 lines): finding 7.
- Vault (3 files): PIN set/remove with the account password, unlock through the account safety PIN. Same.

## Units not read in this pass
- PlayerActions/Chat (12), Craftings (19, data-driven: the data agent), Discord (1), Messenger (7 of 8), MiniGames (6 of 7), MuHelper (2), Quests (7 of 8), Skills (28 files, 3,321 lines: combat, audited in groups 1-6), ItemConsumeActions effect code (55).
- server/src/GameServer/MessageHandler: only the handlers behind the actions above were opened (ConsumeItemHandlerPlugIn, WarpGateHandler075PlugIn, the PlayerShop handlers, GuildCreateHandlerPlugIn, QuestCompletionRequestHandlerPlugIn) plus the dispatch in RemoteView/RemotePlayer. Not read: the other handlers (about 13,000 lines).
- server/src/GameServer/RemoteView (about 23,000 lines): view serialization, no game rules - not read; mark as No game rules except where a view computes a value (e.g. item prices) - left for the client/server unit.

## Owner questions
1. Guild creation level: HNMO's guild master asks for level 100 (and an optional MinimumCreateLevel setting); the Season 6/9 server suites have GuildCreateLevel 150. Which?
4. Talisman stat items (HNMO feature): should they need the same state as all other items (not while dead, trading or in an NPC dialog)?
5. Mini game entry: refuse while trading, in an NPC dialog, dead or duelling (classic)? The ticket and fee are taken from an inventory that a trade cancel rolls back.
6. Repair: repair-all without an NPC, and the NPC price at any NPC (not only at smiths/merchants): refuse?
7. Quest completion without the quest NPC: allowed (HNMO), or only at the NPC (classic)?
8. Personal store: buying from any distance on the same map, and opening a store below level 6: keep?

## PRs
- #215 Trade: two presses at the same time no longer lock both traders.
- #216 No item duplication through the inventory rollback of a trade.
- #219 Duel: a request is answered once, by the requested player.
- #221 Guild: creation and join answers check their requirements.
