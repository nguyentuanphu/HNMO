# Unit Audit: `client/src/source/UI/Inventory (files 3)`

- **Unit**: `client/src/source/UI/Inventory (files 3)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `23` (`4,699` lines) including `StorageInventory.cpp`, `StorageInventoryExt.cpp`, `Trade.cpp`, `UIJewelHarmony.cpp`, `PurchaseShopInventory.cpp`, `UnitedMarketPlaceWindow.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Vault withdrawal/storage Zen fee formula | `StorageInventory.cpp:523-531` | `TotalLevel = Level + MasterLevel`; `nFee = int(TotalLevel^2 * 0.04) + (m_bLock ? Level * 2 : 0)`; clamped `>= 1`, rounded down to `100` (`>= 1000`) or `10` (`>= 100`) | Server `Vault` Zen withdrawal fee | Same |
| Vault Zen deposit / withdraw bounds | `StorageInventory.cpp:273,323,535` | Blocks depositing `> CharacterMachine->Gold` or withdrawing past `kMaxInventoryZen` (`2,000,000,000`); checks `IsVaultExtended > 0` for extra vault UI | `ZenAmount.h:14,28-36` & Server `Vault` | Same |
| Trade anti-scam item backup & gold check | `Trade.cpp:630,660-686,754-785` | Refuses trade while impossible interface (Vault/etc.) is open; checks `nInputGold <= Gold + MyTradeGold`; waits `MyTradeWaitAfterChange` frames (`6s` at 25 FPS) after changes; backs up helpers, jewels, wings, capes, `+5+` non-wings, excellent items & alerts on downgrade | Web `tradeWindow.ts:17,38-62,194,360` | Same |
| Harmony upgrade/refine Zen tables & Market/Shop | `UIJewelHarmony.cpp`, `UIJewelHarmony.h:14`, `PurchaseShopInventory.cpp`, `UnitedMarketPlaceWindow.cpp` | Loads `JewelOfHarmonyOption.bmd` (`Zen[14]` level cost array), browses player stores, handles Julia NPC Loren Market warp dialog | `Server` Harmony crafting & Shop/Market | Same |
