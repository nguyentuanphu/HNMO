# Desktop Client Data Audit: Season 9 Muun System

Audited on `2026-11-27` (Sydney time) from the Season 9 desktop client `Data` pack using
`tools/local-checks/desktop-data/muun.py` and `tools/local-checks/desktop-data/items_options.py`.

## Summary

- **`Local/MuunInfo.bmd` (`76` rows × 56 bytes):** Every field (`id`, item `group:number` in `13:212..307`,
  `muunType`, `maxRank`, `optionIndex`, `addOptionType`, `addOptionValue`, `evolutionItem`, `skillDelayMs`)
  matches `SeasonNineSystemsCatalog.Muuns` (`#403`) **76 / 76**. The client table additionally stores active
  date windows (`startDate`..`endDate`, `2013-08-01`..`2015-03-31`) omitted by HNMO.
- **`Local/Eng/Item_eng.bmd` (Season 9):** Defines all **87** Muun/egg/stone/converter items in Group `13:210..350`.
  All `86` entries (`>= 211`) + `13:210` (`WereRabbit Egg`) exist in `SeasonNineOnlyItemsCatalog.cs` (`0` missing).
  All Muun pets have `size = 1x1`, `slot = 255` (no 1.04 equipment slot index in `Item_eng.bmd`), `durability = 0..255`.
- **`Local/Eng/MuunOption_eng.bmd` (`37` rows × 560 bytes):** All `37` value tuples `(ValueEvolved, Lv1..Lv5)` and
  condition tuples `(ConditionType, Val1, Val2)` match `SeasonNineSystemsCatalog.MuunOptions` **37 / 37**.
  Record `36` (`optType = 12, cond = (3, 13, 24)`) has empty text in `MuunOption_eng.bmd`, whereas HNMO adds
  `"Increases +%d skill damage (Non-PvP)"`.
- **`Local/MuunExchange.bmd` (`3 + 3` rows):** Client defines NPC Muun Exchange conversions using Wizardry Stone
  (`13:240`) counts `2/2/3` into Evolution Stone (`13:211`) + Muuns Mantis (`13:278`), Scolpy (`13:280`), and
  Chafer (`13:282`) — not yet implemented in `#427`.
- **What the Desktop Client `Data/` Does Not Contain:**
  1. **Level-up energy cost table:** None exists in `Muun*.bmd` (desktop feeds duplicate Muun items inside the
     dedicated MUUN Inventory rather than spending an HNMO energy pool, see `Text_eng.bmd[4281..4530]`). PR `#427`'s
     energy pool and `MuunLevelUpEnergyCost` (`10 * level` default) remain HNMO's admin-configurable mechanism.
  2. **Drop tables:** Egg (`13:210, 220..222`) / Muun drop rules live on the server, not in client `.bmd` files.
  3. **UI Window (`client/src/source`):** HNMO's C++ desktop client currently has no Muun Inventory (`Text[4281]`)
     or Illustrated Book (`Text[4285]`) code; Season 9 client assets use `Interface/GFx/MuunExchange.ozg`,
     `Local/MuunMeshInfo.bmd` (`49` rows), `Local/MuunRenderInfo.bmd` (`82` rows), and `Local/MuunTooltipInfo.bmd`
     (`82` rows of 7 preview floats per Muun).

## Discrepancies & Action Items (`#403`, `#427`)

1. **PvE-only ("non-PvP") separation (`docs/owner-decisions.md`, 27 Nov):** Owner decided `"Tách đúng: chỉ đánh quái"`
   (PvE-only stat). Options `8, 10, 11, 12, 14` (and `53` for attack skills) in `MuunOption_eng.bmd` explicitly state
   `(non-PVP)`. PR `#427` (`MuunCompanion.cs:74-84`) currently maps them to general stats that also apply in PvP.
2. **Wizardry Stone / Muun Exchange (`Local/MuunExchange.bmd`):** Add NPC exchange recipes `13:240` → `13:211` +
   `13:278`/`280`/`282` (`docs/audit/data/client-s9/muun_exchange.csv`).
3. **Attack Options `50` / `53`:** `Lycan`, `Behemoth`, `Hound`, `Cyclop`, `Muty`, `Chafer`, `Sporty Girl`, `Taaf`,
   `Frozen` (`skillDelayMs = 10000`) use `optionType = 50` or `53` (`Attack Skill (%dLevel)` / `non-PVP`), currently
   skipped (`null`) by `MuunCompanion.StatOf` until companion attack skill casting is wired.
4. **Catalogue Provenance:** Because `MuunInfo.bmd` (`76` rows) and `MuunOption_eng.bmd` (`37` rows) match 100% of
   the values in `SeasonNineSystemsCatalog.cs` (except `MuunOption[36]` string), those values are verified directly
   from Season 9 desktop client binary tables (`docs/audit/data/client-s9/muun_info.csv`, `muun_options.csv`).
