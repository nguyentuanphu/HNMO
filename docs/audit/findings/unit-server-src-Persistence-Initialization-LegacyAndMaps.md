# Audit Findings: `server/src/Persistence/Initialization` (Maps, TestAccounts & Legacy Versions)

- **Units**:
  - `server/src/Persistence/Initialization/VersionSeasonSix/Maps (files 1)`, `(files 2)`, `(files 3)`
  - `server/src/Persistence/Initialization/VersionSeasonSix/TestAccounts`
  - `server/src/Persistence/Initialization/Version075 (files 1)`, `Version075/Items`, `Version075/Maps`, `Version075/TestAccounts`
  - `server/src/Persistence/Initialization/Version095d`, `Version097d`
- **Checked on**: 2026-11-24
- **Status**: `Checked`

## Summary

Audited the Season 6 map initializers, test account seeders, and legacy 0.75 / 0.95d / 0.97d version initializers.

## Key Observations

- **Season 6 Maps (`VersionSeasonSix/Maps`)**: Define per-map NPC and monster spawn areas, safezone/terrain prefixes, and event map configurations.
- **Test Accounts (`VersionSeasonSix/TestAccounts`)**: Seed development/test accounts (`test0`..`test9`, `Level300`, `Level400`, `Ancient`, `Socket`, `Quest150/220/400`, `GameMaster`, `GameMaster2`, `Unlocked`) with BCrypt-hashed credentials.
- **Legacy Versions (`Version075`, `Version095d`, `Version097d`)**: Provide historical client definitions (0.75, 0.95d, 0.97d), base maps, items, skills, and Chaos mixes used as inheritance bases and legacy initializers.
