# Unit: client/src/source/UI/MuHelper

Checked against: Web `autoPlayWindows.ts` & classic MuHelper UI bounds.

Scope: `client/src/source/UI/MuHelper` (`10` files, `2162` lines).

## Findings

| File & Line | Rule / Constant | Parity Reference | Result |
|---|---|---|---|
| `MuHelperConfigWindow.cpp:36-37,51-57`, `MuHelperDetailWindow.cpp:40` | `MaxHuntingRange = 6`, `MaxObtainingRange = 8`, mutually exclusive Timer vs Condition flags, `GaugeMaxLevel = 10` | Web `autoPlayWindows.ts:24-31,106-111` | Same |
