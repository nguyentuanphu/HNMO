# Feature Gap Analysis: Previous Server Packages vs. HNMO

> Date: 2026-11-18  
> Source compared: Two Windows server suites previously used by the owner (a Season 6.3 server package and a Season 9 Episode 2 server package, plus accompanying Windows configuration and database tools), described entirely in HNMO's own words.

## Legend

- **Already in HNMO**: Implemented and usable in HNMO's .NET server, Admin Panel, and/or web client.
- **Partly in HNMO**: Core exists in HNMO, with specific parity items still needed.
- **Missing**: Not yet implemented in HNMO.
- **S9 Data Needed**: Season 9-only system or content requiring Season 9 client/map/item/UI data that HNMO does not ship yet.

---

## Summary & Season 9 Boundary

HNMO already covers **most core Season 6.3 server & web-client mechanics**, plus numerous VN-server and live-ops features the reference packages do not include (web push event alerts, built-in WebSocket browser client, crash-watch auto-restart, dynamic catch-up cap, season progress gating, mount shard chain, guild levels/weekly ladder, missions/login calendar, and item/economy telemetry).

The main gaps fall into three buckets:
1. **Season 6-ready server/gameplay systems** that work with HNMO's existing game data (e.g., Lucky Item system, Cherry Blossom & Lucky Coin exchanges, Elemental/Pet/Buff tweaks, Gens full battle-zone/chat parity, Castle Siege Senior mix, Mining / MU Rummy / Bot helpers, enhanced GM/Anti-hack server checks).
2. **Quality-of-life & Desktop-to-Web Parity** (e.g., `/lock` & `/unlock` item lock command parity, per-skill cooldown wipe, personal store non-zen currencies in web client, Mobile/Tablet adaptive HUD ergonomics).
3. **Season 9-Only Systems (S9 Data Needed)**: features introduced after Season 6.3 that depend on Season 9 maps, items, monster bags, Scaleform/GFx interface assets, or BMD v14+ models not yet in HNMO's data pack (e.g., Arca Battle, Acheron / Acheron Guardian, Tormented Square, Chaos Castle Survival, Illusion Temple Renewal, Pentagram & Erzsebet/Errtel elemental system, Muun pet system & Evolution Stone, Stat Specialization, BattleCore cross-server server type, and Season 9 maps/monsters/wings/master skills up to level 330+).

---

## 1. Gameplay Systems

