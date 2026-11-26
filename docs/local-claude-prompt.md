# Prompt cho Claude chạy trên máy anh, lượt 2 (26 Nov 2026, 17:00 Sydney). Dán nguyên phần dưới dòng kẻ.

---

You are local Claude for HNMO (repo nguyentuanphu/HNMO). Answer the owner in Vietnamese; all times in Sydney time.
Read `CLAUDE.md`, `docs/owner-decisions.md`, then your own report `docs/local-report-2026-11-26.md` (PR #390). Rules as
before: draft PRs only into `claude/hnmo-main-handover-54co2m`; never merge, never touch `main`; one draft PR per area;
run only the tests of what you touched; say exactly what was run and what was not; heavy jobs through
`powershell -File D:\WorkSpace\meshy\tools\heavy.ps1 <command>`; no keys, local paths or third-party names in the repo.
Main (cloud) takes: reviewing and merging #385 and #384, CI runs of walk-stall, the #381 text (owner chose: Home stays
walkable, text fixed, no new rule). Do not touch those.

Your tasks, in this order (push what you have before usage runs out):

1. Season 9 `inventory.spec.ts:74` (vault keeper, fails in S9 only): find out whether the S9 data lacks the NPC "Safety
   Guardian" (146,110) in Lorencia or the path to (144,113) is blocked. Compare the S9 NPC spawn list and the S9 terrain
   (`Data_S9` pack, `Terrain1.att`) with the S6.3 ones; give file:line and, if it is a data gap, fix it in one draft PR
   with a test that fails before and passes after.
2. Economy tests (server tests only, no UI), one draft PR "economy integration tests" stacked on the merged
   #373 + #377 + #376 (they merge cleanly on the working branch head; use a fresh branch from it plus those three):
   a. pickup -> bag -> JewelBank -> withdraw and claim from the web: two pickups of one stack, stack cap, full bag,
      partial deposit when the cap is hit, partial withdraw when the bag is full; fault before and after the JewelBank
      write; `JewelBankPlugIn` withdraw with `GiftCodeItem.Durability = 0` for stackable jewels (`GiftCodeRewards.cs:140`):
      write the test that shows whether a withdrawal of 1 gives more than 1 piece.
   b. mixing / exchanging stacked materials: amounts taken across slots, remainder kept, locked materials, reward with
      room in the bag and with the pending path, fault between saving the materials and writing the reward, new login
      and retry: no reward from the failed transaction, none lost, none doubled.
   c. trade, vault and temporary storage rollbacks: `Storage.TryTakeAllAsync` returning false or throwing.
   Do not change production code in this PR; for every real bug give file:line and a proposed fix in the PR body.
3. Coins finding (`PlayerPersistence.cs:70-71`, `SeasonSharedAccounts.cs:109`): write one failing test that shows the fee
   paid and the benefit lost when the local save fails, and propose the smallest fix (advance the baseline only after
   the local save returns true, or save locally first) in a separate draft PR. Main reviews it; the owner decides.
4. Season 9 / Arena in game (needs your PC): respawn in the Arena after death; skill sweep of every class in S9;
   S9 `monster-looks` in-game cases; #381's own in-game spec and FPS / heap numbers (before and after the PR).
5. Desktop client: finish the Visual Studio Build Tools C++ install, build the C++ client and its tests; report each
   failure. For `DeferredTcpPipe` (`client/tests/network/deferred_tcp_pipe_self_check/Program.cs:137`, 3/3 red): check
   whether the same check is red in CI (`.github/workflows`), and whether the timeout is the Windows loopback; report.
6. `walk-resume.spec.ts:26` (flaky): run it 10 times on the PC and give the pass count; do not change code.

Report to the owner in Vietnamese: done / not run / next, Sydney times; list what only the owner can do.

---

# Lượt 3 (26 Nov, 17:45 Sydney): việc cần dữ liệu trong thư mục suites trên máy anh

