# Prompt for Antigravity, 27 Nov 2026 10:20 Sydney (replaces the 09:30 and 10:10 prompts)

The owner moved all local work to Antigravity; local Claude gets nothing this round. Paste the block below.

```
From main cloud Claude, Fri 27 Nov 10:20 Sydney. This replaces my 09:30 and 10:10 notes (the owner never sent them).
The owner moved all of local Claude's work to you, so you now own everything below. Local Claude is idle and has
no packaged folder running; the heavy lock is yours, one heavy job at a time.

Rules as before: CLAUDE.md, server/AGENTS.md, server/docs/CODING_RULES.md (rule 9: the desktop client and its data are
the reference; never copy another suite's data or code), docs/owner-decisions.md. Draft PRs only, never merge,
never touch main. Never name other suites, the original game or its publisher. Never put the client packs or their
files in the repo. Times in Sydney time.

## What main did since your 08:25 update (fetch first; don't redo these)
- #411 and #422 are no longer DIRTY. Main merged the base into both (5b914090, 708a1cc5), keeping both sides.
  On #422 the item profile count is now 268.
- #428 (feat/s9-pentagram, c75f0a5d) holds #424 and the Muun work (#427). Alt+N opens Muun, Ctrl+P opens Pentagram,
  entering the world sends both, and the packets are regenerated. For S9 work, start from origin/feat/s9-pentagram.
  Don't merge #424 or #427 again.
- #430 merged the base (4ecd26d0).
- Main is fixing on #423 that the Jewel Bank reads old JSON from server/hnmo-state.json instead of save/. Fetch #423
  before task 5.

## Local Claude's findings you inherit (all checked by local on the owner's PC, 27 Nov 10:00)
- On #419 (integration/s9 4e96cb38, the owner's S9 pack), all good:
  - S9 -> S6 -> S9 in one tab works.
  - Cape of Lord tooltip: 150 on S9, 180 on S6.
  - M menu locked for outlaws.
  - Fresh and existing installs update without asking.
  - Shared coins agree between hnmo and hnmo_s9.
- On #423 (head 56649d63), all good: deposit, kill, restart; withdraw, kill, restart; JSON imported once.
- Blocked, now yours (task 2): the desktop [data] ctest fails 148 of 734 on integration/s9. Nothing is committed.

## Your queue, in this order
1. Your running job (task-4582: #367), then commit and push #382's z-index fix.
2. 5c, the desktop loader for S9 data on integration/s9. Decision: keep #401's data (the S9 pack has the same
   values) and extend the desktop C++ loader:
   - weapon kinds claw (0:40), sceptre (2:21), stick (5:39);
   - slot "pentagram" (12:200-214);
   - defense wider than 255 (288 on 12:212-214);
   - S9 models under Data/s9/Item/*.bmd (289 items): load from the pack when it's there; when it isn't, skip
     with a warning, never fatal;
   - ctest: cases that need the pack are skipped without it, like the [gpu] ones.
   Then record the S9 tooltip snapshot and the item/model/monster counts. Open a draft PR into integration/s9 with
   the ctest numbers before (148 of 734 failing) and after.
3. B.10 shared coins 2PC: Postgres + the six-stage SIGKILL. Open the PR into fix/asset-db-transactions (#423).
   Report each stage as passed or failed, with the exact balances before and after.
4. #420 and #421: rerun their own tests and post the results on each PR.
   - #421: its e2e groups 6 and 7 are red; rerun quest-window-buttons.spec x3 and say whether the red is its own.
   - #420: there are no CI checks; run its desktop ctest and the move-window check.
5. #423 crash follow-up: kill the server between commit and reply with HNMO_TEST_ASSET_BARRIER=before-commit and
   =after-commit (also _FILE/_GO), for the Jewel Bank (deposit, withdraw) and for Leftovers delivery. Each case:
   restart, then exactly one receipt, and bag + bank (or the waiting reward) add up.
6. B.9: only the shorter connection timeout (owner: "Rút ngắn thời gian chờ"), with its test. Drop the D3D11 GPU
   FPS probe (owner: "GPU FPS: Không cần"). On #381, run only skill-effects.spec.ts x3.
7. #386: fix tests/e2e/social.spec.ts:53 (x3 green).
8. Read-only, between heavy jobs:
   a) PK rules of S6 and S9. HNMO today (server/src/GameLogic/OutlawPoints.cs):
      - first kill: 21,600 points (Warning);
      - a kill at Warning: 43,200, exactly the PK1 threshold (>= 43,200). Points drop by 1 per second, so PK1 shows
        for one second and the character falls back to Warning, with about 12 hours left;
      - a kill at PK1: 86,400 (murderer), then +3,600 per kill;
      - a monster kill removes half its level.
      Read the rule values (never code):
      - S6: the PK keys of the common config (PKTIME, PKLimitTime, PK clear, any per-level time);
      - S9: the PK system file (8 PK levels, LevelRange, exp deduction, PK clear).
      Per PK level, give: the points or time on entering it, the threshold that keeps it, how it goes down, and
      the level after the 1st, 2nd, 3rd and further kills.
      Answer plainly: after the second kill, does the character stay PK1 for a real stretch of time in S6 and in
      S9, and for how long?
      Write docs/audit/references/pk-rules-s6-s9.md on #431 and post the answer on #430. No code change.
   b) How the S9 client marks an empty socket. Local saw five "Fire" socket lines on S9 for an item with sockets
      255, possibly only because of its made-up item. Which value means "empty" and which "no socket" in S6 and S9,
      with file and record.
9. Area 4 (S9 items/monsters, #401/#419), after your loader PR in task 2: compare the pack with
   client/src/bin/Data on integration/s9 and list what is missing or different.
10. Then the rest of the client audit (docs/antigravity-client-audit-2026-11-27.md), and steps 3-5 of your plan
   (618-621, Muun and Pentagram with the pack values, EN+VI texts, the desktop RmlUi windows, the full S9 pass),
   all from origin/feat/s9-pentagram.

Not yours: the Gens 2x rule (#386 already has it), and the "Server 1..3" names on the S9 start screen (main takes it).
Only the owner: real launches on Mac, the 15 Vietnamese texts on #425.

Report as before: per task, what was run, what was verified and what wasn't, the PR links, Sydney times.
```
