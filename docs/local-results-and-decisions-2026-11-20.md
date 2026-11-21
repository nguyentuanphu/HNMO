# Local results and the owner's decisions (20–21 Nov 2026, Sydney)

This file holds what the local session only said in chat, so that a cloud session can read it: the owner's decisions
(answered in popups), and the results measured on the owner's PC. Merged work is in the PRs and docs listed in
`docs/handover-2026-11-21.md`.

## The owner's standing rules

- **One extra agent at a time.** Run at most one extra agent (subagent) besides the session; queue the rest. Set on
  20 Nov, after three agents at once took the PC's servers down.
- **Ask with a popup.** Put decisions in a popup with the recommended option first, and answer in Vietnamese.
- **No third-party names.** The legacy server suites, their publishers and projects are not named in code, comments,
  docs, tests, commits or PRs.
- **Keep the owner's PC light.** Heavy local jobs go through `D:\WorkSpace\meshy\tools\heavy.ps1`, one at a time.
- **Code audits.** Read the code and test the real numbers. A play-and-run-tests pass alone misses number bugs like
  the zen one.

## The owner's decisions

| Topic | Decision | Where |
|---|---|---|
| Zen of a kill vs the server EXP rate | Zen does not follow the EXP rate | #147 |
| MG / DL / RF creation level (220 / 250 / 150) | Classic rule: needed | #149 |
| Event EXP rewards | Classic rule: × server EXP rate, master EXP at the maximum level | #150 |
| Rage Fighter Chain Drive / Dragon Slasher / Charge while riding | Classic rule: not allowed; the chat says why | #143 |
| Happy Hour and bonus events at the same time | Only the larger bonus applies | #158 |
| Zen from kills in Devil Square / Chaos Castle | None (classic); Blood Castle keeps its zen | #159 |
| Mini-game money reward with a nearly full inventory | Pay up to the maximum; the table shows the amount paid | #160 |
| `/pkclear` with 0 kills but still PK | Costs at least one kill | #161 |
| Repair all without an NPC (2.5× price) | Keep | — |
| Vault fee | None | — |
| Illusion Temple 5 maximum level 399 | Keep | — |
| Blood Castle losers' zen when nobody wins | Keep | — |
| Times in legacy server files | Converted between time zones chosen on the import page; bonus events have a time-zone setting (default Sydney) | importer PR (#138) |
| Imported maximum master level above 200 | Not applied (HNMO's master tree stops at 200) | #138 |
| Audit method | Read the code + test real numbers, group by group, economy first | `docs/handover-2026-11-21.md` |

## Results measured on the owner's PC

### Season 6 skill sweep

Run with `tools/local-checks/skillsweep.mjs`, one row per class.

| Class (run) | Skills | Cast | Did damage | Frame p95 | Worst frame |
|---|---|---|---|---|---|
| Dark Knight | 12 | 12 | 11 | 17 ms | 17 ms |
| Dark Wizard | 26 | 24 | 26 | 17 ms | 50 ms |
| Elf | 21 | 18 | 2* | 17 ms | 50 ms |
| Dark Lord | 13 | 10 | 1* | 17 ms | 17 ms |
| Magic Gladiator | 26 | 13 | 0* | 17 ms | 67 ms |
| Summoner | 18 | 9 | 1* | 17 ms | 17 ms |
| Rage Fighter | 13 | 7 | 0* | 17 ms | 17 ms |

\* The monster field was empty for these runs (killed by the earlier ones), so damage was not measured. Rerun them,
then run the Season 9 sweep. The effect counts of `skillsweep.mjs` are too low (it subtracts the effects already
there); count effects with `fxprobe.mjs`. The missing effects it pointed at are fixed in #140–#144.

### Importer trial on the real legacy suites

Run with `ServerImporterRealServerTrial` into a fresh Season 6 configuration (after #138 and its follow-ups).

| | Season 6 suite | Season 9 suite |
|---|---|---|
| Version detected | Season 6 | Season 9 |
| Rates | EXP 1000 / master 1000, item drop 0.5, excellent 0.002, Soul 50 + 25 luck, Life 50 | same |
| Monster drops | 9 monsters with their own drops, 30 boxes, 15 Blood/Chaos Castle rewards | same, plus 5 more reward bags left out |
| Spawns | 4,028 mapped; 3,725 already in HNMO; 247 on event maps left out; 1,483 event/invasion spawns left out | 4,217 mapped; 4,154 already in HNMO |
| Shops | 24 mapped, 0 overlapping cells | 24 mapped, 0 overlapping cells |
| Happy hours | 4 × 2 h, EXP +50 %, drops +30 % | same |
| Event starts | BC :30 every 2 h, DS even hours, CC odd hours | BC :30 every 2 h, DS 03:00 every 4 h, CC as in the file |
| Monster stats | 287 of the monsters HNMO has differ | — |

### Map check

`docs/map-check-2026-11-20.md`, with screenshots in `docs/screenshots/map-check-2026-11-20/`.

## Not uploaded, and why

- **The legacy server suites and clients** (`D:\MuOnline`). They are third-party files: read-only, and never copied
  into the repository.
- **Raw probe output** (`C:\hnmo-work\skillsweep/*/rows.json`, the map check's JSON reports, the importer trial text
  reports). The trial reports start with the suite's folder path, which carries a third-party name. Their numbers
  are summarised above.
- **Uncommitted changes in some local worktrees.** These are runtime state files (`hnmo-state.json`,
  `hnmo-worlds.json`, `save/`) and local merge-test copies whose changes are in merged PRs.
  - `HNMO-data`, `HNMO-int` and `HNMO-monsters` hold another session's uncommitted or unpushed work from 21 Nov:
    second-wing options, skill ranges and Nova AG, and a WIP monster chase marked "superseded by #235". That session
    should push or discard it.
