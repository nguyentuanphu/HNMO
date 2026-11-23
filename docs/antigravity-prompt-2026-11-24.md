# Prompt for Antigravity: short and medium tasks (24 Nov 2026)

Paste everything below the line into Antigravity on the owner's PC.

---

You are Antigravity, working on HNMO (repo nguyentuanphu/HNMO) on the owner's Windows PC. Answer the owner in
Vietnamese; code, commits and PRs in English; times in Sydney time.

Other agents are busy elsewhere, so stay out of their areas: the VS Code agent does the audit (`docs/audit/`,
`tools/audit/`, PR #330); ChatGPT does the website (`client/web/src/website*.ts`, `client/web/public/home.html`,
`account*`, the `codex/website-*` branches). Do not touch those.

## Rules

1. Start each task from a fresh `origin/claude/hnmo-main-handover-54co2m`. One DRAFT PR per area into it; each fix has
   a test that fails before and passes after. Commits end with
   `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
2. Run only the tests of what you touched (the web build and unit tests for `client/web`, the matching server test
   project, the browser specs you touched). GitHub runs everything on the PR. Builds go through
   `powershell -File D:\WorkSpace\meshy\tools\heavy.ps1 <command>` if that file exists; otherwise one at a time.
3. Never say done or fixed unless you ran the test in this session; paste the result in the PR. If a test is red
   for another reason, show it is also red on the base branch.
4. Do not stop between tasks to ask whether to go on: after a PR, start the next task. Owner questions go into the
   PR description and you continue.
5. Never name the original game, its publisher or other server projects in code, docs, commits or PRs. Visible text
   in English and Vietnamese. Never hard-code the game's name (`gameName()` / `data-game-name`).

## Tasks, in this order (the owner chose "fix the red tests first")

First look at the latest "Web game tests" run on `claude/hnmo-main-handover-54co2m` (GitHub Actions; main started
one on 24 Nov) and list which specs fail. The ones below were red on 23 Nov (run 762).

**A. Browser tests broken by your own merged PRs (short)**
1. `client/web/tests/e2e/bottom-hud-bar.spec.ts:25` (from #322): bottom HUD buttons, highlight, WindowMenu/SysMenu.
2. `client/web/tests/e2e/quest-window-buttons.spec.ts:26` (from #325): Quest Window closes the CashShop overlay, tabs
   and bottom buttons. Find whether the game or the test is wrong; fix the game when it is the game.
   One PR for both.

**B. Other red browser tests (short to medium)**, one PR each unless they share a cause:
3. `pet-zen.spec.ts:19` (a Panda picks up zen next to the hero).
4. `sound.spec.ts:28` (the sound pauses while the game is out of sight).
5. `skill-effects.spec.ts:394` (effect light reaches the characters in it).
6. `monster-looks.spec.ts:118` (Season 9 monsters and NPCs drawn with their Season 9 models, not the ghost).
   Leave `walk-resume` / `walk-stall` (main's area) and `website-*` / `game-name` (ChatGPT) alone.

**C. NPC dialogs found by your sweep #328 (medium)**: finish and close #328, then:
7. `ObjectMessage` (C1 01) not shown in the web game: Leo the Helper (371), Sebina (235), Apostle Devin (406), Royal
   Guard Captain Lorence (241), Marlon (229). Show them as the desktop client does.
8. `NpcWindowResponse` (C3 30) kinds the web game ignores while the server dialog stays open: 0x07 pet trainer (226),
   0x09 Lahap (256/546), 0x11 Elphis (368), 0x17 seed master (452), 0x18 seed researcher (453), 0x19 stat
   re-initializer, 0x25 Julia (547), 0x26 lucky item combination (579). At least close the dialog cleanly and open
   the window the desktop client opens; list any you could not do. Golden Archer (236) answers nothing: find why.

**D. Small UI items from the owner's 22 Nov feedback (short)**, one PR for all:
9. Vault zen: an in-game dialog instead of the browser's `prompt()`.
10. Alt toggles always-show names, as on the desktop.
11. The Ultimate HUD can be minimized.
12. Admin panel shop item icons bigger.

When each PR is open, tell the owner: the PR, what was wrong, the test result before and after, and what is left.
