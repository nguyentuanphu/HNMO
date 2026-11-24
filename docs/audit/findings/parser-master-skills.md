# Parser Findings: Master Skill Tree (`diff-master-skills.txt`)

- **Checked on**: 2026-11-24
- **Outputs**: [diff-master-skills.txt](../data/diff-master-skills.txt) (487 lines), [s9/diff-master-skills.txt](../data/s9/diff-master-skills.txt) (487 lines)
- **Sources**: the Season 6 suite's master skill tree data file (`.dat`), skill list XML, and master skill value Lua script; the Season 9 suite's master skill tree XML, skill list XML, and master skill value Lua script; and HNMO `docs/audit/data/hnmo/skills.csv`, `docs/audit/data/hnmo-s9/skills.csv`, `server/src/Persistence/Initialization/VersionSeasonSix/SkillsInitializer.cs`

## Fix Summary

`parseSuiteMasterSkillTree` previously required `>= 10` whitespace columns on the Season 6 `.dat` file (`cols.length < 10`) and used column `[9]` for `MagicNumber`. The Season 6 `.dat` file actually has **8** columns (`Index, ClassType, Tree, ReqPoint, MaxPoint, Parent1, Parent2, SkillID`), so every row was skipped (`S6 = 0`). The updated parser reads all 474 Season 6 entries and all 480 Season 9 entries (via `<Class>/<Tree>/<Skill>`), joins `Rank`, `STID`, and `ReqMLPoint` from each suite's skill list XML, evaluates value-per-level formulas (`Lv1 / Lv10 / Lv20`) from `MasterSkillPoint.lua`, and compares them against HNMO's `skills.csv` (`MasterRank`, `MasterMaxLevel`, `MasterFormula`).

## Hand-Verified Sample Values

1. **`1:300` Durability Reduction (1) (Blade Master, Tree 0)**:
   - Hand check in the Season 6 suite's master skill tree `.dat`: `1 1 0 1 20 0 0 300` -> `Class=1`, `Tree=0`, `ReqMinPoint=1`, `MaxPoint=20`, `Parent1=0`, `SkillID=300`.
   - Hand check in the Season 9 suite's master skill tree `.xml`: `<Skill Index="1" ReqMinPoint="1" MaxPoint="20" ParentSkill1="0" ParentSkill2="0" MagicNumber="300" />`.
   - Hand check in both suites' skill list XML & `MasterSkillPoint.lua`: `Rank="1"`, `STID="17"` -> `FN_LuaGetIncreaseStat(nPoint)` = `12.04 / 66.78 / 90.16` at Lv `1 / 10 / 20`.
   - HNMO `skills.csv` & `SkillsInitializer.cs:264,793`: `MasterRank=1`, `MasterMaxLevel=20`, `MasterFormula=(1 + (((((((level - 30) ^ 3) + 25000) / 499) / 6)))) * 10 / 100` (`0.12 / 0.67 / 0.90`, decimal fraction for `%`) -> **same**.
2. **`1:326` Cyclone Strengthener (Blade Master, Tree 1)**:
   - Hand check in both suites' `SkillList.xml` & `MasterSkillPoint.lua`: `STID=3` (`FN_LuaGetSkillDamageMin` -> `6.32 / 39.17 / 53.2` at Lv `1 / 10 / 20`).
   - HNMO `SkillsInitializer.cs:289,810` & `skills.csv`: uses `Formula502` (`5.02 / 32.39 / 44.08`) -> **differs** (real formula discrepancy caught by the fixed parser).
3. **Summary across all 480 suite entries**: `same = 259`, `differs = 34`, `missing_in_HNMO = 187` (entries like `314` Cast Invincibility, `315` Armor Set Bonus, `316` Vengeance, `317-320` Stat Increases, `321-324` Wing PowerUps, plus Season 9 skills not yet seeded in HNMO).
