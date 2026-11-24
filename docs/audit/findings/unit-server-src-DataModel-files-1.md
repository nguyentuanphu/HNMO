# Unit: server/src/DataModel (files 1)

Checked against: Season 6 and Season 9 client inventory layout & item formulas.

Scope: `server/src/DataModel (files 1)` (`10` files, `1073` lines).

## Summary

- Core MU slot indexing and item rules match Season 6/9 client constants; durability rounding (`Math.Ceiling`) prevents fractional wear exploits.

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `InventoryConstants.cs:15-177` | `12` equippable slots (`0..11`), `8x8=64` main inventory, `4x32=128` extensions, `32` personal store slots at offset `204`, `15x8=120` vault slots | Season 6/9 client extended inventory & storage layout | Same |
| `ItemExtensions.cs:34-38,80-83` | `IsWing` (`Group 12|13`, size `>=2x2`, `MaxLv>=11`, `Dur>=200`); Dark Raven Leadership = `(petLevel * 15) + 185` | Classic MU / Season 6 Dark Raven command req formula | Same |
| `ItemExtensions.cs:135-143,205` | `MaximumStackSize` clamped `[1..255]` with `StackLimit` hook; client durability rounded with `Math.Ceiling` | Season 6/9 max stack `255` (configurable per item); ceiling durability matches original client | Same + HNMO StackLimit hook |
| `GameConfigurationHelper.cs:1-100`, `ModelResourceProvider.cs` | Generic config reflection enumerators & localization helpers | Infrastructure | No game rules |
