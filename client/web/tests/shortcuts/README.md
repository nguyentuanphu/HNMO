# Web game keyboard shortcuts

Letter shortcuts follow the typed layout. The nine Alt/Option shortcuts use the
physical key code. Unless a row specifies a context, the shortcut requires the
game, with chat closed and no active text editor.

| Keys | Action and context | Handler in `client/web/src` |
| --- | --- | --- |
| C, I / V | Character, inventory | `main.ts`, `ui/itemWindows.ts` |
| A / Shift+A | Master Skill Tree for master characters / Add Stats | `ui/masterLevelWindow.ts`, `main.ts` |
| B, D | Gens, Command Window. D → Special also opens Chat Commands | `ui/gensWindow.ts`, `main.ts` |
| F, G | Friends from level 6, guild | `game/social.ts` |
| H, N | Timetable, world bosses | `main.ts` |
| J / Shift+J | Missions / Chat Commands when commands are available | `main.ts` |
| K | Inventory expansion with bag open, otherwise Community | `ui/itemWindows.ts`, `main.ts` |
| L / Shift+L | Repair mode / repair all at a repair shop. Plain L opens Lucky Number elsewhere | `ui/itemWindows.ts`, `main.ts` |
| M / Shift+M | Move / Marriage | `main.ts` |
| O, P | Settings, party | `ui/settingsWindow.ts`, `main.ts` |
| Q / W / E / R, Ctrl+Q / W / E / R | Use quick slot, or assign hovered bag item | `main.ts` |
| Shift+R | Ranking | `main.ts` |
| S, X | Personal shop with bag open and eligible, Cash Shop in safe zone | `ui/personalShop.ts`, `ui/cashShop.ts` |
| T / Shift+T, U / Shift+U | Quests / top-up milestones, Window Menu / Mount Chain | `ui/questWindows.ts`, `main.ts` |
| Y | New-player guide | `main.ts` |
| Home, Z / Shift+Z / Alt+Z | Autoplay toggle, Window 1 Hunting / Extras / Ultimate | `main.ts`, `ui/autoPlayWindows.ts` |
| Alt+A / B / M / U | Arena / bug report / Gift Box / VIP | `main.ts` |
| Alt+E / O / P / W | Fewer effects / hide players / Performance Mode / hide wings and pets | `ui/settingsWindow.ts` |
| Alt held / tapped | Hold ground labels / toggle labels. A chord or pointer click cancels the tap | `game/keyboardShortcuts.ts` |
| 0–9, Ctrl+0–9 | Use skill slot, or assign hovered skill | `main.ts`, `ui/mobileHud.ts`, `ui/touchControls.ts` |
| Tab / Shift+Tab | Plain Tab toggles minimap over the game or switches chat fields. Controls and Shift+Tab retain navigation | `main.ts`, `game/chat.ts` |
| Enter / Escape | Open or submit chat, confirm or close active dialog, collapse HUD tools, or open system menu depending on context | `game/chat.ts`, `ui/gameDialogs.ts`, `main.ts`, panel modules |
| F1–F5 | Guide, whisper-only view, whisper target, chat frame size, chat frame | `main.ts`, `game/chat.ts` |
| F6 / Shift+F6, F7–F10 | Market / Escrow, Jewel Bank, Season Plan, Top Races, Leftovers | `main.ts` |
| Page Up/Down, Up/Down | Scroll a framed chat log, navigate history in chat fields | `game/chat.ts` |
| Print Screen | Screenshot, including browsers that deliver only keyup | `ui/screenshot.ts` |
| Ctrl+Shift+P | Record a performance report | `ui/perfReportWindow.ts` |

Test the function keys, Alt/Option, AltGr, Print Screen, and Ctrl+Shift+P
on physical keyboards because browsers, operating systems and Fn modes can
intercept them. The ⚙ drawer provides Guide, Report and Settings, the bottom
bar provides Window Menu, the Autoplay bar opens Window 1, and the touch menu
offers the report, screenshot and feature windows. Settings → Graphics opens
the performance report.

Run `npm run build && npx playwright test -c playwright.shortcuts.config.ts`
from `client/web`. Include the live cases with `SHORTCUTS_IN_GAME=1` and point
`BASE_URL` to an isolated demo bridge. The Windows and macOS workflow starts
its own demo server and bridge.
