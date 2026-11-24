# Unit Audit: `client/src/source/UI/NPCs`

- **Unit**: `client/src/source/UI/NPCs`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `16` (`2,897` lines) including `NPCShop.cpp`, `GatemanWindow.cpp`, `NPCDialogue.cpp`, `HighValueWarning.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `NPCShop.cpp` and `GatemanWindow.cpp` enforce Zen caps, sell bans, Harmony sell prohibition, high-value confirmation, and Blood Castle/CS gatekeeper fee checks)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| NPC Sell Zen cap guard | `NPCShop.cpp:368-373` | Blocks selling when `CharacterMachine->Gold + ItemValue(pItem) > GameLogic::Items::kMaxInventoryZen` (`2,000,000,000`) | Server `SellItemToNpcAction` & `ZenAmount.h:14` | Same |
| Harmony-strengthened item sell block | `NPCShop.cpp:375-380` | Blocks selling any item with `pItem->Jewel_Of_Harmony_Option != 0` (`ReinforcedItemCanTBeSold`) | Server `SellItemToNpcAction` / classic Harmony lock | Same |
| Sell ban & high-value confirmation | `NPCShop.cpp:381-405` | Blocks `GameLogic::Items::IsSellingBan(pItem)`; prompts confirmation for `IsHighValueItem(pItem)` before sending `SendSellItemToNpcRequest` | Server `SellItemToNpcAction` | Same |
| Castle Siege Gatekeeper entrance fee check | `GatemanWindow.cpp:186,347` | Compares `(int)CharacterMachine->Gold >= g_pUIGateKeeper->GetEnteranceFee()` before allowing CS entry | Server `CastleSiege` Gatekeeper NPC | Same |
