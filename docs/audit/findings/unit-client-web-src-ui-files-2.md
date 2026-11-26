# Unit Audit: `client/web/src/ui (files 2)`

- **Unit**: `client/web/src/ui (files 2)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `18`: `commandWindow.ts`, `communityWindow.ts`, `confirmDialog.ts`, `duelAndGens.ts`, `duelWindows.ts`, `escapeHtml.ts`, `escrowWindow.ts`, `eventAlertsWidget.ts`, `eventWindows.ts`, `fieldEventDocs.ts`, `fieldEventWindows.ts`, `friendWindows.ts`, `gameDialogs.ts`, `gensWindow.ts`, `giftBoxWindow.ts`, `giftCodesWindow.ts`, `guide.ts`, `guildLevelsWindow.ts`
- **Status**: `Checked` (reclassified from `"No game rules"` because `commandWindow.ts`, `escrowWindow.ts`, `eventWindows.ts`, `fieldEventWindows.ts`, `friendWindows.ts`, and `gensWindow.ts` enforce client rules)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Command window (D) proximity threshold | `commandWindow.ts:18` | `MAX_DISTANCE_TILE = 2` cells for proximity Trade/Buy/Party/Guild/Alliance/Hostility/Duel actions | Desktop `CCommandWindow` (`kMaxDistanceTile = 2`) | Same |
| Escrow fee preview formula | `escrowWindow.ts:214-250` | `computeEscrowFee`: clamps `pct` to `0..25`; `raw = Math.round(priceAmount * (pct / 100))`, clamps `[minFee, maxFee]` and `<= priceAmount`; `Split`: `buyerFee = floor(totalFee / 2)`, `sellerFee = totalFee - buyerFee` | `server/src/GameLogic/PlugIns/Escrow` (`EscrowEngine.ComputeFee`) | Same |
| Direct VND receipt upload limit & PIN cap | `escrowWindow.ts:496,690-691` | Receipt upload defaults max `524288` bytes; 2nd-password PIN `maxLength = 4`; deadline `Math.max(1, ...)` | `EscrowConfiguration` / `EscrowDirectVndEngine` | Same |
| Event ticket bag right-click guard | `eventWindows.ts:387-388,425-428` | Blocks right-click use on slots `< 12` or while NPC Merchant/Vault window (`0, 1, 2`) is open before calling `events.useTicket` | Server/desktop ticket usage guards | Same |
| Kalima Lost Map safezone/map block on bag right-click | `fieldEventWindows.ts:209-212,227` | Slot `>= 12` calls `kalimaTicketRefused(item, safeZone, mapNumber)`; Crywolf contract checks `MOUNTS` in `PET_SLOT` | Server `Hellas` / `CrywolfContractRequestAction` | Same |
| Castle Siege guild master / alliance guard | `fieldEventWindows.ts:292,306-308` | Commander HUD enabled only when `siege.formation !== 0` and Guild Master (`alliance === '' || alliance === name`) | Server `CastleSiege` alliance commander privileges | Same |
| Letter cost & max text length | `friendWindows.ts:470,477,521-530` | Displays `LETTER_COST` zen cost in title; clamps letter body to `MAX_LETTER_TEXT = 1000` chars; requires non-empty `mailto`, `subject`, `body` | Desktop `CUILetterWriteWindow` (`1000` bytes) & `FriendServer` | Same |
| Gens window non-member block & CC name-tag hide | `gensWindow.ts:55-58,152,206` | `toggle()` blocks non-members (`!gens.familyName`); `GensMarks` hides hover name/emblem tag in Chaos Castle | Desktop `CGensRanking` + `CNameWindow` Chaos Castle hiding | Same |
| Remaining UI dialogs & widgets | `communityWindow.ts`, `confirmDialog.ts`, `duelAndGens.ts`, `duelWindows.ts`, `escapeHtml.ts`, `eventAlertsWidget.ts`, `fieldEventDocs.ts`, `gameDialogs.ts`, `giftBoxWindow.ts`, `giftCodesWindow.ts`, `guide.ts`, `guildLevelsWindow.ts` | Server-driven RML views & modal panels displaying server DTOs | Presentation only | No game rules |
