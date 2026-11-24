# Audit Findings — `server/src/AttributeSystem`

- **Unit**: `server/src/AttributeSystem` (20 files, 1,743 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `AggregateType.cs`, `AttributeDefinition.cs`, `AttributeDisplayNames.cs`, `AttributeRelationship.cs`
  - `AttributeRelationshipElement.cs`, `AttributeRelationshipExtensions.cs`, `AttributeSystem.cs`
  - `BaseAttribute.cs`, `BaseStatAttribute.cs`, `CombinedElement.cs`, `ComposableAttribute.cs`
  - `ConstantElement.cs`, `ConstValueAttribute.cs`, `Extensions.cs`, `IAttribute.cs`
  - `IAttributeSystem.cs`, `IComposableAttribute.cs`, `IElement.cs`, `SimpleElement.cs`, `StatAttribute.cs` (plus `Readme.md`)

## Core Formula & Concurrency Design
- **Aggregation formula (`ComposableAttribute.cs:179-199`)**:
  - `rawValues = Sum(AddRaw) + Max(Maximum)` (if `elements.All(Multiplicate)`, `rawValues = 1`)
  - `multiValues = Product(Multiplicate)` (defaults to `1.0f`)
  - `finalValues = Sum(AddFinal)`
  - `Value = min(innerMax, definitionMax, (rawValues * multiValues) + finalValues)`
- **Operators (`AttributeRelationshipElement.cs:138-155`)**: supports `Multiply`, `Add`, `Exponentiate`, `ExponentiateByAttribute`, `Maximum`, `Minimum` over summed `InputElements` and `InputOperand`.
- **Thread safety (`AttributeSystem.cs:30`, `ComposableAttribute.cs:27`, `AttributeRelationshipElement.cs:23`)**: dictionary & copy-on-write element snapshots are synchronized under private locks with monotonic `_version` guards so concurrent timer-thread expirations do not latch stale cached values; `ValueChanged` notifications fire outside locks.

## Findings & Observations
1. **Formula Edge Case — `ComposableAttribute.cs:185-188`**:
   - The `rawValues = 1` fallback triggers only when `elements.All(e => e.AggregateType == AggregateType.Multiplicate)`. If an attribute receives `Multiplicate` + `AddFinal` elements with no `AddRaw` or `Maximum` element, `rawValues` remains `0`, nullifying all `Multiplicate` modifiers instead of treating base as `0` or `1`. Safe whenever `AddRaw` base elements exist, but surprising for bonus-only attributes.
2. **Threshold Difference — `StatAttribute.cs:53` vs `SimpleElement.cs:43`**:
   - `StatAttribute.Value` ignores setter deltas `<= 0.01f`, whereas `SimpleElement.Value` uses `0.00001f`.
3. **Double Clamping (`StatAttribute.cs:43-46,62` & `AttributeSystem.cs:155-158`)**:
   - `StatAttribute.Value` and `AttributeSystem.GetValueOfAttribute` both clamp to `Definition.MaximumValue`, while `BaseStatAttribute.Value` (`IElement.Value`) reads raw `_statValue`. Keeps `IncreaseStatsAction` reads capped at `MaximumValue`.
