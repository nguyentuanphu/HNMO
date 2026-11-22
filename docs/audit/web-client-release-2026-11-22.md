# Web Client Release Verification & Notes (22 Nov 2026, Australia/Sydney)

Release readiness verification for item **#19** (`Web client release: package, browser checks, notes`) from `docs/main-todo-2026-11-21.md`, checked on a clean branch from `origin/claude/hnmo-main-handover-54co2m`.

## 1. Web Client Build & Localization Checks (PASS)

All steps from `.github/workflows/web-client.yml` ran cleanly and exited `0`:

| Check | Command | Observed Result |
| --- | --- | --- |
| Packet schema sync | `node client/web/tools/generate-packets.mjs server/src/Network/Packets && git diff --exit-code client/web/src/net/packets.generated.ts` | `ConnectServer: 11, ClientToServer: 246, ServerToClient: 420, ChatServer: 7`; zero diff |
| TypeScript strict check | `npm run typecheck --prefix client/web` | `tsc -p . --noEmit` & `tsc -p tsconfig.tests.json` passed (`0` errors) |
| Vietnamese coverage & placeholders | `python client/web/tools/lang/check-vi.py` | `Checked 10599 nonempty server resource entries and 8071 game/page translations. 0 coverage/format errors.` |
| Client & World Editor build | `npm run build --prefix client/web` | Built cleanly (`world-editor.js` restored to avoid unrelated binary noise) |
| Node unit tests | `npm run test:unit --prefix client/web` | **32 passed, 0 failed, 1 skipped** (`HNMO_S9_DATA_DIR is not set`) |

## 2. Browser Checks — Chromium via Playwright (PASS)

Executed offline/local-mocked Chromium Playwright specs against built `client/web/public` assets (`13/13` passed, `0` failures):

- `tests/e2e/auto-play-ultimate.spec.ts`: **3 / 3 passed** (`708ms`) — Free vs VIP policy gating, target priorities, anti-stuck watchdog.
- `tests/e2e/build-calculator.spec.ts`: **1 / 1 passed** — Stats / Build Calculator rendering and mocked server computation.
- `tests/e2e/vip-account-component.spec.ts`: **4 / 4 passed** (`en` + `vi`) — VIP account price review, idempotent retry, price change & logout guards.
- `tests/e2e/website-details.spec.ts`: **4 / 4 passed** — Schedule, rankings, drops (`en`/`vi`), offline error fallback, website registration & Google login link.
- `tests/e2e/website.spec.ts`: **1 / 1 passed** — Website localization & disconnect count cleanup.

Live Season 6/9 full-server gameplay playtests (`combat`, `rage-fighter`, `skill-cast`, `skill-effects`, `new-skills`, `quests`, `events`, `events-instance`, `trade`, `party`, `guilds`, `shop`, `inventory`, `chaos`, `season`, and Arena warp/respawn walk checks) were additionally run and documented in [playtest-2026-11-22.md](playtest-2026-11-22.md) and fixed by `aa9d3a23` (`Restore hero visibility and clear stale viewport objects on respawn`).

## 3. Release Packaging Check (`package-hnmo.sh win-x64 --no-data`, PASS)

Verified `client/web/tools/package-hnmo.sh` end-to-end packaging into a temporary folder (`HNMO_KEEP_WAV=1 HNMO_SKIP_KTX2=1`, `win-x64 --no-data`, deleted after check):

- **ItemEditor icons packed**: `8,383` icons (`46.05 MB`) -> `server/wwwroot/_content/HNMO.Web.ItemEditor/img/items.zip` (`46.92 MB`).
- **Static asset endpoint pruning**: `HNMO.Startup.staticwebassets.endpoints.json: 111 routes kept, 17098 without a file removed` (`server/wwwroot` reduced from `8,521` files / `73 MB` to `55` files / `52 MB`).
- **Web client bundling**: `esbuild` bundle succeeded.
- **Source leak guard**: No `.ts`, `.cs`, `.csproj`, or `.git` files leaked into output.
- **Update manifest**: `manifest.json: version 1.0.0, 174 files, 360 MB` (`server`: `70` files / `230 MB`, `web`: `94` files / `133 MB`, `licenses`: `2` files / `1 MB`, total `175` files / `362 MB` without game `Data/`).

## 4. Release Checklist & Notes for Main / Owner

1. Merge queued web/gameplay/docs PRs (`#280`–`#316`) into `claude/hnmo-main-handover-54co2m` before cutting the release commit to `main` (specifically client fixes `#300`–`#303`, `#313` rule texts, `#314` player announcement, `#315` Ultimate Autoplay quest trail/sparkles/live VIP sync, and `#316` parallel R2 upload helper).
2. Wire `tools/package/upload-r2-files.sh` into `.github/workflows/hnmo-package.yml` (per PR `#316`) so R2 uploads run 8-way in parallel.
3. Bump root `VERSION` (from `1.0.0` to the next release version, e.g. `1.0.1`) on the release commit to `main` so `.github/workflows/hnmo-package.yml` publishes `releases/<version>/manifest.json` and `HNMO.zip` without colliding with `1.0.0`.
4. Ensure GitHub Secrets (`R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET`, `HNMO_SIGNING_KEY`) are configured on the repository before publishing the signed production bundle to `main`.
