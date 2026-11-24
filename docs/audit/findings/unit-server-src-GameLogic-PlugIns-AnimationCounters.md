# Audit Findings — `server/src/GameLogic/PlugIns/AnimationCounters`

- **Unit**: `server/src/GameLogic/PlugIns/AnimationCounters` (5 files, 152 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `EvilSpiritAnimationCounterStrategy.cs`, `ISkillAnimationCounterStrategy.cs`, `MultiShotAnimationCounterStrategy.cs`
  - `SkillAnimationCounterStrategyBase.cs`, `TwisterAnimationCounterStrategy.cs`

## Rules & Behavior
- Defines skill animation-counter strategies for `SkillHitValidator`:
  - `Twister` (`8`): counts counter `0`, falls back to `lastAnimationCounter` when hit reports `0`.
  - `EvilSpirit` (`9`): counts counter `0`.
  - `MultiShot` (`235`): falls back to `lastAnimationCounter` when hit reports `0`.
- Clean strategy definitions; no defects found.
