# Prompt cho Antigravity, lượt tiếp theo (27 Nov 2026, 08:20 Sydney). Dán nguyên phần dưới dòng kẻ.

---

You are Antigravity on the owner's Windows PC (repo nguyentuanphu/HNMO, base branch `claude/hnmo-main-handover-54co2m`).
Same rules as before (`CLAUDE.md`, `server/AGENTS.md`, `server/docs/CODING_RULES.md`, `docs/owner-decisions.md`; draft
PRs only, never merge, never touch `main`; never name the original game, its publisher or other server suites or their
file prefixes; never put client pack files in the repo; resx texts EN+VI; report SHA, commands, pass/fail/skip, what was
not run; Sydney time). Main (cloud) keeps merging and does the small fixes.

## 0. Share the PC with local Claude (do this first)
- The heavy lock (`heavy.ps1`) is shared and not first-come-first-served: local Claude's short jobs keep losing it to
  your long e2e runs. From now on queue ONE heavy job at a time, and let local Claude's queued jobs run before your next
  one (it has about 2.5-3 hours left, mostly 10-60 minute jobs). If you have several jobs waiting now, cancel all but the
  one running and requeue them one by one.
- `integration/s9` test data: local Claude commits the desktop tooltip snapshot and the data test counts there first
  (its task 5c); report your S9 item/monster data changes (audit area 4) after that, rebased on its commit.
- #423 real-game kill tests are local Claude's (on #423's head). Your branches stacked on it (`feat/shared-coins-2pc`,
  `feat/admin-jewel-bank-receipts` / #429) stay separate PRs into `fix/asset-db-transactions`.

## 1. Finish your current round and report it
A.1-A.6, B.7, B.8, B.9 (shorter connection timeout), B.10 (shared coins 2PC), B.11 (#429), B.12 (Arena on S9). Also:
- #386 (Gens: same gens for guild/alliance, outlaw warp 2x into the battle zone, 50x elsewhere) is blocked by
  `tests/e2e/social.spec.ts:53`, which #386 changed (`joinDuprian`). Fix that spec on `fix/gens-same-gens-rules`
  (3 green runs). The outlaw 2x rule the owner wants is in #386: local Claude found it missing on the base branch only
  because #386 isn't merged.
- #381: `skill-effects.spec.ts:248` fighter and summoner red; run 3x on #381 and 3x on the base; fix or show it is red
  on the base too.

## 2. The client audit
Read and do `docs/antigravity-client-audit-2026-11-27.md` (on branch `claude/hnmo-main-handover-kpts7l`). Local Claude
gave you its decoders and dumps. Its findings so far, from the owner's S9 pack (already posted on #427/#428):
- Muun: the 76 Muuns and 37 options match the #403 table. The pack gives every Muun an add-option DATE WINDOW (the PR
  applies the add option always); add-option type 3 gives no bonus in the PR; option 36's text exists only in the PR.
  ("non-PvP" options: main already made them monster-only in `3b3d696c`.)
- Errtel: values match at levels 0/5/10; at levels 1-4 and 6-9 the PR interpolates a straight line, 23 options too high:
  use the pack's per-level values.
- #428 has none of the 7 Pentagram set options and none of the mixes; the pack has no slot count per pentagram.
- Not in the pack: Muun costs and drop rates, monster elements, errtel extraction, drop rates (these stay admin settings
  with HNMO defaults; say so in the PRs).
Areas 1-3 of the audit first (skills 618-621, Muun, Pentagram/Errtel).

## 3. Finish the features with those values, on their branches
a. `fix/s9c-gameplay` (#424): master tree rows for 618-621 (server initializer + update plug-in with its three tests,
   web `master-tree.json`), learning through master points, the effects of 618-620 as the S9 desktop client does.
b. `feat/s9-muun` (#427): the add-option date windows, add-option type 3, option 36 text as the pack has it; the pack's
   tables as admin defaults; Muun attack skills (options 50/53) and the Muun next to the hero if the client does it.
c. `feat/s9-pentagram` (#428): per-level errtel values from the pack, the 7 set options, the mixes as the pack
   describes them; admin defaults for what the pack lacks.
Packet codes already taken: F5 2B/2C Muun, F5 58/59 S9 client state, F5 5B/5C/5D Pentagram; pick new F5 codes only
after checking every open PR's XML.

## 4. Desktop client windows (C++, `client/src/source`, Windows build + ctest)
A Muun window and a Pentagram/Errtel window speaking the web's packets (F5 2B/2C, F5 5B/5C/5D), following the client's
RmlUi window patterns, EN+VI; and the S9 minimap state (F5 58/59). One draft PR per window into the feature's branch.

## 5. After main merges #419 into the base branch
A full real-game Season 9 pass on the packaged folder with the S9 pack, web and desktop (log in, an S9 item, the Muun
and Pentagram windows, an S9 event, an Arena cycle, S9 -> S6 -> S9 in one session); results on #419 with screenshots.

Report after each numbered step (push what you have before your usage runs out).
