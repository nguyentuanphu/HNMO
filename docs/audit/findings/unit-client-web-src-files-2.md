# Unit Audit: `client/web/src (files 2)`

- **Unit**: `client/web/src (files 2)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `8` (`4,955` lines): `main.ts` (`3,031` lines — read), `modulusCipher.ts` (`854`), `siteLanguage.ts` (`46`), `terrain.ts` (`302`), `topupPage.ts` (`265`), `vietqr.ts` (`404`), `vipModels.ts` (`40`), `vipTexts.ts` (`62`)
- **Status**: `Checked` (reclassified from `"No game rules"` because `main.ts` enforces concrete character, inventory, shop, party, guild, warp, and duel client rules)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | Location | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Character creation name length | `main.ts:1066-1071` | Trims name; requires `4 <= name.length <= 10`, else refuses locally | Server `CreateCharacterAction` & suite character rules (`4..10` bytes/chars) | Same |
| Max character slots check | `main.ts:1059` | Refuses creation locally when `characterList.length >= 5` | Suite / server max 5 characters per account | Same |
| Equipment +15 glow suppression in Chaos Castle | `main.ts:1482` | Disables/suppresses `+15` gear sparkle inside Chaos Castle maps (`18..23, 53`) to avoid identifying players | Standard Chaos Castle stealth rule (desktop hides sparkles & player names in CC) | Same |
| Repair-capable merchant NPCs | `main.ts:1711` | Hardcodes 5 NPC type IDs `[239, 240, 251, 254, 255]` where NPC Repair button is enabled (`Arena Guard`, `Safety Guardian`, `Hanzo`, `Pasi`, `Lumen`) | Desktop `CNPCShop` / server merchant repair whitelist (`NpcDefinitions`) | Same (static whitelist) |
| Friend Messenger level & CC block | `main.ts:1766-1774` | Blocks opening/using Friends list in Chaos Castle or below level `6` | Server Friend system minimum level `6` & Chaos Castle social block | Same |
| Move (M) window outlaw flag bug | `main.ts:1885-1891` | Passes hardcoded `outlaw: false` to `MoveWindow` `check()` instead of hero's PK state (`pkLevel >= 6`) | Desktop `CMoveCommandWindow` & server `WarpCommandAction` block PK/Murderer (`>= 6`, killer warp ban when server disallows) | **Differs** (client UI shows PK players as eligible to warp even when server rejects) |
| Bag space guard before quest / reward claim | `main.ts:1936-1978` | Requires `>= 4` free slots in `12..75` (base bag) and checks gate/keeper preconditions before proceeding | Server bag space validation; note extended bag (`76+`) is excluded from client check | **Differs** (checks only base bag `12..75` for 4 free slots) |
| Command Window (D) target/guild/party checks | `main.ts:2540-2578` | Checks proximity, guild/alliance/hostility status, and party membership before dispatching Command Window requests (`/party`, `/trade`, `/guild`, etc.) | Server `GuildRequest`, `PartyRequest`, `TradeRequest` handlers | Same |
| Chat `/party` & `/trade` facing/range checks | `main.ts:2617-2633` | `/party` and `/trade` look up named/facing player nearby; blocks ChaosMachine & Vault opening while Duel UI is active | Server `PartyRequest` / `TradeRequest` / active Duel state blocks | Same |
| Mobile HUD quick-sell & repair actions | `main.ts:2784-2803` | Guards mobile sell/repair clicks by open NPC merchant state | Server `SellItemToNpc` & `RepairItem` handlers | Same |
| Auxiliary utility/portal files | `modulusCipher.ts`, `terrain.ts`, `vietqr.ts`, `topupPage.ts`, `siteLanguage.ts`, `vipModels.ts`, `vipTexts.ts` | Packet modulus decrypt/encrypt (`modulusCipher`), Three.js terrain mesh/shader (`terrain`), VietQR QR encoder (`vietqr`), top-up milestone form (`topupPage`), portal language & VIP DTO labels | Client transport / rendering / website portal only | No game rules |

## Findings / Discrepancies

1. **`main.ts:1885-1891` (`outlaw: false` in `MoveWindow`)**: Client hardcodes `outlaw: false` instead of wiring the hero's PK level, so Murderer characters see locked maps as movable in the `M` list before the server rejects the warp. (Logged for Part 9 Item 3 bug fix.)
2. **`main.ts:1936-1978`**: Base bag check inspects slots `12..75` only (does not count extended inventory slots `76..203`).
