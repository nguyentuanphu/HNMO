# Unit Audit Findings — `server/src/GameLogic/MuHelper`

- **Unit**: `server/src/GameLogic/MuHelper` (7 files, 631 lines)
- **Checked on**: 2026-11-24

## Scope Covered

- `IMuHelperSettings.cs`, `MuHelper.cs`, `MuHelperConfiguration.cs`, `MuHelperFeaturePlugIn.cs`, `MuHelperStatus.cs`, `MuHelperZenCostCalculator.cs`, `PartyRequestHandler.cs`

## Findings & Bugs

1. **Free MU Helper (`amount == 0`) stops on first periodic collection tick (`MuHelper.cs:171-179`)**:
   - `TryStartAsync()` (`line 76`) allows `requiredMoney == 0` (e.g. when `CostPerStage` is empty or `StageInterval <= 0`), because `TryRemoveMoney(0)` returns `true`.
   - However, `CollectAsync()` (`line 172`) checks `if (amount > 0 && this._player.TryRemoveMoney(amount))`, causing any zero-cost MU Helper session to enter the `else` branch and terminate on the first `PayInterval` tick.
2. **Non-master party member can auto-accept into party (`PartyRequestHandler.cs:78-84`)**:
   - In `AcceptPartyRequestAsync`, if `receiver.Party != null` and `requester.Party == null`, `receiver.Party.AddAsync(requester)` is invoked without verifying `receiver` is `receiver.Party.PartyMaster` (unlike standard party invite rules).
3. **`OfflinePlayer` / Bot extensions (`IMuHelperSettings.cs`)**:
   - Defines standard client settings (`BasicSkillId`, `ActivationSkill1/2`, `HuntingRange`, `BuffSkill0..2`, `AutoHeal`, `Pick*`, `RepairItem`, `UseSelfDefense`, `AutoAcceptFriend/Guild`) plus HNMO server-side bot extensions (`AutoAcceptAnyone`, `AutoSelectBestSkill`, `AutoSelectBuffs`, `UseManaPotion`, `OnlyHuntSafeMonsters`, `PickUpgradeItems`).
4. **Configuration & S6/S9 defaults (`MuHelperConfiguration.cs`)**:
   - Defaults (`MinLevel = 80`, `VipMinLevel = 1`, `MaxLevel = 400`, `PayInterval = 5 min`, `StageInterval = 200 min`, `CostPerStage = [20, 50, 80, 100, 120]`) match classic helper level gates and per-stage cost structure.
