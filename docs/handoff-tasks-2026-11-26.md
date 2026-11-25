# Tasks left by the website agent (26 Nov 2026)

Three tasks the owner passed on when ChatGPT ran out of usage. None is done. Common rules: never push to the working
branch or `main`, never merge, deploy or force-push (main merges after review); work in an isolated worktree with fake
data, never production accounts, databases or keys; code, commits and PRs in English, reports to the owner in
Vietnamese, Sydney time; say what was run and what was not, never "done" for something not run.

## 1. Take over draft PR #384: HNMO easy to start on macOS and Linux

PR https://github.com/nguyentuanphu/HNMO/pull/384 (branch `codex/desktop-installers`, head `422e4a52`, base the working
branch at `8cb8539b`). Packaging CI run: https://github.com/nguyentuanphu/HNMO/actions/runs/45716182137.

Goal (the owner's): a person who does not know the technology can run HNMO on Mac and Linux. macOS: unzip
`HNMO-macos.zip` and double-click `Start HNMO.app`. Ubuntu / Debian: open `HNMO-linux.deb` with the app installer, then
open HNMO from the menu. Both packages carry .NET and PostgreSQL 16, start the server by themselves, open the browser and
keep the data between runs. The Mac app is not signed or notarized; Gatekeeper may ask once.

What the author ran: Linux x64 .deb opened from the app menu (game and admin pages answered, launcher ready, bundled
PostgreSQL 16.16 ran, psql and pg_dump tried); web build, 72 web unit tests (1 skipped), .NET launcher 34/34;
Playwright 28/28 on the development setup and 27/28 on the Linux package (the failing one imports the development module
`/js/websiteTheme.js` directly, which the packaging step also drops on the base); the packaging step on the base
reproduces 35 missing JavaScript references and the PR creates all 21 bundles for 12 pages. Not verified: macOS start,
Linux ARM64, Windows with the new package. Not released.

What main found on 26 Nov in that run: package, Linux x64, Windows and the packaged PostgreSQL jobs pass; `unix-start
(macos-15)` (Apple Silicon) fails; `unix-start` on Linux ARM64 and Mac Intel were cancelled. In the macOS job's log the
game (8080) and admin (8095) pages did answer, the launcher log says `PostgreSQL: .../postgres/osx-x64/bin`, and
`client/web/tools/hnmo/smoke-unix-package.sh` then looks for `postgres/osx-arm64/bin` and exits 1. So on Apple Silicon
the package started the Intel PostgreSQL. Find whether the zip has no arm64 PostgreSQL or the launcher chooses wrongly.
An Intel PostgreSQL on Apple Silicon needs Rosetta, which a fresh Mac may not have: that is what a non-technical user
would meet; fix it so Apple Silicon gets a native build, or say clearly what is missing.

To do:
1. Fetch and check head, base and the latest CI runs of #384; continue on the existing PR (no duplicate PR).
2. Confirm the real packages start: Finder on Mac Intel and Apple Silicon, the menu on Linux x64 and ARM64, the Windows
   launcher. Check the bundled PostgreSQL, the game and admin pages, and that the data is intact after reopening.
3. If CI is red, read the right job, compare the same case on the base, fix what the PR caused, keep the tests as strict.
   Write commands, pass / fail counts and what was not verified into the PR.
4. When there is enough evidence, tell the owner in Vietnamese: which build can be downloaded and tried, the conditions
   for a release, and the signing / notarization state of the Mac app.

Details if the old workspace is inherited: `/workspace/shared/hnmo-main-handoff-2026-11-25.md` and
`/workspace/scratch/hnmo-desktop-{package-final,base-bundle,dev,browser,launcher,unit}.log`.

## 2. Full audit and integration test: PRs #373, #376, #377 applied together

PRs: https://github.com/nguyentuanphu/HNMO/pull/373, /376, /377. Heads used by the earlier audit (all still the heads on
GitHub on 26 Nov): base `8cb8539b1f16959728446a75379aef241cdd5c5a`; #373 `49b959acea1d085b88e73bbd12406333e42f0775`;
#377 `39443f938639e0f3abb2e87fef5914282a9f7bc5`; #376 `478f9cf30ba278af5f41921ec4cf2b1c9305fc33`. Build in the order
#373, #377, #376; if a head changed, record the SHA used and re-check the patch. The earlier pass applied all 24 changed
files cleanly but did not build or run tests.