| Feature / System | Status | Where in HNMO / What's Missing | Scope |
|---|---|---|---|
| Character progression, Level 400, 3rd class quests | **Already in HNMO** | `GameLogic/PlayerActions/Quests`, `Persistence/Initialization`; web quest windows (`game/quests.ts`) | S6 |
| Master Level & Master Skill Tree (200 cap) | **Partly in HNMO** | Server Master Level & tree exist (`ui/masterLevelWindow.ts`); web UI missing full master damage calculations & casting all strengthened master skills outside Rage Fighter | S6 |
| Extended Master Level (330+ cap & S9 skill tree nodes) | **Missing** | **S9 Data Needed**: Requires Season 9 master skill tree BMD/data & level 330+ expansion | S9 only |
| Resets & Relife + `/reset` command | **Already in HNMO** | `GameLogic/PlugIns/Resets/` (`ResetOptions`), `/reset`, `/delegatedreset`, web + Admin `/resets` | S6 |
| Bulk Stat Add (`/add`, `/addstr`, `/addagi`, `/addvit`, `/addene`, `/addcmd`) & `Shift+A` UI | **Already in HNMO** | `PlugIns/ChatCommands/`, `PlugIns/PlayerRanking/` + `ui/addStatsWindow.ts` (bulk dialog & `/add*` commands) | S6 |
| In-game Auto-Play Helper (MU Helper / Bot) | **Already in HNMO** | Server `MuHelper` packets + web `game/autoPlay.ts`, `ui/autoPlayWindows.ts` (`Z`) + extra auto-return/resupply (`autoPlayExtras.ts`) | S6 |
| Helper / Buffer Bots spawned as world NPCs (`BK_Helper`, `SM_Helper`, `BufferBot`) | **Partly in HNMO** | Buffer NPC (`PlugIns/BufferNpc/`, `/buffer-npc`) covers Buff bots; PvP/combating AI helper bots (`BK_Helper`, `SM_Helper`) are **Missing** | S6 |
| Offline Leveling (`/offlevel`) | **Partly in HNMO** | `OfflineLevelingChatCommandPlugIn` + `OfflinePlayer`; needs full auto-skill/potion/party-pickup parity & explicit per-map/VIP limits like dedicated `/offlevel` configs | S6 |
| Character Specialization / Stat Focus bonus | **Missing** | Bonus stats scaling by stat distribution profile (STR/AGI/ENE tiers) | S9 / Custom |
| Gens System (Duprian / Vanert ranks & strife rules) | **Partly in HNMO** | `game/gens.ts`, `ui/gensWindow.ts` join/leave/rewards work; missing strife-map PvP/move locks, all-player chat/nameplate icons, and admin-tunable contribution brackets | S6 |
| Monster AI Groups / Multi-phase Boss AI / Move Paths | **Partly in HNMO** | Basic boss skills & invasions exist; Crywolf/Doppelganger scripted path waypoints & multi-element AI rule tables are **Partly** supported (`MonsterSpawnArea`) | S6 |
| Configurable Formula Tables (CalcCharacter / ExpCalc / ItemCalc overrides) | **Partly in HNMO** | Class attributes are C# calculated; `PlugIns/GameplayRules/` adds damage caps & Sydney XP bonus, but full per-class PvP vs. PvM damage multipliers & dynamic formula tuning per pair of classes are **Partly** missing | S6 / S9 |
| Muun Pet System, Muun Slots, Evolution Stone & Mount/Pet Fusion | **Missing** | **S9 Data Needed**: Dedicated Muun inventory, timed evolutions, Muun Exchange, and 3D assets not present | S9 only |
| Pentagram & Elemental Combat System (Fire/Water/Earth/Wind/Darkness) | **Missing** | **S9 Data Needed**: Elemental attack/defense tables, Pentagram equip slot, Errtel socketing/combining, and elemental damage UI | S9 only |

---

## 2. Events

