# Antigravity Windows Check5 Report (2026-11-24)

- **Branch**: `antigravity-check5` (branched from `origin/claude/hnmo-main-handover-54co2m` at `09e62f36`)
- **Package**: `<scratch>/HNMO-pkg3` (`<scratch>/HNMO-pkg4` not present), started via `<scratch>/HNMO-pkg3/Start HNMO.bat`
- **Base URL**: `http://127.0.0.1:8080`
- **Code / test changes**: None (test and report only)

---

## Summary

| Test | Run 1 | Run 2 | Run 3 | Total |
| --- | --- | --- | --- | --- |
| `tests/e2e/quests.spec.ts:52` (`test0`) | **PASS** (1.2m) | **PASS** (1.1m) | **PASS** (1.1m) | **3 / 3 passed** |
| `tests/e2e/touch.spec.ts:12` (`test6`) | **PASS** (53.3s) | **PASS** (54.2s) | **PASS** (53.1s) | **3 / 3 passed** |

No failures occurred across the 6 runs (no failure screenshots generated; trace zips preserved outside the repository under `<scratch>/traces-check5/`).

---

## 1. `tests/e2e/quests.spec.ts:52` (3 runs, `test0`)

Command per run (from `client/web`):
```powershell
$env:BASE_URL = "http://127.0.0.1:8080"
npx playwright test tests/e2e/quests.spec.ts:52 --workers=1 --retries=0 --trace on
```

| Run | Pre-run account | Pre-run `/move lorencia` state (`map`, `cell`) | Result | Trace zip (not committed) |
| --- | --- | --- | --- | --- |
| **1** | `test0` (`test0Dk`) | `map: 0`, `cell: { x: 148, y: 128 }` | **PASS** (`1 passed (1.2m)`) | `<scratch>/traces-check5/quests-run1-trace.zip` |
| **2** | `test0` (`test0Dk`) | `map: 0`, `cell: { x: 131, y: 128 }` | **PASS** (`1 passed (1.1m)`) | `<scratch>/traces-check5/quests-run2-trace.zip` |
| **3** | `test0` (`test0Dk`) | `map: 0`, `cell: { x: 131, y: 130 }` | **PASS** (`1 passed (1.1m)`) | `<scratch>/traces-check5/quests-run3-trace.zip` |

Last output lines (Run 3):
```text
Running 1 test using 1 worker

  ok 1 tests\e2e\quests.spec.ts:52:1 › talks to an NPC, accepts a quest and finds it in the quest window (1.0m)

  1 passed (1.1m)
```

---

## 2. `tests/e2e/touch.spec.ts:12` (3 runs, `test6`)

Command per run (from `client/web`):
```powershell
$env:BASE_URL = "http://127.0.0.1:8080"
npx playwright test tests/e2e/touch.spec.ts:12 --workers=1 --retries=0 --trace on
```

| Run | Pre-run account | Pre-run `/move lorencia` state (`map`, `cell`) | Result | Trace zip (not committed) |
| --- | --- | --- | --- | --- |
| **1** | `test6` (`test6Dk`) | `map: 0`, `cell: { x: 133, y: 125 }` | **PASS** (`1 passed (53.3s)`) | `<scratch>/traces-check5/touch-run1-trace.zip` |
| **2** | `test6` (`test6Dk`) | `map: 0`, `cell: { x: 135, y: 126 }` | **PASS** (`1 passed (54.2s)`) | `<scratch>/traces-check5/touch-run2-trace.zip` |
| **3** | `test6` (`test6Dk`) | `map: 0`, `cell: { x: 137, y: 127 }` | **PASS** (`1 passed (53.1s)`) | `<scratch>/traces-check5/touch-run3-trace.zip` |

Last output lines (Run 3):
```text
Running 1 test using 1 worker

  ok 1 tests\e2e\touch.spec.ts:12:1 › touch: the button bar, a tap walks, two fingers zoom and turn (50.2s)

  1 passed (53.1s)
```
