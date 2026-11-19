# Antigravity (Windows), about 45 minutes, 2026-11-19 19:00 Sydney

Paste everything below the line into Antigravity.

---

You test HNMO on this Windows computer. You do not change code. Stop when time runs out: an unfinished step is
fine, a wrong "pass" is not.

**What changed:** the Jewel Bank (F7) stayed on "Loading Jewel Bank..." because the server dropped the window's
request. That is fixed on `claude/hnmo-main-handover-54co2m` (commit `b64334b1`). Your report from PR #87 is
merged; PR #87 is closed.

**1. Setup (15 min)**
1. `git fetch origin` and `git checkout -B antigravity-check3 origin/claude/hnmo-main-handover-54co2m`.
2. Build the package as last time (without `HNMO_S9_DATA_DIR`), into a **new** folder `<scratch>/HNMO-pkg3`, and
   start it with `Start HNMO.bat`.

**2. Jewel Bank switched off (5 min)**
Log in as `test1` and press F7. Expected: the window says "Jewel Bank is currently disabled by the server
administrator." (not "Loading"). Screenshot.

**3. Jewel Bank switched on (10 min)**
On `http://127.0.0.1:8095/jewel-bank`, turn on "Enable Jewel Bank" and press Save. Restart the package. Log in as
`test1` and press F7. Expected: the bank lists the jewels with "Banked: N / 10,000". Then press "Deposit 1" next
to Jewel of Bless. Expected: Banked goes up by 1 and the bag has one Jewel of Bless less. Write both numbers.
Screenshot.

**4. Jewel Bank during a trade (15 min)**
Open a trade between `test1` and `test2`. In test1's F7 window, press "Deposit 1" for Jewel of Bless again.
Expected: nothing changes (same Banked number, same bag count). Cancel the trade: both bags are as before the
trade. Write the before/after numbers in a table.

**Rules**
- Write `<scratch>` instead of your own folder paths.
- No third-party names in anything you write or commit.
- No code changes. Do not push to `main`. Do not merge.
- For every result, give the clicks or the command and its output, plus a screenshot.

**Finish**
Write `docs/antigravity-report-2026-11-22.md`, with screenshots in `docs/screenshots/antigravity-2026-11-22/`.
Commit, push `antigravity-check3`, open a **draft** PR into `claude/hnmo-main-handover-54co2m`, and stop.
