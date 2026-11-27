# HUD Reference Sweep: Web Client vs Desktop Season 6.3 and Season 9

Reviewed the web game's HUD (`client/web/src/ui/hud.ts`, `autoPlayBar.ts`, `buffStrip.ts`, `partyList.ts`, `menuWindows.ts`, `mapNameBanner.ts`, `ultimateAutoPlayWindow.ts`, `mobileHud.ts`, `public/index.html`, and shared RmlUi layouts under `client/src/bin/Data/Interface/RmlUi/`) against the Season 6.3 (`UI/HUD/*`, `themes/legacy/*`, Season 6.3 client assets) and Season 9 (Season 9 client GFx HUD layouts) desktop clients.

## 1. Reference Comparison (Positions, Buttons, Gauges, Menus)

| HUD Element | Season 6.3 Desktop | Season 9 Desktop | Web Client (`client/web`) |
| --- | --- | --- | --- |
| **Bottom Main Frame (`#hud_layout`)** | Fixed 640x480 reference grid (`640x51dp` bottom-center): `newui_menu01/02/03`, scaled by `hudScale()` | GFx / EX700 (`MainFrame.ozg`, `1024x768+`) curved centered orbs + side slots | Uses S6 `themes/legacy/main_frame.rcss` (`640x51dp` bottom-center); supports S6 bottom buttons (`#buttons`) and `main_frame_top.rml` bindings for all 8 buttons |
| **Resource Gauges (`#gauges_left`, `#gauges_right`)** | HP orb (`45x39`), SD bar (`16x39`), AG bar (`16x39`), MP orb (`45x39`); HP orb swaps to `newui_menu_green.jpg` under `eDeBuff_Poison`; Master Level max stats applied | Centered split orbs + purple AG (`ag_purple.ozd`) & yellow SD (`sd_yellow.ozd`) + stamina gauge | Matches S6 sizes/offsets; Master stats supported; **`poisoned` HP swap fixed in this sweep** |
| **Experience Bar (`#exp`)** | 10-decile progress fill (`newui_exbar.jpg`, `629x4dp` track) + current decile digit `0..9`; switches to Master EXP above Lv 400 | Bottom full-width Master/Normal bar | Matches S6 10-decile + digit formula (`masterActive` supported) |
| **Potion (`#item_slots`) & Skill (`#skill_slots`) Slots** | Q/W/E/R `20x20` slots (`pitch 38dp`) with stack count & hover spin; 5 skill slots (`32x38`, `1..5` / `6..0`) + current skill + zig-zag popup + cooldown wipe + 4 pet commands | Extended hotkeys + GFx skill cooldowns & pet frame | Matches S6 slots, zig-zag grid, pet commands, hover spin, right-click potion consume, and cooldown wipe |
| **Bottom Corner Buttons (`#buttons`)** | 5 buttons (`30x41dp`): Cash Shop (`X`), Character (`C`, quest alert dot), Inventory (`I`/`V`), Friends (`F`, mail/chat dot), Menu (`U`) | Extended HUD quick buttons (Character, Inventory, Party, Guild, Command, CashShop, Muun, Pentagram, EventInventory, GremoryCase, SNS, Menu) | Renders the 5 S6 buttons (`legacy`) with hover/open states; `WindowMenu` covers `System (Esc)`, `Help (F1)`, `Guild (G)`, `Move (M)`, `Map (Tab)`, `Gens Info (B)`; **`chainfo_alert`/`friend_alert` dots fixed in this sweep** |
| **Top-Left Helper Bar (`mu_helper_bar`)** | `207x25dp` bar (`MapName (x , y)`, Config `Z`, Start/Stop `Home`) | `HeroPosition.ozg` + `MacroMain.ozg` | Matches S6 `mu_helper_bar` (`207x25dp`) + web `.hud` utility strip (`[Name \| FPS]`, sound, guide, language, settings) aligned immediately to its right |
| **Buff Strip & Party List (`buff_strip`, `party_list`)** | `200dp` 8-column strip & right-aligned party cards, re-centered inside `(0 .. worldRight)` when right-hand panels dock | `BuffFrame.ozg` & `PartyFrame.ozg` | Uses same `.rml`/`.rcss` layouts; **`dockedRight` calculation and live re-placement wired in this sweep** |
| **Mini Map (`mini_map`) & Map Banner (`map_name`)** | `Tab` full-screen `640x430` map + `CUIMapName` (`5s` fade banner + Strife indicator) | `NaviMap.ozg` corner radar + `MapName.ozg` | Matches S6 `MiniMap` (`640x430` clipped viewport + NPC/portal markers) and `MapNameBanner` (**locale sync fixed in this sweep**) |
| **Mobile Touch HUD (`mobileHud.ts`)** | Desktop-only | Desktop-only | Dedicated responsive touch overlay hiding `main_frame` / `mu_helper_bar` / chat when active (`hudMode`) |

