# Audit Findings — `server/src/GameLogic/Bots (files 2)`

- **Unit**: `server/src/GameLogic/Bots (files 2)` (16 files, checked on `2026-11-24`)
- **Files reviewed**:
  - `BotNavigator.cs`, `BotPartyComposer.cs`, `BotPartyHandler.cs`, `BotPartyPolicy.cs`
  - `BotPlayer.cs`, `BotProgression.cs`, `BotPvpRules.cs`, `BotResetHandler.cs`
  - `BotRevengePlugIn.cs`, `BotSelfDefensePlugIn.cs`, `BotServerPartition.cs`, `BotShoppingHandler.cs`
  - `BotSkillHandler.cs`, `BotSkillProgressionPlugIn.cs`, `BotStarterGearEquipper.cs`, `BotStartupProfile.cs`

## Behavior Summary
- **`BotNavigator`**: Shared pathfinder pool (`TravelSearchLimit = 20000`), `Guid`-keyed cached `HasWalkableSpawnGate`, party leader following/settle (`20s`) with reverse flood-fill warp search (`FindBestReachableLegalWarp`), stuck watchdog (`8s` / `30s`), scheduled trips (shopping, Elf buff NPC, Chaos Machine, revenge march, reset after `1–10m` delay, warping to better hunting maps using `WarpImprovementMargin = 8`, `BarrenMapDuration = 3m`, `BestCandidateShare = 0.35`), and emergency potion/ammo top-ups (`EmergencyPotionCharges = 21`, `HealPotionRefillBelow = 10`).
- **`BotParty*` & `BotPvpRules`**: Enforces class-diverse party policy, PvP/self-defense cooldown safety margin (`3s`), Chaos Castle free-for-all, and outlaw (`>= PlayerKiller1stStage`) / Gens penalty exceptions.
- **`BotProgression`, `BotSkillHandler`, `BotSkillProgressionPlugIn`**: Class evolution at level `200`, stat point allocation with gear stat minimums and reset-meta vitality caps (`100–500`), item-granted skills learned via looting and `ItemConsumeAction` (`BotSkillHandler`), and direct class skills learned on level-up (`BotSkillProgressionPlugIn`), excluding Siege-only, mount-bound, and buff skills.
- **`BotShoppingHandler`, `BotStarterGearEquipper`, `BotStartupProfile`, `BotResetHandler`**: Gear repair before buying/selling (keeping `10000` Zen reserve), protected jewel/potion stock, Fresh (`Lv 1`) vs Veteran (`Lv 10–250`, `+6` starter set) startup profiles, and bot reset execution via reset plugin integration.

## Edge Cases & Observations
1. **Minor — `BotResetHandler.cs:111-114, 188-199` (stackable reset item cost)**: `TryConsumeCostsAsync` selects inventory items with `.Take(RequiredItemAmount)` (counting item slots/stacks) without inspecting `Durability` when a server configures a stackable item as reset cost.
2. **Minor — `BotSkillProgressionPlugIn.cs:172` (`ushort` cast on stat spend)**: `IncreaseStatsAction.IncreaseStatsAsync(player, stat, (ushort)amount)` truncates `amount` modulo `65536` if a high-reset configuration grants > 65,535 unspent points into a single stat at once.
3. **Note — `BotStarterGearEquipper.cs:168-181`**: `AddAmmunition()` always spawns Arrows (`group 4, number 15`); safe for default lowest-`DropLevel` bows (`Short Bow`), though would mismatch if configuration changed the lowest-`DropLevel` bow to a crossbow.
