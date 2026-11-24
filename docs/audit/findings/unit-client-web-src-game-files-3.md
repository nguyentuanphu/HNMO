# Unit: client/web/src/game (files 3)

Checked against: desktop client `GMHellas.cpp`, `ZzzInfomation.cpp`, and `ZzzInventory.cpp`.

Scope: `client/web/src/game (files 3)` (`9` files, `5120` lines).

## Findings

| File & Line | Rule / Constant | Desktop / Suite Role & Value | Result |
|---|---|---|---|
| `kalima.ts:16-85` | Maps `24..29, 36`; `KALIMA_LEVELS` `[40,131,181,231,281,331,350]` vs MG/DL/RF `[20,111,161,211,261,311,350]`; `5` Symbols of Kundun -> Lost Map; Kalima Ticket blocked in safezone/Kalima | Desktop `World/GameMaps/GMHellas.cpp:181-265` | Same |
| `itemValue.ts:213-250` | Port of `ItemValue`, `CalcRepairCost`, `COMGEM_FIRST = 10`, `MAX_SOCKETS = 5`, `MAX_EXCELLENT_OPTION_NUMBER = 6`, 32-bit int truncation & tens/hundreds rounding | Desktop `ZzzInventory.cpp` / `CComGem.cpp` | Same |
| `itemTooltip.ts:49-100` | `MAX_SOCKETS = 5`, `SOCKET_EMPTY = 0xFE`, `MAX_SOCKET_OPTION = 50`, 3rd-class display & option lines | Desktop `ZzzInventory.cpp` tooltip builder | Same |
| `instanceEvents.ts:24-78` | Illusion Temple & Doppelganger / Fortress of Empire event UI state machine | Client event packet UI logic | Same |
