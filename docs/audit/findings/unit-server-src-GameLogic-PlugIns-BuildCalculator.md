# Audit Unit: `server/src/GameLogic/PlugIns/BuildCalculator`

- **Status**: Checked (on commit `f9b96fc8`, 2026-11-24)
- **Files**: 4 files read in full (`BuildCalculatorInput.cs`, `BuildCalculatorItemInput.cs`, `BuildCalculatorResult.cs`, `BuildCalculatorService.cs`).
- **Rule Category**: Custom HNMO feature — Server-authoritative build stat calculator.

## Findings

1. **Zero formula duplication**: `BuildCalculatorService.Compute` instantiates an ephemeral `CalculatorAccount`/`CalculatorCharacter`, clamps input stats (`Level` 1..1000, `MasterLevel` 0..1000, base stats 0..100,000), validates each item via `GameMasterItemFactory.IsValid` (falling back to safe spec if invalid), and evaluates `ItemAwareAttributeSystem` + `ItemPowerUpFactory` directly. No defects found.
