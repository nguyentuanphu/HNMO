# Antigravity Windows Verification Report (2026-11-20, Australia/Sydney)

- **Branch:** `antigravity-win-check` (branched from `origin/claude/hnmo-main-handover-54co2m` at `5c6f6a7c`, since PR #85 is not yet merged into `origin/main`)
- **Host:** Windows (`win-x64`)
- **Rules observed:** Read-only testing & reporting only (no code edits, no third-party names or files committed).

---

## STEP 1 — Package and Start

### 1.1 Package Build (`win-x64`)
- **Command:**
  ```bash
  export POSTGRES_DIR="<scratch>/pg-portable-cache"
  client/web/tools/package-hnmo.sh "<scratch>/HNMO-int/server" win-x64 "<scratch>/HNMO-pkg"
  ```
- **Build time:** `403` seconds (`6m 43s`)
- **Folder size:** `1,284 MB` (`1.3G` via `du -sh`), `1,846` files (`Data`: 50 packs / 794 MB; `server`: 67 files / 234 MB; `web`: 93 files / 133 MB; `postgres`: 1,631 files / 125 MB; `licenses`: 2 files / 1 MB).
- **Last output lines:**
  ```text
    Data before pack: 14891 files, 1066 MB -> after pack: 50 files, 794 MB
    server/wwwroot before: 8279 files, 69 MB -> after: 54 files, 49 MB
    TOTAL                  1846    1284 MB
  Done: <scratch>/HNMO-pkg
  BUILD_SECONDS=403
  1.3G	<scratch>/HNMO-pkg
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

---

## STEP 2 — Audit Fixes Checked by Hand

### 2a. Trade & Chaos Goblin Window Blocking (Two Browsers: `test1` & `test2`)
- **Trade check** (`scratch/step2-trade-and-market.mjs`):
  - Opened trade between `test1` (`test1Dk`) and `test2` (`test2Dk`) in Lorencia (`(147, 118)` / `(146, 121)`).
  - Triggered `window.__jb?.open?.()`, `window.__market?.open?.()`, `window.__mc?.open?.()`, and sent `/code TEST123` while trade was open:
    - Result: `jbOpen: false`, `mktOpen: false`, `mcOpen: false` (windows stayed closed), and chat displayed `"Close the trade or crafting window before using a command."`.
  - Cancelled trade and compared full bag snapshots for both accounts: `bagAEqual: true`, `bagBEqual: true`.
  - Screenshot: [step2a-trade-block.png](screenshots/antigravity-2026-11-20/step2a-trade-block.png)
- **Chaos Goblin check** (`scratch/step2-abde.mjs`):
  - Warped `test1` to Noria (`/move noria`), talked to Chaos Goblin (`238`), opened Regular Combination (`mixOpen: true`).
  - Triggered Jewel Bank, Item Market, Mount Chain, and `/code TEST123`:
    - Result: `jbOpenMix: false`, `mktOpenMix: false`, `mcOpenMix: false`, and chat displayed `"Close the trade or crafting window before using a command."`.
  - Screenshot: [step2a-chaos-block.png](screenshots/antigravity-2026-11-20/step2a-chaos-block.png)
- **Verdict:** **PASS**

### 2b. Host-Only `/control` Access
- **Checks:**
  - Local `http://localhost:8080/control`: HTTP **`200`** (renders HNMO Control setup & actions). Screenshot: [step2b-control-local.png](screenshots/antigravity-2026-11-20/step2b-control-local.png)
  - Non-loopback LAN IP `http://192.168.5.27:8080/control` and `http://192.168.5.27:8080/api/control/status`: HTTP **`403`**.
- **Verdict:** **PASS**

### 2c. Admin Panel `/worlds` (Season 9 World Creation, Restart, Badges, Rate Isolation)
- **Result:** **BLOCKED / FAIL on packaged `8095` Admin Panel**
  - `http://127.0.0.1:8095/worlds` renders unstyled HTML and fails to start its interactive Blazor Server circuit:
    - Console errors:
      - `PAGEERR: System is not defined`
      - `Failed to find a valid digest in the 'integrity' attribute for resource 'http://127.0.0.1:8095/_content/HNMO.Web.AdminPanel/Components/Layout/ReconnectModal.13ja33weya.razor.js' with computed SHA-256 integrity '47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU='. The resource has been blocked.`
    - Cause observed on disk: `HNMO-pkg/server/wwwroot/_content/HNMO.Web.AdminPanel/Components/Layout/` contains `ReconnectModal.razor.js` (`2,364` bytes, un-fingerprinted), whereas the rendered HTML requests `ReconnectModal.13ja33weya.razor.js` (missing -> empty/404 SHA-256 `47DEQpj...`).
    - Because the circuit never connects, clicking `Create World from Template` / `Edit` / `Save` on `8095` does nothing, so creating the Season 9 second world, restarting with it, verifying `S6.3`/`S9` badges, and verifying EXP `3` isolation could not complete in `HNMO-pkg`.
  - Screenshot: [step2c-worlds-blazor-err.png](screenshots/antigravity-2026-11-20/step2c-worlds-blazor-err.png)

### 2d. Cash Shop in Safe Zone (`X`)
- **Check** (`scratch/step2-abde.mjs`): Pressed `X` in Lorencia safe zone (`test1`).
- **Result:** Cash Shop opened with `W Coin (C)`, `W Coin (P)`, `Goblin Point` tabs and `9` packages listed (`Gold Channel Ticket`, `Magic Backpack`, `Vault Expansion Certificate`, `Premium Service`, `Gladiator Package`, etc.).
- **Screenshot:** [step2d-cash-shop.png](screenshots/antigravity-2026-11-20/step2d-cash-shop.png)
- **Verdict:** **PASS**

### 2e. Refusing Ancient / Socket Item on Item Market (EN + VI)
- **Result:** **BLOCKED by same `8095` Blazor issue as 2c**
  - Logged in `testgm`, spawned an Ancient item (`/item 7 5 0 0 0 0 0 1 5`), picked it up (`beforeBag: 64 -> afterBag: 65`), and pressed `F6` to open `#hnmo-market-panel`.
  - Market showed `"Item Market is currently disabled by the server administrator."`. Toggling `Enable Item Market` (`#market-active`) and clicking `Save` on `http://127.0.0.1:8095/market` did not persist (`MARKET_ACTIVE_AFTER_SAVE: false`) due to the broken `8095` Blazor circuit (`ReconnectModal.13ja33weya.razor.js` SRI block + `System is not defined`).

---

## STEP 3 — Season 9 Pack with Real Files

### 3a. Package Build with `HNMO_S9_DATA_DIR`
- **Command:**
  ```bash
  export POSTGRES_DIR="<scratch>/pg-portable-cache"
  export HNMO_S9_DATA_DIR="<owner's extracted Season 9 Data folder>"
  client/web/tools/package-hnmo.sh "<scratch>/HNMO-int/server" win-x64 "<scratch>/HNMO-pkg"
  ```
- **Build time:** `381` seconds (`6m 21s`)
- **Sizes:**
  - `HNMO-pkg` total: `2,550 MB` (`2.5G`), `20,811` files
  - `HNMO-pkg/Data`: `50` packs (`794 MB`), zero Season 9 files inside `Data/` (`Get-ChildItem Data -Name` -> `packs` only)
  - `HNMO-pkg/Data_S9`: **`18,965` files, `1,217.6 MB`**
- **Last output lines:**
  ```text
    Data before pack: 14891 files, 1066 MB -> after pack: 50 files, 794 MB
    server/wwwroot before: 8279 files, 69 MB -> after: 54 files, 49 MB
    TOTAL                 20811    2550 MB
  Done: <scratch>/HNMO-pkg
  BUILD_SECONDS=381
  2.5G	<scratch>/HNMO-pkg
  ```

### 3b. `/data/seasons.json`, Overlay Routing, and S6 vs. S9 Requests
- **Endpoint byte checks (`http://127.0.0.1:8080`):**
  - `/data/seasons.json` -> `{"s6":true,"s9":true}` (`200`)
  - `Player/player.bmd`: `/data/Player/player.bmd` (`200`, `3,324,834` bytes) vs. `/data/s9/Player/player.bmd` (`200`, `3,472,315` bytes) — **different sizes (`differ: true`)**.
  - `/data/Local/Mix.bmd` vs. `/data/s9/Local/Mix.bmd`: both `200`, `65,000` bytes (`sameBytes: true` SHA-256).
  - `/data/Interface/Item_B.ozj` vs. `/data/s9/Interface/Item_B.ozj`: both `200`, `185,947` bytes (`sameBytes: true` SHA-256).
- **Season 6 session (`test1` entering Lorencia):**
  - `s6DataReqs: 642`, **`s9DataReqs: 0`** (no request went to `/data/s9/`).
- **Season 9 session (`testgm` with Playwright `/website/data` route override setting `season: "s9"`, since `8095/worlds` is blocked by 2c):**
  - `totalS9: 167+` requests under `/data/s9/` (models e.g. `/data/s9/Object8/Object23.bmd`, `/data/s9/Local/Eng/skill_eng.bmd`; maps e.g. `/data/s9/World8/EncTerrain8.map`, `EncTerrain8.att`, `EncTerrain8.obj`, `TerrainHeight.OZB`; sounds e.g. `/data/s9/Sound/aWater.wav`, `/data/s9/Music/atlans.mp3`), entering Lorencia with **zero `pageerror`s**.

### 3c. Season 9 Walkthrough (`Lorencia`, `Devias`, `Noria`, `Lost Tower`, `Atlans`)
- All 5 maps rendered cleanly with **`pageErrs: []`** (`testgm`, `season: "s9"`):
  - **Lorencia (`map 0`)**: [step3c-lorencia.png](screenshots/antigravity-2026-11-20/step3c-lorencia.png)
    - `404`s: `/data/s9/Object1/ston03.ktx2`, `/data/s9/Object1/light_02.ktx2`, `/data/s9/Object1/bridge_shadow01.ktx2`, `/data/s9/Effect/twlighthik01.{ktx2,jpg}`, shared `/data/s9/Skill/*` (`Swordeff_mono`, `grandmark`, `grandmark2`, `lines2`, `JointSpirit03`, `Shockwave`, `firehik02`, `flamestani`, `Chrome01`, `testmsop`, `monolines`, `Damage1*`, `Damage2*`, `volcano_*`, `blur02`, `flare`, `Kwave*`, `ground_wind`, `magic_ground1`, `shiny05`), and shared `/data/Interface/RmlUi/*.rcss` / `themes/legacy/hnmo_onboarding.rml` (also 404 in S6).
  - **Devias (`map 2`)**: [step3c-devias.png](screenshots/antigravity-2026-11-20/step3c-devias.png)
    - `404`s: `/data/s9/Object3/sword14.{ktx2,tga,jpg}`, `sword11.{ktx2,tga,jpg}`, `hound_sword1.{ktx2,jpg}`, `hound_shield1.{ktx2,tga}`, `sword06.{ktx2,jpg}`, `shield05.{ktx2,jpg}`, `sword03.{ktx2,jpg}`, `light_02.ktx2`.
  - **Noria (`map 3`)**: [step3c-noria.png](screenshots/antigravity-2026-11-20/step3c-noria.png)
    - `404`s: `/data/s9/World4/TileGrass02.jpg`, `/data/s9/Object4/filling_up02.ktx2`, `/data/s9/Object4/itp01.ktx2`.
  - **Lost Tower (`map 4`)**: [step3c-losttower.png](screenshots/antigravity-2026-11-20/step3c-losttower.png)
    - `failed404s: []`, `pageErrs: []`.
  - **Atlans (`map 7`)**: [step3c-atlans.png](screenshots/antigravity-2026-11-20/step3c-atlans.png)
    - `failed404s: []`, `pageErrs: []` (visually, a foreground archway pillar partially occludes the spawn view at `(15, 21)`).

### 3d. `client/web/tools/build-s9-pack.mjs`
- Skipped: `Test-Path client/web/tools/build-s9-pack.mjs` returned `False` (script is not on this branch yet).


