# Unit Audit: `client/web/src/ui (files 3)`

- **Unit**: `client/web/src/ui (files 3)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `19`: `guildWindows.ts`, `hnmoPanel.ts`, `hud.ts`, `instanceEventWindows.ts`, `itemIcons.ts`, `itemStacks.ts`, `itemTooltipView.ts`, `itemWindows.ts`, `jewelBankWidget.ts`, `jewelBankWindow.ts`, `layoutCache.ts`, `leftoversWindow.ts`, `loadingScreen.ts`, `luckyNumberWidget.ts`, `luckyNumberWindow.ts`, `mapNameBanner.ts`, `marketWidget.ts`, `marketWindow.ts`, `marriageWindow.ts`
- **Status**: `Checked` (reclassified from `"No game rules"` because `guildWindows.ts`, `itemStacks.ts`, `itemWindows.ts`, `jewelBankWindow.ts`, `instanceEventWindows.ts`, and `marriageWindow.ts` enforce client validation/formulas)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Guild member limit formula | `guildWindows.ts:325-327` | Guild Master: Dark Lord `min(80, floor(level / 10) + floor(leadership / 10))`; other classes `floor(level / 10)` | Desktop `GuildInfoWindow` & `common-server-and-character-rules-s6-s9.md:39` | Same |
| Guild creation, disband & appoint validation | `guildWindows.ts:217-247,433-436` | Rejects `/[^0-9A-Za-z]/`, name `< 4` chars, empty `8x8` mark; alliance master cannot disband/withdraw; password prompt (`maxLength: 20`) | Server `GuildCreateAction` / `GuildRemoveAction` | Same |
| Stack split & unlock validation | `itemStacks.ts:90-118` | Shift+click split on `group === 14` or stack kinds when `durability >= 2`, `1 <= count < durability`; unlocks only `!unlocked && price > 0` | `server/src/GameLogic/PlugIns/ItemStacks` | Same |
| Equipment slot map, gold color tiers, 4 bag extensions & high-value sell guard | `itemWindows.ts:85-109,534-584,1173-1178` | Slots `0..11`; tiers `100k / 1M / 10M`; `BAG_EXTENSION_COUNT = 4` (`32` cells each); blocks `isSellingBan(item)` and confirms `isHighValue(item)` | Server `InventoryConstants` & Desktop `CNPCShop` | Same (`window.confirm` at `:1176`) |
| Jewel Bank withdraw quantity clamp | `jewelBankWindow.ts:190-208` | Clamps withdraw amount to `1..50` | `JewelBankPlugin.cs:241` (`Math.Clamp(quantity, 1, 50)`) | Same |
| Marriage form field limits | `marriageWindow.ts:177,217` | Partner character name `maxLength = 10`; custom title `maxLength = 32` | `MarriagePlugin` | Same |
| Illusion Temple mini-map projection & shift+right-click skill trigger | `instanceEventWindows.ts:196-199,844-860` | Fixed anchor arrays & `MINI_SCALE = 1.56`; requires `ILLUSION_TEMPLE_MAPS` + Shift+right-click | Desktop `CNewUICursedTempleSystem` | Same |
| Remaining HUD & panel views | `hnmoPanel.ts`, `hud.ts`, `itemIcons.ts`, `itemTooltipView.ts`, `jewelBankWidget.ts`, `layoutCache.ts`, `leftoversWindow.ts`, `loadingScreen.ts`, `luckyNumber*.ts`, `mapNameBanner.ts`, `market*.ts` | Server-driven UI widgets/panels and icon/tooltip renderers | Presentation only | No game rules |

## Re-check 2026-11-27 (changes 8cb8539b..a108e846)

Commits read (every changed file read in full):

- 719d4380 Trade OK and vault zen boxes are bound to their trade, offer and vault page
- e385ec55 Trade and vault zen boxes belong to the trade or vault they were asked in

Evidence:

- itemWindows.ts: the vault zen box belongs to its vault session and page; closing, reopening or switching the page dismisses it and its answer does nothing - fixed (#410/#414). Note: an open() that refreshes an already open vault also ends the session, so a zen box open at that moment is dismissed (harmless; the player asks again).
