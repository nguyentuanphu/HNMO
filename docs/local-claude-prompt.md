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
