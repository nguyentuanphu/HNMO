# Prompt for Antigravity, 27 Nov 2026 17:25 Sydney (anti-crack approved; replaces the 17:10 queue)

Owner approved (27 Nov ~17:20): all four anti-crack layers, Obfuscar (free) for Layer 3, and proprietary headers on
HNMO-authored files (keep MIT on third-party). Rules stand: heavy/on-PC work is yours; draft PRs only, never main;
no other suite/original-game/publisher names; no keys/tokens/local paths or pack files in the repo; Sydney times.

Paste the block below.

```
From main cloud Claude, Fri 27 Nov 17:25 Sydney. Merged since your 16:53 update: #433 (b62f7b84), #435 (6ca6620f).
#437 already merged. #438 is blocked only by a branch conflict. Read docs/anti-crack-plan-2026-11-27.md. Queue:

## 1. Rebase so I can merge (do first — small)
- #438 (fix/state-into-save): rebase on origin/claude/hnmo-main-handover-54co2m and push. Conflict is likely in
  JewelBankDatabaseImportTests.cs and the state-store files — keep both sides (the #423 DB import AND the save/ move).
  Re-run HnmoSaveFolderTests + JewelBankDatabaseImportTests + HnmoStateStoreTests. Tell me on the PR when green.

## 2. Anti-crack — owner approved all four layers. One draft PR per layer, server tests for what you touch.
LAYER 1 — hub-gated updates (the real lock-out). Two PRs: one into the hnmobug branch, one into the HNMO working branch.
  Hub (hnmobug): add POST /api/update-check. Request { licenceId, machineId, buildStamp, licence(base64 of the
  licence file) }. The hub verifies the licence Ed25519 signature with the public half of the build/licence key
  (store that public key in Netlify env + Actions secret, NEVER the private key), checks the licence is not in the
  withdrawn list, and that machineId matches the licence (or its legacy id). Response 200 { manifestVersion,
  files:[{path, sha256}], downloadBase, token } — a short-lived token / signed URLs for the CURRENT build + pack.
  403 when invalid/withdrawn. Store only licenceId, machineId, buildStamp, lastSeen (plus the setup IP/country you
  already keep) — no player data. Reuse HUB_KEY/HUB_SALT style; add npm ci && npx tsc -p . && npm test.
  Server (Updater.cs + DeveloperHub.cs): before fetching the update manifest, call /api/update-check with this
  folder's licence; only fetch/apply the files the hub returns. No licence / invalid / withdrawn / hub unreachable →
  stay on the current files, never crash, never wipe. Tests for: valid → manifest applied; withdrawn → no update,
  server keeps running; unreachable → keeps running.
LAYER 2 — silent multi-point downgrade. When integrity is FileMismatch/SignatureInvalid, OR the hub withdrew, OR the
  hub has been unreachable past DeveloperHub.GraceDays, drop silently to 10 players (no loud "integrity failed"
  string). Re-derive the limit in at least two independent places besides Program.cs (the login path LoginAction.cs
  and the weekly service), each from the signed licence + integrity result. Fold HnmoBuildStamp into the weekly
  report. Unit test each downgrade cause.
LAYER 3 — Obfuscar. Add an Obfuscar step to client/web/tools/package-hnmo.sh for the published server assemblies
  (HNMO.GameLogic, HNMO.Startup, HNMO.Persistence) in the self-contained publish path. EXCLUDE namespaces/types that
  EF Core and the plug-in loader resolve by name (entities, migrations, plug-in point/strategy types, packet
  handlers) — list what you excluded. Then build a real packaged folder and launch it (start-hnmo.sh): prove the
  server starts, the admin panel answers, and a test account (test0) logs in. Draft PR with the launch evidence.
LAYER 4 — clone detection (hub only, passive). Store the first-run fingerprint per licence; when the same licence
  reports from two unrelated fingerprints/IPs, flag it in the hub (no action on the host, no player data). hnmobug
  branch, with tests.

## 3. Proprietary headers (owner approved). Conservative scope — do NOT strip third-party notices.
Replace the MIT line in the header ONLY on clearly HNMO-authored files, starting with the licence/integrity/hub code
(server/src/Integrity/*.cs, server/src/GameLogic/DeveloperHub*.cs, LicensedPlayerLimit.cs and the Program.cs licence
path). New header body: "Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or
distribution is prohibited." KEEP company="HNMO". Leave the repo LICENSE file and every third-party / vendored file
(anything not HNMO-authored, e.g. derived Pathfinding code) exactly as is. List every file you changed, and list any
file you are UNSURE about without touching it — I'll take those to the owner. No behavior change; build must stay green.

## 4. #439 (shorter connection timeout) — finish it
- Complete PlugInResources.zh-CN.resx (2 of 6 new entries → add the other 4).
- Add a test next to IdleConnectionTimeoutTests: a client that answers PingRequest stays connected past the idle
  timeout. Push (that also makes CI run).
- Real-game check owed on your PC via the web relay: a player idle 5 min, and a tab backgrounded 2 min (timers
  throttle). Nobody kicked. If the background tab is kicked, raise the lost-connection timeout to 60s. Report on #439.

## 5. S9 chain — keep green (your branches): #441 e2e(4) — which spec, yours or flaky? fix if yours. #440 — confirm
ba876788 CI green. #434 — confirm e2e(4) is the same flaky spec, not the loader diff. Report on each PR.

## 6. PK rules S6/S9 (read-only, between heavy jobs): per earlier — write docs/audit/references/pk-rules-s6-s9.md,
answer on #430 how long the character stays PK1 after the 2nd kill in S6 and S9. No code change.

Not yours: merging (mine); #436 (I'm merging once its minimap rerun clears).
Report per task: what ran, what was verified and what wasn't, PR links, Sydney times.
```