| Event | Status | Where in HNMO / What's Missing | Scope |
|---|---|---|---|
| Blood Castle (1-8) | **Already in HNMO** | `GameLogic/MiniGames/BloodCastle*`, web `game/events.ts`, `/bcstart` | S6 |
| Devil Square (1-7) | **Already in HNMO** | `GameLogic/MiniGames/DevilSquare*`, web `game/events.ts`, `/dsstart` | S6 |
| Chaos Castle (1-7) | **Already in HNMO** | `GameLogic/MiniGames/ChaosCastle*`, falling walls & terrain updates (`game/events.ts`) | S6 |
| Illusion Temple Classic (1-6) | **Already in HNMO** | `GameLogic/MiniGames/IllusionTemple*`, web `game/instanceEvents.ts`, `ui/instanceEventWindows.ts` | S6 |
| Illusion Temple Renewal (Season 9) | **Missing** | **S9 Data Needed**: Occupied-pedestal tournament rules, 6-band renewal NPC/map scripts & client packets | S9 only |
| Doppelganger | **Already in HNMO** | `GameLogic/MiniGames/Doppelganger*`, web `game/instanceEvents.ts` | S6 |
| Imperial Guardian (Gaion & Fortresses) | **Already in HNMO** | `GameLogic/MiniGames/ImperialGuardian*`, web `game/instanceEvents.ts` | S6 |
| Kanturu (Maya / Nightmare) & Raklion (Selupan) | **Already in HNMO** | `GameLogic/MiniGames/Kanturu*`, `/kt*`, Raklion boss & drop bags | S6 |
| Crywolf Fortress & Castle Siege + Loren Deep / Land of Trial | **Already in HNMO** | Server Castle Siege/Crywolf + `game/crywolf.ts`, `game/castleSiege.ts`, `ui/castleSiegeWindows.ts` | S6 |
| Scheduled Invasions (Red Dragon, Golden, White Wizard, Rabbits, Snowman/XMas, Pouch of Blessing, Moon Rabbit, Flame Ghost) | **Partly in HNMO** | `PlugIns/InvasionEvents/` supports Golden, Red Dragon, White Wizard, Rabbit, Snowman, Find-the-GM; Missing dedicated Ring Attack, Moon Rabbit / Fire Flame Ghost / Snake egg invasions & custom per-wave invasion XML scheduling | S6 |
| World Bosses (Timed spawns, Damage Rank Rewards, Discord/Website Alerts) | **Already in HNMO** | `PlugIns/WorldBosses/` + `ui/worldBossWindow.ts` (exceeds reference packages) | S6 |
| Bonus EXP / Drop Event Windows (per-map, per-day-of-week schedule) | **Partly in HNMO** | `PlugIns/GameplayRules/` supports Sydney happy-hour bonus; missing granular per-map + per-day multi-slot bonus schedules | S6 |
| Last Man Standing (scheduled PvP elimination arena event) | **Missing** | Timed open-registration PvP survival event with auto-warp, elimination count, and winner pot reward (works on existing S6 arenas) | S6 |
| Cherry Blossom Mix / Collection Event (NPC) | **Missing** | Turn in Cherry Blossom Play Boxes/Branches for timed buffs (Rice Cake / Wine / Petal) and 3-tier item bag rewards | S6 |
| Lucky Coin Exchange Event (10 / 20 / 30 coins) | **Missing** | Exchange stacked Lucky Coins at Senior/Event NPC Delgado for tiered random item bags | S6 |
| Mining System (Pandora Pick / Jewels Extraction) | **Missing** | Equip excavation pick on Mining Areas for multi-stage Jewel of Bless/Soul rewards (or fail/break risk) | S6 |
| MU Rummy Card Mini-Game | **Missing** | Client & server card-matching event mini-game (works best once dedicated mini-game UI/packets are added) | S6 / S9 |
| Egg Hunt Event (Event Inventory) | **Missing** | Separate temporary Event Inventory + egg drops spawning event monsters/rewards | S6 / S9 |
| Arca Battle (Guild vs Guild Obelisk War) | **Missing** | **S9 Data Needed**: Guild registration, elemental obelisks, Acheron server channel, Booty Mix, Conqueror buffs | S9 only |
| Acheron Guardian (Boss Defense) | **Missing** | **S9 Data Needed**: Requires Acheron map, Elemental boss/obelisks & reward bags | S9 only |
| Chaos Castle Survival | **Missing** | **S9 Data Needed**: Multi-round preliminary -> semi-final -> final BattleCore survival tournament | S9 only |
| Tormented Square | **Missing** | **S9 Data Needed**: Timed multi-wave PvM/PvP tournament on map 102 (`TormentedSquare`) | S9 only |
| BattleCore Dedicated Cross-Server Channel | **Missing** | Cross-realm tournament server instance pooling characters from regular realms | S9 only |

---

## 3. Items & Crafting

