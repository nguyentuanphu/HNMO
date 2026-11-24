# Audit Findings — `server/src/GameLogic/PlugIns/BufferNpc`

- **Unit**: `server/src/GameLogic/PlugIns/BufferNpc` (6 files, 421 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `BufferNpcBuffPreset.cs`, `BufferNpcConfiguration.cs`, `BufferNpcDecision.cs`
  - `BufferNpcDenialReason.cs`, `BufferNpcEngine.cs`, `BufferNpcPlugIn.cs`

## Rules & Cross-Check
| Rule | HNMO Implementation | Season 6 / 9 Suite Reference | Status |
| --- | --- | --- | --- |
| Elf Soldier (257) max level | `BufferNpcConfiguration.cs:33`: `MaxLevel = 220` (`MaxResets = -1`, `DurationSeconds = 1800`, `ZenCost = 0`) | `CommonServer.cfg`: `ShadowPhantomMaxLevel = 220` | Same (`220`) |
| Applied buffs | `BuffEffectNumbers = [3, 1, 2]` (Elf Soldier + Greater Damage + Greater Defense) | Vanilla Elf Soldier grants Attack/Defense Soldier Buff (effect `3`) | Differs (adds Greater Damage/Defense by default when plugin is enabled) |

## Observations
- `IDisabledByDefault`. Clean level/reset/Zen/power-up checks; no defects found.
