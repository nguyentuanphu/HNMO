# Unit Audit: `client/src/source/Engine/Object (files 3)`

- **Unit**: `client/src/source/Engine/Object (files 3)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `3` (`3,064` lines): `ZzzCharacter.h`, `ZzzInfomation.cpp` (`2,731L`), `ZzzInfomation.h` (`129L`)
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Wing & Cape option slot values (1st/2nd/3rd tier) | `ZzzInfomation.cpp:316-380` | 2nd wings (`ExcellentFlags`): HP/MP `+50 + Level*5`, `+3%` ignore (`AT_ONE_PERCENT_DAMAGE`), AG `+50`, AtkSpeed `+5`; Cape of Lord/Fighter: HP/MP `+50 + Level*5`, Ignore `3%`, Cape of Lord Command `+10 + Level*5` (`Cape of Fighter` excluded `:356`); 3rd wings: Ignore `5%`, Reflect `5%`, Full HP recover `5%`, Full MP recover `5%` | Server `Wings` item option definitions | Same |
| Wizard's Ring & Lethal Wizard's Ring stats | `ZzzInfomation.cpp:384-404` | `ITEM_WIZARDS_RING` levels `0` and `3`: `+10%` magic dmg, `+10%` phys dmg, `+10` attack speed; `ITEM_LETHAL_WIZARDS_RING`: `+15%` magic/phys dmg, `+10` attack speed | Server Wizard's Ring attribute definitions | Same |
| Base item attribute & durability penalty calc | `ZzzInfomation.cpp:406-415+`, `ZzzInfomation.h:22` | Copies `TwoHand`/`WeaponSpeed`/`WalkSpeed`, computes `ComputeItemStats`, and evaluates `CalcDurabilityPercent` wear reduction | Web `characterCalc.ts` & `itemTooltip.ts` | Same |
