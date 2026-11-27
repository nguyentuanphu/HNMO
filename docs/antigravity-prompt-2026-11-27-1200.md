# Prompt for Antigravity, 27 Nov 2026 12:00 Sydney (replaces every earlier unsent note)

Owner, 11:55: heavy work and anything that runs on the owner's PC goes to Antigravity; local Claude gets nothing.
Main cloud Claude takes the code-only items (save/ move, Vietnamese wording, socket 255 tooltip).

```
From main cloud Claude, Fri 27 Nov 12:00 Sydney. This replaces my 11:50 note (never sent). The owner's rule from now
on: everything heavy and everything that runs on the owner's PC is yours; local Claude gets no work. One heavy.ps1
job at a time. Rules as before (CLAUDE.md, server/AGENTS.md, CODING_RULES rule 9, docs/owner-decisions.md; draft
PRs only; never main; no other suite, original game or publisher names; no pack files in the repo; Sydney times).

## Merged since your 11:25 update (fetch first)
- Into the working branch claude/hnmo-main-handover-54co2m: #423 (9bcd386b, Jewel Bank + Leftovers in the DB), #430,
  #411, #422.
- Into integration/s9: #428 (af8d4669), which holds #424 and the Muun work (#427).
- Local Claude finished #433 (S9 server names, draft, waiting for CI) and read-only reviews of #431 and #432
  (posted on the PRs). Read those comments.

## Your queue, in this order
1. B.10 shared coins 2PC: finish the Postgres + six-stage SIGKILL run. Rebase feat/shared-coins-2pc on the
   working branch, since #423 is merged, and open the PR into claude/hnmo-main-handover-54co2m. Per stage: passed or
   failed, with the balances before and after.
2. #434 (desktop loader for S9 data): rebase on origin/integration/s9 (af8d4669). In the same PR, fix the 10
   [data][items] failures that are #401 data gaps, following the S9 pack:
   - 6:22..28 weaponKind;
   - the 5:40 tag;
   - 12:262..268 wing descriptions;
   - the 0:29.. and 0:41 options;
   - the vi descriptions of 12:15 and 13:38;
   - the JSON field order.
   Target: ctest 0 failed except deferred_tcp_pipe (1212). Say whether the 4 [data][buffs] cases still fail now that
   #425 is merged, and fix them if they're data.
3. Settle 12:212 @596/@590 (209/284). #431 reads reqLvl/reqEne, while local's decoder and integration/s9's catalog
   read attack min/max. Decide from the desktop client's own item record reader in client/src/source (the struct
   field order), not from either decoder. Fix the wrong side (#431's CSV or the integration/s9 catalog), and say
   which on #431.
4. #423 crash follow-up, now on the working branch: HNMO_TEST_ASSET_BARRIER=before-commit and =after-commit (with
   _FILE/_GO) for the Jewel Bank (deposit, withdraw) and Leftovers delivery. Each case: after a restart, exactly one
   receipt, and bag + bank (or the waiting reward) add up.
5. #420 and #421, which you took over from local Claude:
   - #421: rerun quest-window-buttons.spec x3 and say whether the e2e 6/7 reds are its own.
   - #420: there are no CI checks; run its desktop ctest and the move-window check.
   Post the results on each PR.
6. B.9: the shorter connection timeout only, with IdleConnectionTimeoutTests. Then #381: skill-effects.spec.ts x3.
   No GPU FPS probe (owner: not needed).
7. #386: social.spec.ts:53 x3 green.
8. PK rules of S6 and S9 (read-only, between heavy jobs). HNMO today (OutlawPoints.cs):
   - first kill: 21,600 points (Warning);
   - a kill at Warning: 43,200 = the PK1 threshold, so PK1 shows for one second, then Warning with about 12 hours
     left;
   - a kill at PK1: 86,400 (murderer), then +3,600 per kill;
   - a monster kill removes half its level.
   Read the values (never code):
   - S6: the PK keys of the common config;
   - S9: the PK system file (8 levels, LevelRange, exp deduction, PK clear).
   Per level, give: entry points/time, the threshold that keeps it, how it goes down, and the level after the 1st,
   2nd, 3rd and further kills.
   Answer plainly: after the second kill, how long does the character stay PK1 in S6 and in S9?
   Write docs/audit/references/pk-rules-s6-s9.md on #431 and post the answer on #430 (merged, the comment still
   works). No code change.
9. Area 4 (S9 items/monsters, #401/#419) after #434 lands: compare the pack with client/src/bin/Data on
   integration/s9 and list what is missing or different.
10. S9 from origin/integration/s9:
    - 618-621, Muun and Pentagram with the pack values;
    - EN+VI texts;
    - the desktop RmlUi windows (Muun, Pentagram/Errtel, S9 minimap state);
    - then the full S9 pass and the rest of docs/antigravity-client-audit-2026-11-27.md.
11. When main cloud Claude opens them (it will say so on the PRs): one real-game check on your PC of
    - fix/state-into-save: a real package update, old build → new, on real PostgreSQL; nothing lost; Jewel Bank JSON
      imported once;
    - fix/vi-wording;
    - fix/socket-255-tooltip.
    Screenshots on each PR.

Not yours (main cloud Claude does them): moving the server/ state files into save/, the 6 Vietnamese wording fixes,
the socket 255 tooltip, merging.

Report as before: per task, what ran, what was verified and what wasn't, the PR links, Sydney times.
```
