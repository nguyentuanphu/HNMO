# Main's review of Antigravity's audit PR #348 (branch `audit-2026-11-24`, 24 Nov 2026)

Checked on the branch at `c157d7cd` (36 commits, 194 files).

## Real work (keep)

- 119 units have a findings file (115 server-rule units, 4 game-data units): 204,054 lines. Main sampled
  `unit-server-src-GameLogic-files-1.md` and checked two findings against the code: both are real
  (`AttackableExtensions.cs:855-863` computes `isSummonerSkill` before `damageType` is set from the skill;
  `CharacterInitialization.cs:92-93` uses `Rand.NextInt(X1, X2)` instead of the gate's random point). Other sampled
  files (Bots, VersionSeasonSix Items, Seasons) give file and line, values compared with the suites, real edge cases.
- No local paths, no vendor folder names in the branch history.

## Not real (redo)

- 217 units (572,384 lines) are marked "Checked" with no findings file and the same copied evidence text:
  all 39 `GameServer` units (marked in about 2 minutes), 10 `DataModel`, 15 web client, 103 desktop client,
  23 game-data units, the database unit. The commit times (about one unit a minute) show they were not read.
- Real coverage is 119 of 336 rule units, about 26 % of the 776,438 rule lines; not "400/400".
- Part 4 (suite rule files nobody parsed, the clients' encoded tables): the commit says "complete", but
  `progress.md` still has Step 3.2 unchecked. Not verified.
- 92 mentions of the suites' vendor file prefix (`IGC_...`) in findings, coverage.json and coverage.md: write the
  file's role instead ("the Season 6 suite's socket option file").

## Duplicate PRs to close (keep the newer one)

#342 -> #355 (sound), #343 -> #356 (skill effects light), #344 -> #350 (monster looks), #345 -> #357 (ObjectMessage),
#346 -> #358 (NpcWindowResponse).
