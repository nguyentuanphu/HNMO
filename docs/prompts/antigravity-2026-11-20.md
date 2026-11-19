# Task for Antigravity (Windows), 2026-11-19 18:00 Sydney (about 4-6 hours)

Paste everything below the line into Antigravity.

---

You are working on HNMO (github.com/nguyentuanphu/HNMO) on the owner's Windows computer. Main Claude reviewed
your report (draft PR #86, branch `antigravity-win-check`) and took it into
`claude/hnmo-main-handover-54co2m` (commit `7cc4895f`). **Close PR #86 without merging; do not push to
`antigravity-win-check` again.**

## Start

1. `git fetch origin`, then branch `antigravity-win-check2` from `origin/claude/hnmo-main-handover-54co2m`.
   Check that `client/web/tools/build-s9-pack.mjs` and `client/web/tools/prune-static-endpoints.mjs`
   exist. If they don't, you are on the wrong commit: stop and say so.
2. Read `docs/antigravity-report-2026-11-20.md` (your report; your local paths were replaced by `<scratch>`)
   and `docs/audit-2026-11-19.md`.

## Rules (unchanged)

- No third-party names anywhere (commits, branch names, docs, file names, screenshots, local paths). Check with
  `git diff --cached | grep -inE "<the real words>"` before each commit.
- **No private details:** write `<scratch>` instead of your local folders (they contain the owner's user name).
- No third-party files in git; the Season 9 files stay outside the repository.
- Never push to `main`, merge, force-push or rewrite pushed history. Web only. EN + VI. Sydney time.
- Report only what you ran: the command and its last output lines for every result. You may write scripts in
  `<scratch>`, but **a check counts only if it would fail when the feature is broken** (see 2a below).
- No code changes, except the one marked in step 4. Main Claude fixes code; you test and report.

## Review of your last report (please read)

- **The 8095 admin panel bug was real; it is fixed.** The cause: the package deletes the `.br`/`.gz` copies
  under `server/wwwroot`, but the server's asset list still named them. A browser that accepts gzip got an
  empty file. `47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=` is the hash of an empty file. Good catch, and
  a clear report.
- **Step 2a, the windows part, does not count.** `window.__jb`, `window.__market` and `window.__mc` do not exist
  in the client. `window.__jb?.open?.()` does nothing whether or not a trade is open, so "jbOpen: false"
  proves nothing. The `/code` refusal and the bag comparison are valid. Always add a positive control: the
  same action must work when no trade is open.
- The 404s for `/data/Interface/RmlUi/themes/legacy/*.rml` and the `.ktx2` 404s that are followed by a `.jpg`
  or `.tga` that loads are expected: the client tries the theme folder and the compressed format first. Only
  a texture that fails in **every** form is a problem.

## 1. Repackage and recheck the admin panel (about 1 hour)

1. Package again (with `HNMO_S9_DATA_DIR`, as in your step 3a). The output must contain a line like
   `HNMO.Startup.staticwebassets.endpoints.json: N routes kept, M without a file removed`. Copy it into the report.
2. Start the package and open `http://127.0.0.1:8095/worlds`. Expected: styled page and no console error.
   Screenshot `step2c-worlds-fixed.png`.
3. **Step 2c again** (from the earlier prompt): create a second world, Season 9, with one free server id;
   save; restart. The world and its season are still there (`save/hnmo-worlds.json`). The home page shows both
   worlds with `S6.3` / `S9` badges and a season choice; the game's server list shows the badge. Set the new
   world's EXP to 3 and save: the first world's rate on `/servers` has not changed. Screenshot each.
4. **Step 2e again:** enable the Item Market on `/market` and save. Reload the page; it stays enabled. Then in
   the game, with `testgm`: spawn an ancient item and a socket item, press F6, and try to list each one.
   Expected: refused, with the notice in EN and, after switching to VN, in VI. Screenshot both. Then list a
   plain item to show listing works (positive control).

## 2. Step 2a, done properly (about 1 hour)

Use `window.__game` (the real test hook: `__game.jewelBank`, `__game.market`, `__game.mountChain`; the keys
are F7, F6 and Shift+U). A window may open during a trade; what must not happen is a change on the
server. For each of the three below:

1. **No trade open (positive control):** do one real action and show that it changes something. Examples:
   deposit one Jewel of Bless in the Jewel Bank (the bank's count goes up and the bag loses the jewel); list a
   plain item on the market (it leaves the bag and shows in the listings); salvage a duplicate mount (it
   leaves the bag and the shard count goes up).
2. **Trade open between test1 and test2:** do the same action again. Expected: nothing changes, neither the
   bag nor the bank, listings or shards. Cancel the trade: both bags are as before.
3. Repeat step 2 with the Chaos Goblin's mix window open instead of a trade.

Report the before/after numbers for each case, with screenshots.

## 3. Season 9 textures that fail in every form (about 1-2 hours)

For each texture in your step 3c list that 404'd in **every** form (for example Devias
`Object3/sword14`, `sword11`, `hound_sword1`, `hound_shield1`, `sword06`, `shield05`, `sword03`; Noria
`World4/TileGrass02`, `Object4/filling_up02`, `itp01`; Lorencia `Object1/ston03`, `bridge_shadow01`,
`Effect/twlighthik01`), search the Season 9 Data folder **and** the base game's Data folder for that name,
ignoring case and extension (`Get-ChildItem -Recurse -Filter "sword14.*"`). Write a table:

| request | found in S9 at | found in base at | extension on disk |

Do not copy any of these files into the repository; only names and folders go in the report. Also say
whether the same objects show a 404 in a Season 6 session (walk the same spot without the season override).
This tells main Claude whether it is a name-mapping bug in the web server or files that are simply missing.

## 4. Season 9 pack script (step 3d, about 30 minutes)

```
node client/web/tools/build-s9-pack.mjs "<S9 Data>" "<base Data>" "<scratch>/s9-pack" --dry-run
node client/web/tools/build-s9-pack.mjs "<S9 Data>" "<base Data>" "<scratch>/s9-pack"
```

Copy the summary table into the report. Then package with `HNMO_S9_DATA_DIR="<scratch>/s9-pack"` and repeat
your 3b byte checks and the Lorencia + Devias walk. Report the size of `Data_S9` (it should be much smaller
than 1,217 MB) and any texture or model that now fails in every form but worked with the full folder. If the
script fails on a real file, report the error; that is the one code fix you may make, in that script, with a
unit test in `client/web/tests/build-s9-pack.test.mjs`.

## 5. The smoke failures (about 1 hour)

- `control.spec.ts` starts its own developer web server on port 18088. It cannot run against the package:
  take it out of your packaged smoke list and say so in the report. This is not a failure.
- Run `quests.spec.ts` and `touch.spec.ts` three times each against the package with `--workers=1
  --trace on`. For each failed run, attach the trace zip's name, the screenshot, and what the character was
  doing (map, cell, open windows). Before each run, put the account in a known place: `/move lorencia` for
  `test0` and `test6`, and close every window. Do not change the tests.

## Finish

Write `docs/antigravity-report-2026-11-21.md`: one section per step, with commands, last output lines, numbers
and screenshots under `docs/screenshots/antigravity-2026-11-21/`. Push `antigravity-win-check2`, open a draft
pull request into `claude/hnmo-main-handover-54co2m` (not `main`), and stop.
