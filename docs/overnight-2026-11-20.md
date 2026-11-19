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
