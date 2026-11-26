# Audit Findings: `server/src/GameLogic/ItemFormulas`

**Status**: Checked (all 2 files read in full, 188 lines)
**References**: [`item-options-380-harmony-cashshop.md`](../references/item-options-380-harmony-cashshop.md), [`client-encoded-tables-s6-s9.md`](../references/client-encoded-tables-s6-s9.md), desktop `ZzzInfomation` / `ItemTooltipValues`

## Scope Covered
- `ItemStatTermPart.cs` (46 lines)
- `ResolvedItemStatProfile.cs` (140 lines)

## Rule & Suite Comparison

| File & Line | Rule / Formula | Suite / Client Role & Value | Verdict |
|---|---|---|---|
| `ItemStatTermPart.cs:33-45` | Excellent/ancient term contribution computes left-to-right integer truncation: `(Times == 0 \|\| dropLevel <= 0 ? 0 : (Int128)value * Times / dropLevel) + Add + (DropLevelDivisor != 0 ? dropLevel / DropLevelDivisor : 0)`, clamped to `int32`. | Matches classic client item formula integer arithmetic (`dropLevel <= 0` omits scaled value term; `DropLevelDivisor == 0` omits drop-level bonus; avoids overflow via `Int128`). | Same (integer parity with client) |
| `ResolvedItemStatProfile.cs:39-49` | Resolves per-item overrides over the `Default` `ItemStatProfile`: `LevelPerItemLevel`, `ExcellentLevelAdd`, `StrengthPerOptionLevel` default `0` if unset. | Matches Season 6/9 classic level/requirement progression when configured in `Default` and per-category profiles (weapons, staffs, shields, armors, wings). | Same (configurable) |
| `ResolvedItemStatProfile.cs:58-72` | `Of(definition, gameConfiguration)` throws `InvalidOperationException` if the `Default` `ItemStatProfile` is missing; `HasDefaultProfile` exposes startup/health validation. | Ensures server never silently runs with zeroed item requirement/stat formulas. | HNMO safeguard |
| `ResolvedItemStatProfile.cs:86-89` | `GetWearableItemsWithoutProfile` flags wearable items (`ItemSlot is not null`) with `StatProfile is null` so admin-created items don't miss category formulas. | Admin diagnostic helper (armor/shield/weapon category profiles differ from `Default`). | HNMO safeguard |
| `ResolvedItemStatProfile.cs:96-139` | `GetRequirementFormula` and `GetPart` (`Excellent` / `Ancient`) fall back from `definition.StatProfile` to `Default` only when `State == ItemStatTermState.NotWritten` (`None` explicitly clears inherited terms). | Matches layered config override semantics so category profiles can explicitly zero out a default term (`ItemStatTermState.None`) without inheriting it. | Sound |

## Bugs / Open Issues
- None found in the formula evaluator or profile resolver itself; actual coefficient parity depends on the seeded `ItemStatProfile` entries (checked in `Persistence/Initialization`).
