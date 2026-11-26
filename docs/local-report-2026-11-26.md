# Local Claude report (26 Nov 2026, Sydney time)

Run on the owner's Windows PC (software WebGL, one heavy job at a time). Working branch head used: `99977d5e`.
Draft PRs from this run: #385 (base fixes), #387 (economy tests, stacked on #377), #389 (`HNMO_SEASON_COLUMN`).

## 1. Red browser tests of the base branch (PR #385)

| Test | Reproduced | Cause | State |
|---|---|---|---|
| `sound.spec.ts:28` | yes, 3/3 | Audio code is correct (unlocked, context running, music playing). The first `page.evaluate` after the tab is visible again took 14.5 s (dynamic `import()` while the page drew ~6 frames/s); the poll allowed 10 s. | test fixed in #385, 6/6 green |
| `walk-stall.spec.ts:25` | yes | The poll that waits for "start + 2 cells" returned late (hero at x=202 of 206), so the 3 s hold was worth 4 cells. | measurement moved into one page call, the >= 6 cells check is green; the final `walkAndWait(END)` is red 3/3: server log `Client reported a walk position "191, 127" which is 2.0 tiles away from the server position "189, 127", exceeding the allowed offset of 1.8` then `Speedhack detected ... 9.1 tiles` (`PlayerMovement.cs:547-553`, `WalkSpeedCheck.cs:172`). Needs a CI run or a quiet machine to tell load from a real lag problem. |
| `walk-resume.spec.ts:26` | once, then green 2/2 | probable: a correction counts as "after the next part" only within 2 s or 3 frames (`online.ts:256`); a starved page can miss it. Not proven. | untouched |
| `website-information.spec.ts:12` vi | yes | the init script reset `hnmo.language` to `vi` on every navigation | #368's spec passes; #368 is the fix |

## 2. #384 (installers)
- Apple Silicon started the Intel PostgreSQL: `GameLauncher.FindPostgres` picks the folder by `RuntimeInformation.ProcessArchitecture`; the shell under Rosetta reported x86_64, so `start-hnmo.sh` chose the `osx-x64` runtime. The zip carries both. Fixed by others in `1407df7b` (hardware check with `sysctl hw.optional.arm64`) and `226312f9` (the app sealed as a whole; the stub signed alone gave "code has no resources but signature indicates they must be present" on Mac Intel).
- CI of head `ef405eb0`: package, Windows start, Ubuntu x64, Ubuntu ARM64 green; Mac Intel red (the signature error above); Mac Apple Silicon cancelled. Head `226312f9` was still running when this was written.
- Windows with the #384 package (head `226312f9`, built by hand: portable PostgreSQL 16.9 added to the folder, `PostgresPath` in `web/appsettings.json` changed from the Linux path to `../postgres/bin` because the folder was built without `POSTGRES_DIR`): launcher log `PostgreSQL: <folder>\postgres\bin`, game 8080 and admin 8095 answer 200, data kept after reopening (a coin value written with psql was read back after a restart).
- Needs a Mac or a Linux desktop (owner): Finder launch on Mac Intel and Apple Silicon, app menu on Ubuntu/Debian x64 and ARM64; the Mac app is not signed or notarized.

## 3. #381
- Arena Home (x 95-100, y 105-110): all 36 cells have flag 0x00 in `Terrain7.att` (row-major, header 3 bytes; the transposed reading gives 0x04 = blocked, so the layout is right). Client and server files agree. `ArenaConfiguration` only uses Home as the standing/respawn rectangle ("the map's own gates lie partly on blocked tiles"); no Home protection rule in `ArenaRules`/`ArenaConfiguration`. Cause: the PR text ("walkable/safe") is wrong; the data is consistent. Behaviour in a fight at Home not tested.
- In a running Arena (`HNMO_SEASON_COLUMN=arena`, test300): spawn map 6 at (96,108), flag 0 from the client's terrain, 4 cells east and 4 south walk fine. Respawn after death not tested.
- Season 9 world (data pack, packaged folder of #384, Season 9 and Arena switched on): 11 suites, 13 passed, 2 failed. `movement.spec.ts:42` flaky (green on rerun in S6.3 and S9). `inventory.spec.ts:74` failed twice right after the first start of the Season 9 server and then passed 4 of 4: the spawn (146,110) is identical in both databases, the client lists and the model file are the same, the hero reaches (144,113), and the NPC shows 10-20 s after arrival. No data gap; not reproduced (corrected on 26 Nov afternoon).
- Not run: skill sweep of every class in S9, S9 `monster-looks` in-game cases, #381's own in-game spec, FPS/heap measurements.

## 4. Desktop client
- No C++ toolchain on the PC at the start; CMake installed, Visual Studio Build Tools C++ install started (UAC accepted by the owner), not finished when this was written. The C++ client and its tests were not built.
- .NET part (`ClientLibrary`, release): builds, 0 errors. Self-checks: ConnectFailure, ManagedLog, OutboundSender, BindingGenerator (with its three arguments) pass; **DeferredTcpPipe fails 3/3** at `tests/network/deferred_tcp_pipe_self_check/Program.cs:137` (`ClosingAnEncryptedConnectionSendsItsLastPacket`, timeout). `ClientLibrary` and that folder have no commits since 21 Nov, so this is not a new rule; compare with CI before treating it as a bug.

## 5. Economy integration of #373 + #377 + #376
- Merged in that order on `99977d5e`, no conflicts; heads 49b959ac, 39443f93, 478f9cf3 (the SHAs of the handoff). `hnmo-economy-integration-regression.patch` was not available.
- Full server suite on that build: 3840 passed, 0 failed, 2 skipped (the log also printed 4 "Skipped" lines; not reconciled).
- #387: `EconomyIntegrationCoinTests` (3 tests, pass): a failed shared write is retried and applied once; a deposit from the other season survives a pending fee; after a lost local save the shared balance wins on the next login.
- Finding by reading (not proven by a test): `PlayerPersistence.SaveProgressAsync` (`PlayerPersistence.cs:70-71`) writes the coin delta to the shared store first and saves locally second; the baseline moves at `SeasonSharedAccounts.cs:109` without knowing the local save result. A failed local save or a restart in between leaves the fee paid and the receipt/benefit lost. Proposed: advance the baseline only after the local save returns true, or save locally first.
- Suspicion to check (not verified): JewelBank withdraw (`JewelBankPlugIn.cs` around the two `GiveGoodsAsync` calls) builds `GiftCodeItem` with `Durability` 0; for a stackable item `GiftCodeRewards.cs:140` then gives the definition's full durability, so a withdrawal of 1 may give more than 1 piece if a stackable jewel definition has Durability > 1.
- Not covered: pickup -> bag -> JewelBank, stacked material mixing, trade/vault rollbacks.

## What only the owner can do
Real launches on Mac Intel, Mac Apple Silicon, Ubuntu/Debian x64 and ARM64; Apple signing/notarization; Q1-Q13 of `docs/audit/findings/questions-for-owner.md`.
