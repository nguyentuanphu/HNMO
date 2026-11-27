# Desktop Client vs Web UI & Season 6.3 / Season 9 Interface Parity Audit

- **Generated**: 2026-11-27 (Australia/Sydney)
- **Sources compared**:
  - `client/src/bin/Data/Interface/RmlUi/` (HNMO Desktop C++ RmlUi documents and themes)
  - `client/src/source/UI/Core/WindowSystem.cpp` and `client/src/source/UI/` (HNMO Desktop C++ window wiring)
  - `client/web/src/ui/*.ts`, `client/web/src/main.ts`, `client/web/public/data/Interface/RmlUi/` (HNMO Web Client)
  - `Season 6.3 desktop client Data/Interface/` and `Season 9 desktop client Data/Interface/GFx/`
- **Checksums / counts verified against `audit/client-s6-s9-data`** (excluding binary packs).

---

## 1. Executive Summary

1. **RmlUi Core Parity (`83 / 101` top-level `.rml` documents shared)**: HNMO's Desktop C++ and Web clients share the same `Data/Interface/RmlUi/*.rml` markup for 83 core game windows, plus `53` themed layout overrides (`classic`: 10, `legacy`: 14, `modern`: 29).
2. **Web UI Gaps vs HNMO Desktop RmlUi (`9` gameplay/NPC RML windows + `7` helper overlays)**:
   - `lucky_item.rml`: `npcWindows.ts` logs *"Lucky Item combination is not yet supported in the web client"* and closes the NPC.
   - `kanturu_enter.rml`, `kanturu_info.rml`: present in Desktop C++ (`KanturuEvent.h`), absent in Web.
   - `gold_bowman.rml`, `gold_bowman_lena.rml`, `lucky_coin_registration.rml`, `lucky_coin_exchange.rml`: Web (`npcWindows.ts`) only provides a generic `chooseInGame` Rena registration dialog, lacking Golden Archer serial exchange, Lena, and Lucky Coin UI.
   - `storage_ext.rml`: replaced in Web by `VaultPages` (`vaultPages.ts` `#hnmo-vault-pages` toolbar) rather than the classic 2nd vault checkbox layout.
   - `united_market_place.rml`: replaced in Web by `confirmInGame` prompt for Julia (`547`).
   - `battle_soccer_score.rml`, `match_status.rml`, `item_endurance.rml`, `item_explanation.rml`, `set_item_explanation.rml`, `help_window.rml`, `quick_command.rml`, `chat_room.rml`, `char_info_balloon.rml`, `notices.rml`, `slide_notice.rml`: not wired as RML documents in Web.
   - Web also replaces pre-world RML scenes (`login_main`, `login_scene`, `server_select`, `server_msg`, `char_sel_main`, `char_make`, `title_scene`, `remember_password_prompt`, `reconnect_dialog`) with responsive HTML/DOM overlays and adds `9` `hnmo_*.rml` panels + DOM modals (Arena, Escrow, Jewel Bank, Market, Rankings, Timetable, Top-Up, etc.).
3. **Season 9 Desktop Client UI (`Data/Interface/GFx/`: `96` `.ozg` Scaleform/GFx panels, `0` implemented in HNMO)**:
   - Season 6.3 Data uses `.ozj`/`.ozt`/`.tga` fixed textures and `60` `Local/*/Minimap*.bmd` files.
   - Season 9 Data introduces `Interface/GFx/` (`96` `.ozg` windows, `.ozd` textures, `NaviMap.ozg` + `44` `Navimap*.OZP` PNG minimap assets including maps `92, 93, 96, 97, 99, 100, 101, 102, 103`, while `Local/*/Minimap*.bmd` drops to `3`).
   - **Zero** of the Season 9 system UI windows exist in HNMO C++ Desktop or Web (`Muun`, `Errtel`, `Gremory`, `Rummy`, `ArkaBattle`, `OrdealSquare`, `StrongestMatch`, `NaviMap` `.OZP` reader = `0` hits; `Pentagram` appears only once in `Core/Globals/_enum.h`).

---

## 2. HNMO Desktop C++ RmlUi vs HNMO Web UI Inventory

### 2.1 Shared Desktop + Web RML Documents (`83` top-level `.rml`)
Core HUD, inventory (`my_inventory`, `inventory_extension`, `storage`, `mix_inventory`, `trade`, `npc_shop`, `my_shop`, `purchase_shop`), character/stats (`character_info`, `master_level`, `master_level_band`, `pet_info`), social/chat (`command_window`, `chat_command`, `chat_input`, `chat_log`, `system_log`, `party_info`, `party_list`, `guild_info`, `guild_make`, `friend_shell`, `letter_read`, `letter_write`), Cash Shop (`in_game_shop`, `igs_buy_package`, `igs_buy_select`, `igs_send_gift`), quests (`my_quest_info`, `npc_dialogue`, `npc_quest`, `quest_progress`, `quest_progress_etc`), Duel/Gens (`duel_window`, `duel_watch`, `duel_watch_frame`, `duel_watch_spectators`, `gens_ranking`), Autoplay (`mu_helper_bar`, `mu_helper_config`, `mu_helper_detail`, `mu_helper_skill_picker`), events (`blood_castle_enter`, `blood_castle_time`, `devil_square_enter`, `chaos_castle_time`, `crywolf`, `cursed_temple_enter`, `cursed_temple_result`, `cursed_temple_system`, `doppelganger_enter`, `doppelganger_frame`, `empire_guardian_enter`, `empire_guardian_timer`, `hud_status`, `siege_warfare`, `guard_window`, `castle_window`, `gateman`, `gate_switch`, `catapult`), and system (`main_frame`, `mini_map`, `move_command`, `option_window`, `sys_menu`, `window_menu`, `tooltip`, `buff_strip`, `map_name`, `loading`, `login`, `generic_confirm_dialog`, `generic_menu_dialog`, `message_box_view`, `workspace`, `notices`).

