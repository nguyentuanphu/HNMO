# Unit: client/src/source/Data/GameData/ItemData (files 3)

Checked against: Server `DataModel/Configuration/Items/ItemStatProfile`.

Scope: `client/src/source/Data/GameData/ItemData (files 3)` (`32` files, `4389` lines).

## Findings

| File & Line | Rule / Constant | Parity Reference | Result |
|---|---|---|---|
| `ItemStatsJson.cpp:42-81,526-704` | Reads/writes item stat formula fields (`add, levelWeight, percent, basis`) matching server `ItemStatProfile` schema | Server `ItemStatProfile.cs` | Same |
