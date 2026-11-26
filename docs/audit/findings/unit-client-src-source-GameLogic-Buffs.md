# Unit Audit: `client/src/source/GameLogic/Buffs`

- **Unit**: `client/src/source/GameLogic/Buffs`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `10` (`1,006` lines) including `w_Buff.cpp`, `w_BuffScriptLoader.cpp`, `w_BuffStateSystem.cpp`, `w_BuffStateValueControl.cpp`, `w_BuffStateValueControl.h`, `w_BuffTimeControl.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — holds client buff mutual-exclusion groups and buff value/duration decoding)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Buff mutual-exclusion groups | `w_Buff.cpp:12-71` | `GetTokenBufflist`: defines mutual replacement groups for Castle Siege regiments (`eBuff_CastleRegimentDefense/Attack`), Crywolf Elf Altars (`1..5`), PC Room Seals, Cash Shop Seals (Ascension/Wealth/Sustenance/Mobility/Healing/Divinity), Elite Scrolls, and Secret Potions (`SecretPotion1..5`) | Server `MagicEffectSystem` / CashShop buff replacement groups | Same |
| Buff value / duration assignment (**BUG**) | `w_BuffStateValueControl.cpp:49-69`, `w_BuffStateValueControl.h:10-12` | Struct `BuffStateValueInfo` has `{ s_Value1, s_Value2, s_Time }`, but `SetValue` (lines `60-61`) writes `valueinfo.s_Value2 = Item_data.m_byValue2;` followed immediately by `valueinfo.s_Value2 = Item_data.m_Time;` instead of `valueinfo.s_Time = Item_data.m_Time;` (and the `else` branch lines `66-67` zeroes `s_Value2` twice without zeroing `s_Time`) | Item add-option buff duration/value contract (overwrites `s_Value2` and leaves `s_Time` uninitialized/stale) | **Differs** (copy-paste bug corrupts `s_Value2` and leaves `s_Time` unset) |

## Discrepancies / Bugs Found

1. **Bug in `w_BuffStateValueControl.cpp:60-61,66-67`**: `valueinfo.s_Value2 = Item_data.m_Time;` overwrites `s_Value2` instead of assigning `valueinfo.s_Time = Item_data.m_Time;` (similarly `else` sets `s_Value2 = 0` twice and omits `s_Time = 0`). Logged for Part 9 Item 3 fix & test.
