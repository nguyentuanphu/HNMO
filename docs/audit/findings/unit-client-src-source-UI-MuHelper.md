# Unit Audit: `client/src/source/UI/MuHelper`

- **Unit**: `client/src/source/UI/MuHelper`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `10` (`2,162` lines): `MuHelperConfigWindow.*`, `MuHelperDetailWindow.*`, `MuHelperShared.*`, `MuHelperSkillPicker.*`, `MuHelperUpdates.*`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| MUHelper UI range bounds & condition mutual exclusion | `MuHelperConfigWindow.cpp:36-37,51-57` | `MaxHuntingRange = 6`, `MaxObtainingRange = 8`; toggles `ON_TIMER` vs `ON_CONDITION` exclusively (`SetConditionMode`), edits staged `ConfigData` copy without live mutation until Save | Web `autoPlayWindows.ts:27-28` & `MUHelper` (`MuHelperData.cpp`) | Same |
| Detail sub-window thresholds & Skill picker filters | `MuHelperDetailWindow.cpp:40+`, `MuHelperSkillPicker.cpp` | Clamps potion/heal HP threshold sliders (step `10%`, `1..10`) and filters learned class skills across `6` slots | Web `autoPlayWindows.ts:26,32` | Same |