| Feature | Status | Where in HNMO / What's Missing | Scope |
|---|---|---|---|
| Standard Item Options (Skill, Luck, +Option, Excellent, Ancient Sets) | **Already in HNMO** | `DefaultDropGenerator`, `GameLogic/Items`, tooltips in `client/web/src/game/itemTooltip.ts` | S6 |
| Chaos Machine (Regular +10..+15, Wings, Fruits, Dinorant, Cape, 380 Options) | **Partly in HNMO** | Server craftings & web `ui/mixWindow.ts` cover core mixes; per-mix customizable rate tables exist via `/vault-chaos-rewards`, but missing Luck/VIP bonus granularity per recipe tier & +15 global mix success announcements | S6 |
| Jewel of Harmony Refinement & Smelting + Option Removal (Elpis / Osbourne / Jerridon) | **Already in HNMO** | Server craftings + `game/itemTooltip.ts` + Refinery/Restore dialogs | S6 |
| Socket System (Seeds, Spheres, Seed Master, Seed Researcher, Socket Bonus) | **Partly in HNMO** | Server sockets & web tooltips work; Seed Master / Researcher UI menus & configurable per-socket drop/mix rates are **Partly** incomplete on web | S6 |
| Jewel Success Rates & Options Caps (Soul/Life rate overrides, max +16/+28 Option toggle) | **Partly in HNMO** | Server item upgrade handlers exist; runtime Admin toggle for Soul/Life/Harmony % and +16 vs +28 max Life option cap is **Missing** | S6 |
| Fixed Item Options & Talismans / Stat Consumables | **Already in HNMO** | `PlugIns/FixedItemOptions/` (`/fixed-options`), `PlugIns/Talismans/` (`/talismans`) | S6 |
| Lucky Items & Chaos Card Lottery (Normal / Mini / Rare / Gold) | **Partly in HNMO** | Web chaos card NPC menu opens; **Missing** Chaos Card mix tier tables, Gambling NPC / Lucky Ticket, and Lucky Item durability decay/repairs | S6 |
| Item Bags per Boss/Box/Grade (`Monster_(id).xml`, `Kundun Box +1..+5`, Ribbon Boxes, Medals, Firecrackers) | **Partly in HNMO** | `DropItemGroup` supports monster/box drops; Admin `/item-drops` edits them, but missing structured weighted multi-pool Item Bag rules (coin + zen + guaranteed option roll + party drop rules + timed expiration items) | S6 |
| Appointed Item Drop & Set Item Drop Manager (per-map/monster/level schedules) | **Partly in HNMO** | `DefaultDropGenerator` + `GameConfiguration` drop groups cover basics; missing Ancient Set Drop Manager rules (scheduled/map-filtered ancient drops outside Land of Trials/Kalima) & timed item drop scheduler | S6 |
| Cancel Item Sale NPC / Item Recovery / Expired Item Handling | **Missing** | Time-limited Cash Shop / Quest items (`PeriodTime`) auto-expiration + NPC/UI re-claim for accidentally sold items (partially covered by NPC buyback, no timed expiry) | S6 |
| Pentagram Combining, Errtel Leveling & Upgrade Mixes | **Missing** | **S9 Data Needed**: Pentagram & Errtel crafting logic and client UI | S9 only |
| Cash Shop / In-Game Item Shop (X Shop) | **Partly in HNMO** | `CashShopStorage`, coin grants (`W Coin`, `Goblin Points`), web & Admin `/cashshop` catalog; missing Goblin Points awarded per monster kill (`MonsterGP`), Cash Shop package bundles & timed items (`PeriodTime`), and full web `X` shop UI parity | S6 |

---

## 4. Economy

| Feature | Status | Where in HNMO / What's Missing | Scope |
|---|---|---|---|
| Personal Stores & Offline Trade (`/offtrade`) | **Partly in HNMO** | `PlugIns/OfflineTrade/` (`/off-trade`, `ui/personalShop.ts`) supports Zen stores & offline persistence; Missing Jewel/W-Coin/Goblin Point currencies in personal store (`PlayerShopPriceTypesExtended` web UI is Zen-only) and per-map/per-tax-rate rules | S6 |
| Jewel Bank (`JewelBankPlugIn`) | **Already in HNMO** | `PlugIns/JewelBank/`, in-game window + `/website/jewel-bank` (exceeds reference packages) | S6 |
| Item Market & Escrow Contracts | **Partly in HNMO** | `PlugIns/ItemMarket/` (`/market`) provides coin escrow listings; P2P/Direct VND & Trade Credit Escrow roadmap (`ROADMAP 16`) queued next | S6 |
| Multi-Currency Zen Drop Formula & Custom Price Overrides (`PriceSettings`) | **Partly in HNMO** | Zen multipliers & `Value` exist; runtime per-item buy/sell override config and monster-level scaled Zen formulas are **Partly** missing | S6 |
| Top-Up Milestones & VietQR Bank Auto-Credit (Casso / SePay) | **Already in HNMO** | `PlugIns/TopUpMilestones/`, `topup.html` (far exceeds reference packages) | S6 |
| Economy Dashboard & Dupe/Threshold Telemetry | **Already in HNMO** | `PlugIns/Economy/` (`/economy-dashboard`) (far exceeds reference packages) | S6 |

