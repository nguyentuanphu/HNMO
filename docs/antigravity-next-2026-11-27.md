# Prompt cho Antigravity, lượt tiếp theo (27 Nov 2026). Dán nguyên phần dưới dòng kẻ, sau khi lượt hiện tại xong.

---

You are Antigravity on the owner's Windows PC (repo nguyentuanphu/HNMO, base branch `claude/hnmo-main-handover-54co2m`).
Same rules as before (`CLAUDE.md`, `server/AGENTS.md`, `server/docs/CODING_RULES.md`, `docs/owner-decisions.md`; draft
PRs only, never merge, never touch `main`; never name the original game, its publisher or other server suites or their
file prefixes; never put client pack files in the repo; resx texts EN+VI; report SHA, commands, pass/fail/skip, what was
not run; Sydney time). Main (cloud) keeps merging and does the small fixes; these are the heavy jobs that need this PC.

Order:
1. Finish your current round and report it (A.1-A.6, B.7, B.8, B.9, B.10, B.11, B.12).
2. The client audit in `docs/antigravity-client-audit-2026-11-27.md` (read the whole file and do it). Areas 1-3 first:
   skills 618-621, Muun, Pentagram/Errtel; post the values on #424, #427, #428.
3. With those values, finish the features yourself on their branches (owner's decisions: 618-621 learned through the
   master tree, effects as the S9 desktop client; Muun and Pentagram "Theo client desktop S9", the rest as admin
   settings):
   a. `fix/s9c-gameplay` (#424): master tree rows for 618-621 (server initializer + update plug-in with its three
      tests, web `master-tree.json`), learning through master points, the effects of 618-620 as the desktop client does.
   b. `feat/s9-muun` (#427): the pack's level-up/evolution costs, pet and option tables as the admin defaults; Muun attack
      skills (options 50/53) and the Muun shown next to the hero if the client does it (new appearance field + packet if
      needed, XML + regenerate server and web).
   c. `feat/s9-pentagram` (#428): monster elements, drops, mix recipes/costs and the option/grade/extraction tables from
      the pack as admin defaults; the missing options (success/critical/excellent rate, errtel 261 stats, raid-only).
   Keep the packet codes already taken: F5 2B/2C Muun, F5 58/59 S9 client state, F5 5B/5C/5D Pentagram; pick new
   F5 codes only after checking every open PR's XML.
4. Desktop client UI (C++, `client/src/source`, Windows build + ctest): a Muun window and a Pentagram/Errtel window that
   speak the same packets as the web (F5 2B/2C, F5 5B/5C/5D), following the client's existing window patterns (RmlUi),
   EN+VI texts; and the S9 minimap state (F5 58/59). One draft PR per window into the feature's branch.
5. After main merges #419 (`integration/s9`) into the base branch: a full real-game Season 9 pass on the packaged folder
   with the S9 pack (log in, an S9 item, the Muun and Pentagram windows, an S9 event, an Arena cycle, switching
   S9 -> S6 -> S9 in one session), web and desktop; post the results on #419 with screenshots.

Report after each numbered step (push what you have before your usage runs out).
