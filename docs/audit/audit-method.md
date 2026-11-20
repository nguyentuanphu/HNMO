# How HNMO is audited

This is the reference for every audit of HNMO (server, web client, desktop client). Follow it instead of
starting from scratch. The goal: read every piece of rule-bearing code once, record it, and later reread
only what changed.

## Why earlier audits kept finding new bugs

The earlier audits (groups 1-13, PRs up to #209) were feature lists: "check PK", "check party". Each one read
the code that came to mind for that feature, so code nobody thought of (monster AI, client/server
agreement, rarely used plug-ins) was never read, and nothing recorded what had been read. The next audit
could not tell what was left, so it found "new" bugs in old code.

## The reference

When an HNMO rule differs from classic MU, the reference is the legacy Season 6 / Season 9 server suites
(read-only local copy). Never write their names in the repo: say "the Season 6/9 server suites". When the
suites don't settle a rule, the owner decides (popup), and the decision is written in the unit's evidence.

## The ledger

`docs/audit/coverage.json` (edited by hand) and `docs/audit/coverage.md` (generated) hold one row per
review unit. `node tools/audit/coverage.mjs` builds the units: every folder of rule-bearing code, split
until each unit has at most 6,000 lines, so one reviewer can read a unit in full in one sitting.

Each row has:
- `status`: `Not checked`, `Checked`, `Changed since check` (set by the script when a file of the unit
  changed after `checkedAt`), or `No game rules` (hosting, database, admin panel; listed so nothing is
  silently skipped).
- `checkedAt`: the commit the unit was read at. `checkedOn`: the date.
- `references`: the suite files (or owner decisions) the unit was compared with.
- `evidence`: one line per rule checked: what HNMO does, what the suites do, same / different / fixed.
  "Looks fine" is not evidence.
- `prs`: the PRs that came out of the unit.

A unit is `Checked` only when every file in it was read, not sampled.

## Order of work (most effective first)

1. **Data before logic.** Most rule differences are numbers: monster stats and ranges, item values, skill
   damage and ranges, drop tables, map gates, shop lists, EXP tables. Compare the game data
   (`server/src/Persistence/Initialization`) with the suites' tables by script, table by table, not by
   reading. One script run checks thousands of rows and can be rerun after every change.
2. **Server rules** (`server/src/GameLogic`, `server/src/GameServer`), unit by unit. For each unit: list the
   rules it implements, find the matching suite file or setting, compare, write the evidence.
3. **Client against server.** For every packet (`server/src/Network` holds the structures) check that the web
   and desktop clients apply the same rule as the server: ranges, cooldowns, prices, requirements, what is
   shown. A rule computed on both sides (shop prices, item options) must have one source or a test that
   pins both.
4. **Runtime probes last**, only for what reading can't settle (timing, AI movement, effects on screen):
   test server, scripted probes, one browser probe at a time on the shared GM accounts.

## Each finding

One bug = one draft PR into the working branch, with a test that fails before the fix and passes after
(prove red by copying the file aside, never `git stash`; touch the file's time after restoring it). A rule
that is unclear goes to the owner as a question; it is not "fixed" by a guess. The PR number goes in the
unit's `prs`.

## Next audit

1. `node tools/audit/coverage.mjs` at the new commit.
2. Reread only the rows marked `Changed since check` (the script lists their changed files) and the rows
   still `Not checked`.
3. `node tools/audit/coverage.mjs --check` passes (exit 0) when everything is checked and unchanged.
