# Audit Findings: `client/web` (20 Units)

Checked on: `2026-11-24`

## Units Covered
- `Checked` (15 units):
  - `client/web/src (files 1..3)` (website, account pages, `modulusCipher.ts`, `main.ts`, `terrain.ts`, `topupPage.ts`)
  - `client/web/src/game (files 1..5)` (game logic: `inventory.ts`, `mixRecipes.ts`, `combat.ts`, `resets.ts`, `skillCast.ts`, `quests.ts`, `masterLevel.ts`, `autoPlay.ts`, etc.)
  - `client/web/src/net` (`connection.ts`, `packets.ts`, `packets.generated.ts`, `session.ts`)
  - `client/web/src/ui (files 1..5)` (HUD, quest windows, mix window, duel/event/arena/market/ranking/vault windows)
  - `client/web/bridge` (`AccountGuessGuard.cs`, `VisitorLimits.cs`, `RelayPorts.cs`, `ControlEndpoints.cs`, `Website.cs`, etc.)
- `No game rules` (5 units):
  - `client/web/src/audio`, `client/web/src/dev`, `client/web/src/render (files 1)`, `client/web/src/render/effects (files 1)`, `client/web/src/render/effects (files 2)`

## Key Rules & Parity Verified
- **Inventory & Mixes (`inventory.ts`, `mixRecipes.ts`)**:
  - 12 equipment slots (`0..11`), 8x8 bag (`12..75`), 8x15 vault (`0..119`), 8x4 trade/mix grids, and `decodeItem` matching `ItemSerializerExtended` (5-15 bytes).
  - `mixRecipes.ts` decodes `Data/Local/mix.bmd` (BUX XOR `[0xfc, 0xcf, 0xab]`, 656-byte records across 14 mix types) and matches `MixMgr.cpp` recipe/rate evaluation.
- **Combat, Skills, Quests & Master Level (`combat.ts`, `skillCast.ts`, `quests.ts`, `masterLevel.ts`, `resets.ts`)**:
  - `skillCast.ts` ports `ExecuteSkill`/`CanExecuteSkill` and per-class executors (`SkillCast.cpp`, `ClassAttack.cpp`) for base skills and Rage Fighter master variants.
  - `masterLevel.ts` decodes `MasterSkillTreeData.bmd` and `MasterSkillTooltip_eng.bmd`, enforcing `MASTER_EXPERIENCE_UNLOCK_LEVEL = 400`, `MASTER_SKILL_LEVEL_REQ_FOR_NEXT_RANK = 10`, and parent/rank prerequisites.
  - `quests.ts` maps server `ConditionType` numbers to desktop `QUEST_REQUEST_TYPE` and supports both Quest Progress (`0xF6`) and legacy class-change quests.
  - `resets.ts` consumes server-authoritative `HnmoResetsState` (`C2 F5 1E`) and dispatches `HnmoResetsAction` (`F5 1F`).
- **Cipher & Bridge Security (`modulusCipher.ts`, `bridge/*`)**:
  - `modulusCipher.ts` implements the two-stage ECB 8-cipher decryption matching `BmdModulusCipher.cpp` for Season 9+ `0x0E` models and `MAP\x01`/`ATT\x01` files.
  - `AccountGuessGuard.cs`: Locks account for 15 minutes after 10 refused passwords in a 15-minute window.
  - `VisitorLimits.cs`: Limits non-loopback addresses to 6 concurrent connections, 30 new connections/minute, 60 packets/sec (burst 200).
  - `RelayPorts.cs`: Blocks relay connections to Postgres (`5432`), configured DB port, admin ports (`adminPort`, `adminPort + 1`), and web server port.
- **Verdict**: No silent rule regressions found in `client/web`.