> Note: Web extensions (`autoPlayHnmoTabs.ts` injecting Extras/Ultimate tabs into `mu_helper_config`, `vaultPages.ts` toolbar over `storage`, and `9` `hnmo_*.rml` docs under `client/web/public/data/Interface/RmlUi/`) intentionally diverge from vanilla desktop C++.

### 2.2 Desktop RML Documents Missing or Differing in Web (`34` top-level `.rml` / `2` themed extensions)

| Desktop RML | Desktop C++ Wiring | Web Status | Required / Recommended Parity Action |
|---|---|---|---|
| `lucky_item.rml` (`themes/{legacy,modern}`) | `MIXTYPE` Lucky Item (`NPC 579`) | **Stubbed** (`npcWindows.ts:88-91`: *"not yet supported"*) | Wire Lucky Item register/exchange UI in Web (`mixWindow.ts` or dedicated RML panel). |
| `kanturu_enter.rml`, `kanturu_info.rml` | `KanturuEvent.h`, `WindowSystem.cpp` | **Missing** in Web | Add Kanturu gateway NPC dialog and battle timer HUD in Web (`instanceEventWindows.ts` or `eventWindows.ts`). |
| `gold_bowman.rml`, `gold_bowman_lena.rml`, `lucky_coin_registration.rml`, `lucky_coin_exchange.rml` | `GoldBowmanWindow.h`, `GoldBowmanLena.h`, `RegistrationLuckyCoin.h`, `ExchangeLuckyCoin.h` | **Minimal stub** (`npcWindows.ts:97-119`: Rena registration only via `chooseInGame`) | Implement full RML serial reward, Lena, and Lucky Coin 10/20/30 exchange dialogs if enabled on S6/S9 servers. |
| `storage_ext.rml` (`themes/{legacy,modern}`) | `CStorageInventoryExt` | **Replaced** by `VaultPages` (`vaultPages.ts`) | Intentional HNMO upgrade (multi-page vault). Keep; ensure server extension flag sync stays clean. |
| `united_market_place.rml` | `UnitedMarketPlaceWindow.h` | **Replaced** by `confirmInGame` (`npcWindows.ts:53-62`) | Optional visual parity; confirm dialog currently handles warp cleanly. |
| `battle_soccer_score.rml`, `match_status.rml` | `BattleSoccerScore.h`, `MatchStatusRmlModel.h` | **Missing** in Web | Add Battle Soccer & generic match scoreboard HUD in Web when PvP guild/team matches run. |
| `item_endurance.rml` | `ItemEnduranceInfo.cpp` | **Missing** in Web | Add durability warning widget when equipped gear/pet durability falls low. |
| `item_explanation.rml`, `set_item_explanation.rml`, `help_window.rml`, `quick_command.rml` | `ItemExplanationWindow.h`, `HelpWindow`, `QuickCommand` | **Missing** in Web | Optional F1 help/item explanation parity (`H`/`F1` already mapped to HNMO timetable/guide overlays). |
| `chat_room.rml` | Friend 1:1 Chat Room | **Missing** in Web | Letter box works via `friendWindows.ts`; 1:1 live chat room UI missing. |
| `notices.rml`, `slide_notice.rml`, `char_info_balloon.rml`, `world_labels.rml`, `main_frame_top.rml`, `cursor_item.rml` | C++ HUD/world layers | **Replaced** by Web DOM/Canvas (`combatOverlay.ts`, canvas text, HTML tooltips) | Keep Web DOM equivalents; verify no clipped text on narrow screens. |
| `login_main`, `login_scene`, `server_select`, `server_msg`, `char_sel_main`, `char_make`, `title_scene`, `remember_password_prompt`, `reconnect_dialog`, `msg_win`, `diagnostics`, `credit_win` | Pre-world RmlUi scenes | **Replaced** by Web HTML (`main.ts`, `seasonColumns.ts`) | Intentional Web-native login/server/character selection flow. |

---

## 3. Season 9 Desktop Client UI (`Interface/GFx/`) Missing in HNMO

Season 9 replaces/extends fixed bitmap panels with `96` Scaleform `.ozg` UI files and `NaviMap` textures (`44` `Navimap*.OZP`). The tables below classify every S9 UI window HNMO must add for full Season 9 desktop & web parity:

