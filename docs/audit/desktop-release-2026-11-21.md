# Desktop Client Release Audit (21 Nov 2026)

Audited on 22 Nov 2026 against the Season 6/9 server suites and the HNMO server rules landed on 21 Nov 2026.

## 1. Option Slots & Additional Option +28 (#202) and Local Build Environment

- **#202 (`clients-jewel-of-life-28`) status**: Already merged into the branch (`client/src/bin/Data/Items/Options/Option.json`, `client/src/source/Engine/Object/ZzzInfomation.cpp`, `client/src/source/Engine/Object/ZzzInfomation.h`, `client/tests/data/test_item_option_slots.cpp`, `client/docs/item-data.md`).
  - `Option.json` defines the 7 option levels (`1`–`7`, i.e. `+4` to `+28` damage/defense/wizardry/curse, `+5` to `+35` shield defense rate, `1 %` to `7 %` health recovery / mana / maximum AG).
  - `ZzzInfomation.cpp` (`AdditionalOptionPriceFactor` / `ItemValue`) prices levels `5`, `6`, `7` (`+20`, `+24`, `+28`) with the server's `14/3`, `28/3`, `56/3` multipliers.
  - `test_item_option_slots.cpp` verifies all 7 option levels (`+4`..`+28`) and the `+20`/`+24`/`+28` NPC buy/sell prices for weapons (`ITEM_KRIS`) and shields (`ITEM_SMALL_SHIELD`).
- **Local C++ build check**: This Windows PC has `.NET 10 SDK` and `Node.js v24.20.0`, but no C++ compiler toolchain (`cl.exe`/Visual Studio Build Tools, `cmake`, `vcpkg`, `g++`/`clang++`, or `WSL`) is installed locally. Native compilation and execution of `test_item_option_slots` therefore run in GitHub Actions (`client/.github/workflows/ci.yml`).

## 2. Jewel and Jewel Bundle NPC Prices (#192)

- Updated `client/src/source/Engine/Object/ZzzInfomation.cpp` and `client/src/source/GameLogic/Items/CComGem.cpp` to match the server and web client prices from #192 (`JewelNpcPricesUpdatePlugIn`):

| Item | Group / Number | Old Desktop Price | New Desktop Price | Bundle Price (`Level` 0 / 1 / 2 = 10 / 20 / 30) |
| --- | --- | ---: | ---: | ---: |
| Jewel of Bless | `14-13` (`12-30`) | `9,000,000` | `3,000,000` | `30,000,000` / `60,000,000` / `90,000,000` |
| Jewel of Soul | `14-14` (`12-31`) | `6,000,000` | `6,000,000` (unchanged) | `60,000,000` / `120,000,000` / `180,000,000` |
| Jewel of Chaos | `12-15` (`12-141`) | `810,000` | `810,000` (unchanged) | `8,100,000` / `16,200,000` / `24,300,000` |
| Jewel of Life | `14-16` (`12-136`) | `45,000,000` | `16,000,000` | `160,000,000` / `320,000,000` / `480,000,000` |
| Jewel of Creation | `14-22` (`12-137`) | `36,000,000` | `15,000,000` | `150,000,000` / `300,000,000` / `450,000,000` |
| Jewel of Guardian | `14-31` (`12-138`) | `60,000,000` | `15,000,000` | `150,000,000` / `300,000,000` / `450,000,000` |

- Added `TEST_CASE("The jewels and their bundles are priced like the server [data][items]")` in `client/tests/data/test_item_option_slots.cpp`.

## 3. Desktop Data Verification Against the Six New Rules

### 3.1 Rank-2 Class Items and Horn of Fenrir
- **Status**: **Matches (102 / 102 rank-2 items + Horn of Fenrir)**.
- All 102 items updated on the server by `SecondClassItemsUpdatePlugIn` already have `classRequirements` set to `2` (`Soul Master`, `Blade Knight`, `Muse Elf`, `Bloody Summoner`) in `client/src/bin/Data/Items/Group00_Sword.json` through `Group12_Wing.json`.
- **Horn of Fenrir (`13-37`)** in `client/src/bin/Data/Items/Group13_Helper.json` already has:
  - `requirements`: `{ "level": 300 }`
  - `classRequirements`: `{ "darkWizard": 2, "darkKnight": 2, "fairyElf": 2, "magicGladiator": 1, "darkLord": 1, "summoner": 2, "rageFighter": 1 }` (second class and up for the base classes; first class and up for MG, DL, RF).

### 3.2 Level 380 Items (and Phoenix Soul Vitality)
- **Status**: **Matches (49 / 49 items)**.
- Every item updated on the server by `Level380ItemRequirementsUpdatePlugIn` already matches in `client/src/bin/Data/Items/Group02_Mace.json` through `Group11_Boots.json`:
  - Weapons (`2-16` Frost Mace, `2-17` Absolute Scepter, `4-23` Dark Stinger), shields (`6-17` Crimson Glory, `6-18` Salamander Shield, `6-19` Frost Barrier, `6-20` Guardian Shield), and the 8 level-380 armor sets (`7..11-45..52`: Titan, Brave, Phantom, Destroy, Seraphim, Faith, Phaewang, Hades) all have `requirements.level = 380`.
  - Destroy Gloves (`10-48`), Faith Gloves (`10-50`), and Hades Gloves (`10-52`) have `requirements.energy = 165`, `138`, and `181`.
  - Phoenix Soul set (`7,8,9,11-73`) has `requirements.vitality = 85`.

