# Unit Audit: `client/web/src/ui (files 5)`

- **Unit**: `client/web/src/ui (files 5)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `16`: `personalShop.ts`, `petInfoWindow.ts`, `questWindows.ts`, `rankingWindow.ts`, `rml.ts`, `screenshot.ts`, `seasonColumns.ts`, `seasonPlanWindow.ts`, `settingsWindow.ts`, `socialDialogs.ts`, `teleportNpcWindow.ts`, `timetableWindow.ts`, `topRacesWindow.ts`, `topUpMilestonesWidget.ts`, `topUpMilestonesWindow.ts`, `touchControls.ts`
- **Status**: `Checked` (reclassified from `"No game rules"` because `personalShop.ts`, `petInfoWindow.ts`, `questWindows.ts`, and `teleportNpcWindow.ts` hold game rules/formulas)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Personal Shop level, map & UI slot limits | `personalShop.ts:50-58,520,530-534,715-727` | `STORE_MIN_LEVEL = 6`; `STORE_GRID` `firstSlot = 204` (`8x4`); `TITLE_MAX = 25`; blocked in Chaos Castle `[18..23, 53]` and while Merchant/Vault/Trade/Mix open | Server `PlayerShop` & `InventoryConstants.FirstStoreItemSlotIndex = 204` | Same |
| Personal Shop item ban, currencies & price warning | `personalShop.ts:65-100,665-674` | `isPersonalShopBan` allows Sign of Lord (`14:21 +3`), bans Heart of Dark Lord (`14:11 +13`) & Wizard's Ring `!= +0`; `0` Zen, `0xfff0..0xfff2` Coins, `(group<<12)\|num` items; confirms `< itemValue(item, 2)` | Desktop `IsPersonalShopBan` & `server/src/GameLogic/PlayerShopCurrencies` | Same |
| Dark Horse stat formulas (Pet Info window) | `petInfoWindow.ts:234-245` | `dmgMin = floor(str/10) + floor(cmd/10) + lv*5`, `dmgMax = dmgMin + floor(dmgMin/2)`, `speed = 20 + floor(lv*4/5) + floor(cmd/50)`, `rate = 1000 + lv + lv*15` | Desktop `GIPetManager.cpp:571-577` | Same |
| Dark Raven stat & Command req formulas | `petInfoWindow.ts:251-263` | `dmgMin = 180 + lv*15 + floor(cmd/8)`, `dmgMax = 200 + lv*15 + floor(cmd/4)`, `speed = 20 + floor(lv*4/5) + floor(cmd/50)`, `rate = 1000 + lv + lv*15`, `Charisma Requirement = 185 + lv*15` | `socket-options-and-pets-s6-s9.md:53` & `GIPetManager.cpp:563-569` | Same |
| Legacy & Progress Quest client conditions | `questWindows.ts:119,198,239-248,588-592,656-685` | Hardcodes BC/IT daily limit text `6` (`EnterPerDay, 6`); `[3,4,6]` dark NPC display; checks bag items & kill counts; NPC dialogue paging `7` lines / `11` answer lines | Desktop `CMyQuestInfoWindow` / `CNPCDialogue` | **Differs** if server daily entry limit `!= 6` |
| Teleport NPC requirement formatting | `teleportNpcWindow.ts:153-160` | Formats server `minLevel`/`maxLevel`, `minResets`, `vipOnly`, `allowPk`, `zenCost` DTOs | `TeleportNpcPlugin` | Same (server-driven) |
| Remaining UI views | `rankingWindow.ts`, `rml.ts`, `screenshot.ts`, `seasonColumns.ts`, `seasonPlanWindow.ts`, `settingsWindow.ts`, `socialDialogs.ts`, `timetableWindow.ts`, `topRacesWindow.ts`, `topUpMilestones*.ts`, `touchControls.ts` | Server-driven HTML/RML panels, settings slider (`0..10` audio level), touch buttons | Presentation only | No game rules |
