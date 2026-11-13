# Web client: parity with the desktop client

The goal is a web client that matches the desktop client (`src/source`) one to one.
This list follows the client's source folders and is worked through top to bottom:
each item is done when it behaves like the desktop client against the HNMO server.

Status: ✅ done · 🟡 partly · ⬜ not started

## 1. Connection and login (`Network/`, `Dotnet/`, `Scenes/LoginScene`, `Scenes/CharacterScene`)

| Feature | Status | Notes |
|---|---|---|
| Relay between browser and server (WebSocket ↔ TCP, SimpleModulus + Xor32) | ✅ | `web/bridge`, uses the server's own network library like `ClientLibrary` |
| All packet definitions | ✅ | Generated from the server's XML: 609 packets (`tools/generate-packets.mjs`) |
| Server list, game server address | ✅ | |
| Login (Xor3 credentials, version `20404`, serial) | ✅ | Error texts for each login result |
| Character list | ✅ | Name, level, class |
| Enter the world with a character | ✅ | Map and position from the server |
| Ping and ping answers | ✅ | |
| Character creation | ✅ | Classes the account has unlocked; the new character comes from the server's answer, like CharacterScene |
| Character deletion | ⬜ | |
| Login and character screens with the original 3D scenes and UI | ⬜ | Plain HTML forms for now |
| Reconnect after a lost connection (`Network/Reconnect`) | ⬜ | |
| Server messages, notices, disconnect reasons | ⬜ | |

## 1b. HNMO folder

| Feature | Status | Notes |
|---|---|---|
| One folder: server, admin panel, web client, game data | ✅ | `tools/package-hnmo.sh`, Windows build by the "HNMO package" workflow |
| Saving in the folder (portable PostgreSQL, started by HNMO) | ✅ | Tested on Linux: a character's map survives a restart |
| Account page: register with free coins, show coins | ✅ | Uses the server's API with a key kept on the host |
| Daily free coins, coin top-ups | ⬜ | |

## 2. World (`World/`, `Render/Terrain`, `Engine/Object`)

| Feature | Status | Notes |
|---|---|---|
| Terrain: heights, two texture layers, alpha, light map | ✅ | |
| Static objects with light-map tint | ✅ | Lorencia's model table, generic `ObjectNN.bmd` for other maps (untested) |
| Walkability (`.att`) | ✅ | |
| Other maps | ✅ | All 36 maps with data load (folders as `GetFilesFolder`, 24-bit heights) |
| Map changes: warps and teleports (`1C 0F`), `ClientReadyAfterMapChange` | ✅ | Tested with `/move devias`, `/move noria`, `/move lorencia` |
| Gates (walking into a gate), the move window (M) | ✅ | The move window (M, the game's `move_command.rml`): the warp list from `MoveReq_eng.bmd` with level and zen (two thirds of the level for Magic Gladiators, Dark Lords and Rage Fighters), the Icarus and Atlans mount rules, a click warps (`WarpCommandRequest` with the server's key). Gates by walking (`game/gates.ts`, CheckGate): `gate.bmd`'s entrances, the target's level (two thirds for MG/DL/RF), the Atlans and Icarus mount rules, `EnterGateRequest` once per entrance, not while a map loads (test `gates.spec.ts`). The gatekeeper NPC window (`gateman.rml`) not yet |
| Water, grass sway, animated objects (fountain, flags, birds, leaves) | ⬜ | |
| Map-specific behavior (`World/GameMaps`: Atlans water, Icarus sky, …) | ⬜ | |
| Day light mood, fog per map (`Data/Maps/LightMood.json`) | ⬜ | Fixed fog for now |
| Enhanced look (`Render/Look`, `Render/PostProcessing`) | ⬜ | |
| Minimap | ✅ | Tab (`mini_map.rml`, `ui/miniMap.ts`): the map's picture, the NPC and gate markers from the map's mini-map data with their names on hover, the hero's arrow, Escape or the close button closes it (`CloseNpcRequest`); test `minimap.spec.ts` |
| Map name banner on entering a map (`map_name.rml`, ornaments and map icons) | ✅ | `ui/mapNameBanner.ts`: fades in, five seconds, fades out |

## 3. Characters and monsters (`Character/`, `GameLogic/Monsters`, `Engine/AI`)

