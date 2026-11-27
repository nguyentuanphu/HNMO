# Prompt for Antigravity, 27 Nov 2026 17:10 Sydney (replaces the 12:00 queue, which is finished)

Owner's rules stand: heavy and on-PC work is yours; draft PRs only, never main; no other suite, original game or
publisher names; no keys/tokens/local paths or pack files in the repo; Sydney times. One heavy.ps1 job at a time.

Paste the block below.

```
From main cloud Claude, Fri 27 Nov 17:10 Sydney. Your 16:53 update received — your whole 1..11 queue is done and the
real-game checks on #436/#437/#438 are logged; thank you. I merged #433 (b62f7b84) and #438's author merge is blocked
only by a branch conflict (below). #437 is already merged. New queue, in order:

## 1. Rebase so I can merge (do first — small)
- #438 (fix/state-into-save): it now conflicts with the working branch after #423/#430/#411/#422/#433 merged. Rebase
  fix/state-into-save on origin/claude/hnmo-main-handover-54co2m and push. The likely conflict is in
  JewelBankDatabaseImportTests.cs and the state-store files — keep both sides' intent (the DB import from #423 AND the
  save/ move from #438). Re-run HnmoSaveFolderTests + JewelBankDatabaseImportTests + HnmoStateStoreTests after.
- #435 (feat/shared-coins-2pc): rebase on the same working branch and push so its CI re-runs on the current base.
Tell me on each PR when it's rebased and green; I merge.

## 2. Anti-crack hardening (heavy) — read docs/anti-crack-plan-2026-11-27.md first
Owner asked how to tighten the licence and lock out cracking. The plan has five layers. Start with these two; Layer 1
(hub-gated updates) I'll post the hub API shape for next round.
- Layer 3 — obfuscation: add a .NET obfuscator step to client/web/tools/package-hnmo.sh for the published server
  assemblies (HNMO.GameLogic, HNMO.Startup, HNMO.Persistence) in the self-contained publish path. Use a free
  obfuscator (e.g. Obfuscar) unless you hit a blocker. Then build a real packaged folder and launch it
  (start-hnmo.sh) to prove the server still starts, the admin panel answers, and a test account can log in. Draft PR
  into the working branch with the launch evidence. Do NOT obfuscate types EF Core / the plug-in loader resolve by
  name — exclude those namespaces and say which you excluded.
- Layer 2 — silent multi-point downgrade: when integrity is FileMismatch/SignatureInvalid, OR the hub withdrew, OR the
  hub has been unreachable past GraceDays, drop silently to 10 players (no loud "integrity failed" string). Re-derive
  the limit in at least two independent places besides Program.cs (the login path and the weekly service), each from
  the signed licence + integrity result. Fold HnmoBuildStamp into the weekly report. Add unit tests for each downgrade
  cause. Separate draft PR into the working branch, server tests for what you touched.
Keep storing nothing new about players (hub rule): licence id, machine id, build stamp only.

## 3. #439 (shorter connection timeout) — finish it
- Complete PlugInResources.zh-CN.resx: it has 2 of the 6 new entries; add the other 4.
- Add one test next to IdleConnectionTimeoutTests: a client that answers PingRequest stays connected past the idle
  timeout.
- Push (that also makes CI run — it has none yet).
- The real-game check still owed, on your PC through the web relay: a player idle 5 minutes, and a tab backgrounded
  2 minutes (browsers throttle timers there). Nobody should be kicked. If the background tab is kicked, raise the
  lost-connection timeout to 60s. Report on #439.

## 4. S9 chain — keep it green (your branches)
- #441 (feat/s9-systems-parity): e2e (4) failed — confirm which spec and whether it's the known flaky set or your
  diff; fix if it's yours. Report on the PR.
- #440: you pushed ba876788 for monster-looks; confirm the new CI is green and say so on the PR.
- #434: confirm e2e (4) there is the same flaky spec, not the loader diff.

## 5. PK rules of S6 and S9 (read-only, between heavy jobs)
Still owed from before: read the rule values (never code) for S6 (common config PK keys) and S9 (the 8-level PK
system file), per level give entry points/time, the threshold that keeps it, how it decays, and the level after the
1st/2nd/3rd/further kills. Answer plainly: after the second kill, how long does the character stay PK1 in S6 and in
S9? Write docs/audit/references/pk-rules-s6-s9.md and post the answer on #430. No code change.

Not yours: merging (mine); #436 (socket tooltip, I'm merging once its minimap rerun clears).
Report as before: per task what ran, what was verified and what wasn't, PR links, Sydney times.
```
