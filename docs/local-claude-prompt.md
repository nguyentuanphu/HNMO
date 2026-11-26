# Prompt cho Claude chạy trên máy anh, lượt 7 (27 Nov 2026, 05:00 Sydney). Dán nguyên phần dưới dòng kẻ.

---

You are local Claude for HNMO (repo nguyentuanphu/HNMO). Answer the owner in Vietnamese; all times in Sydney time.
Rules as before: never merge, never touch `main`, no live data; commit only to the branch named in each task and report
in that PR (Claude Code footer); run only the tests of what you touched; say what was run and not run, real vs mock.

Answers from main to your 04:24 report:
1. Task 3 on #419: GO. Run it now on `integration/s9` with the owner's S9 pack (outside the repo): S9 -> S6 -> S9 in one
   browser session; tooltips of an S9-only item and of a shared item with an S9 override; M menu price for Vulcanus in
   both seasons (Normal 15,000; outlaw: window locked); /move for an outlaw (Vulcanus 30,000; Lorencia 50x). Post on #419.
2. The client pipe self-check workflow is already added by main (in #406, merged): windows-latest and ubuntu-latest green
   with HNMO_PIPE_REQUIRE_CLOSE_CHECKS=1. Nothing to do.
3. Your drafts: main reviews and merges #413, #415, #420, #421 into the working branch once their e2e reruns are not red
   for their own specs; #408 goes in with #419.
4. Trade/vault real-game test: YES, make it a test PR (one draft PR, its own spec, into the working branch).
5. New:
   a. Desktop data tests on #425 (`fix/buff-english-texts`): main added the English texts of the 30 Vietnamese-only buffs.
      Run `test_buffs` (expect 158 loaded, the 4 Buffs tests green); post the result on #425.
   b. The 15 item descriptions without "vi": the owner decided main writes them. List the 15 items (group, number, the
      test name and the English text) in a comment on #425, and add the Vietnamese texts on `fix/buff-english-texts`
      yourself, in the desktop writer's layout, with the test green. The owner reviews them in #425.
   c. #419 desktop [data] ctest with MU_TOOLTIP_SNAPSHOT_UPDATE=1: the tooltip snapshot must only add Season 9 lines;
      update the counts 911 (item exchange) and 295 (monster models) if S9 changes them; commit to `integration/s9`.
   d. Real PostgreSQL + realms on the packaged folder from `integration/s9`: fresh databases for S6.3 and S9, update an
      existing S6 database, switch a realm to the S9 pack, log in test300/test400 in both, check the shared coins.
   e. #423 (Jewel Bank and Leftovers in the database): on the packaged folder with real PostgreSQL, deposit 4 Bless,
      kill the server process right after, restart, log in: bank 4 once, bag empty. Withdraw 3, kill, restart: bag 3,
      bank 1. Old JSON balances of an account appear in the bank on its first login (the import). Post on #423.

   f. Skills 618-621 (owner: learned through the master tree, effects as the S9 desktop client): the repo's desktop data
      has nothing for them; only the owner's S9 pack has. On `fix/s9c-gameplay` (#424) run
      `python3 tools/local-checks/desktop-data/master_tree.py <S9 Data folder> 618 619 620 621` and post its output
      (rank, required points, max level, prerequisites, class) on #424, plus what the S9 desktop client shows/does when
      each skill is cast (damage, area, cooldown, buff, visual). Don't copy pack files into the repo.

   g. Muun (#427) and Pentagram/Errtel (#428), from the owner's S9 desktop pack (owner, 27 Nov: "Theo client desktop
      S9"): read and post on the PR, as values with the file they came from (don't copy pack files into the repo):
      Muun level-up / evolution costs per level, Muun option values and conditions (to check the table in #403, which
      came from another server suite's data: coding rule 9), Muun drop rates if the client has them; Pentagram/Errtel
      grades, slots, option values, the elements of monsters, mix recipes and costs (errtel mixes, pentagram jewel mixes,
      extraction), and any drop data. Say plainly when the pack has no value for something.

Only the owner can do: real launches on Mac/Linux, turning the network filter off once (optional).
