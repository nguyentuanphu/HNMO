# Unit: client/src/source/GameLogic/Skills

Checked against: Season 6/9 client skill requirement formula & hotkey serialization.

Scope: `client/src/source/GameLogic/Skills` (`7` files, `1225` lines).

## Findings

| File & Line | Rule / Formula | Parity Reference | Result |
|---|---|---|---|
| `SkillManager.cpp:91-95,165-180` | Energy req: `0` if BMD Energy=0, direct for Lv 0 summons, else `20 + (Energy * Level * 4 / 100)` (`BASE_KNIGHT=10`, `SCALE_SUMMON=3%`) | Classic MU skill attribute requirement formula | Same |
| `SkillHotkeyConfiguration.h:15` | `KeyConfigurationSize = 30` bytes persisted server-side | Web `skills.ts` & Server `KeyConfiguration` | Same |
