# Unit Audit: `client/web/src/ui (files 1)`

- **Unit**: `client/web/src/ui (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `14` (`5,705` lines): `addStatsWindow.ts`, `arenaWindow.ts`, `autoPlayBar.ts`, `autoPlayExtrasPanel.ts`, `autoPlayHnmoTabs.ts`, `autoPlayWindows.ts`, `buffStrip.ts`, `bugReportWindow.ts`, `cashShop.ts`, `castleSiegeWindows.ts`, `catchupBanner.ts`, `characterWindow.ts`, `chatCommandWindow.ts`, `combatOverlay.ts`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Desktop / Server / Suite Reference | Match |
|---|---|---|---|---|
| MUHelper UI hard limits & skill slots | `autoPlayWindows.ts:26-43`, `autoPlayHnmoTabs.ts` | `SKILL_SLOTS = 6` (`0..2` attack, `3..5` buff), `MAX_HUNTING_RANGE = 6`, `MAX_OBTAINING_RANGE = 8`, `MAX_NUMBER_DIGITS = 3`, `MAX_ITEM_NAME = 50`, `MAX_MAGIC = 64`, `GAUGE_MAX_LEVEL = 10` | Desktop `UI/MuHelper` & `GameLogic/MuHelper` (`MuHelperConfiguration`) | Same |
| AutoPlay Extras input bounds | `autoPlayExtrasPanel.ts:18,96` | Clamps `stopAfterMinutes` inside `0..1440` (`24` h) and threshold percentages cleanly | `autoPlayExtras.ts` | HNMO custom |
| Add Stats window point budget clamp | `addStatsWindow.ts:129-137,171-173` | Limits staged stat allocations to `remaining = max(0, levelUpPoints - plannedTotal)` before dispatching `/add*` requests | Server `AddCharacterStatPointAction` | HNMO custom |
| Castle Siege Guard / Sign of Lord slot check | `castleSiegeWindows.ts:103,141-175` | Scans base bag slots `12..75` for `Sign of Lord` (`14:21 +3`); requires Guild Master (`GuildStatus.MASTER`) & `CastleSiege.canDeclare(master, level)` (`>= 200`); blocks own/allied guild from declaring | Server `CastleSiege` NPC Guard actions (`castle-siege-s6-s9.md`) | Same |
| Character window Fruit success rate table & PK/Command visibility | `characterWindow.ts:20,103-107,156-157` | `PVP_NEUTRAL = 3`; shows Command stat row only when `classIndex === BaseClass.DARK_LORD`; `fruitChance`: `<= 10` pts -> `100%`, else share `<=10%` -> `70%`, `<=30%` -> `60%`, `<=50%` -> `50%`, else `40%` | Desktop `CCharacterInfoWindow` & `FruitSystem` | Same |
| CashShop item code bit-packing | `cashShop.ts:60-63` | Decodes item code as `group = code >> 9`, `number = code & 0x1ff` (`512` items/group) | Server `CashShop` item definitions | Same |
| Buff strip icon atlas index clamp | `buffStrip.ts:18-22` | Maps buff icons `< 81` (`buff - 1`) vs `>= 81` (`buff - 81`) clamped to `0..89` | Desktop `BuffStrip` atlas layout | Same |
| Remaining presentation UI panels | `arenaWindow.ts`, `autoPlayBar.ts`, `bugReportWindow.ts`, `catchupBanner.ts`, `chatCommandWindow.ts`, `combatOverlay.ts` | Server-driven HTML/RML UI wrappers & telemetry overlays | UI presentation | No game rules |
