# Antigravity Windows Verification Report (2026-11-20, Australia/Sydney)

- **Branch:** `antigravity-win-check` (branched from `origin/claude/hnmo-main-handover-54co2m` at `5c6f6a7c`, since PR #85 is not yet merged into `origin/main`)
- **Host:** Windows (`win-x64`)
- **Rules observed:** Read-only testing & reporting only (no code edits, no third-party names or files committed).

---

## STEP 1 — Package and Start

### 1.1 Package Build (`win-x64`)
- **Command:**
  ```bash
  export POSTGRES_DIR="/c/Users/nguye/.gemini/antigravity/scratch/pg-portable-cache"
  client/web/tools/package-hnmo.sh "/c/Users/nguye/.gemini/antigravity/scratch/HNMO-int/server" win-x64 "/c/Users/nguye/.gemini/antigravity/scratch/HNMO-pkg"
  ```
- **Build time:** `403` seconds (`6m 43s`)
- **Folder size:** `1,284 MB` (`1.3G` via `du -sh`), `1,846` files (`Data`: 50 packs / 794 MB; `server`: 67 files / 234 MB; `web`: 93 files / 133 MB; `postgres`: 1,631 files / 125 MB; `licenses`: 2 files / 1 MB).
- **Last output lines:**
  ```text
    Data before pack: 14891 files, 1066 MB -> after pack: 50 files, 794 MB
    server/wwwroot before: 8279 files, 69 MB -> after: 54 files, 49 MB
    TOTAL                  1846    1284 MB
  Done: /c/Users/nguye/.gemini/antigravity/scratch/HNMO-pkg
  BUILD_SECONDS=403
  1.3G	/c/Users/nguye/.gemini/antigravity/scratch/HNMO-pkg
  ```

### 1.2 Startup (`Start HNMO.bat`)
- **Command:** `cmd /c "Start HNMO.bat"` inside `HNMO-pkg` (`HNMO_WINDOW=1`, `HNMO_NO_BROWSER=1`)
- **Time until HTTP 200:**
  - `http://127.0.0.1:8080/` (game): **`2.06s`** (HTTP `200`)
  - `http://127.0.0.1:8095/` (admin panel): **`53.90s`** (HTTP `200`)

### 1.3 Packaged Smoke Set + `cash-shop.spec.ts`
- **Command:**
  ```powershell
  $env:BASE_URL = "http://127.0.0.1:8080"
  npx playwright test tests/e2e/character.spec.ts tests/e2e/chat.spec.ts tests/e2e/control.spec.ts tests/e2e/inventory.spec.ts tests/e2e/quests.spec.ts tests/e2e/shop.spec.ts tests/e2e/touch-login.spec.ts tests/e2e/touch.spec.ts tests/e2e/cash-shop.spec.ts --workers=1 --trace off
  ```
- **Summary:** **12 passed, 3 failed** (`11.6m`)
  - `cash-shop.spec.ts:48` — **PASS** (`55.2s`)
  - `character.spec.ts:14` — **PASS** (`38.3s`)
  - `chat.spec.ts:25` — **PASS** (`1.4m`)
  - `control.spec.ts:14` — **FAIL** (`12.8s`): `page.goto: net::ERR_CONNECTION_REFUSED at http://127.0.0.1:18088/control` (isolated dev bridge spawned by `control.spec.ts` on `18088` did not answer before `page.goto`)
  - `inventory.spec.ts:11` — **PASS** (`12.9s`)
  - `inventory.spec.ts:27` — **PASS** (`22.4s`)
  - `inventory.spec.ts:50` — **PASS** (`37.2s`)
  - `inventory.spec.ts:74` — **PASS** (`45.0s`)
  - `quests.spec.ts:52` — **FAIL** (`1.8m`): `expect(locator('.rml-my_quest_info #panel')).toBeVisible()` timed out (`90000ms`) after `page.keyboard.press('t')`
  - `quests.spec.ts:123` — **PASS** (`29.5s`)
  - `quests.spec.ts:149` — **PASS** (`42.0s`)
  - `shop.spec.ts:19` — **PASS** (`56.4s`)
  - `touch-login.spec.ts:11` — **PASS** (`40.7s`)
  - `touch.spec.ts:12` — **FAIL** (`1.1m`): `touchscreen.tap(760, 420)` did not change `g.online.hero.cell` within `30000ms`
  - `touch.spec.ts:46` — **PASS** (`39.6s`)
- **Last output lines:**
  ```text
    3 failed
      tests\e2e\control.spec.ts:14:3 › /control page logs, retention, backups, and disk usage › renders disk usage, searches active + gzipped logs, saves settings, backs up and restores in EN + VI
      tests\e2e\quests.spec.ts:52:1 › talks to an NPC, accepts a quest and finds it in the quest window
      tests\e2e\touch.spec.ts:12:1 › touch: the button bar, a tap walks, two fingers zoom and turn
    12 passed (11.6m)
  ```