### 3.1 New Season 9 Gameplay & System Windows (`0` implemented in HNMO Desktop C++ or Web)

| S9 System | Desktop Client S9 Assets (`Data/Interface/GFx/`) | Data Files Inspected | HNMO Gap & Required UI Work |
|---|---|---|---|
| **Muun Companion** | `PetInventoryFrame.ozg`, `MuunExchange.ozg`, `PetInfoPopup.ozg`, `PetFrame.ozg` | `MuunInfo.bmd` (76), `MuunOption_eng.bmd` (37), `MuunExchange.bmd` (3+3), `MuunTooltipInfo.bmd` (82) | Add Muun Inventory (`Y`), Main/Sub equip slots, Feed/Evolution drop-on-slot UX, Wizardry Stone exchange NPC (`MuunExchange`), energy converter, and minimap/world Muun follower icon (`PetFrame`). |
| **Pentagram & Errtel** | `Pentagram.ozg`, `MixStone.ozg`, `MixSel.ozg` | `PentagramOption_eng.bmd` (15+7), `PentagramJewelOptionValue_eng.bmd` (60), `PentagramMixNeedSource.bmd` (30) | Add Pentagram equipment slot (`slot 236` in Character/`my_inventory`), 5-slot Errtel socket inspection/insertion/extraction modal (`Pentagram.ozg`), and Pentagram/Errtel Blessing/Combination Mix NPC UI (`MixStone`/`MixSel`). |
| **Gremory Case** | `GremoryCase.ozg` | — | Add Character / Account event reward storage window (`K`) with expiry countdown & claim to inventory. |
| **Event Inventory & MU Rummy** | `EventInventoryFrame.ozg`, `MiniGameRummy.ozg` | — | Add Event Inventory drawer and MU Rummy card mini-game window (`MiniGameRummy`). |
| **Arca Battle** | `ArkaBattleRegister.ozg`, `ArkaBattleProgress.ozg`, `ArkaBattleNotice.ozg`, `ArkaResultInfo.ozg` | Server S9 events (`Season 9 server suite`) | Add Arca Battle guild sign-up (`200` Sign of Lord UI), obelisk occupation HUD, notices, and reward summary window. |
| **Tormented Square** | `OrdealSquareEnterMenu.ozg`, `OrdealSquareScore.ozg`, `OrdealSquareResult.ozg` | Server S9 events | Add Tormented Square team entry dialog, live wave/point scoreboard HUD, and round/final result modal. |
| **Chaos Castle Survival** | `StrongestMatchMenu.ozg`, `StrongestMatchRank.ozg`, `StrongestMatchResult.ozg` | Server S9 events | Add Chaos Castle Survival registration/preliminary window, live point leaderboard, and finals result modal. |
| **Party / Guild Matching & Store Search** | `MatchingSelect.ozg`, `MatchingParty.ozg`, `MatchingGuild.ozg`, `MatchingAlarm.ozg`, `SearchPrivateStore.ozg`, `RebuyList.ozg` | Server S9 social/store packets | Add Party/Guild Matching finder UI, Personal Store cross-map search (`SearchPrivateStore`), and NPC Shop Buyback (`RebuyList`). |
| **Season 9 Minimap (`NaviMap`)** | `NaviMap.ozg` + `44` `.OZP` maps (`Navimap01.OZP`..`Navimap103.OZP`) | `Gate.bmd` (438), `movereq_eng.bmd` (37) | HNMO Desktop C++ & `client/web/src/game/miniMapData.ts` still load `WorldN/mini_map.ozt` + `Minimap_WorldN_eng.bmd` (only `3` `.bmd` files in S9 Data). Add `.OZP` loader/fallback for S9 maps `92, 93, 96, 97, 99, 100, 101, 102, 103` (Acheron / Debenter / Uruk Mountain / Tormented Square). |

### 3.2 Shared Core Windows Redesigned in S9 GFx (`Interface/GFx/`)
The remaining `.ozg` panels mirror existing HNMO RmlUi windows (`MainFrame`, `InventoryFrame`, `ExtensionBagFrame`, `StorageInven`, `CharacterFrame`, `PlayerInfo`, `MixInventoryFrame`, `MixGoblinSel`, `NpcShop`, `Trade`, `PrivateStoreFrame`, `PurchaseInven`, `PartyFrame`, `GuildFrame`, `GuildCreateFrame`, `GuildInfoFrame`, `GuildPosition`, `SnsWindow`, `ChatWindow`, `CommandWindow`, `MacroMain`/`MacroSub`, `Option`, `MoveCommand`, `BuffFrame`, `ItemInfo`, `MyQuestInfo`, `NpcDialogue`, `NpcQuestProgress`, `NpcJobChangeQuest`, `EnterBloodCastle`, `EnterEmpireGuardian`, `CursedTemple*`, `Duel*`, `Gens*`, `ListofMatches`, `SystemMenu`, `QuickCommand`, `HelpWindow`, `GoldArcher`, `Trainer`, `EquipDurabilityInfo`). HNMO already covers these via RmlUi `themes/modern` and `themes/legacy`, so visual S9 GFx parity is lower priority than implementing the **missing S9 system panels** above.
