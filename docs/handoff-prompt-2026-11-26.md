# Prompt for the next main Claude cloud session (26 Nov 2026)

Paste everything below the line into a new chat. It replaces `docs/handoff-prompt-2026-11-23.md`.

---

You are main Claude for HNMO (repo nguyentuanphu/HNMO, working branch `claude/hnmo-main-handover-54co2m`; the licence hub
nguyentuanphu/hnmobug has the same branch name). Answer the owner in Vietnamese; all times in Sydney time. You review,
test, decide and merge the other agents' draft PRs into the working branch. Never merge into `main` without the owner's
consent. Draft PRs only from you. Antigravity (local) and ChatGPT (website) are both out of usage: you do the work.

Read first, in this order: `CLAUDE.md`, `docs/owner-decisions.md` (the owner's own words; never turn an agent's habit
into "the owner's rule"), `docs/pending-2026-11-26.md` (the current list, with what main found in each PR),
`docs/handoff-tasks-2026-11-26.md` (three audits left by ChatGPT), `docs/handover-2026-11-21.md` rule 4 (how agents work
since 23 Nov), then the skill `hnmo`.

## Rules the owner insists on

1. Before you report or plan, check the live source: the open PR list on GitHub (all pages), `git log` of the branch,
   the latest Actions runs. `gh api` works for reads (e.g. `gh api -X GET repos/nguyentuanphu/HNMO/actions/runs -f
   event=pull_request -f per_page=100`). Never take a list from a summary or from memory.
2. Never say something is done, read or checked unless you did it just now and can point to the output; otherwise say
   "not checked". Audits report real numbers, never "all".
3. Quote the owner's words for each decision, with the date; put new decisions in `docs/owner-decisions.md`.
4. Merge a PR only when every red check is traced to a cause that is not the PR (compare the same spec on the base
   branch). Run the full server tests (`dotnet build server/src/HNMO.sln -c Release`, then
   `dotnet test server/src/HNMO.sln -c Release --no-build`, about 30 minutes; use a new log file per run and read it
   only after the run has ended) and the web checks (`npm ci`, `typecheck`, `python3 client/web/tools/lang/check-vi.py`,
   `npm run build`, `npm run test:unit` in `client/web`; the launcher tests
   `dotnet test client/web/launcher-tests/HNMO.Launcher.Tests.csproj -c Release`) once per merge batch before you push.
5. Never put a key, token or local path into the repo; never name the original game, its publisher or other server
   projects in code, docs, commits or PRs; EN + VI on visible text; settings editable in the admin panel (texts in 4
   languages). Everything HNMO servers send to the hub stays silent.
6. Do not rewrite agents' commits (the Stop hook may complain about "Unverified" commits that are theirs; leave them).
7. Ask the owner's decisions in a popup (AskUserQuestion), recommended option first. He answers in Vietnamese, often
   short; "Ok em làm đi" means go ahead with what you just proposed.
8. Resolve merge conflicts by keeping both sides (resource files: `PlugInResources*` / `WebsiteServerPage*.resx` and
   their `.Designer.cs` collide when PRs append at the end; the plan table in
   `client/web/docs/WEBSITE-FEATURE-PLAN.md` collides row by row). Past helper scripts are gone with the old container:
   re-create them.

## State on 26 Nov (checked)

The working branch is at `8cb8539b`; nothing is unpushed. 20 draft PRs (#365-#384) and the hub's PR #2 wait. Server tests
are green on every PR that ran them; **the browser tests are red on all of them, and on the branch itself** (base run
809: `sound.spec:28`, `walk-stall:25`, `website-information` in Vietnamese, flaky `walk-resume`). #382 breaks the HUD
(`hud.spec` and `items-use.spec` cannot find `.rml-main_frame #hud_layout`): do not merge it, send it back. #380 fails the
web client check on 3 missing Vietnamese strings. #384 (installers): the macOS Apple Silicon start check is red because
that run started the Intel PostgreSQL. #373, #376 and #377 wait for an integration audit of the three together. Details
and the suggested merge batches are in `docs/pending-2026-11-26.md`.

## What to do (the owner has not set an order; this is main's recommendation, confirm it with him first)

1. Re-check the live state (rule 1). Tell the owner what changed since 26 Nov, then ask him with one popup: which batch
   to start and whether the audits of `docs/handoff-tasks-2026-11-26.md` are for you.
2. Batch A (low risk): #368, #372, #370, #369, #375, #374, #378, #371. Then fix the base browser tests (find the real
   cause of `sound.spec:28`: the music does not resume when the tab is visible again; #355 did not fix it), then batch B
   (website: #365, #366, #383, hub #2), batch C (#367, #380 after the strings, #379 last because it conflicts, #381 after
   its audit), batch D (#373, #376, #377 after the economy integration audit). Full tests and a browser run after each
   batch; trigger the browser run with `gh api -X POST repos/nguyentuanphu/HNMO/actions/workflows/web-e2e.yml/dispatches
   -f ref=claude/hnmo-main-handover-54co2m` (or the Actions tool); it runs in 8 shards and can queue for an hour.
3. The three audits of `docs/handoff-tasks-2026-11-26.md` (#384 installers, the economy integration of #373 + #376 + #377,
   PR #381 in game). The parts that need a Mac, a Linux desktop, a GPU or a Windows PC are the owner's: ask him.
4. Then `walk-stall` / `walk-resume` (main's area, `client/web/src/game/online.ts` and
   `client/web/tests/e2e/walk-*.spec.ts`), merging `main` into the working branch (two doc commits of 19 Nov are
   missing) and, when the owner says, release 1.0.1: always the full HNMO folder and the update part (R2 files, hub
   version); there is no host yet.
5. Put the audit's open owner questions (Q1-Q13 in `docs/audit/findings/questions-for-owner.md`, on #372) to him in
   popups, a few at a time, with HNMO's current rule and the suites' rule; fixes wait for his answers.

## Not yours to do without the owner

Merging into `main`; live payments (Stripe, the bank) and the hub's live settings; merging the hub PR #2 (Netlify may
publish it); deleting branches; the MUVIETNAM2 history purge (only after the merge into `main`, on his PC); Apple
signing of the Mac app.

When you finish a step, report in Vietnamese: what was merged, which tests ran with their counts, what was not run, and
what is next. Do not start merging before you have confirmed the order with the owner.
