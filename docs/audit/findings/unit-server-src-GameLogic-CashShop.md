# Audit Findings: `server/src/GameLogic/CashShop`

- **Unit**: `server/src/GameLogic/CashShop` (20 files, 1,073 lines)
- **Status**: `Checked` on 2026-11-24
- **Reference**: [item-options-380-harmony-cashshop.md](../references/item-options-380-harmony-cashshop.md)

## Files Reviewed (in full — all 20 files)
1. `CashShopBalanceDetails.cs` (1–11)
2. `CashShopBalanceReason.cs` (1–45)
3. `CashShopBalanceReceipt.cs` (1–87)
4. `CashShopCoinExtensions.cs` (1–134)
5. `CashShopFeaturePlugIn.cs` (1–39)
6. `CashShopProductDelivery.cs` (1–75)
7. `CashShopProductExtensions.cs` (1–28)
8. `CashShopPurchaseDetails.cs` (1–12)
9. `CashShopPurchaseReceipt.cs` (1–58)
10. `CashShopSettings.cs` (1–31)
11. `CashShopVersionPlugIn.cs` (1–42)
12. `CharacterCardDeliveryPlugInBase.cs` (1–68)
13. `GoblinPointsForPlayTimeConfiguration.cs` (1–31)
14. `GoblinPointsForPlayTimePlugIn.cs` (1–117)
15. `InventoryExtensionDeliveryConfiguration.cs` (1–17)
16. `InventoryExtensionDeliveryPlugIn.cs` (1–61)
17. `InventoryProductDelivery.cs` (1–85)
18. `RageFighterCharacterCardDeliveryPlugIn.cs` (1–32)
19. `SummonerCharacterCardDeliveryPlugIn.cs` (1–32)
20. `VaultExtensionDeliveryPlugIn.cs` (1–48)

## Rules & Behavior Summary
- `CashShopCoinExtensions` / `CashShopBalanceReceipt` / `CashShopPurchaseReceipt`: Atomic coin grant application (skips grants that would overflow `int.MaxValue` without partial splitting; clamps debits at `0`), bilingual JSON audit receipts (`account-balance-v1` and `cash-shop-purchase-v1`).
- `CashShopProductDelivery` / `InventoryProductDelivery`: Resolves `ICashShopProductDeliveryPlugIn` by item or group; blocks delivery if a known strategy exists for the item/group but is currently deactivated (e.g., disabled character card); otherwise delivers permanent items into inventory (`InventoryProductDelivery`) splitting stackable items across `definition.Durability` with full rollback if inventory lacks space.
- Specialized deliveries: `SummonerCharacterCardDeliveryPlugIn` (`14:91` -> class `20`), `RageFighterCharacterCardDeliveryPlugIn` (`14:169` -> class `24`), `InventoryExtensionDeliveryPlugIn` (`14:162`, capped at `InventoryConstants.MaximumNumberOfExtensions`), `VaultExtensionDeliveryPlugIn` (`14:163`).
- `GoblinPointsForPlayTimePlugIn` (disabled by default): Checks every `1 min`, awards `10` GoblinPoints per `1 hr` continuous online play meeting `MinimumLevel` (excludes `OfflinePlayer` unless `IncludeOfflineLeveling = true`).
- `CashShopVersionPlugIn`: Sends script/banner versions on `CharacterSelection -> EnteredWorld` and resets `IsCashShopOpen = false` on returning to character select.

## Discrepancies & Observations
1. **Expiring Cash Shop products unsupported (`CashShopProductExtensions.cs:26`, `InventoryProductDelivery.cs:27`, `CharacterCardDeliveryPlugInBase.cs:36`, `InventoryExtensionDeliveryPlugIn.cs:36`, `VaultExtensionDeliveryPlugIn.cs:29`)**: All built-in deliveries require `product.Duration == TimeSpan.Zero`; timed buffs/pets/seals cannot be bought or delivered yet.
2. **Dark Lord / Magic Gladiator character cards**: Only Summoner (`14:91`) and Rage Fighter (`14:169`) cards have delivery plugins (`CharacterCardDeliveryPlugInBase`); MG/DL unlock is handled by level thresholds unless configured otherwise.
