# Audit Findings: `server/src/GameLogic/ItemDefinitions`

**Status**: Checked (all 3 files read in full, 895 lines)
**References**: Admin panel item sync & client `Data/Items/*.json` / `Data/Effects/GlowColors.json`, [`ledger-combat-items-events-2026-11-22.md`](ledger-combat-items-events-2026-11-22.md)

## Scope Covered
- `ItemClientMetadata.cs` (141 lines)
- `ItemDefinitionStateStore.cs` (286 lines)
- `ItemClientDataSync.cs` (465 lines)

## Rule & Design Comparison

| File & Line | Rule / Behavior | Suite / Client Reference | Verdict |
|---|---|---|---|
| `ItemClientMetadata.cs:37-140` | Represents editable metadata (`Group 0-15`, `Number`, `NameEn`/`NameVi`, `Width`/`Height` default `1`, `DropLevel`, `MaximumItemLevel` default `15`, `Durability` default `20`, `BuyPrice`, `DropsFromMonsters`, 6 stat requirements, `GlowColor`, EN/VI tooltips, `ModelSourceGroup`/`ModelSourceNumber`). | Custom HNMO admin-to-client synchronization model (classic suites rely on offline BMD/`Item.txt` editing). | HNMO custom |
| `ItemClientDataSync.cs:48-111,282` | `NormalizeGlowColor` accepts 45 built-in `GlowColors.json` names (case-insensitive) or 6-digit `#rrggbb` hex (`ToGlowColorKey` -> `custom_<hex>`); rejects invalid strings to empty string. | Keeps web/desktop client `GlowColors.json` key space valid and case-normalized. | Sound |
| `ItemClientDataSync.cs:118-151` | `TryResolveDataRoot` checks `configuredDataPath`, then walks up 8 parents from `AppContext.BaseDirectory` and 6 from CWD for `Items/Group00_Sword.json`. | Deterministic loose-data discovery for admin panel export. | Sound |
| `ItemClientDataSync.cs:159-254,299-425` | `BuildPatchedFiles` updates `Effects/GlowColors.json` RGB triplets, `Items/GroupNN_*.json` entries (`name`, `width`, `height`, `level`, `maximumItemLevel`, `durability`, `buyPrice`, `requirements`, `tooltip`), and `Items/Models/GroupNN_*.json` (cloning model entry from `ModelSourceGroup`/`ModelSourceNumber` when missing, applying `glow.color`). | Note (`ItemClientDataSync.cs:345`): uses key `"dexterity"` for `AgilityRequirement`, matching client web JSON schema. | HNMO custom |
| `ItemDefinitionStateStore.cs:66-117` | Persists overrides in `HnmoStateStore` (`section "itemDefinitionsMeta"`, sorted by `Group, Number`) under static `Gate` lock, normalizing `GlowColor` on `Upsert`. | Thread-safe admin persistence (`temp` fallback store for isolated tests). | Sound |
| `ItemDefinitionStateStore.cs:125-196` | `FromDefinition` merges live `ItemDefinition` fields and saved overrides (`existing` wins on strings > whitespace, numeric properties, and positive requirements). | Preserves admin edits across restarts while reading neutral/VI translations. | Sound |
| `ItemDefinitionStateStore.cs:205-255` | `ApplyToDefinition` clamps `Width` & `Height` to `[1..8]`, `MaximumItemLevel` to `[0..15]`, `Value = Math.Max(0, BuyPrice)`, updates EN/VI translations, prefers `*RequirementValue` attribute over primary stat (`ResolveAttribute`), and deletes requirement rows when set to `0`. | Matches HNMO dynamic requirement system and inventory grid (`8` columns max width). | Sound |

## Bugs / Open Issues
- Minor asymmetry (`ItemClientDataSync.cs:337-340`): `PatchItemJson` only writes `entry["buyPrice"]` when `meta.BuyPrice > 0`, leaving any previous non-zero `buyPrice` intact if overridden back to `0` (unlike `tooltip` at `:375` or `requirements` at `:355` which remove cleared keys). Low severity since buyable shop items rarely revert to `0`.
