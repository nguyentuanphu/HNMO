# Prompt for Antigravity, 27 Nov 2026 18:45 Sydney (adds items 8-12; queue of 17:25 + item 7 of 17:50 stand)

```
From main cloud Claude, Fri 27 Nov 18:45 Sydney. Cross-check of today's audit: docs/crosscheck-2026-11-27-1830.md
(kpts7l branch). Owner decisions 18:40: skill numbers follow the SERVER SUITE when it and the desktop client differ;
Arena PK: no penalty between opponents in Arena events (already so: ArenaTeamRulesPlugIn), normal S6 PK rules outside
them. #438 is next to merge once its e2e is green. New items, after 1 (#438) and 7 (PK per season):

## 8. Season 9 integration (blocks S9)
- Merge origin/claude/hnmo-main-handover-54co2m into integration/s9 (it lacks #423 and everything merged today),
  then land #434 → #440 → #441 in order. Keep both sides on conflicts; server tests + e2e green; report on #419.
- 618-621: add the Master Tree learning path (AddMasterSkillDefinition: tree, rank, required skill/points, max
  level) per the S9 client master_tree.csv (#431 docs/audit/data/client-s9) — today SeasonNineSystemsUpdatePlugIn
  only registers plain skills. Test like 7062d551 (web S9 tree vs server master skills) and a learn-from-tree test.
- 571 max level 10 (fd21968b) is only in integration/s9: check it also reaches S6.3 (W has 20) — S6 desktop says 10;
  read the S6 suite and say what it has before changing S6.
- #441's desktop part (Muun/Pentagram windows, packet bindings) STAYS: owner 19:00 "merge everything" incl. desktop.
  #432 (codegen + 5 regenerated packet files) is merged into W (1fdeb8ec): when W goes into integration/s9, regenerate
  the 5 files from the merged packet XML and check `git status` is clean after the generator.

## 9. Skill numbers vs the S6 suite (read on your PC, then fix where the suite says so)
- 263 Dark Side and 559 Dark Side Strengthener: desktop range 5, HNMO 4. 336: desktop range 3 "Slash Strengthener",
  HNMO range 2 "Death Stab Strengthener". Read the S6 server suite's skill table: if the suite matches the desktop,
  fix HNMO (update plug-in, existing saves); if it matches HNMO, leave it. Report the suite's values per skill.
- Nova (40): server takes 15 mana per stage x 12 = 180 at full charge (desktop shows 180). Confirm in the S6 suite
  whether mana is taken per stage or up front; report, change only if the suite differs.

## 10. Fault injection, trade + Muun (S6.3 and S9 PostgreSQL fixtures, worktree only)
- Extend server/tests/asset-crash-harness: trade item + Zen (both sides), Muun convert, Muun level-up. SIGKILL at each
  stage (before write, in transaction, after commit); a NEW process reads the DB: every item exactly once (same item
  id), Zen total unchanged, one receipt per committed operation. Run on S6.3 and S9 DBs; attach the output.
- Repeat the Jewel of Bless 12→75 + disconnect + relog check on the merged W (1803cf4d) and report the DB rows.

## 11. CI evidence
- #432: run Windows x64 CTest on W 1803cf4d and on 6ade6cb7; attach the logs; per failing id say whether #408 fixes
  it (#425 is already in W, so 1586/1587/1591/1592 should pass — confirm).
- social.spec.ts:52 failed after retries on batch D (2d6a6481, run 45773437969) but is green on base: rerun it 3x on
  the working branch and say whether it's a regression (artifact 15102520607 has the report).

## 12. Chat / whisper / messenger windows resizable on the web (owner 19:00: yes, as an HNMO extra)
- Drag a corner to resize; the game remembers the size per player; default = the desktop size. Mark it as an HNMO
  extra in PARITY.md and guide.html. Reuse what old #380 did if it still fits (check its 3 missing vi strings). e2e
  test: resize, reload, size kept. Draft PR into the working branch.
```
