# Unit: server/src/DataModel/Configuration/ItemCrafting

Checked against: Season 6 & Season 9 Chaos Machine mix rule roles.

Scope: `server/src/DataModel/Configuration/ItemCrafting` (`6` files, `406` lines).

## Summary

- Models Chaos Mix recipe schema (`SimpleCraftingSettings`, `ItemCraftingRequiredItem`, `MixResult`, `ResultItemSelection`), capturing classic MU Chaos Weapon / 1st Wing `NpcPriceDivisor` (+1% rate & cost per divisor) and Luck/Excellent/Ancient/Guardian/Socket bonus terms without hardcoded mix rates (seeded in `Initialization`).
