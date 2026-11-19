# Antigravity Windows Check3 Report (2026-11-22)

- **Branch**: `antigravity-check3` (branched from `origin/claude/hnmo-main-handover-54co2m` at `a3e27187`)
- **Environment**: Windows, `<scratch>/HNMO-pkg3` packaged from `<scratch>/HNMO-int/server` (`win-x64`) without `HNMO_S9_DATA_DIR`
- **Code changes**: None (test and report only)

---

## 1. Setup

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

### 1.3 Start `Start HNMO.bat` — **BLOCKED (Device Guard policy)**
Command:
```powershell
$env:HNMO_WINDOW = "1"
$env:HNMO_NO_BROWSER = "1"
Set-Location "<scratch>\HNMO-pkg3"
cmd /c "Start HNMO.bat"
```
Output:
```text
'<scratch>\HNMO-pkg3\web\HNMO.exe' was blocked by your organization's Device Guard policy.
Contact your support person for more info.

HNMO has stopped. If it stopped by itself, the lines above say why
(the logs are in the "save\logs" folder). You can close this window.
```

---

## 2–4. Jewel Bank Switched Off / On / During Trade — **BLOCKED**

Because `<scratch>\HNMO-pkg3\web\HNMO.exe` was blocked by the host's Device Guard policy when starting `Start HNMO.bat`, Steps 2, 3, and 4 could not be executed.
