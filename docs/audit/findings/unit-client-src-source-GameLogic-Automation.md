# Unit Audit: `client/src/source/GameLogic/Automation`

- **Unit**: `client/src/source/GameLogic/Automation`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `12` (`1,114` lines) including `HelperPickupFilter.cpp`, `AttackSkillRotation.cpp`, `Attack.cpp`, `Movement.cpp`, `Pickup.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| MUHelper pickup filter priority & keyword match | `HelperPickupFilter.cpp:10-41` | If `bPickAllItems` returns `true` (preserves legacy saves with both bits); if `!bPickSelectItems` returns `false`; matches Zen, Jewel, Ancient, Excellent, or extra item substring against `GetItemDisplayName` | Web `autoPlay.ts` & `MuHelperConfiguration` | Same |
| MUHelper skill rotation & cooldown checks | `AttackSkillRotation.cpp:21+`, `Attack.cpp` | Evaluates `SkillAttribute[skill].Delay > 0`, conditional activation (`ON_TIMER` vs `ON_CONDITION`), nearby/attacking mob count thresholds, and Elf/DK/DW support buffs | Web `autoPlay.ts` & `MUHelper` (`MuHelperData.cpp`) | Same |
| Return-to-origin movement & loot distance | `Movement.cpp`, `Pickup.cpp` | Bounds movement inside `iHuntingRange` / `iObtainingRange` and returns to origin after `iMaxSecondsAway` | Web `autoPlay.ts` | Same |