| Feature | Status | Notes |
|---|---|---|
| Own character: class body set, stand and walk animations | ✅ | |
| Other players in view (`12`), out of view (`14`), walking (`D4`), moved (`15`) | ✅ | Others walk straight to their target |
| NPCs and monsters in view (`13`) with models from `Data/Monsters` | ✅ | Scale and animation speeds from the data |
| Pathfinding and walk packets (15 steps per packet, continued) | ✅ | Verified: another player sees the same cell |
| Name labels | 🟡 | Players and NPCs, always shown; the client shows them on hover |
| Armour on characters from the appearance bytes (helm, armour, pants, gloves, boots) | ✅ | Models from `Data/Items/Models`; class body where nothing is worn |
| Weapons and shields in the hands, on the back in safe zones | ✅ | Bones 33, 42 and 47 with the back placements of `PlaceLinkedPart`; crossbow and a few special placements not yet |
| Wings | ✅ | Bone 47, animated, glowing meshes drawn additively |
| Capes (Dark Lord, Rage Fighter) as cloth | 🟡 | Drawn stiff on the back bone; the client hangs them from bone 19 as cloth |
| Pets and mounts (Guardian Angel, Imp, Horn of Fenrir, Dark Horse, …) | ⬜ | Read from the appearance, not drawn |
| Item glow by level (+3 to +15), excellent and ancient | ✅ | The passes of `RenderPartObjectEffect` with the chrome texture coordinates of the skinning shader; colors from `GlowColors.json`; the server's glow step converted with `LevelConvert`. Shines below +3 (seals, harmony) not yet |
| Wardrobe (offline): try on any class, armour set, weapon and wings | ✅ | A developer tool, like the client's item previews |
| NPCs drawn with the player model (guards, …) with their equipment | 🟡 | Drawn with a default body set |
| Others' exact walking steps (`StepData`) | ⬜ | |
| Actions: attack, skills, die, emotes, sit (`18`, `19`, `1E`, `17`) | 🟡 | Attacks, flinching, death and emotes from `ObjectAnimation`/`ObjectGotKilled`, with the desktop's animation speeds; skills not yet |
| Monster special setups (`hooks`, `glowMesh`, `stateVariants`, …) | ⬜ | |
| Shadows | ⬜ | |

## 4. Gameplay (`GameLogic/`)

