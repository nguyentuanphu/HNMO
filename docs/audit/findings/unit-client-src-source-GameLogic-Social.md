# Unit Audit: `client/src/source/GameLogic/Social`

- **Unit**: `client/src/source/GameLogic/Social`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `6` (`2,189` lines): `GambleSystem.cpp`, `GambleSystem.h`, `MonkSystem.cpp`, `MonkSystem.h`, `PartyManager.cpp`, `PartyManager.h`
- **Status**: `Checked` (reclassified from `"No game rules"` — holds Rage Fighter weapon/glove/ride/ring/terrain rules, gamble index checks, and party count checks)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Rage Fighter glove dual-wield & common weapon whitelist | `MonkSystem.cpp:182-230,402-422` | Enforces RF glove pairing rules, common weapon compatibility list (`Kris`, etc.), mount skill blocks (`IsRideNotUseSkill`), and transformation ring skill blocks (`IsChangeringNotUseSkill`) | Server RF item/skill restrictions (`CharacterClass`, `ItemDefinitions`) | Same |
| Rage Fighter terrain skill cast guard | `MonkSystem.cpp:524-564` | Blocks movement-dependent RF skills (`Charge`, `Dark Side`, etc.) across `TW_NOMOVE` / `TW_NOGROUND` cells | Server `SkillHitValidator` / terrain walkability checks | Same |
| Gamble selection bounds check | `GambleSystem.cpp:12-45` | Validates selected item index `>= 0` before gamble dispatch | `unit-server-src-GameLogic-NPC.md` / Moss Gambling | Same |
| Party active state check | `PartyManager.cpp:90-123` | Validates `PartyNumber > 1` / party membership helpers | `PartyManager` server rules | Same |
