# Unit: client/src/source/Data/DataHandler/ItemData

Checked against: Server `ItemRequirementFormula` & Web `itemTooltip.ts`.

Scope: `client/src/source/Data/DataHandler/ItemData` (`19` files, `4363` lines).

## Findings

| File & Line | Rule / Formula | Parity Reference | Result |
|---|---|---|---|
| `ItemBmdImport.cpp:342-360` | `OriginalRequirementAdd = 20`, `OriginalRequirementPercent = 3%`, `OriginalEnergyRequirementPercent = 4%`; `ITEM_GROUP_ETC` scroll energy scales from level req | Season 6/9 client item BMD requirement growth & Server `Initialization` | Same |
