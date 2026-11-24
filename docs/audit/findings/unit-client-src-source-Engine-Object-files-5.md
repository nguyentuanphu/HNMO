# Unit: client/src/source/Engine/Object (files 5)

Checked against: Web `itemValue.ts` & classic MU repair/tax pricing formulas.

Scope: `client/src/source/Engine/Object (files 5)` (`2` files, `4231` lines).

## Findings

| File & Line | Rule / Formula | Parity Reference | Result |
|---|---|---|---|
| `ZzzInventory.cpp:832-866` | Applies NPC tax rate & Chaos tax rate (`Gold += Gold * TaxRate / 100`) | Castle Siege NPC/Goblin tax display | Same |
| `ZzzInventory.cpp:868-904` | `CalcRepairCost`: caps ItemValue at `400,000,000`, base `3.5 * ItemValue^(3/4) * lostRatio + 1`, broken `x2.0` (Dark Lord pet) / `x1.4` (other), self-repair `x2.5` (`+150%`) | Season 6/9 repair cost formula & Web `itemValue.ts` | Same |
