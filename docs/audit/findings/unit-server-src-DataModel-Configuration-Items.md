# Unit: server/src/DataModel/Configuration/Items

Checked against: Season 6 & Season 9 game client item stat/price formula structure.

Scope: `server/src/DataModel/Configuration/Items` (`25` files, `1638` lines).

## Summary

- Encodes client-par item scaling and pricing model semantics; numeric tables are seeded in `Initialization`.

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `ItemStatProfile.cs:19-57` | Profile inheritance from `"default"` matching game client item stat profile format | Client item stat table structure | Same |
| `ItemStatTerms.cs:15-96` | Integer left-to-right scaling formulas: `excellent = base*Times/dropLv + Add + dropLv/Div`, `ancient = val*Times/d + Add + d/Div` (`d = dropLv + AncientDropLevelBonus`) | Client `Item.bmd` / stat profile formula | Same |
| `ItemPriceDefinition.cs:15-83` | Buy price pipeline (+ `16%` guardian option, `3,000,000,000` max cap); sell price = `1/3` buy price scaled by durability; `CraftingReferencePrice` override | Client shop pricing & Chaos Mix jewel reference price | Same |
