# Season 9 Item Key Reconciliation (24 Nov 2026)

## Summary

- **HNMO Season 6 dump** (`docs/audit/data/hnmo/items.csv`): **848** items
- **HNMO Season 9 dump** (`docs/audit/data/hnmo-s9/items.csv`): **1,112** items
- **HNMO Season 9-only** (`S9 dump \ S6 dump`): **264** items (`1,112 - 848 = 264`)
- **`SeasonNineOnlyItemsCatalog.cs`**: **290** entries
- **Season 6 suite** (`ItemList.xml`): **1,003** items
- **Season 9 suite** (`ItemList.xml`): **1,260** items
- **Suite Season 9-only** (`S9 suite \ S6 suite`): **263** items

## 1. HNMO Season 9-only (264) vs `SeasonNineOnlyItemsCatalog.cs` (290)

In `SeasonNineOnlyItemsCatalog.cs`, `Shields` (5 entries: numbers `22..26`) and `Wings` (7 entries: numbers `262..268`) store the **item number** in the first tuple field and are created under Group `6` (`6-22..6-26`) and Group `12` (`12-262..12-268`), respectively.

After mapping `Shields` -> Group `6` and `Wings` -> Group `12`:
- **In HNMO S9-only dump (264) but NOT in catalog**: **0** keys.
- **In catalog (290) but NOT in HNMO S9-only dump**: **26** keys — all 26 are **already present in the HNMO Season 6 dump** (`848` items):
  - 13 weapons: `0-29`, `0-30`, `0-36`, `0-37`, `0-38`, `2-19`, `2-20`, `3-12`, `4-25`, `4-26`, `5-32`, `5-35`, `5-37`
  - 13 group-14 items: `14-191`, `14-192`, `14-193`, `14-194`, `14-195`, `14-196`, `14-197`, `14-198`, `14-199`, `14-200`, `14-201`, `14-202`, `14-203`
- Check: `290 - 26 = 264` (exact match).

## 2. HNMO Season 9-only (264) vs Suite Season 9-only (263)

- **In HNMO S9-only (264) but NOT in Suite S9-only** (**15** keys — these already exist in the Season 6 suite's `ItemList.xml`):
  `7-77`, `8-77`, `9-77`, `10-77`, `11-77`, `12-20`, `13-162`, `13-166`, `13-168`, `14-27`, `14-172`, `14-173`, `14-174`, `14-175`, `15-25`
- **In Suite S9-only (263) but NOT in HNMO S9-only** (**14** keys — these are already in HNMO's Season 6 dump):
  `0-29`, `0-30`, `0-36`, `0-37`, `2-19`, `2-20`, `3-12`, `4-25`, `4-26`, `5-35`, `5-37`, `14-191`, `14-192`, `14-193`
- Check: `264 - 15 + 14 = 263` (exact match).
