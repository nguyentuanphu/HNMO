# Unit Audit: `client/src/source/UI/Events (files 1)`

- **Unit**: `client/src/source/UI/Events (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `37` (`5,983` lines) including `BloodCastleEnter.cpp`, `CursedTempleEnter.cpp`, `CursedTempleSystem.cpp`, `CryWolf.cpp`, `DoppelGangerWindow.cpp`, `CatapultWindow.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Illusion Temple level brackets (`1..5` + Master `6`) | `CursedTempleEnter.cpp:43-45,94-114` | Normal classes: `220..270`, `271..320`, `321..350`, `351..380`, `381..400`; `IsMasterLevel(Hero->Class)` maps to Temple `6` | Server `IllusionTemple` level bands (`minigames-bc-cc-ds-it-s6-s9.md`) | Same |
| Illusion Temple ticket durability & matching grade check | `CursedTempleEnter.cpp:116-136` | `ITEM_ILLUSION_TEMPLE_TICKET` uses hero's tier; `ITEM_SCROLL_OF_BLOOD` must have `p->Level == enterlevel`; requires `p->Durability >= 1` | Server `IllusionTempleEnterAction` | Same |
| Illusion Temple NPC Stone Statue ID | `CursedTempleSystem.cpp:59` | `HolyItemNpc = 380` (Stone Statue); tracks SP (`10..90`) & skills (`210..213`) | `minigames-bc-cc-ds-it-s6-s9.md:36` (`Stone Statue 380`) | Same |
| Blood Castle entry bands (Normal vs MG/DL/RF) | `BloodCastleEnter.cpp:117,176` | Normal vs MG/DL/RF level bracket lookup + Master -> BC8 | Web `eventWindows.ts` & Server `BloodCastle` | Same |
| Crywolf scoreboard, Catapult & Doppelganger HUD | `CryWolf.cpp`, `CatapultWindow.cpp`, `DoppelGangerWindow.cpp` | Formats Crywolf score table, CS catapult coordinates, and Doppelganger entry state | UI presentation / server DTOs | No game rules |
