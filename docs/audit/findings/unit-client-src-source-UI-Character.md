# Unit: client/src/source/UI/Character

Checked against: Web `characterCalc.ts` & classic staff/book display logic.

Scope: `client/src/source/UI/Character` (`15` files, `3444` lines).

## Findings

| File & Line | Rule / Formula | Parity Reference | Result |
|---|---|---|---|
| `CharacterInfoWindow.cpp:1421-1524` | Reduces staff WizardryDmg% & book CurseDmg% by `CalcDurabilityPercent` when weapon durability wears down | Classic client formula & Web `characterCalc.ts` | Same |
