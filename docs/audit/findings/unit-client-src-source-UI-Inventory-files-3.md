# Unit: client/src/source/UI/Inventory (files 3)

Checked against: classic MU / Server vault Zen fee calculation.

Scope: `client/src/source/UI/Inventory (files 3)` (`23` files, `4699` lines).

## Findings

| File & Line | Rule / Formula | Role & Value | Result |
|---|---|---|---|
| `StorageInventory.cpp:523-530` | Warehouse fee: `floor(totalLevel^2 * 0.04) + (locked ? Level * 2 : 0)`, min `1`, rounded to `100` (`>=1000`) or `10` (`>=100`) | Classic MU vault deposit fee | Same |
