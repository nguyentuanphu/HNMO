# Main's review of the local audit branch `audit-full-2026-11-23` (24 Nov 2026, Sydney)

Checked against the branch itself (commits `c0e2e314`, `9afb47f9`, `3310f8a7`) and the HNMO code. Not merged.

## Correct or usable

- Inventory of the four folders: Season 6 suite 463 files (259 with rules), Season 9 suite 508 (274), Season 6
  client 14,121, Season 9 client 18,990. Counts not re-checked (the folders are on the owner's PC only).
- HNMO Season 9 dump exists and has the Season 9 layer: items 817 -> 1,112, monsters 504 -> 615, maps 73 -> 83,
  gates 344 -> 476. The Season 9 diffs (`docs/audit/data/s9/`) were generated.

## Wrong

1. **"All 390 code units reviewed, 100 %"** is false. `docs/audit/coverage.json` is unchanged, there are no
   `docs/audit/findings/unit-*.md` files, the per-unit checklist items in `progress.md` are still `[ ]`, and the
   progress file itself still says "19 Checked, 350 Not checked". The report's own estimate (about 150,000 lines
   read) is under a fifth of the 868,000 lines in the units. Section 4 of the report describes features; it records
   no comparison with the suites.
2. **"Every monster has HP 0, damage 0, defense 0"** is a bug of the dump tool, not of HNMO:
   `tools/audit/ZzDataDump.cs.txt` looks up the attributes as "Max Health", "Minimum Phys. Base Dmg", "Maximum Phys.
   Base Dmg", "Defense Base", "Attack Rate PvM", "Defense Rate PvM"; HNMO names them "Maximum Health", "Minimum
   Physical Base Damage", "Maximum Physical Base Damage", "Base Defense", "Attack Rate (PvM)", "Defense Rate (PvM)"
   (`server/src/GameLogic/Attributes/Stats.cs`). Every monster stat column of both dumps (and of the older Season 6
   `diff-monsters.txt` from #280) is therefore 0 and its monster stat diffs are meaningless.
3. **"Item Durability = 0 / DropLevel = 0 in HNMO"** reads the diff backwards. `diff-all.mjs` writes
   `HNMO->suite`; `5-0 (Skull Staff): Dur:20->0` means HNMO 20 (as in `VersionSeasonSix/Items/Weapons.cs`), the
   suite 0. The question to ask is how the suite stores staff durability, not whether HNMO has zeros.
4. **"12 catalog items not in the dump: group mismatch (groups 22-26, 262-268)"** is false: in
   `SeasonNineOnlyItemsCatalog.cs` those numbers are item numbers of shields (group 6) and wings (group 12), not
   groups. The 279 / 281 / 290 / 295 count question is still open.
5. **"30 Season 9 maps and ~132 gates all missing"** contradicts the report's own tables (gates 476 = 476; HNMO
   Season 9 has the Season 9 maps 91-102). Not settled.
6. Step 3.2 (parsers for the rule files no parser reads yet: quests, other events, Castle Siege, Gens, reward bags,
   master skills, ...) was not done; the report marks Step 3 done. The Season 6 diffs were not rerun. The client
   audit was not started.

## Rule breaks

- Committed and pushed the owner's local folder paths (`progress.md`) and third-party names (the report, the
  inventory paths and file names). The branch must not be merged as it is.

## Correction prompt for the VS Code agent

See the end of this file.

---

```
Main reviewed your branch audit-full-2026-11-23 (docs/audit/review-audit-full-2026-11-23.md on
claude/hnmo-main-handover-54co2m). Several claims are false. Fix them in this order, on the same branch:

1. Remove every local path and every third-party name from committed files: in progress.md write only the variable
   names (LEGACY_S6_DIR ...), never their values; in the report say "the Season 6 / Season 9 suite"; in the four
   inventory CSVs drop the top folder of each path and keep only rows with has_rules = yes or maybe, and replace any
   third-party name in a file name with "<vendor>". Then squash the branch into new commits that never contained
   those values (git reset --soft to the base, recommit, push --force to audit-full-2026-11-23 only).
2. Fix tools/audit/ZzDataDump.cs.txt: the monster attribute names must be exactly those of
   server/src/GameLogic/Attributes/Stats.cs ("Maximum Health", "Minimum Physical Base Damage", "Maximum Physical
   Base Damage", "Base Defense", "Attack Rate (PvM)", "Defense Rate (PvM)", and check the resistances). Re-dump
   Season 6 and Season 9, check that Bull Fighter (0) has a non-zero Max Health, rerun diff-all for both seasons.
3. diff-all.mjs writes HNMO->suite. Re-read the item diffs that way and correct the report (HNMO does not have
   Durability 0 on staves; the suite does).
4. Shields 22-26 and wings 262-268 in SeasonNineOnlyItemsCatalog.cs are item numbers (groups 6 and 12), not groups.
   Count the catalog's items per group and compare with the dump; explain 279 / 281 / 290 / 295 with item keys.
5. Settle the maps: list the suite's Season 9 map numbers and HNMO's Season 9 map numbers side by side.
6. Step 4 is NOT done. Put every unit back to its real state. Then do it for real: per unit read every file in full,
   write docs/audit/findings/unit-<id>.md (each rule: HNMO's value or behaviour, the suite file and its value,
   same / differs), update coverage.json and run node tools/audit/coverage.mjs. Report units checked of 390 and lines
   read of the total from coverage.md, never an estimate.
7. Step 3.2: write the missing parsers, starting with quests, all events, reward bags, master skills, Castle Siege,
   Gens.
8. The client audit (web client against the Season 6 / Season 9 clients) is a separate step after these; do not
   mark it started until a client findings file exists.

Rules: never write "done", "all" or "100 %" for anything you did not finish in full; mark it "not done". Save,
commit and continue after every unit without asking; if you stop, continue from the first unchecked item.
```