---

## 5. PvP & Guild

| Feature | Status | Where in HNMO / What's Missing | Scope |
|---|---|---|---|
| PK System & `/pkclear` | **Partly in HNMO** | `PKChatCommandPlugIn`, `/pkclear`, and `TeleportNpc` PK gates exist; missing full PK configuration (PkLimitFree rules for shop/warp/drop, per-map PvP/NonPK overrides, PK timer & kill-level Zen cost multipliers) | S6 |
| Safe-Zone Attack Blocking & Server-Side Skill Range Enforcement | **Partly in HNMO** | Basic safe-zone checks exist; strict server-side safe-zone & range validation (`EnableAttackBlockInSafeZone`) matching anti-hack parity can be hardened | S6 |
| Guild Create / Alliance / Hostility / Members & Notices | **Already in HNMO** | Server Guilds + `game/guild.ts`, `ui/guildWindows.ts`, guild XP levels & weekly ladder (`PlugIns/Guilds/`) | S6 |
| Guild Wars & Battle Soccer | **Partly in HNMO** | Chat commands `/war`, `/battlesoccer` exist on server; web UI scoreboards/match prompts and war rewards are incomplete | S6 |
| Duel System & Duel Arena Spectator Colosseums | **Already in HNMO** | Server Duel + `game/duel.ts`, `ui/duelWindows.ts` (including Doorkeeper Titus watch mode) | S6 |

---

## 6. Social

| Feature | Status | Where in HNMO / What's Missing | Scope |
|---|---|---|---|
| Friends List & In-Game Messenger / Letters (`F`) | **Already in HNMO** | Chat server + `game/messenger.ts`, `ui/friendWindows.ts` | S6 |
| Party Create / Invite / Kick & Password/Spot Finder | **Already in HNMO** | Standard party + `PlugIns/PartyOptions/` (`/partypass`, hunting spots) | S6 |
| Marriage System (`/prop`, `/accept`, `/teleport`, `/divorce`) | **Already in HNMO** | `PlugIns/Marriage/` + `/marriage` + website stats | S6 |
| Global/Server Chat (`/post`, `/gpost`, custom chat colors, level/zen/VIP gates, banned words) | **Already in HNMO** | `PostChatCommandPlugIn`, `WorldChatChatCommandPlugIn`, `PlugIns/BannedWords/` (`/banned-words`) | S6 |
| Discord Webhook Bridge & Event Web Push Alerts | **Already in HNMO** | `PlugIns/Community/`, `PlugIns/EventAlerts/` (`/event-alerts`) | S6 |
| In-Game Community News Notices (`/news`, timed rotating notices) | **Missing** | **ChatGPT PR scope / ROADMAP 14.20**: periodic rotating in-game news ticker + website news feed | S6 |

---

## 7. Quality of Life

