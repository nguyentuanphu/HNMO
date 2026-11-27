# Desktop Client Data Audit: Season 9 Pentagram & Errtel

Audited on `2026-11-27` (Sydney time) from the Season 9 desktop client `Data` pack using
`tools/local-checks/desktop-data/pentagram.py` and `tools/local-checks/desktop-data/items_options.py`.

## Summary & Critical Findings for PR `#428`

1. **23 of 60 Errtel Options Have Non-Linear Level `0..10` Curves (`PentagramJewelOptionValue_eng.bmd`):**
   PR `#428` (`Pentagram.cs:107-122`) computes option values by piecewise linear interpolation across `(ValueLv0,
   ValueLv5, ValueLv10)`. In Season 9 desktop `Local/Eng/PentagramJewelOptionValue_eng.bmd` (`60` rows × 372 bytes),
   all 11 level values `lv0..lv10` are stored explicitly, and **23 rows are non-linear (convex)**:
   - `idx=0` (Anger R1 `Elemental Dmg +%d`): Client = `[30, 33, 38, 45, 54, 65, 78, 93, 113, 138, 168]` vs
     PR `#428` linear = `[30, 37, 44, 51, 58, 65, 85, 106, 126, 147, 168]` (up to **+13** error at level 8).
   - `idx=20, 21, 30, 31, 120, 121, 130, 131` (Elemental Attack in PvP/Raids/Ranged/Melee):
     Client = `[30, 35, 43, 54, 66, 80, 97, 117, 142, 172, 207]` vs linear = `[30, 40, 50, 60, 70, 80, 105, 130, 156, 181, 207]`.
   - `idx=50, 70, 71, 80, 81, 170, 171, 180, 181` (Elemental Defense):
     Client = `[10, 13, 17, 22, 28, 35, 43, 52, 62, 73, 85]` vs linear = `[10, 15, 20, 25, 30, 35, 45, 55, 65, 75, 85]`.
   - `idx=200, 240` (Radiance R1/R5): Client = `[1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 15]` vs linear = `[1, 2, 3, 4, 5, 6, 7, 9, 11, 13, 15]`.
   - `idx=220, 221, 222` (Radiance R3 HP/MP/AG): Client = `[20, 25, 30, 35, 40, 50, 60, 70, 80, 90, 100]` vs
     linear = `[20, 26, 32, 38, 44, 50, 60, 70, 80, 90, 100]`.
   Replace `ValueLv0/5/10` interpolation with the exact 11-level array from `docs/audit/data/client-s9/errtel_options.csv`.

2. **Errtel Level-Up Success Rates & Mix Costs Are Present in S9 Client `Data/` (`s9-pentagram-design.md` noted them missing):**
   - `PentagramJewelOptionValue_eng.bmd` (`mixRateLv1..mixRateLv10`, `/ 10000`) stores per-level upgrade rates:
     - Anger (`12:221`) & Blessing (`12:231`) & Divinity R3-4: `[9000, 8000, 7000, 7000, 6000, 6000, 6000, 5000, 5000, 5000]`
     - Integrity (`12:241`) & Divinity R1-2/R5: `[8000, 8000, 7000, 7000, 6000, 6000, 5000, 5000, 4000, 4000]`
     - Radiance (`12:261`): `[7000, 7000, 6000, 6000, 5000, 5000, 4000, 4000, 3000, 3000]`
     - Also `optionRate` weights (`/ 10000`) for Rank 1..5 option rolls (`10000` for R1, `2000` for R2 elements,
       `5000` for R3-5 pairs, `2500` for Radiance R2-3).
   - `PentagramMixNeedSource.bmd` (`30` rows × 92 bytes, `docs/audit/data/client-s9/pentagram_mixes.csv`):
     - Base/Refinement (`recipes 0..17`): Mithril Bunch + Chaos (`8000`, `100,000 Zen`), Elixir Bunch + Chaos (`6000`,
       `100,000 Zen`), Mithril+Elixir+Bless → Frame (`5000`, `100,000 Zen`), Pentagrams `12:200..214` + Jewel of Harmony (`5000`, `100,000 Zen`).
     - **Errtel Level-Up (`recipes 1..10`, `category = 2`)**: Jewel Combination Frame (`12:150`)×1 + Jewel of Bless (`14:13`)×1 +
       Zen **`100,000, 100,000, 150,000, 200,000, 250,000, 300,000, 350,000, 400,000, 450,000, 500,000`**.
     - **Errtel Rank-Up (`ranks 2..3`, `category = 3`)**: Spirit Powder (`12:151`)×1 + Bless (`14:13`)×1..2 +
       Soul (`14:14`)×1..2 + 1..2 Errtel(s), base rate **`5000`**, Zen **`100,000`**.

3. **Pentagram Equipment Slot, Item Dimensions & Combination Options:**
   - `Item_eng.bmd` (`12:200..214`): all 15 Pentagrams have **`slot = 236`** (`InventoryConstants.PentagramSlot = 236` in
     `#428` matches the client!), **`size = 3x3`**, `DropLevel = 100` (`12:200`), `200` (`12:201`), `300` (`12:202..214`).
   - All 5 Errtels (`12:221..261`) have **`size = 2x2`**, `DropLevel = 110, 115, 120, 125, 130` (matches `SeasonNineOnlyItemsCatalog.cs`).
   - `PentagramOption_eng.bmd` (`15` × 48 bytes + `7` × 268 bytes): matches `#403` Grades (`1/1/1/1/1/1/1/1/2/2/2/2/3/3/3`),
     defines the **`7` Pentagram Combination Bonuses** (`1`: damage +10%, `2`: defense +10%, `3`: crit chance +10%,
     `4`: defense to elem def +5%, `5`: damage to elem atk +1%, `6`: resistance vs stronger element +5%, `7`: resistance
     vs elemental attack +5%) with exact required Errtel rank/level triples (`docs/audit/data/client-s9/pentagram_combo_options.csv`).
     Note: `PentagramOption_eng.bmd` Table 1 does not store the `(OptionNum1, OptionNum2, OptionNum3)` mapping per
     Pentagram item from `#403`, and some item names differ (`12:202/203` `"Empire Guardian's Iron Wall Sh"` in `.bmd` vs
     `Item_eng.bmd` `"Lorencia Knights' Iron Shield"` / `"Iron Shield of the Magic"`).

4. **Desktop Rules Noted in `Text_eng.bmd` & Absent Tables:**
   - **Errtel extraction risk (`Text_eng.bmd[3721, 3730]`):** Client warns *"When you take an Errtel out of the Pentagram,
     it may disappear"* / *"Unequip has failed. The Errtel was destroyed"*, and `Text[3725]` requires moving the
     Pentagram to inventory before equipping/removing Errtels (and blocking changes during trade/combination `Text[4064, 4727]`).
     PR `#428` `RemoveErrtelAsync` currently succeeds 100% without rolling extraction failure; S9 client `Data/` has **no**
     `ErrtelExtractionRates` table (`#403`'s 55 rates `10000..7000` came from server suite / must be admin-configurable).
   - **Monster Elements (`Local/AttributeVariation.bmd`, `38` rows):** Lists **15 monsters** (`586..591, 608..610, 670..672,
     678..680`) with texture variant indices `0..3` (and `23` pentagram/errtel/mithril items). No general monster-element
     assignment table exists in client `.bmd` files (server-only / admin config per `docs/owner-decisions.md` 27 Nov).
   - **Desktop Client UI:** No C++ Pentagram/Errtel window exists yet (`Interface/GFx/Pentagram.ozg` / `MixStone.ozg` exist
     in S9 client pack only).
