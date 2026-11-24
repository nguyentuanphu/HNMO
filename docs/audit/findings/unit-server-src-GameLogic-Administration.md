# Unit Audit: `server/src/GameLogic/Administration`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (8 files read in full)

## Summary

- `GameServerAdministration` & `GameServerAdministration.Plugins`: live player snapshot/paging, `WithPlayerAsync` (serialized via `RunPersistenceExclusiveAsync` with ID/name recheck), `PluginGates` with `PreviewAsync` -> `JsonNode.DeepEquals` -> `ExecuteAsync`, 16 KiB input/output caps, depth-16 JSON parsing.
- `GameMasterActions`, `GameMasterEventActions`, `GameMasterItemFactory`: shared GM actions for level, teleport, item creation, buffs (`10..86400s`), and mini-game start/restart.
- `CashShopCoinGrantsAdminOperationPlugIn` (`cash_shop_apply_grants`) & `HnmoSkillsAdminOperationPlugIn` (`hnmo_skills_set`, skills 700..727).
- No defects found.
