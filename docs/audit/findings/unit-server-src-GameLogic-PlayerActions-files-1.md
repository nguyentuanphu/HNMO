# Audit Findings: `server/src/GameLogic/PlayerActions (files 1)`

- **Unit**: `server/src/GameLogic/PlayerActions (files 1)` (16 files, 1,949 lines)
- **Status**: `Checked` on 2026-11-24
- **Reference saved**: [warps-and-npc-dialogs-s6-s9.md](file:///D:/WorkSpace/HNMO-ag/docs/audit/references/warps-and-npc-dialogs-s6-s9.md)

## Files Reviewed (in full — all 16 files)
1. `CloseNpcDialogAction.cs` (1–59)
2. `EnterBalgassRefugeeAction.cs` (1–29)
3. `EnterBarracksOfBalgassAction.cs` (1–31)
4. `EnterMarketPlaceAction.cs` (1–61)
5. `EnterQuestMapAction.cs` (1–105)
6. `GameMapDefinitionExtensions.cs` (1–87)
7. `HitAction.cs` (1–102)
8. `LoginAction.cs` (1–657)
9. `LogoutAction.cs` (1–38)
10. `PingAction.cs` (1–29)
11. `TalkNpcAction.cs` (1–217)
12. `TalkNpcAction075.cs` (1–26)
13. `WarpAction.cs` (1–113)
14. `WarpGateAction.cs` (1–124)
15. `WebLoginTicketStore.cs` (1–169)
16. `WizardTeleportAction.cs` (1–98)

## Rules & Behavior Summary
- `LoginAction` / `LogoutAction` / `WebLoginTicketStore`: Handles password & single-use SHA-256 website ticket login (60s TTL), `LoginAttemptGuard`, `BanManagerService` / moderation notices, shared-account mirroring (S9), offline-session handover, per-account `LoginGate` with orphan cleanup, `MaximumPlayers` / `LicensedPlayerLimit`, `JoinRequirements` (bypassed for GMs), and template multi-login.
- `TalkNpcAction` / `TalkNpcAction075` / `CloseNpcDialogAction`: Enforces Crywolf NPC hiding, Outlaw PK2+ shop denial, dialog state transitions, vault sizing (`WarehouseSize * 2` if extended), guild master Lv/no-guild check, legacy quest dialog filtering, Duel Titus room polling, crafting `BackupInventory`, and `1000 ms` delayed Chaos Goblin save on close.
- `WarpAction` / `WarpGateAction` / `GameMapDefinitionExtensions` / `EnterMarketPlaceAction` / `EnterQuestMapAction`: Enforces `ArenaRules`, `HnmoWarpGuard`, `IWarpGateEnteringPlugIn`, effective level requirements, Atlans mount block (`13:2`, `13:3`), PK `50x` Zen cost, gate entry tolerance (`12` tiles), Barracks (`407`, 3M Zen, Quest 5/6) & Refuge (`408`, Quest 6) party warps (range `10`), and Loren Market <-> Lorencia Julia warp.
- `WizardTeleportAction`: Enforces `!IsAtSafezone`, walkable non-safezone target tile, range, blocking CC effects (`Stone`, `Stun`, `Sleep`, `Freeze2`, `Earth Binds`), and `IPlayerTeleportRestrictionPlugIn`.

## Observations & Potential Discrepancies
1. **`EnterMarketPlaceAction.cs:58-59` leaves `PlayerState.NpcDialogOpened`**: Clears `player.OpenedNpc = null` and calls `WarpToAsync` without transitioning `player.PlayerState` back to `EnteredWorld` (relies on `Player.WarpToAsync` handling `ChangingMap` from `NpcDialogOpened`).
2. **`WizardTeleportAction.cs:61-79` (`Teleport Ally`) does not check `!targetPlayer.IsAtSafezone()`**: Only checks `!player.IsAtSafezone()` on the caster and `!SafezoneMap` on the destination tile, allowing a wizard just outside a safezone to pull an ally standing inside a safezone out of safety.
3. **`EnterQuestMapAction.cs:93-103` warps party members unconditionally**: Warps any party member within range `10` without checking `HnmoWarpGuard.Refusal` (duel/trade/dead) or alive state, and does not trigger an inventory Zen view update after `player.TryRemoveMoney(this._price)`.
4. **`WarpAction.cs:80-112` has no `MaximumLevel` check**: Classic/S6 `IGC_MoveReq.xml` enforces `MaxLevel` caps on `Atlans` (`220`) and `Kanturu Ruins 1/2` (`350`), whereas HNMO `WarpInfo` only checks `LevelRequirement` (minimum level).
5. **`CloseNpcDialogAction.cs:43` blocks on `Task.Delay(1000)`**: Wait delay before `SaveProgressAsync()` on closing Chaos Goblin (`238`).
