# Unit Audit: `client/src/source/UI/HUD (files 2)`

- **Unit**: `client/src/source/UI/HUD (files 2)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `27` (`5,418` lines) including `MoveCommandWindow.cpp`, `Notices.cpp`, `MiniMap.cpp`, `QuickCommandWindow.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `MoveCommandWindow.cpp` enforces client warp level/zen/PK/equipment/Gens gates)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| MG / DL / RF warp level discount | `MoveCommandWindow.cpp:282-287` | Applies `iReqLevel = int(float(iReqLevel) * 2.f / 3.f)` for MG, DL, and RF except when `iReqLevel == 400` | `docs/audit/references/warps-and-npc-dialogs-s6-s9.md` (`Swamp of Calmness = 400` un-discounted; `2/3` elsewhere) | Same |
| PK/Outlaw warp ban | `MoveCommandWindow.cpp:289` | Requires `iLevel >= iReqLevel && iZen >= iReqZen && (int)Hero->PK < PVP_MURDERER1` | Classic Murderer warp restriction (note HNMO `WarpAction.cs:22` uses `50x` Zen cost rather than hard block on custom settings) | **Differs** (desktop blocks `PK >= PVP_MURDERER1` locally; web hardcodes `outlaw: false`) |
| Icarus flight & ring warp bans | `MoveCommandWindow.cpp:296-309` | Requires `HasFlightEquipment(helper, wing)`, forbids `ITEM_HORN_OF_UNIRIA`, and checks `CheckBanMoveIcarusMap(ringRight, ringLeft)` | Classic Icarus entry rules (Cape/Wing/Dinorant/Fenrir required; Uniria/rings forbidden) | Same |
| Atlans mount ban | `MoveCommandWindow.cpp:311-320` | Blocks warp to Atlans while riding `ITEM_HORN_OF_UNIRIA` or `ITEM_HORN_OF_DINORANT` | `docs/audit/references/warps-and-npc-dialogs-s6-s9.md:36` (`Uniria (13:2) and Dinorant (13:3)` blocked from Atlans) | Same |
| Vulcanus Non-PvP & Strife Gens gate | `MoveCommandWindow.cpp:322-333` | Blocks Vulcanus on Non-PvP servers (`IsNonPvP()`), and requires `Hero->m_byGensInfluence != 0` when `_bStrife` is true | Server `WarpCommandAction` & Gens Strife map rules | Same |
| HUD notices & minimap widgets | Remaining files (`Notices.cpp`, `MiniMap.cpp`, etc.) | Screen marquee, minimap markers, command HUD buttons | UI presentation | No game rules |
