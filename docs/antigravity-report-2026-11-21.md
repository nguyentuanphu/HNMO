# Antigravity Windows Check Report (2026-11-21)

- **Branch**: `antigravity-check2` (branched from `origin/claude/hnmo-main-handover-54co2m` at `119600a2`)
- **Environment**: Windows, `<scratch>/HNMO-pkg` packaged from `<scratch>/HNMO-int/server` (`win-x64`) without `HNMO_S9_DATA_DIR`
- **Code changes**: None (test and report only)

---

## Setup

### 1. Branch checkout
```bash
git fetch origin
git checkout -B antigravity-check2 origin/claude/hnmo-main-handover-54co2m
```

### 2. Close draft PR #86
```bash
gh pr close 86
```
Output:
```text
✓ Closed pull request nguyentuanphu/HNMO#86
```

### 3. Clean package build (without `HNMO_S9_DATA_DIR`)
Command (Git Bash):
```bash
POSTGRES_DIR="<scratch>/pg-portable-cache" client/web/tools/package-hnmo.sh "<scratch>/HNMO-int/server" win-x64 "<scratch>/HNMO-pkg"
```
Key output lines:
```text
HNMO.Startup.staticwebassets.endpoints.json: 109 routes kept, 16612 without a file removed
Data before pack: 14891 files, 1066 MB -> after pack: 50 files, 794 MB
server/wwwroot before: 8279 files, 69 MB -> after: 54 files, 49 MB
TOTAL                  1848    1274 MB
BUILD_SECONDS=432
```

### 4. Start `Start HNMO.bat` and verify `/worlds` styling
Started `<scratch>/HNMO-pkg/Start HNMO.bat` (`HNMO_WINDOW=1`, `HNMO_NO_BROWSER=1`), then opened `http://127.0.0.1:8095/worlds` in headless Chromium:
```text
SETUP4_WORLDS: { bg: 'rgb(244, 245, 248)', consoleErrs: [], pageErrs: [] }
```
- Styled light background (`rgb(244, 245, 248)`), dark sidebar, zero console errors, zero page errors.
- Screenshot: [setup4-worlds-styled.png](screenshots/antigravity-2026-11-21/setup4-worlds-styled.png)

---

## Step A — Admin Panel Saves

### A1. `/market`: Enable Item Market, Save, and Reload — **PASS**
- **Actions**: Navigated to `http://127.0.0.1:8095/market`, checked `#market-active` ("Enable Item Market"), clicked `#market-save` (`Save`), restarted `HNMO-pkg`, and reloaded `/market`.
- **Result**: `MARKET_RELOADED_CHECKED: true`.
- **Screenshot**: [stepA1-market-saved.png](screenshots/antigravity-2026-11-21/stepA1-market-saved.png)

### A2. `/worlds`: Create Season 9 Second World, Restart, and Verify Badges — **PASS**
- **Actions**: On `http://127.0.0.1:8095/worlds`, created a second world (`s9-test`, `World 2 — S9`, `Thế giới 2 — S9`, `Season9`, database `hnmo_world_2`, server ID `3`, EXP rate `3`), saved, and restarted `HNMO-pkg`.
- **Output on `/worlds` after restart**:
```text
STEP_A2_WORLDS: ID NAME DATABASE SERVERS RATES EVENTS OPENING (SYDNEY) PLAYERS ACTIONS
main World 1 — Origin S6.3 Thế giới 1 — Khởi Nguyên hnmo_world_1 0, 1, 2 EXP 1x · M 1x · Drop 1x · Zen 1x Standard schedule Open now 0 Stop Edit
s9-test World 2 — S9 S9 Thế giới 2 — S9 hnmo_world_2 3 EXP 3x · M 1x · Drop 1x · Zen 1x Standard schedule Open now 0 Stop Edit
```
- **Output on `http://127.0.0.1:8080/` (`home.html`)**:
```text
HOME_VISIBLE_WORLD2: "Season:\nAll\nS6.3\nS9\nWorld 1 — Origin S6.3\nOpen · 0\nEXP ×1 · Drop ×1 · Zen ×1 · Standard schedule\nServer 0 (0/1000)\nServer 1 (0/1000)\nServer 2 (0/1000)\nWorld 2 — S9 S9\nOpen · 0\nEXP ×3 · Drop ×1 · Zen ×1 · Standard schedule"
```
- **Screenshots**:
  - [stepA2-worlds-after-restart.png](screenshots/antigravity-2026-11-21/stepA2-worlds-after-restart.png)
  - [stepA2-home-badges.png](screenshots/antigravity-2026-11-21/stepA2-home-badges.png)

### A3. World 1 EXP Rate Unchanged on `/servers` — **PASS**
- **Actions**: After setting `s9-test` EXP rate to `3` and saving, opened `http://127.0.0.1:8095/servers`.
- **Result**: World 1 servers (`Server 0`, `Server 1`, `Server 2`) remain at `EXP 1`, `DROP 1`, `ZEN 1`.
- **Screenshot**: [stepA3-servers-rates.png](screenshots/antigravity-2026-11-21/stepA3-servers-rates.png)

---

