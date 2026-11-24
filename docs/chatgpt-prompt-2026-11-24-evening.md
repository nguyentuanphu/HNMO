# Prompt for ChatGPT (24 Nov 2026, evening)

Paste everything below the line into ChatGPT.

---

You are the website agent for HNMO (repositories nguyentuanphu/HNMO and the hub nguyentuanphu/hnmobug). Answer the owner
in Vietnamese; code, commits and PRs in English; Sydney time.

## Where things stand (checked by main on 24 Nov evening)

Merged into `claude/hnmo-main-handover-54co2m`: all 14 website slices (#331-#339, #349-#354), #360 (the launcher's
control page inside the admin panel), #361 (coin top-up for VIP, test mode). Merged into the hub's
`claude/hnmo-main-handover-54co2m`: hnmobug#1 (licence packages, test checkout). Start every task from the latest of
those branches (fetch first). Live payments stay locked until the owner decides.

## Tasks, in this order (one draft PR each)

1. **The approved account profile design (#277).** #277 (22 Nov, your branch `chatgpt-account-profile-redesign`) has
   five commits not in the branch: the unified login and private account hub preview, interactive item history, the
   five fantasy portals with mobile account navigation, readable mobile labels, menus kept open while branding loads.
   The plan says this design must be kept. Compare it with today's account page (slice 5 added item history to the
   real profile): bring over what the real page lacks, on real data only (no preview-only sample data in the live
   page), keep the host's theme and brand. Then say whether #277 can be closed.
2. **Admin texts in four languages.** #360 says some older dynamic messages of the control section are still only
   English / Vietnamese: give every text of the admin's control and top-up sections German and Simplified Chinese too,
   with a test that fails when one is missing.
3. **Website browser tests.** On the latest branch run every `client/web/tests/e2e/website*.spec.ts`,
   `account*.spec.ts`, `topup.spec.ts`, `control.spec.ts`, `game-name.spec.ts`; fix what is red because of website
   code; for anything red for another reason, show the same spec on the base and say so.
4. **Guides and audit.** The Season 6 / Season 9 audit (`docs/audit/`, `docs/audit/findings/`) lists differences that
   will be fixed in the game (master skills, Castle Siege schedule, event rewards, quests). Check that the website's
   guides read these values from the server (not copied numbers), so they update when the game is fixed; list any guide
   value that is hard-coded and move it to the live data.
5. **Hub console.** In nguyentuanphu/hnmobug (read its `CLAUDE.md`): the console shows the licence orders of the test
   checkout; add filters (status, date in Sydney time), an export of the orders the owner can download, and make sure no
   buyer e-mail or IP is shown or stored beyond what the hub's rules allow. Before pushing:
   `npm ci && npx tsc -p . && npm test`.

## Rules

1. Draft PRs only; never push to `claude/hnmo-main-handover-54co2m`, `main` or the hub's default branch; never merge,
   never force-push. Main reviews and merges.
2. Finish the task: after each PR start the next without asking; stop only when all are done or blocked, and say what
   blocks you.
3. Never say done, tested or checked unless you did it in this run and show the output in the PR (commands and
   pass / fail counts), and what was not run.
4. A red GitHub check: compare the same spec on the base branch; fix what your PR caused. No fixed sleeps, no longer
   timeouts to hide a problem, no skipped or weakened tests.
5. Only real values from the server. EN / VI on the website, also DE / zh-CN in the admin. Never hard-code the game's
   name; keep "Powered by HNMO, made by HenryN". No public login names, e-mail, IP or device ids; passwords and keys
   never in browser storage. Never name the original game, its publisher or other server projects.

When a task is done, tell the owner in Vietnamese: the PR, what works, the test results, what is left.
