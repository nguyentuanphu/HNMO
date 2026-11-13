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
| Gates (walking into a gate), the move window (M) | ⬜ | |
| Water, grass sway, animated objects (fountain, flags, birds, leaves) | ⬜ | |
| Map-specific behavior (`World/GameMaps`: Atlans water, Icarus sky, …) | ⬜ | |
| Day light mood, fog per map (`Data/Maps/LightMood.json`) | ⬜ | Fixed fog for now |
| Enhanced look (`Render/Look`, `Render/PostProcessing`) | ⬜ | |
| Minimap | ⬜ | |

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
| Skills and buffs (`GameLogic/Skills`, `Buffs`) | ⬜ | |
| Experience, level up, stats and points | ⬜ | |
| Items: drops, pick up, inventory, equip, use | 🟡 | Inventory from the server, moving and equipping by clicking, the hero re-dressed from the equipment slots; drops on the ground with their models and ground-label colours (Alt shows all), picking up (walk there, 2 cells), dropping from the inventory onto the ground; using items (potions, jewels) not yet; using items: potions on Q W E R (bindings from the key configuration, Ctrl + key over a bag item rebinds and saves, the best potion of its kind first, counts, 3D icons that turn on hover, right-click on a slot), right-click in the bag (use: potions, scrolls, skill books, elite potions, elixirs, …; else wear; else drop at the feet, not expensive items; into the vault while it is open), one use request at a time. Fruits, reset fruits, event tickets and talismans with their dialogs come later |
| NPC dialogs, shops, buy and sell, repair | 🟡 | Clicking an NPC walks next to it and talks to it; shops, buying, selling and repair not yet |
| Vault, trade, personal shop, chaos machine | 🟡 | Vault: talking to the vault keeper opens it (`TalkToNpcRequest`, `NpcWindowResponse`, `StoreItemList`), items move both ways, zen deposit and withdraw; trade, personal shop and chaos machine not yet |
| Party, guild, friends, messenger | ⬜ | |
| Quests | ⬜ | |
| Events (Blood Castle, Devil Square, Chaos Castle, …) | ⬜ | |
| MU Helper | ⬜ | |
| Cash shop | ⬜ | |

## 5. Interface (`UI/`, `Render/RmlUi`)

| Feature | Status | Notes |
|---|---|---|
| HUD: health, mana, experience bar, skill bar | 🟡 | The game's `main_frame.rml`: life, mana, SD and AG gauges with numbers, the experience bar (digit and tenth progress, CMainFrameWindow's formula), the inventory button; skill and potion slots come with the skills |
| Chat window, whisper, party and guild chat, chat commands | 🟡 | Public chat and commands (Enter), chat and server messages shown; whisper, party, guild not yet |
| Inventory, character, skill tree and option windows | 🟡 | The windows are the game's own RML documents and RCSS themes (`ui/rml.js` shows them in the browser): inventory and vault done, with the 3D items at their inventory angles (`ui/itemIcons.js`); the others not yet |
| Tooltips | 🟡 | Item tooltips from the game's own layouts (`Tooltip.json`, `ItemStats.json`, option files; `src/game/itemTooltip.ts`) in `tooltip.rml`: name, damage/defense with levels, durability, requirements (red when lacking), classes, luck/option/excellent/ancient/harmony/socket/guardian lines, descriptions. Not yet: the skill line (skill list not decoded), an ancient set's option list while wearing the set, shop prices, expiry and a few special items |
| Themes and UI scaling | ⬜ | |
| **(extra)** The (i) guide: what to play, the controls, each feature and whether it works yet | ✅ | `public/guide.html`, opened by the (i) next to the character name and from the login screen |
| Translations (`src/Localization`) | ⬜ | |

## 6. Effects and sound (`Render/Effects`, `Audio/`)

| Feature | Status | Notes |
|---|---|---|
| Skill effects, particles, sprites | ⬜ | |
| Item glow by level, excellent and ancient effects | ⬜ | |
| Sound effects and music | ⬜ | The sound files are not in the repository |

## 7. Models and formats

| Feature | Status | Notes |
|---|---|---|
| BMD versions 10 and 12 | ✅ | |
| BMD version 14 | ⬜ | Needed for newer models |
| Texture scripts (`_R` bright, `_H` hidden, `_N` no blending), `hid…` textures | ✅ | Hair is drawn as hair, not bright |
| Item glow meshes (`glowingMesh`, `pulsingGlow`, `noneBlendMeshes`) | 🟡 | Drawn additively; the pulsing and render styles (chrome, Flamberge fire, …) not yet. Staff of Resurrection still shows a black plane |
| Texture folders: first folder that has the texture | ✅ | |
| GLB models (`Data/Models/ModelFiles.json`) | ⬜ | |
