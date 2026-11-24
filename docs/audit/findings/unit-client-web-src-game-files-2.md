# Unit: client/web/src/game (files 2)

Checked against: desktop client `GMCrywolf1st.cpp` & `ZzzInterface.cpp`.

Scope: `client/web/src/game (files 2)` (`13` files, `5968` lines: `combat*.ts`, `crywolf.ts`, `duel.ts`, `events.ts`, `gates.ts`, `gens.ts`, `guild.ts`, etc.).

## Findings

| File & Line | Rule / Constant | Desktop / Suite Role & Value | Result |
|---|---|---|---|
| `crywolf.ts:19-61` | Map `34`, `>350` Elf contract threshold, `10%` monster strength decrease, `5%` castle/arena mix rate decrease | Desktop `World/GameMaps/GMCrywolf1st.cpp:48-75` | Same |
| `gates.ts:16-66` | `RESEND_MS = 3000`; MG/DL/RF `2/3` gate level, gate `28` level-only, Atlans blocks Uniria/Dinorant, Icarus requires wings/Dinorant/Fenrir | Desktop `CheckGate` rule parity | Same |
| `events.ts:24-27` | BC maps `11..17, 52`, CC maps `18..23, 53`, DS maps `9, 32`, Market `79` | Desktop `CMapManager` event map ranges | Same |
| `combat.ts:28-30`, `duel.ts:276` | Hit shock bit `0x8000`, `ID_MASK = 0x7FFF`, server duel HP/SD `0..100` scaling | Client packet protocol parity | Same |
