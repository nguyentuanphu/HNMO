# Prompt cho Antigravity (27 Nov 2026, 05:00 Sydney). Dán nguyên phần dưới dòng kẻ.

---

You are Antigravity working on HNMO (repo nguyentuanphu/HNMO, base branch `claude/hnmo-main-handover-54co2m`).
Rules: read `CLAUDE.md`, `server/AGENTS.md`, `server/docs/CODING_RULES.md`, `docs/owner-decisions.md`. Draft PRs only into
the base branch (or into the branch named below); never merge, never touch `main`; one draft PR per area; never name the
original game, its publisher, other server suites or their file prefixes; no keys or local paths in the repo; player
texts in resx (EN + VI); config data = initializer + update plug-in (rule 4) with its three tests. Run the tests of what you
touched, then report per task: SHA, commands, pass/fail/skip, what was not run. Times in Sydney time.

Main (cloud Claude) reviewed your PRs on 27 Nov 04:40. Main is building Muun, Pentagram/Errtel and the S9 skills 618-621
(branches `feat/s9-muun`, `feat/s9-pentagram`, `fix/s9c-gameplay`): do NOT touch those areas.

## A. Fix your open PRs (CI red for real)
1. #405 (Chaos Card mixes): Initialization tests fail: `FreshDatabaseContainsCashShopAsync` and
   `UpdateAssignsDeliveryItemsToExistingCatalogAsync` (expected 60 cash shop entries), `Season6ItemCountsPerProfile`
   (the per-profile item count list changed). Fix the data or update the expectations with the reason in the commit.
2. #411 (master passives 314-324): `Season6Async`, `Season6AfterTheUpdateAsync`,
   `Season6WithRenamedTablesAndOwnTablesAsync`: "4312 differences in 287555 requirement, 171588 power-up and 449758
   price cases". Find what changed the Season 6 reference data; the update plug-in must give the same result as a fresh
   database.
3. #422 (S6 remaining events): `Season6ItemCountsPerProfile`, `SeasonNineModeAddsSeasonNineMonstersAndUpdatesDarkElfAsync`
   (expected 623), `SeasonNineModeAddsSeasonNineItemsAsync` (expected 1115). Fix or update with reasons.
4. #367 (Autoplay), #382 (HUD sweep), #416 (episode quests): e2e shards red. For each failing spec, run it locally 3 times
   on your branch and on the base branch; fix the ones your PR causes; list the others as "also red on base".
5. #381: reconcile `Terrain7.att`: take the base branch's file (its Arena gate 101,116 passes `s9-arena-walk.spec.ts`),
   keep the rest of #381, rerun `s9-arena-walk.spec.ts` and `monster-looks.spec.ts`.
6. #401/#402/#403: no new commits there. They are merged together in #419 (`integration/s9`); push follow-ups to
   `integration/s9` only if main asks.

## B. New work
7. Desktop client build: on the latest base branch, fix `InGameShop.cpp` (keys `YouCanOnlyHNMOItemShopInATownOrSafeZone`,
   `CannotHNMOItemShopPleaseReconnectToTheGame` vs the names in `Game.h` and the resx), build windows-x64 with
   BUILD_TESTING=ON and run the full ctest. One draft PR "desktop build fixes" with the results.
8. `small-ui-feedback.spec.ts:28`: raise the `30_000` wait on `.vault-zen-dialog #panel` to the default 60 s, and look
   at why the first `openDocument('generic_confirm_dialog')` after entering the world takes so long (RML sprite decode);
   fix it if it is a real slowness. Draft PR with before/after timings.
9. Hardware GPU FPS (d3d11, not SwiftShader): measure before/after #381 with the same 15 s AOE scene; post on #381.
10. Shared coins (follow-up of #407): durable coordinator between the Season 6.3 database (it owns the balances) and a
    Season 9 database. Design to build: PREPARE TRANSACTION on the canonical database, the local decision written in the
    same transaction as the local entitlement, a realm id, a recovery pass with a lock that resolves prepared
    transactions from the durable decision and keeps them pending while the outcome can't be read; a fee the shared
    balance can't cover is refused in full; check whether Arena uses the same store. Build it on top of #423
    (`fix/asset-db-transactions`, the asset receipts and the PostgreSQL SIGKILL harness in
    `server/tests/asset-crash-harness`): reuse the receipt and extend the harness to kill before/after local commit,
    PREPARE and canonical commit. Two PostgreSQL databases on loopback. One draft PR into `fix/asset-db-transactions`.
11. Admin panel Jewel Bank page: show the history from the database receipts (`AssetOperation`) instead of the JSON log,
    with the import dry-run report (`JewelBankDatabaseImport.RunAsync(..., dryRun: true)`) and a button to run the
    import. Stack on #423; draft PR into `fix/asset-db-transactions`; admin texts in the four admin languages.
12. Arena with the Season 9 pack: run a full Arena cycle (queue, battle, respawn, rewards) on `integration/s9` and post
    the results on #419.

Report when done, or before your usage runs out (push what you have).
