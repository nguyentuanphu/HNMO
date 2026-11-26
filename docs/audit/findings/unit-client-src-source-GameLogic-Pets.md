# Unit Audit: `client/src/source/GameLogic/Pets`

- **Unit**: `client/src/source/GameLogic/Pets`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `23` (`4,608` lines) including `GIPetManager.cpp`, `CSPetSystem.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — holds Dark Horse & Dark Spirit stat/level/EXP/req/price formulas and Chaos Castle restrictions)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Pet next-level EXP curve | `GIPetManager.cpp:559-578` | `Level = m_wLevel + 1`; `m_dwExp2 = (10 + Level) * Level^3 * 100` for both Dark Spirit & Dark Horse | `docs/audit/references/socket-options-and-pets-s6-s9.md:53` & `PetLevelHelper.cs` | Same |
| Dark Spirit combat formulas | `GIPetManager.cpp:563-569` | `DmgMin = 180 + lv*15 + Cmd/8`, `DmgMax = 200 + lv*15 + Cmd/4`, `AtkSpeed = 20 + (lv*4)/5 + Cmd/50`, `AtkSuccess = 1000 + lv + lv*15` | Server `RavenAttributeSystem` (`unit-server-src-GameLogic-Pet.md`) | Same |
| Dark Horse combat & defense formulas | `GIPetManager.cpp:571-589` | `DmgMin = Str/10 + Cmd/10 + lv*5`, `DmgMax = DmgMin + DmgMin/2`, `AtkSpeed = 20 + (lv*4)/5 + Cmd/50`, `AtkSuccess = 1000 + lv + lv*15`, `Defense = 5 + Agi/20 + lv*2` | `docs/audit/references/socket-options-and-pets-s6-s9.md:53` (`5 + 2*lvl + Agi/20` def) | Same |
| Pet NPC sell value & equip requirements | `GIPetManager.cpp:591-612,714-761` | Dark Horse value `lv * 2,000,000`, `ReqLv = 218 + lv*2`; Dark Spirit value `lv * 1,000,000`, `ReqCmd = 185 + lv*15`; Dark Lord class restriction (`:765`) | `docs/audit/references/socket-options-and-pets-s6-s9.md:53` (`ReqLevel 218`, `ReqCmd 185`) | Same |
| Dark Spirit commands & Chaos Castle block | `GIPetManager.cpp:204-207,258-281` | Shift+1..4 command hotkeys for Dark Lord; suppresses Dark Spirit actions/display inside Chaos Castle | `MiniGames/ChaosCastleContext` & server Pet commands | Same |
| Pet stun / cloak / duel disable handling | `CSPetSystem.cpp:290-299,352-417` | Stops pet attack on stun/cloak and clears active state on duel reset | Server pet AI & Duel state handling | Same |
