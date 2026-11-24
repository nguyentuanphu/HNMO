# Unit Audit: `client/src/source/Engine/Object (files 6)`

- **Unit**: `client/src/source/Engine/Object (files 6)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `1` (`8,311` lines): `ZzzObject.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `ZzzObject.cpp` checks character class & level thresholds for Devias object visibility and Archangel glow overrides)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Devias gate/castle object level gate (`o->Type == 100`) | `ZzzObject.cpp:4300-4310,4591-4602` | On `WD_2DEVIAS`, checks level threshold `50` (`50 * 2 / 3 = 33` for MG/DL/RF) at `:4303-4310` and `80` (`80 * 2 / 3 = 53` for MG/DL/RF) at `:4595-4602` before rendering castle object pass | Classic Devias warp/castle level gate rule with `2/3` MG/DL/RF discount | Same |
| Weapon of Archangel level glow override | `ZzzObject.cpp:6160` | Suppresses upgrade glow level on `ITEM_WEAPON_OF_ARCHANGEL` (returns `0`) | Blood Castle Archangel quest weapon special rendering rule | Same |
| Scene object loops, culling & mesh draws | Remaining `ZzzObject.cpp` routines | 3D world object draw batching, bounding-box/frustum culling, Doppelganger 2 static prop filtering | Client 3D rendering | No game rules |
