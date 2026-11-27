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

## Re-check 2026-11-27 (changes 8cb8539b..a108e846)

Commits read (every changed file read in full):

- 42a8ada9 Merge #415 (queued confirm boxes reuse the open document) into the integration branch
- 8a25e0ee fix(web): a queued confirm or menu box reuses the open box's document
- e385ec55 Trade and vault zen boxes belong to the trade or vault they were asked in
- 07af4b55 Merge remote-tracking branch 'origin/claude/hnmo-main-handover-54co2m' into fix380
- ba5139b1 feat(web): make chat, whisper and messenger windows resizable with localStorage persistence
- be1d1b3e fix(web): trade zen in-game prompt and clean Alt name-tag toggle (Part 9 Item 1)

Evidence:

- gameDialogs.ts: a queued confirm/menu box reuses the open document (a second document stayed on top and caught clicks) - fixed (#415, confirm-queue.spec).
- confirmDialog.ts promptInGame and the trade zen box use the in-game box instead of window.prompt (which blocked packets) - fixed.
- friendWindows.ts: messenger windows resizable and remembered - web-only convenience, same remark as the chat (owner to confirm).