| Feature | Status | Where in HNMO / What's Missing | Scope |
|---|---|---|---|
| Multi-Warehouse Switching (`/ware <number>`) | **Already in HNMO** | Implemented in Multi-Vault (`PlugIns/AccountSafety/` & `/vault-chaos-rewards`) + `/ware` command | S6 |
| Character/Item Lock (`/lock <code>`, `/unlock <code>`) + Account Safety PIN | **Partly in HNMO** | `PlugIns/AccountSafety/` implements Second Password PIN, item lock, and trade/drop blocks via Admin & Account UI; adding `/lock` and `/unlock` chat shortcuts gives instant CLI parity | S6 |
| `/requests <on|off>` (Auto-refuse Trade / Party / Duel / Guild requests) | **Partly in HNMO** | Client request handling works; `/requests on|off` convenience command + saved preference flag is **Missing** | S6 |
| `/online`, `/srvinfo`, `/charinfo` Player Info Commands | **Partly in HNMO** | `OnlineChatCommandPlugIn` & `CharInfoChatCommandPlugIn` exist; `/srvinfo` (show active EXP/Drop/VIP/event rates to player) is **Missing** | S6 |
| Custom Ranks / Titles (`/title`) & Teleport NPC | **Already in HNMO** | `PlugIns/CustomRanks/` (`/custom-ranks`), `PlugIns/TeleportNpc/` (`/teleport-npc`) | S6 |
| Dynamic Item Definitions & Client Override Sync | **Already in HNMO** | `ItemDefinitionStateStore`, `/item-definitions`, `/website/items` | S6 |
| Web Mobile & Tablet Responsive HUD | **Partly in HNMO** | `ui/touchControls.ts` + landscape touch bars exist (`PARITY.md`), **Missing** cohesive mobile-MMO HUD polish matching modern tablet/phone layouts (clean virtual thumbstick, clustered skill arc + big attack trigger + potion quick-slots, compact collapsible top action bar for Market/Events/Rankings/Shop/Auto/Camera, readable HP/MP/SD/AG bar & tabbed chat box, zero horizontal overflow) | Web / Mobile |

---

## 8. Admin & GM Tools

| Feature | Status | Where in HNMO / What's Missing | Scope |
|---|---|---|---|
| Web Admin Panel (`/host-settings`, `/worlds`, `/accounts`, `/plugins`, etc.) | **Already in HNMO** | Blazor AdminPanel (built-in web UI replacing legacy Windows DB/INI editors) | S6 |
| Server Maintenance & Crash Watch (`/control`, backups, state retention) | **Already in HNMO** | `/control`, `CrashWatchStore`, `BackupController`, `DbLogRetention` | S6 |
| GM Chat Commands (`/item`, `/move`, `/trace`, `/watch`, `/dc`, `/dcfriend`, `/gdc`, `/skin`, `/hide`, `/unhide`, `/setpk`, `/setparty`, `/bcstart`, `/cs*`, `/kt*`, `/crywolfset`) | **Partly in HNMO** | Almost all exist under `PlugIns/ChatCommands/` (`BanAcc`, `Skin`, `Hide`, `Trace`, `Track`, `Item`, `CreateMonster`, `Disconnect`, `CastleSiege`/`Crywolf`/`Event` controls); **Missing** per-GM granular privilege flags matrix (`CanUseItemCmd`, `CanUseBanCmd`, etc.) & ChatGPT's in-progress GM/Ban/Mail Admin UI (`PR #64+`) | S6 |
| Server-Suite Migration Importer ("Import from another server") | **Missing** | **Planned in Part 2**: Admin tool to preview, convert (with backup & undo), and import rates, drops, shops, event schedules, chaos mix rates, spawns, and optional MSSQL accounts/characters/inventories/guilds into HNMO | S6 / S9 |
| Monster Spawn / Item Drop / Cash Shop Visual Editors | **Partly in HNMO** | `/map-editor`, `/terrain-editor`, `/item-drops`, `/item-definitions`, `/cashshop`; **3D World Editor (`14.24`)** upcoming in Phase 3 | S6 |

---

## 9. Anti-Cheat & Security

