# Unit: server/src/GameLogic/Views (files 1)

Checked against: interface/type scan of all `server/src/GameLogic/Views` files (`260` files total: `257` view interfaces/enums + `3` helper/enum files).

Scope: `server/src/GameLogic/Views (files 1)` (`40` files, `834` lines; sample: `ChatMessageType.cs, IChatCommandListViewPlugIn.cs, IChatViewPlugIn.cs, IConsumeSpecialItemPlugIn.cs`).

## Summary

- `No game rules`: contains `ViewExtensions.cs:12-60` (`ConstantPlayerId = 0x0200` dynamic map ID helper) + view interfaces. Packet formatting/UI notification contracts only; no gameplay thresholds, formulas, or rates.
