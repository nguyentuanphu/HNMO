# Socket Empty Marker (`254` vs `255`) — Season 6.3 and Season 9 Desktop Packs

Audited on `2026-11-27` (`Australia/Sydney`). Sources: Season 6.3 and Season 9 desktop client data packs (external to the repo) and HNMO client/server sources.

## 1. Empty Socket vs No-Socket Byte Markers

Both the **Season 6.3** and **Season 9** client packs and protocols use the same sentinel bytes:

| Marker | Meaning | Where Defined / Checked |
|---|---|---|
| `0xFE` (`254`) | Open, **empty** socket slot (`No item application`) | Server `ItemSerializerHelper.EmptySocket = 0xFE`; Desktop `_define.h:348` (`SOCKET_EMPTY 0xFE`), `ItemMng.cpp:194, 269`, `SocketSystem.cpp:267`; Web `itemTooltip.ts:53`, `itemValue.ts:233` |
| `0xFF` (`255`) | **No socket** in this slot (unused tail slot in legacy fixed 5-byte arrays) | Server `ItemSerializerHelper.NoSocket = 0xFF`; Desktop `ItemMng.cpp:264` (`CreateItem` stops counting `SocketCount` at first `0xFF`) |

## 2. Why `[255, 255, 255, 255, 255]` Renders Five "Fire" Lines

In the legacy packet path (`ItemMng.cpp:264`), `0xFF` stops counting sockets (`SocketCount = i; break;`). However, in the **Extended item format** (`ItemMng.cpp:189-203`, `FillItemFromParameters`) and the **Web tooltip renderer** (`client/web/src/game/itemTooltip.ts:815-818, 1392-1400`):

1. `SocketCount` is transmitted explicitly (`0..5`), and only `0xFE` (`254`) is treated as an empty slot.
2. When `255` (`0xFF`) is supplied instead of `254` (`0xFE`) with `SocketCount = 5`:
   - `255 != 0xFE` (`254`)
   - `seed = 255 % 50 = 5` (`MAX_SOCKET_OPTION = 50`)
   - `sphereLevel = floor(255 / 50) + 1 = 6`
3. Index `5` of Type 0 (`SOT_SOCKET_ITEM_OPTIONS`) in **both** desktop packs has `Category = 1` (**`"Fire"`**):
   - **S6 pack** `Data/Local/Eng/socketitem_eng.bmd` (`15,600` bytes = `150` records x `104` bytes), Type 0 record `[5]`:
     - `m_iOptionID = 5`, `m_iOptionCategory = 1`, `m_szOptionName = "AG cost decrease"`, `m_bOptionType = 2` (`%`), `m_iOptionValue[0..4] = (40, 41, 42, 43, 44)`
   - **S9 pack** `Data/Local/Eng/SocketItem_eng.bmd` (`16,200` bytes = `150` records x `108` bytes), Type 0 record `[5]`:
     - `m_iOptionID = 5`, `m_iOptionCategory = 1`, `subIndex = 5`, `m_szOptionName = "AG cost decrease"`, `m_bOptionType = 2` (`%`), `m_iOptionValue[0..4] = (40, 41, 42, 43, 44)`
   - **Category and empty-slot text IDs** in both `Data/Local/Eng/Text_eng.bmd`:
     - `[2640]` = `"Fire"` (used by `GetSocketCategoryName(1)`)
     - `[2652]` = `"No item application"` (shown only when `seed == 0xFE` / `254`)
     - `[2655]` = `"Socket %d: %s"`

Because all 5 bytes equal `255`, both Web and Desktop Extended tooltips render **5 `"Fire(AG cost decrease +0%)"` lines** (with out-of-range `sphereLevel = 6`, reading index `5` past the 5-level `values[0..4]` array) instead of `"No item application"` (`254`).

## 3. Additional S9 `SocketItem_eng.bmd` Record Differences Found

Comparing S6 (`Data/Local/Eng/socketitem_eng.bmd`, `104`-byte records) vs S9 (`Data/Local/Eng/SocketItem_eng.bmd`, `108`-byte records):
- **Record size:** S9 adds a 4-byte integer sub-index field at offset `8` (`104` -> `108` bytes per record). Desktop `SocketSystem.cpp:638` reads `sizeof(SOCKET_OPTION_INFO_FILE) = 104` bytes and becomes misaligned if given the S9 pack file directly.
- **Type 0 record `[0]`** (`(Level type)Attack/Wizardry increase`): `m_iOptionValue[3]` (Lv4) is `17` in S6 vs `15` in S9.
- **Type 1 (Mix Set Bonus)** records `[6..11]`: S9 adds 6 higher-tier bonus options (`(22, 22, 10, 22, 27, 31)`); S6 has only records `[0..5]`.