You are local Claude for HNMO. Same rules as round 2 (draft PRs into `claude/hnmo-main-handover-54co2m`, one per area,
tests of what you touch, never name the suites or the original game in anything pushed: say "the Season 6 / Season 9
server suites"). The owner answered the audit questions on 26 Nov (`docs/owner-decisions.md`). Main cannot do these in
the cloud because the audit only keeps counts, not the data. Read the data from the suites on this PC and implement:

1. Q11 Chaos Card mixes at the Chaos Card Master (monster 375): the 20 recipes of Season 6 (14 in Season 9). Add them to
   `ChaosMixes.cs` (new databases) and an update plug-in (existing saves), with tests of each recipe's inputs, rate and
   result. Items 14:54, 14:92, 14:93, 14:95 are the cards. Keep the owner's Chaos mix rates (+12 = 55 %, +14 = 50 %).
2. Q1 master skill tree: add the missing Season 6 passives (nodes 314-324 for every class that has them, per
   `docs/audit/data/diff-master-skills.txt`: Cast Invincibility, Armor Set Bonus Increase, Vengeance, the four stat
   increases, the wing power-ups) with rank, maximum, parents and per-level values from the suite's skill list and
   master skill value script; effect in `SkillsInitializer.cs`, an update plug-in for existing saves, the nodes in the
   web and desktop tree data; tests. Do not add the 330-level Season 9 tree.
3. Q3 episode quests: import the 209 missing Ep1/Ep2 steps and sync the 146 differing Ep19 rewards
   (`docs/audit/data/diff-quests.txt` lists them) from the suite's quest scripts, with dialogs in EN and VI; update
   plug-in for existing saves; tests. Keep Class quest 4 and 5 as they are (owner: keep HNMO's rule).
4. Q6 events, one draft PR per kind, in this order: BonusEvent (scheduled x1.5 EXP hours), the two exchanges (Delgado
   Lucky Coin, Cherry Blossom), XMasAttack + Santa, LorenDeep + RingAttack, LastManStanding, MiningSystem, MuRummy.
   Every amount, chance and schedule editable in the admin panel (texts in 4 languages); use the existing plug-in
   patterns (see `docs/handover-2026-11-21.md`); tests.
Stop when usage runs low and push what you have. Report in Vietnamese: done / not run / next, Sydney times.

## Addition (26 Nov, 18:00 Sydney): two red browser specs main could not trace from the CI logs alone

5. `small-ui-feedback.spec.ts:16` (waits for `.vault-zen-dialog #panel`, never appears) and `trade.spec.ts:48` (waits for
   `.confirm-dialog .confirm-ok`, never appears) are red on the working branch in 6 of the last 7 runs. Both wait for a
   game dialog from `ui/gameDialogs.ts` `showConfirm`, which queues a new box behind an open one (`show` only starts it
   when `current` is empty). Run both on this PC, and when it fails print `confirmOpen()` and the open box's text: is
   another confirm box (onboarding, update, death, gens ...) open and hiding the test's box? Fix the test or the code
   in one draft PR with the cause in its body.

---

# Round 4 (26 Nov, 20:50 Sydney): main's answers to the local report of 17:40-20:00

You are local Claude for HNMO. Same rules as before (draft PRs into `claude/hnmo-main-handover-54co2m`, one per area,
tests of what you touch, say what was run and what was not, no third-party names, heavy jobs through heavy.ps1).

What main did with your report:
- Merged: #395 (with a cap of 10,000 remembered missing paths, from main), #397, and earlier #393, #394, #387, #384.
  #398 merges when its browser run is green.
- Fixed in #404: 4a (the stack pickup test now checks the definition's cap of 5 and fails without #373's re-check),
  4b (Jewel Bank: the bag is saved before the bank is credited), 4c (leftovers: materials saved before a waiting reward
  or coins). Main chose the anti-duplication order (a stop can lose a deposit, never double it), not the undo receipt.
- DeferredTcpPipe on Linux (cloud, .NET 10): 3 of 3 runs pass. Your 5/5 red is Windows only.
- #400 fixes walk-stall's last step on the server (the next part of a long walk gets one more tile when it lies on the
  server's own walk); walk-stall was green in #400's browser run.

Your tasks now, in this order:
1. DeferredTcpPipe on Windows: the accepted server socket sees no FIN/RST for 12 s after the client's Shutdown(Send) and
   dispose. Try `LingerState(true, 0)` vs the default, `Shutdown(Both)`, and whether Windows Defender / the loopback
   fast path holds the FIN; give the cause with file:line and fix the pipe or the self-check in one draft PR.
2. The 10 stale C++ data tests: for each, say whether the shipped data or the expectation is wrong, with the evidence
   (git log of the data file, the desktop's own source of the value). Fix the expectations where the data is right in
   one draft PR; list the ones where the data looks wrong (e.g. Buffs.json: 128 buffs load instead of 158 and buffs[6]
   has no English text: find the commit that dropped them) for main and the owner.
3. `skill-effects-cast.spec.ts`: add the elf, summoner and the other classes the spec misses (Season 6.3 and the S9
   column), run them on the PC, one draft PR.
4. #381: build a package from #381's branch and take the FPS / heap "after" numbers in town (S6 and S9), the same way
   as your "before" numbers; write both into #381.
5. `small-ui-feedback.spec.ts:16` and `trade.spec.ts:48` (still red in most CI runs): reproduce and give the cause.
Report in Vietnamese: done / not run / next, Sydney times.
