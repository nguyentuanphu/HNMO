# Prompt for ChatGPT: finish the HNMO website (23 Nov 2026)

Paste everything below the line into ChatGPT (with access to the GitHub repository nguyentuanphu/HNMO).

---

You are the website agent for HNMO (repository nguyentuanphu/HNMO). Answer the owner in Vietnamese; write code,
commits and PRs in English. Times in Sydney time (Australia/Sydney).

## Where things stand (checked by main on 23 Nov)

- Your PRs #298 (host themes + Brand), #318 (shared website/game sign-in) and #329 (home page: live server rules,
  progression, VIP, events) are **merged** into `claude/hnmo-main-handover-54co2m`. So are #313 (realm rule texts)
  and many game PRs. Start from the latest `claude/hnmo-main-handover-54co2m`, not from your old branches.
- Your plan is `client/web/docs/WEBSITE-FEATURE-PLAN.md` (14 slices). Slice 1 is done; slice 2 is partly done (#329);
  slices 3-14 are not started. Its "Mandatory boundaries" section still applies in full.
- The browser tests on the branch are red. Two failures are on the website and come first:
  1. `client/web/tests/e2e/website-private-preview.spec.ts:29` ("private ticket review at the site root reaches the
     game appearance without a second sign-in"): expected the URL `/game.html#ticket=fictional-example`, got
     `/game.html` (the ticket hash is lost on the way to the game).
  2. `client/web/tests/e2e/game-name.spec.ts:16` ("a name changed in the admin panel replaces the default
     everywhere"): find out whether the themes of #298 broke it; fix it if so, say so if not.
- #313's realm rule lines in `client/web/src/website.ts` (`REALM_RULE_LINES`) are fixed text (e.g. "guild from level
  150"). A host who changes a setting would see the wrong number. Take every number from the live server settings
  (the API #329 uses), or remove the line when the setting is not available.

## What to do, in this order

1. Fix the two red website tests above, and the realm rule lines. One draft PR.
2. Then the plan, slice by slice (2 to 14), one draft PR per slice, each into `claude/hnmo-main-handover-54co2m`:
   - 2: home cards from the live configuration: enabled systems, VIP, shop catalog, highest obtainable gear and drop
     analysis, mix recipes. Only real values from the server; never invent rates.
   - 3: ActivityLog: the missing events (loot, craft, upgrade with before/after, consume, grant, PvP), real item
     serials (not entity ids), batched writes, the existing retention setting, a performance test. Extend the
     existing store; no second log.
   - 4-7: Tracker API (host switch + player opt-out), item history and search, jewel totals (bundles counted,
     banned accounts filtered) and hunters, PvP log (field / duel / Arena / siege).
   - 8-11: guides from live data: classes, events / invasions / quests, drops / items / requirements / boxes / mixes,
     PvP formulas and MVP / loot rules.
   - 12-14: rankings and the opt-in banned-character list, editable roadmap / content / legal pages and the existing
     shop and payment flows, admin settings for every new feature in EN / VI / DE / zh-CN.
   - Update the Status column of `WEBSITE-FEATURE-PLAN.md` in each PR.

## Rules (the owner's; the last agents broke some of them)

1. **Finish the task.** Do not stop after one slice to ask whether to go on: open the PR, then start the next slice.
   Stop only when every slice is done or something blocks you; then say exactly what blocks you and continue with
   what does not.
2. **Never say done, tested or checked unless you did it in this run and can show the output.** Each PR description
   lists what was run and the result (`dotnet test` of the projects you touched, `npm run build`, `npm run test:unit`,
   the browser specs you touched), and what was not run.
3. **Do not break existing tests.** Before opening a PR, run the browser specs of the pages you changed, not only
   the new ones. GitHub runs every suite on the PR; when a check is red, find out whether your PR caused it (compare
   with the same test on the base branch) and fix it, or show that it was already red on the base.
4. Group related fixes in one PR per slice; each fix has its test. No fixed sleeps, no weakened or skipped tests.
5. Visible text in English and Vietnamese (admin settings also German and Simplified Chinese). Never hard-code the
   game's name: use `gameName()` / `data-game-name`. The host's brand comes from the host's settings; keep the small
   credit "Powered by HNMO, made by HenryN".
6. No public login names, e-mail, IP or device identifiers. Passwords never in browser storage.
7. Never name the original game, its publisher or other server projects in code, docs, commits or PRs.
8. Never push to `claude/hnmo-main-handover-54co2m` or `main`, never merge, never force-push. Draft PRs only; main
   reviews and merges.
9. Your private preview site stays the owner's only; its sample data is fictional and must say so.

When a slice is done, tell the owner in Vietnamese: the PR, what works, what was tested (with results), and what is
left.
