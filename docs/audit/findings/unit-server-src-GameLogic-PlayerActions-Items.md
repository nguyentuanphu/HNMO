# Audit: `server/src/GameLogic/PlayerActions/Items`

- **Unit**: `server/src/GameLogic/PlayerActions/Items`
- **Files read in full**: 25 of 25 (3,209+ lines total)
  - `BaseItemCraftingHandler.cs` (262), `BuyNpcItemAction.cs` (137), `CraftingRequiredItemLink.cs` (34), `DropItemAction.cs` (128), `IItemCraftingHandler.cs` (33), `ItemBoxDroppedPlugIn.cs` (71), `ItemCraftAction.cs` (248), `ItemCraftingHandlerFactory.cs` (23), `ItemRepairAction.cs` (136), `ItemStackAction.cs` (160), `KalimaGate.cs` (97), `LostMapDroppedPlugIn.cs` (58), `MoveItemAction.cs` (533), `OutlawNpcShopRule.cs` (38), `PetInfoRequestAction.cs` (66), `PetStorageLocation.cs` (77), `PickupItemAction.cs` (274), `SellItemToNpcAction.cs` (105), `SetPetBehaviourRequestAction.cs` (35), `SimpleItemCraftingHandler.cs` (305)
  - `ItemRegistration/`: `BaseItemRegistrationStrategy.cs` (187), `GoldenArcherRegistrationStrategy.cs` (50), `IItemRegistrationStrategy.cs` (46), `ItemRegistrationAction.cs` (50), `ItemRegistrationNpcTalkPlugIn.cs` (49)
- **Suite references**:
  - Season 6 & Season 9 `IGCData/IGC_Common.ini` (PK shop restriction, Lahap combine/dismantle fees, Kalima gate, pickup range)

## Rules & Comparison

| Rule / Behaviour | HNMO | Season 6 / Season 9 Suite | Match |
|---|---|---|---|
| Outlaw NPC shop ban (`OutlawNpcShopRule`) | Blocks buy, sell & NPC repair for `HeroState >= PlayerKiller1stStage` (PK2+); allows inventory self-repair | Outlaws PK2+ blocked from NPC shops; PK Warning allowed | Same |
| Lahap Jewel stacking (`ItemStackAction`) | Combine: `500,000 * (stackSize / 10)` zen; Dismantle: `1,000,000` zen | S6/S9 Lahap combine/dismantle fees (500k/10, 1M dismantle) | Same |
| Pickup & drop (`PickupItemAction`, `DropItemAction`) | `MaximumPickupDistance = 3`; checks `BackupInventory`, `StorageLimitPerCharacter`, `IsQuestItem` (active quest check), `IsBoundToCharacter`, `IsOwnerPickupPriorityActive`; `DropItemAction` saves before detaching | Standard pickup range 3, ownership timer, quest/bound rules | Same |
| Move & equip (`MoveItemAction`) | Enforces `CompliesRequirements`, two-hand/shield conflicts, `CompliesWithMapRequirements` on unequip, resets both trade buttons on offer change, plus HNMO Arena/Duplicate/AccountSafety guards | Standard equipment & trade-reset rules | Same |
| Repair (`ItemRepairAction`) | Pet slot requires opened NPC (Pet Trainer); `RepairAllItemsAsync` skips pet slot; charges NPC vs self-repair rate | Standard S6/S9 repair rules | Same |
| Crafting pipeline (`BaseItemCraftingHandler`, `SimpleItemCraftingHandler`, `ItemCraftAction`) | Applies `CraftingBonusPercentage` (keeps 0 or 100 fixed) then `ChaosRatesPlugIn.AdjustSuccessRate`; enforces `SeasonPlanPlugIn` gates & `AccountSafetyPlugIn` | Standard rate & downgrade rules (+ Talisman/SeasonPlan/Safety guards) | Same |
| Kalima gate & Golden Archer (`KalimaGate`, `LostMapDroppedPlugIn`, `ItemRegistration`) | Kalima gate NPCs 152..158 (1m duration), blocked in safezone/events/Kalima & by character level; Golden Archer (236) Rena registration | Standard Kalima gate & Golden Archer Rena registration | Same |
