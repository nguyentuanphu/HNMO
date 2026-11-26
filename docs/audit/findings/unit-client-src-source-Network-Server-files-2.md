# Unit Audit: `client/src/source/Network/Server (files 2)`

- **Unit**: `client/src/source/Network/Server (files 2)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `1` (`15,727` lines): `WSclient.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `WSclient.cpp` implements the client-side normal EXP and Master EXP curves as well as incoming state clamping)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Normal character EXP lower-bound formula | `WSclient.cpp:212-229` | `prior = level - 1`: `(9 + prior) * prior^2 * 10`, plus if `prior > 255`: `(9 + (prior - 255)) * (prior - 255)^2 * 1000` | `Server` `ExperienceTable` (`common-server-and-character-rules-s6-s9.md` standard MU EXP curve) | Same |
| Master Level EXP lower-bound formula | `WSclient.cpp:231-318` | `totalLevel = masterLevel + 400`, `overLevel = totalLevel - 255`; computes `(((9 + totalLevel)*totalLevel^2 * 10 + (9 + overLevel)*overLevel^2 * 1000) - 3,892,250,000) / 2` with saturating 64-bit arithmetic (compatible up through S9 master cap 330+) | `MasterLevelExperienceTable` server curve (`(f(400+m) - f(400)) / 2`) | Same |
| EXP/state interval clamping helpers | `WSclient.cpp:320-390` | Clamps displayed EXP/HP bounds inside `[lower, upper]` and saturates additions cleanly without overflow | Display safety helper | Same |
| Packet handlers & decoders | Remaining `WSclient.cpp` routines | S->C packet parsing dispatching to UI windows, character buffers, inventory, and scene transitions | Protocol transport layer | No game rules |
