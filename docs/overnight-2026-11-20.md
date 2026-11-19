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

## 00:57

Main Claude's new draft PRs, each tested:
- #105: coin grants not saved are never counted as given (audit 1.10).
- #106: spouse warp and Teleport NPC checks (audit 1.10).
- #107: marriage proposal spam, and a deleted character's marriage (audit 1.10).
- #108: top-up milestone goods are delivered under the player's lock (audit 1.10).
- #109: the relay reaches game servers only; no coin balances without a login (audit 2.8).
- #111: the light effect test waits for the light (it failed CI on the slowest shard).
- #113: per-account password guessing guard on the website (audit 2.8).

Local Claude's PRs, reviewed:
- #110 (Job 1, movement and NPC talk): code reviewed. Together with #104, events.spec passed 3/3, where it failed
  every other run before. The movement tests passed 6/6 (spawn-while-loading, command-window, quests, gens).
- #112 (Job 2, Lorencia's black slabs: objects the desktop never draws): the list matches the desktop's
  CreateObject.

Both merge once their CI is green.

## 03:00-04:15

Main Claude's new work, each tested:
- **#123, audit 1.8 (multi-vault).** Each vault is now a real item storage in the database. A switch is one save,
  and items keep every detail. Checked in the web game on the packaged PostgreSQL:
  - `/vault 1`, `/vault 2` and `/vault 0` keep vault 0's 16 items as the same rows;
  - an item put into vault 1 loads after a new login, with its option row.
- **#123, audit 1.6.** A character banned by hand stays banned after a timed ban ends.
- **#123, audit 1.10.** A coin grant above the largest balance waits instead of being cut.
- **Audit 1.3:** the last two stores were checked; they already share one lock per file. Closed.
- **#124:** the admin sign-in pages no longer show the "An unhandled error has occurred" bar. Found by local Claude
  (Job 8); checked on a published build.
- **#111, light effect test, second cause.** On CI it still failed: the skill cases before it leave test400 at the
  hunting spot, where monsters keep putting a debuff's tint back. It now starts in town.

Local Claude's PRs, reviewed:
- **#121 (importer):** maps sharing a number no longer stop the import preview (Devil Square 1-4 are all map 9).
  Tests pass. With it, Job 8 imported a Season 9 server: 4,266 spawns applied, 803 skipped.
- **#122:** two tests build the three.module.js path the Windows way too. Correct.
- **#120 (Job 8):** Season 9 as its own program works in a Windows package. Checked:
  - a Season 6.3 account plays Season 9;
  - coins are shared.

  Not checked: website "Play" for Season 9 and a website purchase (the throwaway folder had no website key).

  Findings: the sign-in error bar (fixed in #124), and Season 9's servers show no world name. Their world names are
  in Season 9's own admin panel, which the start screen doesn't read yet.
- **#118 (Job 5):** the Season 9 package check on Windows. The 26 missing paths are all expected.
- **#116 (Job 3):** the browser tests in a Season 9 world: 45 passed, 9 failed.
  - Five failures are Season 9's own: the skill effects of the elf, lord and summoner, the monster debuff colour,
    and the Dark Knight's new buff.
  - Season 9's `player.bmd` has 293 actions instead of 284. Given to local Claude as Job 10.
- **#117:** notes from Job 4 before main Claude took it. Its finding matches #110.

New jobs for local Claude:
- **Job 9:** draw Stardust from the Season 9 pack.
- **Job 10:** the five Season 9-only failures.

Overnight batch #115 now also holds #121 to #124. Server tests all pass on it (HNMO.Tests 3403, Web 1398).

## 04:55

Overnight batch #115 green on CI (build, server tests, browser shards 1-6) and merged into the working branch,
with #116, #117, #118, #120 (local Claude's reports) and the Job 9/10 prompts. Main is not merged (waits for the
owner).

Local Claude takes, on the owner's request: friends list online state (social.spec), pet-zen, pets walk, the five
Season 9-only failures (Job 10), Lorencia's light posts' fire, other maps' per-frame hidden objects. Main Claude
keeps: Season 9 servers' world names on the start screen, the website setup in a fresh folder.

## 06:45

Local Claude's morning PRs, reviewed and merged into the working branch:
- **#125:** friends see a player online again after its guild was disbanded (the guild server's error no longer stops
  the friend server from hearing about the player). Server tests 3404 pass.
- **#126:** the pet-zen test makes room for the worn helper before the Panda comes. No check changed.
- **#127:** a long walk goes on after the server puts the hero back a little. It joined #110's map-change and NPC-talk
  walking by hand (both kept).
- **#129:** Lorencia's lamps, torches, bridge, dungeon gate and bonfire burn again (the desktop's CreateFire).
- **#128:** not merged; the same fix as #124, which is already in.

Checked together, on the demo server and the development bridge, with no retries:
- 12 of 12 browser tests pass: walk-resume, command-window, pets (3), pet-zen, social, hidden-objects,
  spawn-while-loading, quests (3);
- the light effect test passes 2 of 2 next to the new Lorencia fires;
- web unit tests pass 16 of 16.
