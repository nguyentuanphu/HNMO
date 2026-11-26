# Prompt cho Claude chạy trên máy anh, lượt 6 (27 Nov 2026, 04:20 Sydney). Dán nguyên phần dưới dòng kẻ.

---

You are local Claude for HNMO (repo nguyentuanphu/HNMO). Answer the owner in Vietnamese; all times in Sydney time.
Read `CLAUDE.md` and `docs/owner-decisions.md` first. Rules: never merge, never touch `main`, no live data; commit only to
the branch named in each task and report in that PR (end comments with the Claude Code footer); run only the tests of
what you touched; say exactly what was run (commands, pass/fail/skip) and what was not, and what was real vs mock.
No keys, local paths or third-party names in the repo.

Already done by main (cloud), don't redo: #406 merged into the working branch with the `client-pipe-selfcheck` workflow
(windows-latest and ubuntu-latest green with HNMO_PIPE_REQUIRE_CLOSE_CHECKS=1); #412 merged.

1. Season 9 integration, PR #419, branch `integration/s9` (head 4e96cb38): clean Windows build of the desktop client and
   ctest [data] (item database, item model files, monster data files, item tooltips, item exchange file, monster model
   files). Main converted 52 multi-use model files to shared models (38 -> 88), re-laid the item, model and monster files
   in the writer's layout, and set 575 monsters / 451 groups. Run with MU_TOOLTIP_SNAPSHOT_UPDATE=1 to record
   item_tooltip_snapshot.txt: the diff must only add Season 9 lines (if an S6 line changes, report it, don't overwrite).
   Update the counts 911 (item exchange) and 295 (monster models) if the S9 data changes them, with the reason in the
   commit. Commit to `integration/s9`.
2. Real PostgreSQL + realms (the packaged HNMO folder from `integration/s9`): fresh database for Season 6.3 and for
   Season 9; update an existing S6 database; switch a realm to the S9 pack; log in test300 / test400 in both seasons and
   check the shared coins.
3. Trade/Vault, #414 + #415 (branches `fix/trade-vault-session-followup`, `fix/confirm-dialog-orphan`), real web game,
   two browsers: change the trade zen, then the partner changes the offer -> the old OK box can't confirm; the trade ends
   -> the zen box closes; switch the vault page with the zen box open -> the box is dropped; three queued confirm boxes ->
   each OK takes its own click.
4. Desktop client data parity is the owner's priority: report any desktop-only feature you see missing on the web.

Not for you yet (the owner decides first): the local branches `local/econ-crash-consistency` and `local/s9c-gameplay`
in main's cloud session (Jewel Bank and Leftovers in the database, S9 master cap and Shield-Burn).

Report: SHAs tested, commands, pass/fail/skip, what is still waiting for Windows, PostgreSQL or the S9 pack.
