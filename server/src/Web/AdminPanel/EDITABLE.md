# What the admin panel can edit

Every kind of HNMO data and setting, and where to change it in the admin panel.
"Sidebar" paths are the groups of the left menu. All game configuration editors save
into the database; most changes apply at once, some (maps, servers) after a server restart.

The generic editors work for any stored type:

- `edit-config-grid/<full type name>/` lists all objects of a type (edit, duplicate, delete, add);
- `edit-config/<full type name>/<id>` edits one object, with its nested objects;
- `edit-config/HNMO.DataModel.Configuration.GameConfiguration/` (Sidebar: Game, Advanced,
  Full configuration) edits the whole game configuration in one form.

So every type below is at least editable through the full configuration; the table names the
direct way.

Last checked: 2026-11-14 (Sydney time), with the browser test `client/web/tests/e2e/admin-panel.spec.ts`
(every sidebar link opens, every grid's first entry opens in the editor) and a save round trip
of the duel configuration.

## Game configuration (`GameConfiguration` and what it contains)

| Data | Type | Where to edit | Editable |
|---|---|---|---|
| General rules (levels, experience rates and formulas, money limits, drop rules, party size, letters, durability, character name rule) | `GameConfiguration` | Sidebar: Game, Rules and plugins, General | yes |
| Character classes, their stats, base values, attribute combinations, start maps, combos | `CharacterClass`, `StatAttributeDefinition`, `SkillComboDefinition` | Game, Characters and skills, Character classes | yes |
| Skills, master skills | `Skill`, `MasterSkillDefinition` | Game, Characters and skills, Skills | yes |
| Master skill trees | `MasterSkillRoot` | Game, Characters and skills, Master skill roots | yes (link added) |
| Magic effects / buffs | `MagicEffectDefinition`, `PowerUpDefinition` | Game, Characters and skills, Magic effects | yes (link added) |
| Attributes | `AttributeDefinition` | Game, Characters and skills, Attributes | yes (link added) |
| Global attribute combinations and base values | `AttributeRelationship`, `ConstValueAttribute` | Game, Advanced | yes (links added) |
| Items (requirements, options, power-ups, prices, slots, storage limits) | `ItemDefinition`, `AttributeRequirement`, `ItemBasePowerUpDefinition`, `LevelBonus` | Game, Items, Items | yes |
| Item options | `ItemOptionDefinition`, `IncreasableItemOption`, `ItemOptionOfLevel` | Game, Items, Item options | yes |
| Item option types | `ItemOptionType` | Game, Items | yes |
| Item sets | `ItemSetGroup`, `ItemOfItemSet` | Game, Items | yes |
| Option combination bonuses | `ItemOptionCombinationBonus`, `CombinationBonusRequirement` | Game, Items | yes (link added) |
| Level bonus tables | `ItemLevelBonusTable` | Game, Items | yes (link added) |
| Item stat profiles | `ItemStatProfile`, `ItemStatTerms` | Game, Items | yes (link added) |
| Item prices | `ItemPriceDefinition`, `ItemLevelPrice`, price modifiers | Game, Items | yes (link added) |
| Item slot types | `ItemSlotType` | Game, Items | yes (link added) |
| Jewel mixes | `JewelMix` | Game, Items, Jewel mixes | yes |
| Crafting recipes (chaos machine and other mixing NPCs) | `ItemCrafting`, `SimpleCraftingSettings`, required/result items | Game, Items, Item craftings | yes (link added; also inside the NPC's monster definition) |
| Maps, spawns, gates, battle zones, terrain variants | `GameMapDefinition`, `MonsterSpawnArea`, `EnterGate`, `ExitGate`, `BattleZoneDefinition`, `GameMapTerrainVariant` | Game, World and monsters, Game maps; Map editor; Terrain editor | yes |
| Monsters and NPCs (stats, drops, merchant stores, quests, crafting) | `MonsterDefinition`, `MonsterAttribute` | Game, World and monsters, Monsters | yes |
| Warp list | `WarpInfo` | Game, World and monsters, Warp list | yes |
| Mini games (event maps, rewards, waves) | `MiniGameDefinition`, rewards, spawn waves, change events | Game, World and monsters, Mini games | yes |
| Quests | `QuestDefinition`, requirements, rewards | Game, World and monsters, Quest definitions | yes (link added; also inside the quest NPC) |
| Merchant stores | `MonsterDefinition.MerchantStore` | Game, Shops and drops, Merchant stores | yes |
| Cash shop products, packages, catalog | `CashShopProduct`, `CashShopPackage` | Game, Shops and drops, Cash shop catalog | yes |
| Cash shop settings | `CashShopConfiguration` | Game, Rules and plugins, Cash shop configurations | yes (link added) |
| Drop item groups | `DropItemGroup`, `ItemDropItemGroup` | Game, Shops and drops, Drop item groups / Item drops | yes |
| Duel settings and arenas | `DuelConfiguration`, `DuelArea` | Game, Rules and plugins, Duel configurations | yes (link added; save tested) |
| Castle siege settings, NPCs, upgrades, zones, schedule | `CastleSiegeConfiguration` and its parts | Game, Rules and plugins, Castle siege configurations | yes (link added) |
| Content activation (which content is on) | plug-in and configuration switches | Game, Rules and plugins, Content activation | yes |
| Plug-ins: on/off and every plug-in's own settings (game name, events, resets, chat commands, connection limits, ...) | `PlugInConfiguration` + each plug-in's configuration class | Game, Rules and plugins, Plugins (gear button opens the settings) | yes |
| Chat commands (who may use them, settings) | chat command plug-ins | Game, Rules and plugins, Chat commands | yes |
| Configuration updates | `ConfigurationUpdate` | Administration, System, Updates | install only (by design) |
| Captions / translations of game data | localized strings | Administration, System, Captions | yes |

## System and servers

| Data | Type | Where to edit | Editable |
|---|---|---|---|
| System settings (IP resolver, auto start, time zone, packet capture limits) | `SystemConfiguration` | Game, Advanced, System | yes |
| Game client versions | `GameClientDefinition` | Game, Advanced, Game clients | yes |
| Game servers (name, maximum players, experience rate, maps, endpoints) | `GameServerDefinition`, `GameServerConfiguration`, `GameServerEndpoint` | Overview, Servers (server name link); Game, Advanced, Game server configurations | yes |
| Connect servers | `ConnectServerDefinition` | Overview, Servers (server name link) | yes |
| Chat server | `ChatServerDefinition`, `ChatServerEndpoint` | Overview, Servers (server name link) | yes |
| Start, stop, add, remove servers; global messages | runtime | Overview, Servers | yes |
| Logging levels | Serilog settings | Administration, Inspect, Logging configuration | yes |
| Admin users, API keys, second factor | panel users | Administration, System, Users / API keys; account security | yes |
| Discord bot | Discord settings | Administration, System, Discord | yes |
| Database connection | `ConnectionSettings.xml` | not in the panel | no: the panel needs the database to start; edit the file |
| Web game ports, relay, launcher | `web/appsettings.json` | not in the panel | no: it belongs to the web game process, which starts before the server |
| Integrity check mode, database provisioning | `server/appsettings.json` | not in the panel | no: read once at start-up |

## Accounts and players

| Data | Type | Where to edit | Editable |
|---|---|---|---|
| Accounts (login, password, state/ban, e-mail, vault, characters) | `Account`, `ItemStorage` | Players, Accounts, Edit | yes |
| Characters (level, stats, class, map, position, money, inventory, skills, quest states, appearance) | `Character`, `Item`, `SkillEntry`, `CharacterQuestState`, `AppearanceData` | Players, Accounts, Edit, then the character; Players, More player data, Characters | yes |
| Items of a character or vault | `Item`, `ItemOptionLink` | from the character's inventory (item editor) | yes |
| Cash shop coins | `CashShopCoinGrant` | Players, Accounts, Coins | yes |
| HNMO skills of an account | HNMO skill grants | Players, Accounts, HNMO skills | yes |
| External links (Discord, ...) | `AccountExternalLink` | Players, Accounts, Links | yes |
| Online players (disconnect, stop off-level and bot players, inspect packets) | runtime | Overview, Online accounts | yes |
| Guilds | `Guild`, `GuildMember` | Players, Guilds, guild page, "Edit guild data" | yes (button added; not tried on a real guild, the test save has none) |
| Alliances | guild hierarchy | Players, Guilds, alliance page | view only (change through the guilds) |
| Castle siege state, owner, taxes, registrations | `CastleSiegeData`, `CastleSiegeGuildRegistration`, `CastleSiegeNpcState` | Players, Castle siege; More player data, Castle siege data | yes |
| Letters | `LetterBody` | Players, More player data, Letter bodies | yes (link added) |
| Gens membership | `GensMember` | Players, More player data, Gens members | yes (link added) |
| Cash shop gift storage | `CashShopStorageItem` | Players, More player data, Cash shop storage items | yes (link added) |
| Crywolf state | `CrywolfData` | Players, More player data, Crywolf data | yes (link added; empty until the event ran) |
| Gens abuse records, pending castle siege rewards, guild chat bindings | `GensAbuse`, `CastleSiegePendingReward`, `GuildChatBinding` | `edit-config-grid/HNMO.DataModel.Entities.<type>/` | yes, by address only (rare, written by the game) |
| Leaderboards, mini game rankings | `MiniGameRankingEntry`, statistics | Overview, Leaderboards | view only: results the game writes |
| Audit history | audit log | Players, Audit history | view only: a log must not change |

## Left out

- **Database connection, web game and start-up settings** (files above): the panel can't change
  the settings it needs to run, or those of the web game process.
- **Alliances, rankings, audit history**: results and logs, shown read-only on purpose.
- **Rare game-written records** (gens abuse, pending castle siege rewards, guild chat bindings)
  have no menu entry, to keep the menu short; the generic grid edits them by address.
