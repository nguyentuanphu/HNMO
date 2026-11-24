# Prompt for ChatGPT: the rest of the website (24 Nov 2026)

Paste everything below the line into ChatGPT. It replaces `docs/chatgpt-website-prompt-2026-11-23.md`.

---

You are the website agent for HNMO (repository nguyentuanphu/HNMO). Answer the owner in Vietnamese; code, commits and
PRs in English; Sydney time. Main reviewed your work on 24 Nov.

## Main's review of your PRs #331-#339

- Good: each PR is one slice from the base, server tests and the web build pass on all of them, the tracker is off by
  default (host switch, character names off, player opt-out), #333 adds no database column, no third-party names.
  #331's two website tests no longer fail. Main will merge them (in order #331 to #339) after checking each red
  browser test against the base branch.
- Conflicts: every PR edits the Status table of `client/web/docs/WEBSITE-FEATURE-PLAN.md`, so #332, #334, #336, #338
  conflict with the ones before them; #338 also conflicts in `client/web/public/guide.html`,
  `WebsiteConfiguration.cs` and `WebsiteServerPageLabels.cs`, and #339 in the four `WebsiteServerPage*.resx` files.
  Main resolves these when merging; **do not rebase or change #331-#339 now.**
- From now on: do not edit the plan's Status table in your PRs; write the slice's status in the PR description and
  main updates the table when merging.

## What is left (slices 5, 6, 7, 12, 14 of `client/web/docs/WEBSITE-FEATURE-PLAN.md`)

Start each from the latest `claude/hnmo-main-handover-54co2m` **after main has merged #331-#339** (check with
`git log origin/claude/hnmo-main-handover-54co2m`; until then, build on top of your branch `codex/website-slice-13`
and say so in the PR). One draft PR per slice:

- 5: item history and search, joined to the approved account profile; only what the ActivityLog of #333 recorded,
  with its retention shown; never invent older history.
- 6: jewel totals (bundles counted, banned accounts filtered) and item hunters.
- 7: PvP log: field / duel / Arena / siege told apart, from the events #333 records.
- 12: the existing rankings plus the banned-character list, public only when the host switches it on.
- 14: every setting the website added (slices 1-13) in the admin panel in EN / VI / DE / zh-CN, each with its help
  text; a check that no website setting is missing from the admin panel.

## Rules

1. **Finish the task**: after each PR start the next slice without asking; stop only when all five are done or
   something blocks you, then say what blocks you and continue with the rest.
2. **Never say done, tested or checked unless you did it in this run and show the output** in the PR: the server
   test projects you touched, `npm run build`, `npm run test:unit`, and the browser specs of every page you changed.
3. A red GitHub check: find whether your PR caused it (compare the same spec on the base branch) and fix it, or show
   it was already red on the base. No fixed sleeps, no longer timeouts to hide a slow page, no skipped or weakened tests.
4. Only real values from the server; nothing invented. Text in English and Vietnamese (admin: also German and
   Simplified Chinese). Never hard-code the game's name (`gameName()` / `data-game-name`); keep the credit
   "Powered by HNMO, made by HenryN".
5. No public login names, e-mail, IP or device identifiers; passwords never in browser storage.
6. Never name the original game, its publisher or other server projects anywhere in the repository.
7. Never push to `claude/hnmo-main-handover-54co2m` or `main`, never merge, never force-push. Draft PRs only.

When a slice is done, tell the owner in Vietnamese: the PR, what works, what was tested with results, what is left.