Apply `hnmo-economy-integration-regression.patch` (if not attached, look for the file name in the ChatGPT Library). Read it,
compare with the existing tests, add the missing cases. When fixing a fixture or test keep the assertions on balances,
ownership and idempotency. Only test / harness changes; if production code is wrong, give file:line and a proposed fix.

Trace and test each flow:

A. WCoin between Season 9 and Season 6.3. `PlayerPersistence.SaveProgressAsync` and `SeasonSharedAccounts.MirrorAsync` /
`SyncCoinsAsync` (the read version synced coins to the shared store before the local commit). Two saves at once, two
processes on one account, a new login, a deposit from the other season in the middle of a fee. Simulate: the shared
store writes but the local DB fails; the shared store commits but the reply is lost; a restart before the retry.
Reconcile WCoinC, WCoinP, GoblinPoints and receipts. ClearMaster: a deposit from the other season survives; no fee or
receipt for the failed operation; skills and master points unchanged on the next save. Check Market Access and Stack
Unlock separately (fee and benefit).

B. Pickup -> bag -> JewelBank -> withdraw or claim from the web. Drop ownership, locked items, two pickups of one stack,
stack cap, full bag. Single Chaos, stacked Chaos and group-12 jewel bundles across inventory, JewelBank and Economy.
Deposit a whole stack; a part when the cap is hit; withdraw a part when the bag is full; claim a pending withdrawal from
the web. Check that `GiftCodeRewards.GiveGoodsAsync` gives the right number of pieces when JewelBank withdraws a
stackable jewel (in the version read, `GiftCodeItem.Durability` defaults, so the reward could take the item
definition's full durability). Inject faults before and after the JewelBank write, when the character save returns
`false`, when it throws, then restart and retry. Reconcile jewels in the bag, the bank, the queue and the Zen fee.

C. Mixing and exchanging stacked materials. Amounts consumed across several slots, the remainder kept, Season Plan caps,
locked materials. Rewards: items, Zen, coins, with room in the bag and with the pending-reward path. Inject a fault
between saving the materials / Zen and writing the reward; after a new login and retry: no reward from the failed
transaction, no committed reward lost, no double delivery.

D. Trade, vault, temporary storage. `Storage.TryTakeAllAsync` when the target returns `false`; when an exception comes
after one item moved; when returning the item to the source slot fails. A trade cut off or a save error before and
after the ownership commit: re-read both players from a new context. Reconcile serial, owner, slot, stack count and
total Zen: every item has exactly one owner after commit or rollback.

Run (from `server`, .NET 10 SDK):

```
dotnet build tests/HNMO.Tests/HNMO.Tests.csproj -p:ci=true
dotnet test tests/HNMO.Tests/HNMO.Tests.csproj --no-build -p:ci=true --filter 'FullyQualifiedName~EconomyIntegration'
dotnet test tests/HNMO.Tests/HNMO.Tests.csproj --no-build -p:ci=true
```

If the environment allows, also two test databases and two processes for restart, lost reply and retry. For the
persistence cases the deciding numbers must be re-read through a new context or process after the fault; assertions on
objects still in RAM are only extra.

Hand over: (1) findings by severity with file:line, SHA, the assets affected and the test that proves each;
(2) the missing test / harness patch, applying to the integration build; (3) commands, pass / fail / skip counts and
the reason for every limit, telling code-reading findings from bugs reproduced by a test; (4) whether the three PRs are
fit to merge and what must be fixed first.

## 3. Full audit and test of PR #381

PR https://github.com/nguyentuanphu/HNMO/pull/381, branch `fix/monsters-effects-s9-arena-layout`, commit used for the
patch `4aac18da8343ee85a16d62e0a23bdff4162ca16d`, patch `HNMO-PR381-in-game.patch` (re-create it from the PR if it is
missing). Verify the four groups of change on the right build: lost character parts, Season 9 NPCs, skill effect lag and
memory, the Arena terrain between client and server. Actually run the tests the environment allows and record each result.

1. Versions and patch. Read the PR diff, `AGENTS.md`, the E2E guide and the fixtures. Record the PR's current HEAD SHA that
   is really tested. Use a separate local worktree; `git apply --check` before applying the patch; if HEAD changed, compare
   and update the patch. Review the fixtures (login, S6.3 / S9 / Arena worlds, demo accounts, GM `/move`, the S9 data
   pack, the server's TerrainData source) for tests that could pass wrongly: model fallbacks, client-side prediction of
   effects or movement, comparing with files on disk that differ from the data the server uses.
2. Offline. Typecheck, build and the related unit tests (`monsters-effects-arena.test.mjs`, `season9-monsters.test.mjs`
   with the S9 pack, the .NET Arena tests if any). The offline part of `monster-looks.spec.ts` through the local bridge:
   number of models, allowed fallbacks, meshes without texture, parent bones with a larger index than their child, Arena
   object count. The PR reported 555 models, 5 offline Playwright passes, 1 in-game test skipped and a burst of
   0.44 ms / frame: record the numbers measured in this run separately.
3. In-game Playwright on a local server. Build S6.3, S9, Arena and the bridge from one checkout as in
   `client/web/tests/e2e/README.md`; demo accounts, an isolated save, loopback only. From `client/web`:
   `IN_GAME=1 BASE_URL=http://127.0.0.1:8081 npm run test:e2e -- pr381-in-game.spec.ts --workers=1 --reporter=list,html`;
   and the Dungeon skeleton test of `monster-looks.spec.ts` with `IN_GAME_ACCOUNT=test300`.
   - Character: all 5 parts of the Rage Fighter, above all the hair of `HelmClass07.bmd`; more equipment models if the
     fixture has them.
   - S9: ids 580, 581, 583, 584, 604, 643 and 689 come from the server, with the right BMD, a visible mesh and texture,
     no ghost fallback; note Jin #604 on map 92 by the server seed.
   - Effects: with an observer client confirm the Twisting Slash packet from the server, the weapon copies drawn,
     released and reused; if the fixture allows, add a BMD effect or burst case for the pose cache / prewarm path.
   - Performance: FPS and p95 frame time idle and during effects, JS heap after GC, geometry / texture / program counts
     before and after, the real renderer. With a real GPU also run `PR381_GPU=1` headed for the 30 FPS and p95 <= 50 ms
     thresholds.
   - Arena: map 6 loads World7 with 5,169 objects (15 instances of Object76); compare the 65,536 cells with the running
     instance's TerrainData if it can be exported; spawn position, safe zone flags, and one step a second client sees
     through the server.
4. Arena Home. At `4aac18d` the offline check showed client and server resources agree on all 65,536 cells, but all 36
   Home cells (x 95-100, y 105-110) have flag `0x00`: walkable, no `SAFEZONE`, while the PR says Home is safe. Re-check
   at HEAD and on the local instance. Find whether the server uses terrain flags or a separate Home protection rule; if
   separate, prove it by behaviour in game with a demo account. Say whether the cause is the data, the runtime, the
   fixture or the PR description. Keep the assertion until the right criterion is proven.

Hand over a PASS / FAIL / SKIP / BLOCKED table per case with SHA, commands, logs or Playwright report, measured numbers
and the reason for each fail or blocked case; for a confirmed bug the steps, expected / actual and the files; fixtures
or tests may be fixed in the worktree and rerun; game or data bugs go as a proposal or a separate patch for review.
If the .NET SDK, the S9 pack, a GPU or a fixture is missing, continue with what can run and say what is blocked.
