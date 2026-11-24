# Audit Findings: `server/src/Persistence/Initialization/VersionSeasonSix (files 2)`

- **Unit**: `server/src/Persistence/Initialization/VersionSeasonSix (files 2)` (2 files: `Quests.cs` 2,327L + `SkillsInitializer.cs` 1,125L; total 3,453 lines)
- **Checked on**: 2026-11-24
- **Checked at**: `a33e7979`
- **Status**: `Checked`

## Comparison against Season 6 / Season 9 Suites

Cross-checked against both suites' `ClassQuest.xml`, `QuestSystem.ini`, `Scripts/Quests/Quest_Ep*.lua`, `SkillList.xml`, `MasterSkillTree.dat`/`.xml`, and `MasterSkillPoint.lua` (see [diff-quests.txt](../data/diff-quests.txt), [diff-skills.txt](../data/diff-skills.txt), and [diff-master-skills.txt](../data/diff-master-skills.txt)).

## File-by-File Findings

- `Quests.cs:1-2327`:
  - `FindScrollOfEmperor` (`Quests.cs:293-314`): NPC `235`, `MinLevel = 150`, `ReqMoney = 1,000,000`, `14:23` Lv `0` (`MonLv 45-60`, `10/10000`), `Reward = 10` pts -> **same** as `ClassQuest.xml` (`Index="0"`).
  - `TreasuresOfMu` (`Quests.cs:267-290`): NPC `235`, `MinLevel = 150`, `ReqMoney = 2,000,000`, `14:24/25/26/68` (`MonLv 62-76`, `10/10000`), `Reward = 10` pts + `1->2` evo -> **same** requirements/rewards as `ClassQuest.xml` (`Index="1"`, where suite XML enforces level via Quest `0` prerequisite instead of `MinLevel=150`).
  - `GainHeroStatus` (`Quests.cs:226-264`) & `SecretOfTheDarkStone` (`Quests.cs:200-223`): NPC `229`, `MinLevel = 220`, `ReqMoney = 3M / 2M`, `14:23 / 14:24` Lv `1` (`MonLv 72-108`, `10/10000`) -> **same** as `ClassQuest.xml` (`Index="2","3"`).
  - `EvidenceOfStrength` (`Quests.cs:175-197`) & `InfiltrationOfBarracksOfBallgass` (`Quests.cs:96-133`): **differs** from `ClassQuest.xml` (`Index="4","5"`): Quest 4 drop rates are `3% / 4% / 5%` (`Quests.cs:188-190`) vs `1000/10000` (`10%`) in both suites, and stat-point rewards are swapped (`20` pts in Quest 4 + `10` pts in Quest 5 vs suite `10` pts in Quest 4 + `20` pts in Quest 5; total `30` preserved).
  - `IntoTheDarknessZone` (`Quests.cs:136-164`): NPC `406`, `MinLevel = 400`, `ReqMoney = 10,000,000`, Kill `1x412`, `30` pts + `2->3` evo -> **same** as `ClassQuest.xml` (`Index="6"`).
  - `CreateNewQuests` (`Quests.cs:358-2326`): `247` Shadow Phantom/Episode `CreateQuest` definitions vs `456` episode steps in `Scripts/Quests/Quest_Ep*.lua` (`same = 101`, `differs = 146`, `missing_in_HNMO = 209`; custom `QuestSystem.ini #1 "The Mighty Spider"` absent).

- `SkillsInitializer.cs:1-1125`:
  - `Initialize` (`112-260`): Regular/event skills vs both suites' `SkillList.xml` -> **same** on core ranges/mana/cooldowns except `Cometfall` (`131`: `ReqEne = 436` vs `150`), `Inferno` (`133`: `578` vs `200`), `Nova` (`160`: `180/12 = 15` mana/stage vs `180`), and `BlastStrengthener` (`316`: `Dist = 6` vs `3`).
  - `CreateSkillCombos` (`581-614`): `MaximumCompletionTime = 3s`, BK 3-step weapon/strengthener combo -> **same** as classic MU combo window.
  - `InitializeSkillAttributes` (`616-699`): Scaling formulas for `Nova`, `Earthshake`, `ElectricSpike`, `ChaoticDiseier`, `Force/FireBlast/FireBurst/ForceWave/FireScream` (`Str/25 + Ene/50`), `MultiShot` (`0.8x`), `IceArrow/Penetration/Starfall` (`2.0x`), `Selupan` (`2.0–2.5x`), `PlasmaStorm`, `DarkSide`, `DragonSlasher` (`3.0x PvM`, shield burn) -> aligns with `RegularSkillCalc.lua` / `CalcCharacter.lua` rules.
  - `InitializeMasterSkillData` (`776-979`): `200` master skills across 3 roots vs `474` S6 `.dat` / `480` S9 `.xml` entries + `MasterSkillPoint.lua` (`diff-master-skills.txt`: `same = 259`, `differs = 34`, `missing_in_HNMO = 187`). Key discrepancies: `CycloneStrengthener` (`289,810`: `Formula502` vs suite `STID=3 -> Formula632`), `SlashStrengthener`/`FallingSlashStreng`/`LungeStrengthener` (`290-292,811-813`: `Formula632` vs suite `STID=4 -> Formula883`), and missing Rank 6–9 passives (`314` Cast Invincibility, `315` Armor Set Bonus, `316` Vengeance, `317-320` Stat Increases, `321-324` Wing PowUps).
  - `CreateSpecialSummonMonsters` (`1059-1124`): `150` Bali (Lv `52`, HP `5000`, Dmg `165-170`, Def `100`) & `151` Soldier (Lv `58`, HP `4000`, Dmg `175-180`, Def `110`) -> **same** monster definitions as Elf summon table.
