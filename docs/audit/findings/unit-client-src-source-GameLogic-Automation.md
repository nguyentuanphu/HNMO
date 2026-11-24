# Unit: client/src/source/GameLogic/Automation

Checked against: Web `autoPlay.ts` & classic MU Helper automation behavior.

Scope: `client/src/source/GameLogic/Automation` (`12` files, `1114` lines).

## Findings

| File & Line | Rule / Constant | Parity Reference | Result |
|---|---|---|---|
| `HelperPickupFilter.cpp:10-41` | `bPickAllItems` overrides; otherwise `bPickSelectItems` filters Zen, Jewels, Ancient, Excellent & keyword matches | Web `autoPlay.ts` pickup filter | Same |
