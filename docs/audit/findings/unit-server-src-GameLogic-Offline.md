# Unit Audit Findings — `server/src/GameLogic/Offline`

- **Unit**: `server/src/GameLogic/Offline` (15 files, 3,717 lines)
- **Checked on**: 2026-11-24

## Scope Covered

- `BuffHandler.cs`, `CombatHandler.cs`, `HealingHandler.cs`, `ItemPickupHandler.cs`, `MovementHandler.cs`, `OfflineMapChangePlugIn.cs`, `OfflinePlayer.cs`, `OfflinePlayerManager.cs`, `OfflinePlayerMuHelper.cs`, `OfflineRespawnPlugIn.cs`, `OfflineViewPlugInContainer.cs`, `PetHandler.cs`, `RepairHandler.cs`, `SkillResourceExtensions.cs`, `ZenConsumptionHandler.cs`

## Comparison Against Legacy Baseline

- Legacy `the suite's offline leveling config file` configures offline hunt timer (`HitInterval = 1000ms`), periodic fee (`ChargeInterval = 3600s`), auto-repair, per-map restrictions, and class priority skills; HNMO adapts this onto the MU Helper settings model (`500ms` tick, `5 min` / `200 min` escalating stage cost, auto-repair at `<= 50%` durability, per-character combat/buff/loot intelligence).

## Findings & Bugs

1. **Area skills in offline/bot combat do not consume Mana, AG, or arrows (`CombatHandler.cs:729-785`)**:
   - `SelectAttackSkill()` verifies `HasEnoughResources(skill)` before picking a skill, and targeted skills delegate to `ITargetedSkillPlugin` which calls `TryConsumeForSkillAsync`.
   - However, `ExecuteAreaSkillAttackAsync` (`lines 745-785`) broadcasts animations and calls `AttackByAsync` directly without calling `TryConsumeForSkillAsync`, so offline players and bots spend zero Mana/AG/ammunition on area skills (`Evil Spirit`, `Twisting Slash`, `Multi-Shot`, etc.). (Similarly, `HealingHandler.PerformSelfHealingAsync` calls `ApplyRegenerationAsync` directly without `TryConsumeForSkillAsync`.)
2. **Conditional skill slot ignored when both `UseTimer` and `UseCondition` are set (`CombatHandler.cs:962, 979`)**:
   - `EvaluateConditionalSkill` checks `if (slot.UseTimer && !slot.UseCondition)` and `if (slot.UseCondition && !slot.UseTimer)`, so configuring both flags causes the conditional skill to never trigger.
3. **Self-healing pre-empts healing/mana potions within the same tick (`HealingHandler.cs:80-102`)**:
   - Both `AutoHeal` (`line 89`) and `UseHealPotion` (`line 96`) `return;` early, deferring mana potion consumption until a subsequent tick when HP is above threshold.
4. **Other logic reviewed (OK)**:
   - `ZenConsumptionHandler.DeductZenAsync` (`lines 52-67`) handles `amount <= 0` properly (unlike `MuHelper.cs`), and exempts bots (`Account.IsBot`).
   - `RepairHandler`, `ItemPickupHandler`, `MovementHandler`, `BuffHandler`, `PetHandler`, `OfflinePlayerManager`, and `OfflinePlayer` are cleanly structured with `RunPersistenceExclusiveAsync` serialization and safe PVP self-defense bounds.
