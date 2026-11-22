# Ledger: combat, items, arena, chat and event rules (22 Nov 2026)

- Read at commit `477b9466` (`origin/claude/hnmo-main-handover-54co2m`, 22 Nov 2026).
- References: classic MU rules and the Season 6 / Season 9 server suites (`ChaosMix.cpp`, `SkillInfo.txt`, `DoppelGanger`, `ImperialGuardian`, `Raklion`, `DuelSystem`, `JewelMix`, `MoveItem`).
- Every unit below had 100 % of its files read in full before being marked `Checked` in `docs/audit/coverage.json`.

## Findings fixed from this pass

| # | Unit | Finding | Fix PR |
|---|---|---|---|
| 1 | `server/src/GameLogic/Pet` | `RavenCommandManager`: zero durability assigned `this._currentBehaviour = PetBehaviour.Idle;` before unconditionally overwriting `this._currentBehaviour = newBehaviour;`. | `#308` |
| 2 | `server/src/GameLogic/PlugIns/Arena` & `PlayerActions/Duel` | Dead/dueling queued players stalled all Arena Teams queues; Guild War didn't enforce duel/Teams/Royale/alive exclusion; regular `/duel` wasn't blocked during Arena battles. | `#304`, `#305` |
| 3 | `server/src/GameLogic/PlugIns/Chat` | `/post` (`PostChatCommandPlugIn`) skipped `ChatLogPlugIn.Record`, omitting `/post` lines from the admin panel Chat Log. | `#306` |
| 4 | `server/src/GameLogic/PlayerActions/Skills` | `NovaSkillStartPlugin`: early return in `AttackTargetsAsync` when player dies or enters safezone left `player.SkillCancelTokenSource` non-null, locking out future Nova casts until relog. | `#309` |
| 5 | `server/src/GameLogic/PlayerActions/Items` | `ItemStackAction.UnstackItemsAsync`: deducted `DismantleFee` (1,000,000 Zen) before checking `freeSlots.Count < pieces`; `MoveItemAction.IsTargetSlotBlocked`: negative `rowIndex` crash when `toSlot < toStorage.StartIndex`. | `#310`, `#311` |

## Checked units (16 units, 191 files, 22,823 lines)

- **`server/src/GameLogic/ItemFormulas`** (2 files, 188 lines): checked option/excellent/socket/ancient item formulas and repair multipliers. Sound.
- **`server/src/GameLogic/ItemDefinitions`** (3 files, 895 lines): `ItemClientMetadata`, `ItemDefinitionStateStore`, `ItemClientDataSync` checked (glow hex/preset normalization, attribute requirement sync, Data JSON patching). Sound.
- **`server/src/GameLogic/PlayerActions/Craftings`** (19 files, 1,616 lines): all specific Chaos Machine / Seed / Wing / Pet / Lucky item / Refinement crafting handlers read in full. Sound.
- **`server/src/GameLogic/PlayerActions/Items`** (25 files, 3,209 lines, incl. `ItemRegistration/*`): NPC buy/sell, drop, pickup, repair, jewel stacking, item move, crafting base/simple handlers, Kalima gate, outlaw shop rule, and Golden Archer registration read in full. Fixed `#310` and `#311`.
- **`server/src/GameLogic/PlayerActions/Skills`** (28 files, 3,321 lines): `AttackActionValidator`, `AreaSkillAttackAction`, `AreaSkillTargetSelector`, `AreaSkillHitAction`, `DeferredHitScheduler`, `FrustumBasedTargetFilter`, `TargetedSkillDefaultPlugin`, `NovaSkillStartPlugin`/`StopPlugin`, `RageSkillAttackAction`, `ForceSkillAction`, `SummonPartySkillPlugin`, and per-skill plug-ins read in full. Fixed `#309`.
- **`server/src/GameLogic/Pet`** (6 files, 579 lines): Dark Raven/Spirit experience, durability decay, command behaviours (`RavenCommandManager`). Fixed `#308`.
- **`server/src/GameLogic/GuildWar`** (5 files, 237 lines): guild war state, scoring, soccer rules. Sound.
- **`server/src/GameLogic/PlayerActions/Duel`** (3 files, 451 lines): `DuelActions`, `EndDuelWhenLeavingDuelMapPlugIn`, `UpdateDuelScorePlugIn`. Fixed battle overlap in `#305` (audited `#307`).
- **`server/src/GameLogic/PlayerActions/Chat`** (12 files, 583 lines): `ChatMessageAction`, all channel processors, mute/ban checks. Audited in `#307`.
- **`server/src/GameLogic/PlugIns/Chat`** (14 files, 1,184 lines): `CrossSeasonFriendsPlugIn`, `OfflineWhisperPlugIn`/`Store`, `ChatSpamGuard`/`PlugIn`, `ChatLog`/`PlugIn`, `ChatMute`, `OnlineCharacters`. Fixed `#306`, audited `#307`.
- **`server/src/GameLogic/PlugIns/Arena`** (25 files, 4,273 lines): `ArenaPlugIn`, `ArenaTeams`, `ArenaRoyale`, `ArenaGuildWar`, `ArenaDuels`, engines, ranking, shop, rules, notices. Fixed `#304`/`#305`, audited `#307`.
- **`server/src/GameLogic/PlugIns/DuplicateItems`** (7 files, 462 lines): serial assignment on entry/scan, `DuplicateItemsEngine`, `RefuseFrozenAsync`, atomic store. Sound.
- **`server/src/GameLogic/PlayerActions/MiniGames`** (7 files, 792 lines): `EnterMiniGameAction`, `EnterDoppelgangerAction`, `EnterImperialGuardianAction`, `FreeTicketItems`, `MiniGameOpeningStateRequestAction`. Sound.
- **`server/src/GameLogic/MiniGames/Doppelganger`** (12 files, 1,862 lines): `DoppelgangerContext`, `DoppelgangerEventDefinition`, `DoppelgangerMonsterIntelligence`, scaling, paths, ice walker mission, interim/final chests. Sound.
- **`server/src/GameLogic/MiniGames/ImperialGuardian`** (14 files, 1,351 lines): `ImperialGuardianContext`, `ImperialGuardianEventDefinition`, `ImperialGuardianGate`, `ImperialGuardianMonsterIntelligence`, day/zone progression, Sunday map & fragment drops. Sound.
- **`server/src/GameLogic/Raklion`** (9 files, 1,820 lines): `RaklionContext`, `RaklionPlugIn`, `RaklionEventDefinition`, `SelupanIntelligence`, `IronKnightIntelligence`, egg/hatchery/pattern state machine. Sound.
