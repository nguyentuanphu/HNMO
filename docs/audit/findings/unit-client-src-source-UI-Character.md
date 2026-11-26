# Unit Audit: `client/src/source/UI/Character`

- **Unit**: `client/src/source/UI/Character`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `15` (`3,444` lines) including `CharacterInfoWindow.cpp` (`1,579L`), `CriticalDamageRateLine.cpp`, `DamageHint.cpp`, `PetInfoWindow.cpp`, `NameWindow.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Wizardry weapon % bonus + durability penalty display | `CharacterInfoWindow.cpp:1420-1433` | `magicPercent = pWeaponRight->MagicPower / 100.f * (1 - CalcDurabilityPercent(...))`; formats `WizardryDmgDDD` / `DD216` with buff highlight (`eBuff_Hellowin2`, `EliteScroll4`, `CherryBlossom_Petal`) | Web `characterWindow.ts` & Server `Stats` | Same |
| Summoner Curse damage, Berserker buff & pet/ring bonuses | `CharacterInfoWindow.cpp:1450-1525` | Calls `GetCurseSkillDamage`; applies Berserker scaling `int(Energy / 30) / 100.f` to `Ene/9..Ene/4`, Panda Ring `+30` min/max (`1482`), Skeleton Ring `+40` (`1487`), Demon Pet `+40%` (`1501-1502`), Skeleton Pet `+20%` (`1509-1510`) ignoring expired period items, and Book curse `%` minus wear | Server `Summoner` Berserker/Book/Pet formulas | Same |
| Pet Info Window & Critical Damage Rate hint | `PetInfoWindow.cpp`, `CriticalDamageRateLine.cpp`, `DamageHint.cpp` | Renders `CalcPetInfo` stats (Dark Horse / Dark Raven) and critical damage tooltip breakdown | Web `petInfoWindow.ts` & `GameLogic/Pets` | Same |