| Feature | Status | Notes |
|---|---|---|
| Attack monsters, damage numbers, health bars | 🟡 | Click a monster: walk into range (1.8 / spear 2.2 / bow 6 cells), swing with the weapon's animation (`SetPlayerAttack`, `src/game/combatLook.ts`), `HitRequest`; auto attack until it dies; hits show the desktop's damage numbers, flinches and the top-centre name and health bar; each monster's own attack animations (Monsters.json `attacks`, monster skills); arrows and bolts of every bow and crossbow and of archer monsters (`src/game/combatEffects.ts`); deaths: the body waits, fades and sinks, blood pools, the special bursts (Shadow pieces, giants' ice, Ice Monster); own death and respawn. Not yet: particles, sprites, trails and sounds (no particle system yet), skill arrows and multi-shot fans, the per-map boss deaths, Rage Fighter streak hits, running |
| Skills and buffs (`GameLogic/Skills`, `Buffs`) | 🟡 | Skill data from `skill_eng.bmd` (name, level, mana, AG, range, cooldown, requirements, icon); the learned skills from the server (`SkillListUpdate`, `SkillAdded`, `SkillRemoved`, list indexes kept); hotkeys 1-9 and 0 from the key configuration, Ctrl + number over a popup skill sets one and saves it; the number keys pick the current skill; the skill tooltip (range, mana, AG, cooldown, requirements with what's lacking, notes, element). Casting: the right mouse button (a click, not a drag) casts the current skill at the monster under the mouse or at the ground: the hero walks into the skill's range, checks mana and AG (drinks a mana potion when short), plays the skill's action and sends TargetedSkill or AreaSkill as the desktop's AttackWizard/AttackKnight/AttackElf do (`game/skillCast.ts`), auto attack repeats attack skills; others' SkillAnimation/AreaSkillAnimation play their skill's action. Buffs: the buff strip (`buff_strip.rml`: the hero's buffs from `MagicEffectStatus` and `EffectItemConsumption`, highest tier of a family, debuffs last, tooltip with name, description and time left, right-click cancels a skill's buff). Not yet: cooldown wipes, the tooltip's damage lines, buff effects on characters, Rage Fighter and master skills and the master skill icons not yet |
| Experience, level up, stats and points | 🟡 | The character window C (the game's `character_info.rml`): name, class, level, experience, fruit points, points left, the five stats with the desktop's derived lines (damage, attack and defense rate, defense, attack speed, HP, mana, skill damage, wizardry, curse; `game/characterCalc.ts`), the + buttons spend points (`IncreaseCharacterStatPoint`, the server's answer updates the stats). Buffs, ancient set and socket options, master level and pet levels don't count yet |
| Items: drops, pick up, inventory, equip, use | 🟡 | Inventory from the server, moving and equipping by clicking, the hero re-dressed from the equipment slots; drops on the ground with their models and ground-label colours (Alt shows all), picking up (walk there, 2 cells), dropping from the inventory onto the ground; using items (potions, jewels) not yet; using items: potions on Q W E R (bindings from the key configuration, Ctrl + key over a bag item rebinds and saves, the best potion of its kind first, counts, 3D icons that turn on hover, right-click on a slot), right-click in the bag (use: potions, scrolls, skill books, elite potions, elixirs, …; else wear; else drop at the feet, not expensive items; into the vault while it is open), one use request at a time. Fruits, reset fruits, event tickets and talismans with their dialogs come later |
| NPC dialogs, shops, buy and sell, repair | 🟡 | Clicking an NPC walks next to it and talks to it. Merchants: the game's `npc_shop.rml` next to the inventory with the shop's items (`StoreItemList`), a click buys (`BuyItemFromNpcRequest`, `ItemBought`), a bag item dropped on the shop or right-clicked is sold (`SellItemToNpcRequest`; not what can't be sold, expensive items after asking), repair shops (Eo, Zienna, Hanzo, Rhea, Bolo) show repair, repair all (L, Shift+L) and what repairing everything costs (the desktop's price formulas, `game/itemValue.ts`), Esc / X close it (`CloseNpcRequest`). Not yet: the NPC dialog window, the tax rate from the server, gamble shops, the inventory's own repair button |
| Vault, trade, personal shop, chaos machine | 🟡 | Vault: talking to the vault keeper opens it (`TalkToNpcRequest`, `NpcWindowResponse`, `StoreItemList`), items move both ways, zen deposit and withdraw; trade (`trade.rml`, `ui/tradeWindow.ts`): `/trade` or a request, the in-game question, items by right click both ways, zen, both OK marks, the result (test `trade.spec.ts`); personal shop and chaos machine not yet |
| Party, guild, friends, messenger | 🟡 | Party (`game/party.ts`, `party_list.rml`): `/party` or an invite, the in-game question (a non-blocking dialog, packets keep flowing), the member cards with life, leaving and kicking, a party kept while away comes back on entering (test `party.spec.ts`); guild, friends, messenger not yet |
| Quests | ⬜ | |
| Events (Blood Castle, Devil Square, Chaos Castle, …) | ⬜ | |
| Auto play (the helper) | 🟡 | Home or the helper bar (`mu_helper_bar.rml`: map, position, start/stop) asks the server to start or stop it (`MuHelperStatusChangeRequest`, `MuHelperStatusUpdate`, the zen it costs). The loop of the desktop's helper (`game/autoPlay.ts`): health potions under the threshold, back to the start spot after a while away, picking up drops by the pick-up rules, the nearest monster in the hunting range (or the farthest attacker, counter attack, self defense), the skill rotation (basic skill, activation skills on a timer or a monster count, combo), a plain attack as fallback, self repair; a safe zone stops it. The settings windows (Z, the bar's button: `mu_helper_config/detail/skill_picker.rml`, `ui/autoPlayWindows.ts`) loaded from and saved to the server (`MuHelperConfigurationData`, `MuHelperSaveDataRequest`, the 257-byte layout in `game/autoPlayConfig.ts`). Not yet: buffs, heal and party support, the Dark Raven in the loop; labels are clipped at the web's text size |
| **(extra)** Auto play extras | ✅ | `game/autoPlayExtras.ts`, settings with Shift + Z (kept in the browser per character): back to town when the bag is full or the healing potions run out (Lorencia's potion seller: sell junk, buy potions, warp back, walk to the hunting spot, carry on), selling junk, a stop timer, alerts (browser notification, chat, page title) on death and a full bag |
| Cash shop | ⬜ | |

## 5. Interface (`UI/`, `Render/RmlUi`)

| Feature | Status | Notes |
|---|---|---|
| HUD: health, mana, experience bar, skill bar | 🟡 | Skill bar: the hotkey row (1-5 or 6-9, 0), the current-skill slot and its popup with the pet commands, icons lit or grey by the skill's requirements and the equipment checks (melee weapon, bow and arrows, dark horse, horn mount) (`skill_icons.rcss`, runtime decorators in `ui/rml.ts`); the buff, party and map checks of the grey icons come with those features. The game's `main_frame.rml`: life, mana, SD and AG gauges with numbers, the experience bar (digit and tenth progress, CMainFrameWindow's formula), the inventory button; skill and potion slots come with the skills |
| Chat window, whisper, party and guild chat, chat commands | 🟡 | Public chat and commands (Enter), chat and server messages shown; whisper, party, guild not yet |
| Inventory, character, skill tree and option windows | 🟡 | The windows are the game's own RML documents and RCSS themes (`ui/rml.js` shows them in the browser): inventory and vault done, with the 3D items at their inventory angles (`ui/itemIcons.js`); the others not yet |
| Tooltips | 🟡 | Item tooltips from the game's own layouts (`Tooltip.json`, `ItemStats.json`, option files; `src/game/itemTooltip.ts`) in `tooltip.rml`: name, damage/defense with levels, durability, requirements (red when lacking), classes, luck/option/excellent/ancient/harmony/socket/guardian lines, descriptions. Not yet: the skill line (skill list not decoded), an ancient set's option list while wearing the set, shop prices, expiry and a few special items |
| Themes and UI scaling | ⬜ | |
| **(extra)** Touch screens (phones, tablets) | ✅ | `ui/touchControls.ts`: a tap does a left click's work (walk, attack, talk, pick up), a long press casts the current skill at what's under the finger, two fingers pinch to zoom and slide to turn, a button bar for what only keys open (I, C, P, M, the mini map, Z, auto play on/off, the chat); test `touch.spec.ts` |
| Option window (settings) | 🟡 | `ui/settingsWindow.ts`: the game's own `option_window.rml` in the `window_shell.rml` frame (`ui/rml.ts` now splices RML templates), opened with `O` or the ⚙ button; saved in the browser (`hnmo.settings`, the volumes in `hnmo.audio`) and applied on load. Rows the web game uses: FPS limit (the desktop's 24/30/60/120/144/uncapped), sound and music volume bars (levels 0..10 like `COptionWindow::RmlGaugeEvent`), windowed mode (browser full screen), disable effects (skill effects), show FPS counter. Left out for now: vsync, the Enhanced look and its tuning, antialiasing, anisotropy, effect limitation, particles, shadows, whisper sound, the Gameplay tab (auto attack, slide help), language, font, theme, texture pack, debug info |
| **(extra)** Settings: render scale (the resolution row), text size (the UI scale row: small / normal / large, `--hnmo-text-scale` for the game's windows and the page's text), mute, low-spec mode (half render scale, 30 FPS, near view distance, effects off), view distance (terrain fog and the camera's far plane) | ✅ | `ui/settingsWindow.ts`, `render/quality.ts` (`applyQuality`, `frameDue` called by the frame loop); browser test `tests/e2e/settings.spec.ts` |
| **(extra)** The (i) guide: what to play, the controls, each feature and whether it works yet | ✅ | `public/guide.html`, opened by the (i) next to the character name and from the login screen |
| Translations (`src/Localization`) | ⬜ | |

## 6. Effects and sound (`Render/Effects`, `Audio/`)

| Feature | Status | Notes |
|---|---|---|
| Skill effects, particles, sprites | 🟡 | The effect system's core (`render/effects/`: effects, particles, sprites, joints at the desktop's 25 ticks a second, with Data/Effects/EffectTypes.json) and the effects of the first skills: Energy Ball, Fire Ball, Lightning, Power Wave, the knight's Falling Slash to Slash sparks, Triple Shot's arrows, Heal / Greater Defense / Greater Damage, Power Slash. Weapon trails, terrain light and the other skills not yet |
| Item glow by level, excellent and ancient effects | ⬜ | |
| Sound effects and music | 🟡 | `audio/` (Web Audio) and `game/gameSounds.ts`: the desktop's sound files and channels, the login theme, map music and ambience by its rules, footsteps by ground, weapon swings, screams when hit and on death, monster attack/hit/death sounds from their sound sets, potions, level up, the skills' sounds; the sound button mutes (kept in the browser). 103 sound files are missing from the data (pick-up, drop, plain-ground steps…), those stay silent. The option window's sound and music volume bars (`O`). Not yet: map object sounds, event sounds |

## 7. Models and formats

| Feature | Status | Notes |
|---|---|---|
| BMD versions 10 and 12 | ✅ | |
| BMD version 14 | ⬜ | Needed for newer models |
| Texture scripts (`_R` bright, `_H` hidden, `_N` no blending), `hid…` textures | ✅ | Hair is drawn as hair, not bright |
| Item glow meshes (`glowingMesh`, `pulsingGlow`, `noneBlendMeshes`) | 🟡 | Drawn additively; the pulsing and render styles (chrome, Flamberge fire, …) not yet. Staff of Resurrection still shows a black plane |
| Texture folders: first folder that has the texture | ✅ | |
| GLB models (`Data/Models/ModelFiles.json`) | ⬜ | |
