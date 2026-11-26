# Big tasks for Antigravity (26 Nov 2026, 19:00 Sydney). Paste everything below the line.

---

You are Antigravity, working for the HNMO owner on his Windows PC. Answer him in Vietnamese; all times in Sydney time.
Read `CLAUDE.md`, `docs/owner-decisions.md`, `docs/handover-2026-11-21.md` (rule 4: one draft PR per area into
`claude/hnmo-main-handover-54co2m`, light tests, GitHub runs the suites, main merges), `docs/browser-test-reds-2026-11-26.md`
and the skill `hnmo`. Never merge, never touch `main`, never name the original game, its publisher or the server suites'
projects in anything pushed (say "the Season 6 / Season 9 server suites"); no keys or local paths in the repo; heavy jobs
through `powershell -File D:\WorkSpace\meshy\tools\heavy.ps1 <command>`. Say exactly what was run and what was not.
Local Claude may be working at the same time: check the open draft PRs first and do not take a task it already has a PR for.

Tasks, biggest value first:

1. Autoplay (owner, 24 Nov: "để antigravity làm"). PR #367 (Free / VIP / Higher VIP tiers, floating toolbar hidden) breaks
   three browser specs in its own run 45388055657: `auto-play-town.spec.ts:20` (`autoPlayExtras.onTrip` never true: the
   town trip no longer starts for test0, probably gated by the new tier), `ultimate-hud-bar.spec.ts:7`
   (`.rml-mu_helper_config #panel` never opens) and `bottom-hud-bar.spec.ts:158` (`.rml-main_frame #hud_layout`). Fix the
   code or the specs (the owner approved the town trip and other extras for players on 13 Nov; if a tier now gates them,
   ask the owner in a popup before keeping that), rebase on the working branch, push to #367's branch.
   Then the one-window plan the owner chose on 24 Nov ("Gộp 3 thành một autoplay"; "Gom vào cửa sổ 1, thêm tab"; see
   `docs/owner-decisions.md`).
2. PR #382 (HUD reference sweep) breaks the HUD: `hud.spec`, `items-use.spec` cannot find `.rml-main_frame #hud_layout` /
   `#item_slots`. Find the change that drops the main frame, fix it on #382's branch, run the HUD specs.
3. Season 9 data set (owner, 26 Nov, Q13: "Làm tất cả cùng lúc"), one draft PR per part, from the suites on this PC:
   (a) the 210 shared S9 item overrides and 281 S9-only items into the web data (`Data/Items/`, `Monsters/Season9.json`,
   35 -> 615 monsters) and the server's S9 configuration behind the world's season check; (b) the S9 events (Acheron
   Guardian, Arca Battle, Chaos Castle Survival, Tormented Square, the invasion manager, Uruk Mountain monsters 669-729);
   (c) the S9 systems (Muun pets, Pentagram, the four S9-only skills 618-621, the 330-level master tree, the S9 minimap).
   Plan in `docs/plans/season9-data-2026-11-26.md` first, then (a), (b), (c). Everything a host may tune goes in the
   admin panel (texts in 4 languages).
4. If local Claude has not opened PRs for them yet: Q11 Chaos Card mixes, Q1 master passives 314-324, Q3 episode quests,
   Q6 events (exact lists in `docs/local-claude-prompt.md`, round 3).
5. In-game checks only this PC can run: #381's own in-game spec and FPS / heap numbers before and after it; respawn in
   the Arena after death; skill sweep of every class in a Season 9 world; S9 `monster-looks` in-game cases; the C++
   desktop client build and its tests (Build Tools, CMake, Ninja and vcpkg are installed; the build stopped in the vcpkg
   step); `small-ui-feedback.spec.ts:16` and `trade.spec.ts:48` (red in most CI runs: both wait for a game dialog that
   never shows; `ui/gameDialogs.ts` `showConfirm` queues a box behind an open one).
Report to the owner in Vietnamese: done / not run / next, Sydney times; what only the owner can do.
6. A desktop workflow (added 26 Nov evening): no GitHub workflow builds or tests the C++ desktop client (#397 found it
   broken on Windows). Add `.github/workflows/desktop-client.yml`: Windows (MSVC, vcpkg, the RmlUi branch named in
   `client/.gitmodules`; the repo records no gitlinks for the client submodules, so check out that branch explicitly)
   and, if it builds there, Linux; build and run `ctest`. Paths: run on changes under `client/` only. Start it as a
   draft PR and keep the known 10 stale data tests listed as expected failures only if the owner agrees.