## Step B — Trade Lock (with Positive Controls)

Two headless Chromium contexts logged in as `test1` (`test1Dk`) and `test2` (`test2Dk`) in Lorencia (`http://127.0.0.1:8080/index.html`) via `<scratch>/check2-stepB-trade-lock.mjs`.

> **Note on Jewel Bank (`F7`)**: By default `#jb-active` on `http://127.0.0.1:8095/jewel-bank` was `false`. We enabled `#jb-active`, clicked `#jb-save`, waited for `SaveAsync` to complete, verified `JB_ACTIVE_AFTER_SAVE_DONE: true` on reload, and restarted `HNMO-pkg`. Even after enabling and restarting, pressing `F7` in-game opened `#hnmo-jewel-bank-panel` ("Jewel Bank (F7)") stuck on `"Loading Jewel Bank..."` (no `HnmoJewelBankState` packet arrived within 15s, so no Jewel of Bless balance or `Deposit 1` button rendered). Therefore Jewel Bank deposit could not be verified as working in the no-trade positive control.

### Before / After Comparison Table

| Action | Condition | Before | After | Result |
| --- | --- | --- | --- | --- |
| **Jewel Bank deposit** (`F7`, 1 Jewel of Bless) | No trade open | Bag Bless: `6`; Bank: `N/A` (`Loading Jewel Bank...`) | Bag Bless: `6`; Bank: `N/A` (`Loading Jewel Bank...`) | **BLOCKED / FAIL** (panel stayed on `Loading Jewel Bank...` even with `#jb-active` saved `true` & restarted) |
| **Jewel Bank deposit** (`F7`, 1 Jewel of Bless) | Trade open | Bag Bless: `5`; Bank: `N/A` | Bag Bless: `5`; Bank: `N/A` | **Not testable** (positive control blocked above) |
| **Market listing** (`F6`, list item from bag) | No trade open | Bag items: `50`; Market listings: `2` | Bag items: `49`; Market listings: `3` (`Listed Jewel of Bless for 100 WCoinC!`) | **PASS** (positive control works) |
| **Market listing** (`F6`, list item from bag) | Trade open | Bag items: `49`; Market listings: `3` | Bag items: `49`; Market listings: `3` | **PASS** (changed nothing) |
| **Chat command** (`/code TEST`) | No trade open | Chat last system line before: `[Guide] Lumen the Barmaid: Lorencia (123, 135).` | Chat output: `That gift code doesn't exist (1 tries left).` | **PASS** (positive control works) |
| **Chat command** (`/code TEST`) | Trade open | Trade open between `test1Dk` and `test2Dk` | Chat output: `Close the trade or crafting window before using a command.` | **PASS** (exact expected lock notice) |
| **Cancel trade** (`window.__game.trade.cancel()`) | End of trade | `test1` bag: `49` items (exact snapshot); `test2` bag: `52` items (exact snapshot) | `bag1Equal: true`, `bag2Equal: true` | **PASS** (both bags identical) |

### Raw Script Output (`<scratch>/check2-stepB-trade-lock.mjs`)
```text
=== PART 1: NO TRADE OPEN ===
NO_TRADE_JB: {"depClicked_noTrade":false,"blessBagBefore":6,"blessBagAfter":6,"bankBefore":null,"bankAfter":null,"rowText":""}
NO_TRADE_MKT: {"bagLenBefore":50,"bagLenAfter":49,"browseBefore":2,"browseAfter":3,"listedItem":null}
NO_TRADE_CHAT: [{"kind":"system","text":"[Guide] Lumen the Barmaid: Lorencia (123, 135)."},{"kind":"system","text":"Listed Jewel of Bless for 100 WCoinC!"},{"kind":"system","text":"Listed Jewel of Bless for 100 WCoinC!"},{"kind":"system","text":"That gift code doesn't exist (1 tries left)."}]
=== PART 2: TRADE OPEN ===
TRADE_OPENED: true
TRADE_LOCKED_RESULTS: {"jbPanelDuringTrade":true,"jbClickedDuringTrade":false,"mktPanelDuringTrade":true,"chatTrade":[{"kind":"system","text":"Listed Jewel of Bless for 100 WCoinC!"},{"kind":"system","text":"That gift code doesn't exist (1 tries left)."},{"kind":"system","text":"You have requested test2Dk to trade."},{"kind":"system","text":"Close the trade or crafting window before using a command."}]}
AFTER_CANCEL: {"bag1Equal":true,"bag2Equal":true,"blessBagBeforeTrade":5,"blessBagAfterCancel":5,"bankBeforeTrade":null,"bankAfterCancel":null,"bagLenBeforeTrade":49,"bagLenAfterCancel":49,"browseBeforeTrade":3,"browseAfterCancel":3}
```

### Step B Screenshots
- [stepB-no-trade-jb.png](screenshots/antigravity-2026-11-21/stepB-no-trade-jb.png)
- [stepB-no-trade-market.png](screenshots/antigravity-2026-11-21/stepB-no-trade-market.png)
- [stepB-trade-locked.png](screenshots/antigravity-2026-11-21/stepB-trade-locked.png)
