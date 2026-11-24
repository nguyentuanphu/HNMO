# Audit Findings: `server/src/GameLogic/PlayerActions/Party`

- **Unit**: `server/src/GameLogic/PlayerActions/Party` (4 files, 270 lines)
- **Status**: `Checked` on 2026-11-24
- **Reference saved**: [party-exp-and-pk.md](file:///D:/WorkSpace/HNMO-ag/docs/audit/references/party-exp-and-pk.md)

## Files Reviewed (in full)
1. `PartyKickAction.cs` (1–42)
2. `PartyListRequestAction.cs` (1–28)
3. `PartyRequestAction.cs` (1–124)
4. `PartyResponseAction.cs` (1–76)

## Rules & Behavior Summary
- `PartyKickAction`: Verifies caller is either `party.PartyList[0]` (party master) or kicking themselves (`members[index]`), logging a warning on unauthorized index.
- `PartyListRequestAction`: Invokes `IUpdatePartyListPlugIn.UpdatePartyListAsync()` if `player.Party != null`.
- `PartyRequestAction`: Blocks party requests when `player.CurrentMiniGame?.Definition.AllowParty is false`, when `IPartyRequestingPlugIn.PartyRequestingAsync` cancels (e.g. opposing Gens), when `QolPlugIn.AreRequestsBlocked(toRequest)` is active, when non-master sends invite, or when target already has a party/pending invite (unless `PartyRequestHandler.TryAutoAcceptPartyRequestAsync` handles a bot leaving its bot party or a party master accepting auto-party). Handles `OfflinePlayer` auto-accept and advances target state to `PlayerState.PartyRequest`.
- `PartyResponseAction`: Requires `PlayerState.PartyRequest` and `LastPartyRequester`, returns state to `EnteredWorld`, re-checks `AllowParty` and `IsPartyDeniedAsync` (so Gens/event changes during prompt are still enforced), joins requester's existing party or creates a new party via `PartyManager.CreateParty()`.

## Discrepancies & Observations
1. **Asymmetric mini-game party guard on initial request (`PartyRequestAction.cs:25`)**: `HandlePartyRequestAsync` checks `player.CurrentMiniGame?.Definition.AllowParty is false` on the inviter only, not `toRequest.CurrentMiniGame?.Definition.AllowParty`. Although `PartyResponseAction` checks the target's mini-game upon acceptance, an inviter outside the mini-game can still prompt a target inside a no-party mini-game.
2. **Silent state rejection when target is busy (`PartyRequestAction.cs:80-84`)**: If `toRequest.PlayerState.TryAdvanceToAsync(PlayerState.PartyRequest)` returns `false` (target in trade/vault/NPC/chaos box), no feedback message is sent to `player`.
3. **`PartyResponseAction` missing full-party capacity feedback (`PartyResponseAction.cs:61-65`)**: Relies on `Party.AddAsync` when adding to `LastPartyRequester.Party`; verify in runtime/Party class if requester's party filled up between invite and accept.
4. **S6/S9 AutoParty & Reset Party removal**: S6/S9 `IGC_Common.ini` specify `AutoParty = 1` and `RemoveFromParty = 1` (checked alongside `PartyRequestHandler` / reset plugins).