| Feature | Status | Where in HNMO / What's Missing | Scope |
|---|---|---|---|
| Multi-Connection Limit per Machine / IP (`MachineIDConnectionLimitCount`) | **Partly in HNMO** | `Relay` forwards real client IP (`SendClientAddress`); hard max-connections-per-IP/fingerprint cap across Login/GameServer with Admin setting & kick alert is **Missing** | S6 |
| Server-Side Attack Speed & Hit-Frequency Validation (Anti-Speed / Anti-HitHack) | **Missing** | Server-side agility-to-attack-interval sanity check, skill cooldown enforcement, and configurable hit-rate violation counter -> disconnect / optional auto-ban | S6 |
| Safe-Zone & Terrain Attack/Move Enforcement | **Partly in HNMO** | Safe-zone checks exist in attack handlers; unified server-side safe-zone attack block + anti-reflection timing check + wall/clipping walk validation needed | S6 |
| Client Integrity & Checksum Verification | **Already in HNMO** | `HNMO.Integrity` (Ed25519 signed manifest verified at startup) & SimpleModulus + Xor32 packet encryption; client binary CRC/DLL checks do not apply directly to open browser JS, where server authority replaces client trust | S6 / Web |
| Prohibited Words & Character/Guild Name Symbol Filtering | **Already in HNMO** | `PlugIns/BannedWords/` (`/banned-words`, Unicode/leetspeak/substring checks for chat, character create, guild create) | S6 |
| Item Serial Tracking & Dupe Anomaly Detection | **Already in HNMO** | `PlugIns/Economy/` anomaly alerts + `PlugIns/ActivityLog/` origin tracking + Escrow cooldowns (upcoming) | S6 |

---

## 10. Logging

| Feature | Status | Where in HNMO / What's Missing | Scope |
|---|---|---|---|
| Rolling File Logs (`server.log`, `database.log`) + Gzip Retention & `/control` Viewer | **Already in HNMO** | `RollingLogWriter`, `ControlEndpoints`, `/control` log search & download | S6 |
| Structured Player Activity Log (Trades, Drops, Vault, Shops, Chaos Mix, Kills, Pickups) | **Already in HNMO** | `PlugIns/ActivityLog/` (`/activity-log`, persisted in PostgreSQL with export/purge via `/db-log-retention`) | S6 |
| Chat & GM Command Audit Log | **Already in HNMO** | Chat log & Admin audit persistence with configurable retention & pre-purge CSV/archive export | S6 |
| Crash Incident History & Admin Banners | **Already in HNMO** | `CrashWatchStore`, `/control`, `/crash-watch` | S6 |
| Flood / Anti-Hack Violation Dedicated Audit Log | **Missing** | Structured DB/log table capturing rejected packets, speed-hack flags, safe-zone attack attempts, and connection-limit kicks for Admin inspection | S6 |

---

## Recommended Priority Order (for Part 3, Until Overridden by Owner)

All items below work with **HNMO's existing Season 6.3 client data**:
1. **Anti-Hack Server Authority (`9. Anti-cheat` + `10. Logging`)**: Server-side attack-speed / hit-frequency & safe-zone validation + max-connections-per-IP limit + dedicated violation log (`/anti-cheat`).
2. **Server-Side Gameplay Rate & Formula Overrides (`1. Gameplay` + `3. Items`)**: Admin-tunable Jewel of Soul/Life rates, Max Option `+16/+28`, chaos mix Luck/VIP rates, +15 global broadcast toggle, and per-map/day bonus schedules.
3. **S6 Events Completion (`2. Events`)**: Last Man Standing PvP elimination event, Cherry Blossom Exchange, Lucky Coin Exchange, and scheduled Boss/Egg multi-wave Invasion tables + Item Bag multi-pool weighted drop rules.
4. **QoL & Store Parity (`4. Economy` + `7. QoL`)**: `/lock` / `/unlock` commands, `/requests on|off`, `/srvinfo`, personal store multi-currency (Jewels / Coins) in web UI, and Mining System.
