# Unit Audit Findings — `server/src/GameLogic (files 2)`

- **Unit**: `server/src/GameLogic (files 2)` (`GameContext.cs` through `ItemStorageAdapter.cs` + `I*.cs` interfaces, 53 files)
- **Checked on**: 2026-11-24

## Scope Covered

- Core context/map/terrain: `GameContext.cs`, `GameMap.cs`, `GameMapTerrain.cs`, `GameMasterMagicEffectDefinition.cs`, `GlobalUsings.cs`
- Item pricing, power-ups, durability & inventory: `InventoryStorage.cs`, `ItemConstants.cs`, `ItemExtensions.cs`, `ItemIdentifier.cs`, `ItemOptionExtensions.cs`, `ItemPowerUpFactory.cs`, `ItemPriceCalculator.cs`, `ItemPriceFormula.cs`, `ItemStorageAdapter.cs`, `HnmoSkills.cs`, `HitInfo.cs`, `GuildEventArgs.cs`, `ItemEventArgs.cs`
- Interfaces: `IAreaOfInterestManager.cs` through `IWorldObserver.cs` (35 files)

## Findings & Bugs

1. **`GameMapTerrain.SafezoneMap` misses combined bitmask on initial `.att` load (`GameMapTerrain.cs:405` vs `438`)**:
   - In `ApplyTerrainAttribute` (`line 438`), `Safezone` is toggled via `(value & Safezone) == Safezone`.
   - However, during `.att` parsing (`line 405`), `SafezoneMap[x, y] = value == 1;` uses strict equality rather than `(value & 1) == 1`. Any terrain cell combining `Safezone` (`0x01`) with another flag (for instance `NoGround` or `Blocked`) is parsed as non-safezone on initial load, inconsistent with `ApplyTerrainAttribute`.
2. **`InventoryStorage` right-hand staff skips `Stats.WizardryBaseDmg` (`InventoryStorage.cs:278-284`) vs `ItemPowerUpFactory` weapon hand remapping (`ItemPowerUpFactory.cs:65-88, 427-442`)**:
   - `InventoryStorage` explicitly ignores `Stats.WizardryBaseDmg` options when a staff is placed in `RightHandSlot`.
   - `ItemPowerUpFactory` remaps `MinimumPhysBaseDmgByWeapon`/`MaximumPhysBaseDmgByWeapon` to `*ByRightWeapon` for `RightHandSlot` items and applies a `-1` adjustment when averaging dual-wield right-hand `PhysicalBaseDmgIncrease` excellent options (`lines 437-442`) — behavior matches the special dual-wield/staff rules documented for Season 6/9 equipment stats.
3. **`GameContext.cs:28-29, 135-136, 397-408`**:
   - Default fallback experience tables use standard `10 * (lvl + 8) * (lvl - 1)^2` (plus `+1000 * (lvl - 255) * (lvl - 254)^2` at `lvl >= 256` — note S6/S9 client/server uses the same formula with `lvl > 255`), and Master EXP table uses `((9 + ml) * ml * ml * 10) + ((ml - 255) * ...)` scaled for master levels.
   - Bot login slot exclusion (`lines 397-408`) ensures bots/templates do not occupy real player login capacity (`OccupiedPlaces`).
4. **`ItemExtensions.cs` (`durability`, `GetRequirement`, `ResolvedItemStatProfile`)**:
   - Item level durability bonus table `[0, 1, 2, 3, 4, 6, 8, 10, 12, 14, 17, 21, 26, 32, 39, 47]`, Ancient `+20`, Excellent `+15`.
   - Durability warning/stat penalty thresholds at `< 50%` (`0.5`), `< 30%` (`0.7`), and `< 20%` (`0.8` loss of effectiveness).
   - Requirement scaling computes `RequirementAdd + baseValue * (dropLevel + itemLevelBonuses) * RequirementMultiplier / 100` using `Int128` clamping to avoid intermediate overflow.
5. **`ItemPriceCalculator.cs` & `ItemPriceFormula.cs`**:
   - `MaximumPrice = 3_000_000_000`.
   - Wings base formula `((dl + 40) * dl^2 * 11) + 40_000_000`; regular items `((dl + 40) * dl^2 / 8) + 100`.
   - Item level drop-level bonus table for `+5..+15`: `[4, 10, 25, 45, 65, 95, 135, 185, 245, 305, 365]`; Excellent adds `+25` effective dropLevel.
   - Modifiers: Skill `* 2.5` (`+150%`), Luck `+25%`, Option L1 `+60%` / Ln `+70% * 2^(n-1)`, Wing `+25%` per option, Excellent `* 2` per excellent option, Level-380 Guardian `+16%`, Sell `base / 3` penalized by durability loss (`0.6` factor).
   - Repair formula: `3 * sqrt(base)^...` (`3 * base^0.75 * (1 - dur/maxDur) + 1`), scaled `2.0x` for destroyed pets, `1.4x` for destroyed regular items, and `2.5x` for self-repair (non-NPC).
6. **`HnmoSkills.cs:1-60`**:
   - Server custom skill range `700..727` (`700 + 4 * class + letter`), 4 skills per class (`A`, `B`, `C` level-unlocked + `D` trial quest). Intended HNMO customization.
7. **Other reviewed classes & 35 `I*.cs` interfaces**:
   - `GameMap.cs`, `ItemConstants.cs`, `ItemIdentifier.cs`, `ItemOptionExtensions.cs`, `ItemStorageAdapter.cs`, `HitInfo.cs`, `GuildEventArgs.cs`, `ItemEventArgs.cs`, `GameMasterMagicEffectDefinition.cs`, and all 35 `I*.cs` files are clean domain abstractions/events/constants with no rule defects.
