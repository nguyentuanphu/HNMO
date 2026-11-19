# Antigravity (Windows), 1-2 hours, 2026-11-19 19:00 Sydney

Paste everything below the line into Antigravity.

---

You test HNMO on this Windows computer. You do not change code. Work in this order and stop when time runs out:
an unfinished step is fine, a wrong "pass" is not.

**Setup (10 min)**
1. `git fetch origin` and `git checkout -B antigravity-check2 origin/claude/hnmo-main-handover-54co2m`.
2. Close draft PR #86 without merging.
3. Build the package (Git Bash, same command as last time, **without** `HNMO_S9_DATA_DIR`):
   `client/web/tools/package-hnmo.sh "<repo>/server" win-x64 "<scratch>/HNMO-pkg"`.
   The output must contain a line `... routes kept, ... without a file removed`. Copy that line.
4. Start it with `Start HNMO.bat`. Open `http://127.0.0.1:8095/worlds`: the page must be styled, with no red
   error in the browser console. Screenshot.

**Step A: admin panel saves (30 min)**
1. `/market`: turn on "Enable Item Market", press Save, reload the page. It must still be on. Screenshot.
2. `/worlds`: create a second world from a template, Season 9, one free server id. Save. Restart the package.
   The world is still there. The home page `http://127.0.0.1:8080/` shows both worlds with `S6.3` and `S9`
   badges. Screenshot.
3. Set the new world's EXP rate to 3 and save. On `/servers`, the first world's EXP rate has not changed.
   Screenshot.

**Step B: trade lock, done properly (40 min)**
Use two browsers: `test1` and `test2` in Lorencia. The browser console has `window.__game`.

For each action below, do it **twice**:
- first with no trade open: the action must work (this proves your test can see a change);
- then with a trade open between test1 and test2: the action must change nothing.

| Action | How | What to compare |
|---|---|---|
| Jewel Bank deposit | F7, deposit one Jewel of Bless | bank count and bag |
| Market listing | F6, list one plain item | bag and the market's list of listings (Step A turned the market on) |
| Chat command | type `/code TEST` | the chat says "Close the trade or crafting window before using a command." |

Cancel the trade at the end: both bags must be as before the trade. Write the before/after numbers in a table.

**Rules**
- Write `<scratch>` instead of your own folder paths (they contain the owner's user name).
- No third-party names in anything you write or commit.
- Do not change code. Do not push to `main`. Do not merge.
- For every result, give the command or the clicks, plus the last output lines or a screenshot.

**Finish**
Write `docs/antigravity-report-2026-11-21.md`, with screenshots in `docs/screenshots/antigravity-2026-11-21/`.
Commit, push `antigravity-check2`, open a **draft** PR into `claude/hnmo-main-handover-54co2m`, and stop.
