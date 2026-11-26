# Prompt cho Antigravity: audit client desktop S6 và S9 (27 Nov 2026). Dán nguyên phần dưới dòng kẻ.

---

You are Antigravity on the owner's PC, auditing the HNMO desktop client data for Season 6.3 and Season 9 (repo
nguyentuanphu/HNMO, base branch `claude/hnmo-main-handover-54co2m`). Rules: read `CLAUDE.md`, `server/AGENTS.md`,
`server/docs/CODING_RULES.md` (rule 9: the desktop client and its data are the reference; never copy another server
suite's data or code), `docs/owner-decisions.md`. Never put the client packs or their files in the repo: only tools,
extracted values with their source file and record, and reports. Never name the original game, its publisher, other
server suites or their file prefixes. Draft PRs only; never merge, never touch `main`. Times in Sydney time.

Inputs: the owner's Season 6.3 client data and the Season 9 client data pack (`Data_S9`), both kept outside the repo.
Tools already in the repo: `tools/local-checks/desktop-data/master_tree.py` (decodes a Data folder's master tree and
skill names; on branch `fix/s9c-gameplay`), the item/model/monster readers of the C++ client under `client/src/source/Data`.

What exists already (read first, don't redo): `docs/audit/audit-method.md` (how HNMO is audited), the code ledger
`docs/audit/coverage.json`/`.md` (224 rule-bearing units read in full on 23-25 Nov, findings in `docs/audit/findings/`),
the server data dumps `docs/audit/data/hnmo/` and `hnmo-s9/` with their diffs against the two server suites
(`diff-*.txt`, `data/s9/`), and `docs/s6-s9-gap-audit-2026-11-20.md`, which compared the two client packs by file names
and counts only. What has NEVER been done: reading the VALUES inside the client packs. That is your job.

Goal, in the same layout as the server dumps:
- `tools/local-checks/desktop-data/` (Python, takes a Data folder as an argument): one decoder per binary file you need
  (skills, master tree, items, item options/sets/sockets, buffs, Muun, Pentagram/Errtel, monsters, maps/gates, shops,
  mixes), so anyone with the packs can rerun it. Extend `master_tree.py` (branch `fix/s9c-gameplay`).
- `docs/audit/data/client-s6/*.csv` and `docs/audit/data/client-s9/*.csv`: the decoded values (values only, no files).
- `docs/audit/data/diff-client-s6.txt` and `diff-client-s9.txt`: each value against HNMO (server dump in
  `docs/audit/data/hnmo*/` and the web data in `client/src/bin/Data`): match / differs / missing, with the source file
  and record.
- `docs/audit/client-s6-s9-<area>.md` for the areas below: what the desktop client does, file and record, and what HNMO
  must change. Add each area to `docs/audit/coverage.json` as evidence the way audit-method.md says.

Areas, most urgent first (main and its agents are waiting for 1-3):
1. Skills 618-621 (owner: learned through the master tree; effects as the S9 desktop client): master tree row (rank,
   required points, max level, prerequisites, class), skill record (mana, AG, cooldown, range, damage, type), what the
   client does on cast (effect, buff ids, visual). Post the values on PR #424.
2. Muun (owner: "Theo client desktop S9"): level-up and evolution costs per level, option values and their conditions
   (map, level, hour, weekday), energy and converter values, attack options 50/53, drop data if any, how the client shows
   the Muun (window, slot, the Muun next to the hero). Check every value of the Muun table that #403 added (it came from
   another suite's data). Post on PR #427.
3. Pentagram/Errtel (owner: "Theo client desktop S9 + Admin"): grades and slots, errtel option values per rank/level,
   elements of monsters, mix recipes and costs (errtel mixes, pentagram jewel mixes, extraction), drop data, the client's
   windows. Check the option/grade/extraction tables #403 added. Post on PR #428.
4. Season 9 items and monsters (#401, #419): compare item definitions, item models, monster list and models of the S9
   pack with `client/src/bin/Data` on `integration/s9`; list what is missing or different.
5. Season 6.3 parity: skills, master tree (incl. Weapon Mastery 571 = 10), buffs, item options/sets/sockets, maps and
   gates, NPC shops, mixes (Chaos Goblin), events. Compare the S6.3 client with HNMO server + web; list every difference.
6. UI parity: windows the desktop client has that the web game lacks (or does differently), with screenshots from the
   desktop client and the web.

Deliver: one draft PR "client S6/S9 audit" into the base branch with the evidence files and the decoder tools, plus the
value comments on #424, #427, #428. For each value say plainly when the pack has none. Report: what was read, from which
files, what is still unknown.