### 3.3 2nd Wing Options
- **Status**: **Matches**.
- `client/src/source/Engine/Object/ZzzInfomation.cpp` (lines 315–350) already decodes all 5 excellent option bits for the 2nd wings (`12-3` Wings of Spirits, `12-4` Wings of Soul, `12-5` Wings of Dragon, `12-6` Wings of Darkness, `12-42` Wings of Despair):
  - Bit 0 (`0x01`): `AT_IMPROVE_HP_MAX` (`50 + Level * 5`)
  - Bit 1 (`0x02`): `AT_IMPROVE_MP_MAX` (`50 + Level * 5`)
  - Bit 2 (`0x04`): `AT_ONE_PERCENT_DAMAGE` (`3 %`)
  - Bit 3 (`0x08`): `AT_IMPROVE_AG_MAX` (`+50`)
  - Bit 4 (`0x10`): `AT_IMPROVE_ATTACK_SPEED` (`+5`)
- `ITEM_CAPE_OF_LORD` (`13-30`) and `ITEM_CAPE_OF_FIGHTER` (`12-49`) are handled in their own branch (lines 337–360) with only their 3 options, matching `SecondWingOptionsUpdatePlugIn`.

### 3.4 Jewel of Harmony +14 / +15 Values
- **Status**: **Gap identified in desktop client binary table & reader**.
- `client/src/bin/Data/Local/Eng/JewelOfHarmonyOption_eng.bmd` (and `Por`/`Spn`) uses the classic 14-level binary record format (`5,400` bytes = `30` entries * `180` bytes, `LEVEL_COUNT = 14` for levels `0`–`13` only).
- In `client/src/source/UI/Inventory/UIJewelHarmony.h` and `UIJewelHarmony.cpp` (line 35), `HARMONYJEWELOPTION::HarmonyJewelLevel` and `Zen` have length `14` (`0`–`13`):
  - `UIJewelHarmony.cpp` (lines 171–274) and `ZzzInventory.cpp` (lines 2131–2135) index `harmonyjewel.HarmonyJewelLevel[ip->Jewel_Of_Harmony_OptionLevel]` without clamping or extending to 16 levels, which reads out of bounds (`Zen[0]` / `Zen[1]`) when the server sends harmony option level `14` or `15`.
  - `CriticalDamageBonus.cpp` (lines 58–60) bounds-checks `>= 14` and returns `0` for harmony critical damage at option levels `14` and `15`.
- Follow-up needed when the desktop client's harmony table is updated: extend `HARMONYJEWELOPTION` to 16 levels (`0`–`15`) and populate levels `14` and `15` (or fall back to the server's `HarmonyLevels14And15UpdatePlugIn` table values when the BMD file has 14 levels).

### 3.5 Teleport and Soul Barrier for the Dark Wizard
- **Status**: **Matches**.
- `client/src/bin/Data/Local/Eng/skill_eng.bmd` (and `client/src/bin/Data/Local/Skill.bmd`) already has `RequireClass[0] = 1` (Dark Wizard, first class) on both Skill `6` (`Teleport`, level `17`, mana `30`, energy `100`, range `6`) and Skill `16` (`Soul Barrier`, level `77`, mana `70`, AG `22`, energy `126`, range `6`).
- `client/src/bin/Data/Items/Group15_Etc.json` already has `classRequirements: { "darkWizard": 1 }` on both `15-5` (`Scroll of Teleport`) and `15-15` (`Scroll of Soul Barrier`), matching `DarkWizardTeleportSoulBarrierUpdatePlugIn`.

### 3.6 Eternal Wing Set (Item Set 44)
- **Status**: **Matches**.
- All 6 pieces in `client/src/bin/Data/Items/` already carry the `"Eternal Wing"` names and the Season 6/9 server suite stats and class rank (`summoner: 2`):
  - `5-20` (`Eternal Wing Stick`): drop level `147`, damage `66`–`74`, magic power `106`, required level `380`, strength `57`, dexterity `13`.
  - `7-44` (`Eternal Wing Helm`): drop level `116`, defense `35`, required level `380`, strength `62`, dexterity `20`, energy `170`.
  - `8-44` (`Eternal Wing Armor`): drop level `131`, defense `49`, required level `380`, strength `62`, dexterity `20`, energy `170`.
  - `9-44` (`Eternal Wing Pants`): drop level `122`, defense `41`, required level `380`, strength `62`, dexterity `20`, energy `170`.
  - `10-44` (`Eternal Wing Gloves`): drop level `102`, defense `29`, required level `380`, strength `62`, dexterity `20`, energy `170`.
  - `11-44` (`Eternal Wing Boots`): drop level `99`, defense `32`, required level `380`, strength `62`, dexterity `20`, energy `170`.
