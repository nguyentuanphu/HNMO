# Antigravity Windows Check3 Report (2026-11-22)

- **Branch**: `antigravity-check3` (branched from `origin/claude/hnmo-main-handover-54co2m` at `a3e27187`)
- **Environment**: Windows, `<scratch>/HNMO-pkg3` packaged from `<scratch>/HNMO-int/server` (`win-x64`) without `HNMO_S9_DATA_DIR`
- **Code changes**: None (test and report only)

---

## 1. Setup — **PASS**

### 1.1 Branch checkout
```bash
git fetch origin
git checkout -B antigravity-check3 origin/claude/hnmo-main-handover-54co2m
```
Output:
```text
Switched to a new branch 'antigravity-check3'
branch 'antigravity-check3' set up to track 'origin/claude/hnmo-main-handover-54co2m'.
a3e27187 Next Windows task (about 45 minutes): Jewel Bank off, on, and during a trade
```

### 1.2 Build package `<scratch>/HNMO-pkg3` (without `HNMO_S9_DATA_DIR`)
Command (Git Bash):
```bash
unset HNMO_S9_DATA_DIR
POSTGRES_DIR="<scratch>/pg-portable-cache" client/web/tools/package-hnmo.sh "<scratch>/HNMO-int/server" win-x64 "<scratch>/HNMO-pkg3"
```
Key output lines:
```text
HNMO.Startup.staticwebassets.endpoints.json: 109 routes kept, 16612 without a file removed
Packed 14493 files (792.25 MB) into 50 packs (793.10 MB) at <scratch>/HNMO-pkg3/Data/packs
TOTAL                  1848    1274 MB
Done: <scratch>/HNMO-pkg3
BUILD_SECONDS=404
```

### 1.3 Start `Start HNMO.bat`
Initial launch hit Device Guard on `<scratch>\HNMO-pkg3\web\HNMO.exe`; after owner intervention on Windows, `<scratch>\HNMO-pkg3\Start HNMO.bat` started cleanly and ports `8080` and `8095` answered (`READY_8095=True`).

---

## 2. Jewel Bank Switched Off — **PASS**

- **Actions**: Logged in as `test1` (`test1Dk`) at `http://127.0.0.1:8080/index.html` and pressed `F7` (`<scratch>/check3-step2-and-enable.mjs`).
- **Output**:
```text
STEP2_JB_DISABLED_TEXT: "Jewel Bank (F7)\n×\n\nJewel Bank is currently disabled by the server administrator."
```
- **Screenshot**: [step2-jb-disabled.png](screenshots/antigravity-2026-11-22/step2-jb-disabled.png)

---

## 3. Jewel Bank Switched On — **PASS**

- **Actions**:
  1. Opened `http://127.0.0.1:8095/jewel-bank`, toggled `#jb-active` ("Enable Jewel Bank") ON, clicked `#jb-save` (`Save`), waited for save toast, reloaded (`STEP3_ADMIN_ENABLE_AFTER: true`).
  2. Restarted `<scratch>/HNMO-pkg3/Start HNMO.bat` (`READY_8095_RESTART=True`).
  3. Logged in as `test1` (`test1Dk`), pressed `F7`, verified `"Banked: 0 / 10,000"` for `Jewel of Bless`, and clicked `Deposit 1` (`<scratch>/check3-step3-and-4.mjs`).
- **Output**:
```text
STEP3_RESULT: {"dep3Clicked":true,"bankedBefore":0,"bankedAfter":1,"blessBagBefore":8,"blessBagAfter":7,"rowText":"Jewel of Bless Banked: 1 / 10,000 Deposit 1 Withdraw"}
```
  - Jewel of Bless `Banked`: `0` -> `1` (`Banked: 1 / 10,000`)
  - Bag Jewel of Bless count: `8` -> `7`
- **Screenshot**: [step3-jb-enabled-deposit.png](screenshots/antigravity-2026-11-22/step3-jb-enabled-deposit.png)

---

## 4. Jewel Bank During a Trade — **PASS**

- **Actions**: Logged in `test1` (`test1Dk`) and `test2` (`test2Dk`) in Lorencia, walked `test1` next to `test2`, opened a trade, pressed `F7` in `test1`, clicked `Deposit 1` for `Jewel of Bless`, then cancelled the trade (`<scratch>/check3-step4.mjs`).
- **Output**:
```text
FOUND_PARTNER: test2Dk {"id":827,"cell":{"x":150,"y":127}}
STEP4_RESULT: {"dep4Clicked":true,"bankedBeforeTrade":1,"bankedDuringTradeBeforeClick":1,"bankedDuringTradeAfterClick":1,"bankedAfterCancel":1,"blessBagBeforeTrade":7,"blessBagDuringTradeAfterClick":7,"blessBagAfterCancel":7,"bag1Equal":true,"bag2Equal":true,"chatTail":["[Guide] Next: Talk to a merchant. Click an NPC in town; the arrow points to the nearest merchant. Merchants sell potions, weapons and armour.","[Guide] Lumen the Barmaid: Lorencia (123, 135).","You have requested test2Dk to trade.","Your trade has been canceled."]}
```
- **Screenshot**: [step4-jb-trade-locked.png](screenshots/antigravity-2026-11-22/step4-jb-trade-locked.png)

### Before / After Summary Table

| Step / Condition | Jewel of Bless (`Banked`) Before | Jewel of Bless (`Banked`) After | Bag Bless Before | Bag Bless After | Both Bags Unchanged After Trade Cancel | Result |
| --- | --- | --- | --- | --- | --- | --- |
| **Step 2: Jewel Bank OFF (`F7`)** | N/A | Shows `"Jewel Bank is currently disabled by the server administrator."` | `8` | `8` | N/A | **PASS** |
| **Step 3: Jewel Bank ON, no trade (`Deposit 1`)** | `0 / 10,000` | `1 / 10,000` | `8` | `7` | N/A | **PASS** |
| **Step 4: Jewel Bank ON, trade open (`Deposit 1`)** | `1 / 10,000` | `1 / 10,000` | `7` | `7` | `bag1Equal: true`, `bag2Equal: true` | **PASS** |