## 2. Findings & Fixes Applied in Web Client

1. **Poisoned HP orb (`poisoned`) never updated (`client/web/src/ui/hud.ts`, `main.ts`)**:
   `Hud.model.poisoned` was initialized to `false` and never synchronized when the hero received `Buff.POISON`. Added `Hud.setPoisoned(boolean)` and wired `buffs.onChange` on the hero to toggle `poisoned`, restoring `newui_menu_green.jpg`.
2. **Character & Friends HUD alert dots never lit (`client/web/src/ui/hud.ts`, `main.ts`)**:
   `chainfo_alert` and `friend_alert` existed in `Hud.model` (`main_frame.rml:75,82`) with no setter or wiring. Added `Hud.setAlertStates({ character, friends })` and connected it to `stats.levelUpPoints > 0` (while Character window is closed) and `messenger.newMailAlert` / unread letters (while Friend window is closed).
3. **HUD tooltips (`data-hint`) did not appear or translate (`client/web/src/i18n.ts`, `ui/hud.ts`, `ui/autoPlayBar.ts`)**:
   `main_frame.rml` and `mu_helper_bar.rml` bind tooltips onto `data-hint`, but `Hud` and `AutoPlayBar` never mirrored `data-hint` to `title`, and `i18n.ts` excluded `data-hint` from its observed attributes (`ATTRIBUTES`). Added `data-hint` to `i18n.ts` and synchronized translated `title` attributes across both HUD documents on refresh and language switches.
4. **Missing low-HP `< 20%` heartbeat audio (`client/web/src/main.ts`)**:
   Desktop `CMainFrameWindow::SyncRmlModel()` plays `SOUND_HEART` when HP falls below 20% (`> 0`). Added a throttled (`1200ms`) `sounds.play('SOUND_HEART')` trigger in `stats.onChange` when the hero is alive below 20% life.
5. **`BuffStrip` & `PartyList` ignored right-docked windows (`client/web/src/main.ts`)**:
   `BuffStrip` and `PartyList` accept `dockedRight`, but `main.ts` did not supply it or call `place()` when right-hand panels (Character, Party, Inventory/Extension/Vault/Shop/Trade, Social, Command) toggled. Wired `dockedRight` (`columns * 190`) and synchronized `place()` via `itemWindows.onPlaced` and `onBagToggle`.
6. **Ultimate AutoPlay floating pill overlapped `Q W E R` potion slots (`client/web/src/ui/ultimateAutoPlayWindow.ts`)**:
   Fixed `bottom: 68px` collided with `#item_slots` at `1280x720` (`51 * 1.5 = 76.5px` HUD height) and `1024x768` (`81.6px` HUD height). Updated placement to `Math.round(54 * hudScale() + 8)px` dynamically on creation and window resize so it clears the bottom HUD at all resolutions.
7. **Map name banner ignored active language (`client/web/src/ui/mapNameBanner.ts`)**:
   Set `this.model.language = currentLanguage()` in `MapNameBanner.show()` instead of leaving `'en'` hardcoded.
