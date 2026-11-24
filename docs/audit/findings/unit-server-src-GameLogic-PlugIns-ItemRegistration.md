# Audit Unit: `server/src/GameLogic/PlugIns/ItemRegistration`

- **Status**: Checked (on commit `f9b96fc8`, 2026-11-24)
- **Files**: 3 files read in full (`ItemRegistrationConfiguration.cs`, `ItemRegistrationFeaturePlugIn.cs`, `NpcItemRegistrationRule.cs`).
- **Rule Category**: NPC item registration (Golden Archer / Rena registration).

## Findings

1. **Default rule**: `ItemRegistrationConfiguration` configures NPC `236` (Golden Archer) accepting item `14:21` (Rena), `RequiredItemsCount = 1`, `RewardZen = 5_000_000`, with optional `RewardDropItemGroup`. `NpcItemRegistrationRule.NpcNumber` defaults to `0` (`[Range(1, short.MaxValue)]`) to avoid silent duplicate rules on creation. No defects found.
