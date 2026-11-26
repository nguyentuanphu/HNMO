# Unit Audit: `client/src/source/World/GameMaps (files 1)`

- **Unit**: `client/src/source/World/GameMaps (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `8` (`5,853` lines): `GMAida.*`, `GMBattleCastle.*`, `GMCryingWolf2nd.*`, `GMCrywolf1st.*`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Crywolf notice board rules, monster `-10%` HP & `-5%` mix rate display | `GMCrywolf1st.cpp:33-75` | `kNoticePageCount = 4` (`kNoticeFirstPage = -1`), displays guardian Elf-only contract rules, mount-contract prohibition, Balgass `5`-minute window, success reparation (`10%` monster strength decrease + `5%` mix benefit), and failure penalty (delete NPCs in Crywolf) | `crywolf-s6-s9.md`, Server `Crywolf` & Web `crywolf.ts` | Same |
| Battle Castle Fenrir shock reaction & Aida/Crywolf terrain rendering | `GMBattleCastle.cpp:133,564`, `GMAida.cpp`, `GMCryingWolf2nd.cpp` | Castle Siege / Crywolf / Aida terrain & atmospheric particles, Fenrir `PLAYER_HIGH_SHOCK` handling, Guard Stone healing beam FX | Visual presentation (`Siege` server rules audited in `CastleSiege`) | Same |
