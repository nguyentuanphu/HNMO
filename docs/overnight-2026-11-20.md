# Overnight log, 2026-11-19/20 (Sydney)

The owner is asleep; the local Claude works from `docs/prompts/local-claude-overnight-2026-11-19.md`; main Claude
reviews and merges its draft PRs into `claude/hnmo-main-handover-54co2m`. Nothing goes to `main` overnight.

## 22:20

- PR #88 (auto-play name) had its first fully green CI: build, server tests and all six browser test shards
  (run 44506324313, on the branch at 94fb4467 plus the rename). Merged into the branch (b8e88997); its translation
  tests pass here. All four Step 0 PRs (#90, #92, #93, #88) are now in the branch.
- Still open for the local Claude: gens / social (NPC talk doesn't walk the hero over), command-window (a walk
  after the server's correction), black rectangles in Lorencia, events (Blood Castle countdown), the Season 9 run.
- Check-ins: 00:21, 03:21, 06:21.

## 22:35

- Seasons step 3 (bridge): draft PR #94 (`seasons-files`).
  - A Season 6.3 world takes only the Season 9-only maps from the Season 9 pack.
  - Season 9 object textures are also looked for in Item/.
  - 22 new tests pass, and the rules were checked live with a test pack. The real Season 9 data is still to try (local).
- Next for main Claude: Seasons step 4, the start screen with Season 6.3 and Season 9 side by side (web client).

## 23:05

- Seasons step 4: draft PR #95 (`seasons-start-screen`).
  - After logging in, the servers stand in two columns, Season 6.3 | Season 9 (one column on a phone), in EN and VI.
  - Tests: 3 unit and 2 browser tests pass; season and VIP server tests still pass (6/6).
- Item pictures: draft PR #96 (`item-icons`).
  - 223 of the 262 items with no picture now have one, drawn with the web game's renderer.
  - The other 39 have no 3D model.
  - Tool: `client/web/tools/make-item-icons.mjs`.
- To merge into the branch once CI is green: #94, #95, #96 (next check-in, 00:21).

## 23:18

Owner's decisions (23:15):
- one database per season;
- shared accounts and coins;
- the 39 item models come from the Season 9 pack (local Claude, Job 7).

Draft PRs:
- **#97, Seasons step 1:** Season 9 runs as its own program (`HNMO_SEASON=s9`), with database `hnmo_s9`, ports +100 and `save/s9/`.
  - Accounts and coins come from the shared database.
  - The launcher starts Season 9 when Host settings switches it on.
  - The web game lists both programs' servers.
  - Checked live on the package's PostgreSQL:
    - a Season 6.3 password logs in on Season 9;
    - Season 6.3 coins show in Season 9;
    - a top-up made during play arrives at the next save;
    - the browser picked a Season 9 server and played on it.
- **#98, Seasons step 2:** big Season 6.3 / Season 9 / Shared settings buttons on the admin home page, and a season badge on every page. Screenshots in `docs/screenshots/admin-seasons-*`.

CI:
- #96: one browser test shard failed in auto-play hunting (`auto-play.spec.ts`); the PR adds only pictures and a tool, so it's to be rerun.
- #94 and #95: still running.

Still open for step 1:
- "Play" from the website into Season 9 (login tickets live in the Season 6.3 program);
- filling `hnmo_s9` with Season 9's own content (the Season 9 import);
- coin grants made from Season 9's admin panel.

## 00:16

Merged into the branch: #94 (data per season), #95 (two-column start screen), #99 (local Claude's report on the
39 item models).

New draft PRs:
- **#97 (step 1), part D:** the website's "Play" works for Season 9 too (tickets as hashes in the shared database).
- **Package check (step 5, Linux):** a packaged folder with Season 9 switched on, started once:
  - both seasons came up (connect servers 44406 and 44506, admin panels 8095 and 8096);
  - the browser played a Season 6.3 server and a Season 9 server;
  - Season 9's save files are in `save/s9/`.
- **#100:** 272 Vietnamese game texts rewritten with accents and the right meaning (audit section 3).
- **#101:** 3D models for 15 items, from local Claude's candidates; each one was drawn and checked first.
- **#102:** audit 2.4 (setup wizard password) and 2.5 (host settings only on the host computer).
- **#103:** audit 1.5 (a website market purchase holds the account).
- **#104:** the events countdown shows from its first second (Job 4, taken from local Claude).

Still waiting:
- #96 (rerun of two flaky shards);
- the local Claude's Jobs 1-3, 5, 6 and 8.
