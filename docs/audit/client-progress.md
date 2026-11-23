# HNMO Client Audit Progress — 2026-11-24

**Branch:** `audit-client-2026-11-24` (from `audit-full-2026-11-23`)  
**Date:** 2026-11-24 (Australia/Sydney)  
**Scope:** Complete client audit of HNMO web client (`client/web/`) against Season 6 and Season 9 desktop clients

---

## Step 1: Inventory Client Files

Catalogue all files in four folders:
- [ ] Season 6 client: `D:\WorkSpace\MuOnline\IGCN\IGCN x6\IGC.Evi.Network x6.3 6.0.0.1 Full Client\IGC.Evi.Network x6.3 Full Client`
- [ ] Season 9 client: `D:\WorkSpace\MuOnline\IGCN\IGCN x9\IGC.Evi.Network x9 Full Client\Season 9`
- [ ] HNMO web client source: `D:\WorkSpace\HNMO-src\client\web\src`
- [ ] HNMO web client public/data: `D:\WorkSpace\HNMO-src\client\web\public\data`

For each folder, categorize by kind:
- **Art/Sound**: `.bmd`, `.ozj`, `.jpg`, `.png`, `.ogg`, `.wav`, `.bmp`, `.tga`
- **Data tables**: `.bmd`, `.txt`, `.json` (items, skills, monsters, maps, gates, etc.)
- **UI/Interface**: `.rml`, `.xml`, `.lua` (RmlUi, layout, scripts)
- **Scripts/Config**: `.ini`, `.cfg`, `.js`, `.ts` (config, game scripts)
- **Binaries**: `.exe`, `.dll`, `.so` (executables, libraries)
- **Other**: everything else

Track `has_rules` (yes/maybe/no) — files that contain game logic/rules vs pure assets.

Output: `docs/audit/client-inventory/*.csv` (4 files)

---

## Step 2: Compare PARITY.md Against Reality

- [ ] Read `client/web/PARITY.md` fully
- [ ] For each ✅/🟡/⬜ item, verify against actual web client code
- [ ] For each ⬜ item, check if Season 6/9 client has equivalent
- [ ] Mark discrepancies in `docs/audit/client-parity-gaps.md`

---

## Step 3: Data Table Comparison (Client-Side)

Compare client-side data tables between:
- Season 6 client `Data/` vs HNMO `public/data/`
- Season 9 client `Data/` vs HNMO `public/data/`

Key tables: Items, Skills, Monsters, Maps, Gates, NPCs, Objects, Effects, Interface/RmlUi

Output: `docs/audit/client-data-diffs/*.txt`

---

## Step 4: UI/UX Parity Check

Compare RmlUi files:
- Season 6 client `Data/Interface/` vs HNMO `public/data/Interface/RmlUi/`
- Season 9 client `Data/Interface/` vs HNMO `public/data/Interface/RmlUi/`

Check: window layouts, element structure, bindings, styles

---

## Step 5: Missing Features Report

Document:
- Features in desktop clients but missing in web client
- Features in web client but not in desktop (HNMO extras)
- Visual/behavioral differences
- Priority recommendations

Output: `docs/audit/client-report-2026-11-24.md`

---

## Progress Tracking

| Step | Status | Started | Completed |
|------|--------|---------|-----------|
| 1. Inventory | ✅ Done | 2026-11-24 | 2026-11-24 |
| 2. PARITY.md verification | 🔄 In progress | 2026-11-24 | |
| 3. Data table diffs | ⬜ Not started | | |
| 4. UI/UX parity | ⬜ Not started | | |
| 5. Report | ⬜ Not started | | |