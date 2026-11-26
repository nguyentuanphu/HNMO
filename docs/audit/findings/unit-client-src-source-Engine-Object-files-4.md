# Unit Audit: `client/src/source/Engine/Object (files 4)`

- **Unit**: `client/src/source/Engine/Object (files 4)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `2` (`4,368` lines): `ZzzInterface.cpp` (`4,239L`), `ZzzInterface.h` (`129L`)
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Elf arrow / bolt durability check & auto-reload | `ZzzInterface.cpp:1150-1225` | Crossbow (right hand) checks `Left == ITEM_BOLT && Durability > 0`; Bow (left hand) checks `Right == ITEM_ARROWS && Durability > 0`; auto-equips matching ammo stack from inventory when empty (`SearchArrow`), else warns `NoMoreArrows` | `Server` Elf ammunition rules & `CharacterManager.cpp:326-342` | Same |
| Animation rotation quantization (`8`-way) | `ZzzInterface.cpp:1227-1231` | Quantizes angle into `8` compass directions: `((Angle[2] + 22.5f) / 360.f * 8.f + 1.f) % 8` | Web `fieldEventWindows.ts:237` | Same |
| Skill targeting, gate checks & action dispatch | `ZzzInterface.cpp:191,445,467,3545,3929-3936` | Gate collision (`CheckGate`), NPC talk distance checks, Elf auto-attack / target interaction rules | Web `gates.ts` & `skillCast.ts` | Same |
