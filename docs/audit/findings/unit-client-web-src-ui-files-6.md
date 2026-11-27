# Unit Audit: `client/web/src/ui (files 6)`

- **Unit**: `client/web/src/ui (files 6)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `5` (`1,136` lines): `tradeWindow.ts`, `ultimateAutoPlayWindow.ts`, `vaultPages.ts`, `vipWindow.ts`, `worldBossWindow.ts`
- **Status**: `Checked` (`ui (files 6)` split out of `ui (files 5)` on `8cb8539b` — audited here for `tradeWindow.ts` and `vaultPages.ts` rules)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Trade minimum level & post-change OK wait | `tradeWindow.ts:15-17,144-148,287,362,389` | `TRADE_MIN_LEVEL = 6`; `TRADE_WAIT_MS = 6000` (`150` frames @ 25 FPS) after partner gold change (`ButtonState.RED`) or own gold/item change | Desktop `CTrade` (`TRADELIMITLEVEL = 6`, `150` frames wait) & Server `TradeRequest` | Same |
| Trade item ban rules | `tradeWindow.ts:29-34` | `isTradeBan`: allows Sign of Lord (`14:21 +3`); bans Heart of Dark Lord (`14:11 +13`) & Wizard's Ring `!= +0`; else `!itemDefinition.tradable` | Desktop `TradeRestrictions.cpp` & Server `TradeItemBlock` | Same |
| Anti-scam trade item backup & compare | `tradeWindow.ts:38-62,243-277` | `worthRemembering` tracks helpers, jewels, wings, capes, `+5+` non-wing gear, excellent items; `compareTradeItems` flags downgraded replacements (`level`, `skill`, `excellent`, `optionCount`, `optionLevel`, `durability`) | Desktop `CTrade::BackUpYourInven` & `CompareItem` | Same |
| Trade auto-decline while Vault open & Zen cap | `tradeWindow.ts:194-197,347-366` | Auto-declines incoming trade while Vault or another trade is open; truncates gold input at 8 digits (`<= 99,999,999`) and checks `<= money + myGold` | Desktop `CTrade` | Same |
| Extra Vault unlock button gate | `vaultPages.ts:85,137` | Enables unlock button only when `state.price > 0 && state.unlocked < state.max` | `MultiVaultPlugin` | Same |
| Server-driven AutoPlay/VIP/WorldBoss panels | `ultimateAutoPlayWindow.ts`, `vipWindow.ts`, `worldBossWindow.ts` | HTML/HUD wrappers rendering server policy, VIP, and World Boss DTOs (`game/autoPlayExtras.ts` holds AutoPlay policy) | Presentation only | No game rules |

## Re-check 2026-11-27 (changes 8cb8539b..a108e846)

Commits read (every changed file read in full):

- 719d4380 Trade OK and vault zen boxes are bound to their trade, offer and vault page
- e385ec55 Trade and vault zen boxes belong to the trade or vault they were asked in
- be1d1b3e fix(web): trade zen in-game prompt and clean Alt name-tag toggle (Part 9 Item 1)

Evidence:

- tradeWindow.ts: the OK after a warning and the zen box are bound to the trade session and the offer revision (partner packets 0x38/0x39/0x3B bump it); the box closes when the trade ends - fixed (#410/#414, checked in the real game by local Claude on 27 Nov).
- vaultPages.ts: pageChanged fires on a sent switch and when the server answers another page - fixed.
