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
