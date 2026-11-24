# Audit Findings: `client/src/source/GameLogic`, `MUHelper`, `GameShop`, `Guild`, `Character` (20 Units)

Checked on: `2026-11-24`

## Units Covered
- `client/src/source/GameLogic/*` (15 units): `Automation`, `Buffs`, `Combat`, `Commands`, `Events`, `Items (files 1..2)`, `Monsters`, `NPCs`, `Party`, `Pets`, `Quests`, `Skills`, `Social`
- `client/src/source/MUHelper`, `client/src/source/GameShop (files 1)`, `client/src/source/GameShop/FileDownloader`, `client/src/source/GameShop/ShopListManager`, `client/src/source/Guild`, `client/src/source/Character`

## Key Rules Verified
- **Item & Crafting Rules (`GameLogic/Items/*`)**:
  - `MixMgr.cpp`: Loads `Data/Local/mix.bmd`, resolves recipe texts (`kMixRecipeTexts` sorted binary search), validates ingredients, and computes mix rates/zen matching the web client's `mixRecipes.ts`.
  - `EquipmentRestrictions.cpp`: Requires flight equipment (wing or flight helper) on `WD_10HEAVEN` (Icarus) before allowing unequip.
  - `TradeRestrictions.cpp` & `ShopRestrictions.cpp`: Enforces trade, drop, store, personal-shop, sell, and repair bans from `ItemDatabase` plus item-level/rental/GM exceptions (e.g., Sign of Lord tradeable, Wizard's Ring/Heart of Dark Lord non-tradeable, rented items non-storable).
- **Combat & Skill Execution (`GameLogic/Combat/*`, `GameLogic/Skills/*`)**:
  - `SkillExecution.cpp`: `CanExecuteSkill` checks dead state and safe-zone exceptions (Blood Castle / Chaos Castle allow buff skills only).
- **Quests, MUHelper, GameShop, Guild, Character**:
  - `QuestMng.cpp`: Loads `NPCDialogue.bmd`, `QuestProgress.bmd`, and `QuestWords_*.bmd` with BUX XOR decoding.
  - `MUHelper`, `GameShop`, `Guild`, `Character`: Client-side UI/state managers backed by server-authoritative packets.
- **Verdict**: Canonical desktop client rule implementations; no silent rule regressions found.
