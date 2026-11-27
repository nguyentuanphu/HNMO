# Prompt for local Claude (owner's Windows PC), 27 Nov 2026 20:00 Sydney

Antigravity is out of usage; local Claude takes over its queue. Paste the block below.

```
From main cloud Claude, Fri 27 Nov 20:00 Sydney. Antigravity is out of usage; you take over its queue on this Windows
PC. Rules: CLAUDE.md, server/AGENTS.md, server/docs/CODING_RULES.md rule 9 (never name other server suites, the
original game or its publisher; never put pack files in the repo); draft PRs only, never touch main; one heavy job at a
time via `powershell -NoProfile -ExecutionPolicy Bypass -File D:\WorkSpace\meshy\tools\heavy.ps1 cmd /c "..."`;
Sydney times. Main cloud Claude merges; you don't.

Done by main since Antigravity's last message (don't redo):
- hnmobug#6 (Layer 1 hub) merged 22b6f50f. Owner: the full /download stays public; Layer 1 only gates auto-update.
- #448 opened: feat/s9-integration-batch -> integration/s9, with ffb9374a (MissingSkillCount 108 -> 54). CI runs there.
- Owner decisions 19:55: 618-621 stay EVENT/SIEGE spells like the S9 original — no Master Tree rows (don't invent any).
  Skill numbers follow the server suite (263/559/336/40 already match; only 571 changed, #447).
- Owner 19:00: merge the desktop PRs too ("merge hết") — so the desktop part of #441 must NOT be dropped.

Queue, in order:

## 1. #448 (S9 integration batch, D:\WorkSpace\HNMO-ag-s9, branch feat/s9-integration-batch)
- Pull ffb9374a. Bring back #441's 29 desktop files (Muun/Pentagram windows, packet bindings, SeasonNineClientState)
  that e0eb3ffc left out. Then regenerate the 5 generated packet files from the merged packet XML
  (`dotnet msbuild client/ClientLibrary/HNMO.Client.Library.csproj -t:PreBuild`) and check `git status` is clean after
  running the generator a second time. Build the desktop client (windows-x64-relwithdebinfo) once.
- Run under heavy.ps1: HNMO.Tests filter SeasonNineMasterTreeParityTest (expect pass with 54), the S9 web unit tests +
  `npm run build`, and Playwright season.spec, muun-window.spec, monster-looks.spec, master.spec (temp junction to
  D:\WorkSpace\HNMO-ag-base\client\web\node_modules; remove it before any git command).
- Update #448's body (desktop part included, 618-621 = event spells per owner), push, report on #448 and #419.

## 2. Asset crash harness (D:\WorkSpace\HNMO-ag-crash, WIP origin/feat/crash-harness-trade-muun @ 8a635a32)
- Build + run HNMO.Tests (TradeTest|MuunCompanionTests|AssetOperation) and AssetCrashHarness.csproj -c Release; fix
  what fails.
- Portable PostgreSQL (D:\Downloads\HNMO\postgres\bin) on 127.0.0.1:5432 in a scratch data dir; run
  server/tests/asset-crash-harness/run.sh with Git bash for S6.3 (hnmo) and S9 (hnmo_s9): stages before-write,
  in-transaction, after-commit for bank deposit/withdraw, 2PC coins, trade item + Zen (both sides), Muun convert, Muun
  level-up. A new process reads the DB each time: each item once (same id), Zen sum unchanged, one receipt per commit.
- Draft PR into claude/hnmo-main-handover-54co2m with the raw output in the body.
- On the working branch head (679d0963 or newer), with Postgres up: Jewel of Bless slot 12 -> 75, disconnect, relog;
  report the DB rows (item id, slot, count, Zen) before and after.

## 3. CI evidence
- Windows x64 CTest -C RelWithDebInfo --output-on-failure on the working branch head (has #432 + #425): attach the
  log; per failing id say whether #408 fixes it (1586/1587/1591/1592 expected to pass now).
- tests/e2e/social.spec.ts:52 three times on the working branch head: flaky or a regression (it failed after retries
  on batch D 2d6a6481, run 45773437969)?

## 4. Anti-crack fixes
- #444 (Obfuscar): the HNMO package job fails on Linux — `Unable to find assembly: .../HNMO/server\HNMO.Persistence.dll`.
  Build the Obfuscar paths with '/' (or the OS separator) in package-hnmo.sh / the generated XML; list the excluded
  namespaces/types in the PR body. Re-run the package workflow; all start checks green.
- #443 (Layer 2): e2e shards 4 and 5 red — name the specs; fix if yours, else say which known flaky spec it is.

## 5. Chat / whisper / messenger resizable on the web (owner: yes, HNMO extra)
- Drag a corner; size remembered per player; default = desktop size; PARITY.md + guide.html mark it as an HNMO extra;
  reuse old #380 where it fits (its 3 missing vi strings). e2e: resize, reload, size kept. Draft PR.

Report per task on its PR: what ran, what was verified and what wasn't, Sydney times.
```
