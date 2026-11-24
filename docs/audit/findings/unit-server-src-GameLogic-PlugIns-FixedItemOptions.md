# Audit Findings — `server/src/GameLogic/PlugIns/FixedItemOptions`

- **Unit**: `server/src/GameLogic/PlugIns/FixedItemOptions` (4 files, 429 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `FixedItemOptionRule.cs`, `FixedItemOptionsConfiguration.cs`, `FixedItemOptionsEngine.cs`, `FixedItemOptionsPlugIn.cs`

## Rules & Comparison
- **Custom HNMO feature** (`IDisabledByDefault`): pins upgrade `MinLevel` (capped at `def.MaximumItemLevel`), `PinSkill`, `PinLuck`, `MinOptionLevel` (clamped `1..7` for `+4..+28`, matching S6/S9 `Is28Option = 1`), `ForcedExcellentBits` bitmask (`1..63`), and `MinExcellentCount` (`1..6`) on matching drops (`"drop"`) or boxes/rewards (`"box_event"`), optionally scoped by map number.
- Clean pure rule engine; no defects found.
