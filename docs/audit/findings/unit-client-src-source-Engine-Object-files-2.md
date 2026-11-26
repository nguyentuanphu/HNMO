# Unit Audit: `client/src/source/Engine/Object (files 2)`

- **Unit**: `client/src/source/Engine/Object (files 2)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `1` (`14,284` lines): `ZzzCharacter.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `ZzzCharacter.cpp` encodes client sprint/swim unlock conditions, walk animation speeds by class, and attack-speed scaling)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Sprint / fast-run unlock conditions | `ZzzCharacter.cpp:509-529` | Outside SafeZone: MG/DL/RF, `+5+` Gloves in Atlans/Kalima(Hellas)/Doppelganger3, `+5+` Boots elsewhere, Fenrir mount, or Cursed Temple players increment `c->Run` up to `40` to unlock running animation | Classic client run condition (`Boots +5` / `Gloves +5` in swim maps) | Same |
| Walk cycle base speed by class | `ZzzCharacter.cpp:532-539` | Rage Fighter walk speed `0.32f`; all other classes `0.33f` | Classic client per-class walk timing | Same |
| Attack speed to animation factor | `ZzzCharacter.cpp:843` | Scales weapon/skill cast playback via `CharacterAttribute->AttackSpeed * 0.004f` | Server/client attack speed scaling (`AttackSpeed * 0.004f` threshold tiers) | Same |
| Rendering, bone transforms & visual attachments | Remaining `ZzzCharacter.cpp` routines | Character mesh blending, cape cloth physics, glow shaders, sound triggers | Client visual rendering | No game rules |
