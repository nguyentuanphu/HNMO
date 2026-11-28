# Season 9 Muun: evidence and design (27 Nov 2026, Sydney)

Owner's decision (27 Nov popup): build Muun in full. Base: `integration/s9` (#419) with `fix/s9c-gameplay` (#424).

## What HNMO has

| Part | Where | State before this branch |
|---|---|---|
| Item definitions: 37 Muun pets and their evolved forms, the Evolution Stone, the Muun Energy Converter | `server/src/Persistence/Initialization/Updates/SeasonNine/SeasonNineOnlyItemsCatalog.cs:196-262` (group 13, numbers 211-262; slot -1, 1x1) | Bag items only. Nothing reads them as companions. |
| Pet table (76 rows: item, rank, option index, add-option type and value, evolved item, name) | `server/src/GameLogic/PlugIns/SeasonNineSystems/SeasonNineSystemsCatalog.cs:70-150` | Data only. Its header says it was taken from a Season 9 server data set, so the values are inspiration under coding rule 9; the owner may want to review them. |
| Option table (37 rows: type, value evolved / level 1..5, condition type and values) | `SeasonNineSystemsCatalog.cs:151-195` | Data only. |
| Calculation engine: condition check (map, total level, hour, weekday), bonus by level or evolved, energy points for a conversion | `server/src/GameLogic/PlugIns/SeasonNineSystems/SeasonNineSystemsEngine.cs:46-175` | Pure functions with unit tests (`server/tests/HNMO.Tests/SeasonNineSystemsTests.cs`). Never applied to a player. |
| Settings: `MuunEnabled`, `MuunEvoPointMultiplier`, `MuunStrictConditions`, `MuunDropRatePermille` | `SeasonNineSystemsConfiguration.cs:25-35`; admin page `server/src/Web/AdminPanel/Pages/S9Systems.razor` | Saved, but no effect in play. |
| `/muun` chat command | `MuunChatCommandPlugIn.cs`, `SeasonNineSystemsPlugIn.cs:110-130` | Prints counts and the multiplier only. |
| Season gate | `SeasonNineSystemsPlugIn.IsActive()` (#424): on, and the realm is Season 9 | Used by the master cap only. |

## What the clients have

- **Desktop client (`client/`, C++):** no Muun code or data. A search for "muun" in `client/src/source` and `client/src/bin/Data` finds nothing. There is no Muun window, slot or packet to copy, so the desktop client gives no reference for this feature. Its window is built later (see "Not in this branch").
- **Web client (`client/web/src`):** no Muun code. `client/web/tests/season9-items.test.mjs` only checks that the items exist in the S9 catalog.
- **Packets:** none for Muun in `server/src/Network/Packets`.

## Design

The companion system follows HNMO's own pattern for server-side features with a web window (the Jewel Bank: F5 3A/3B, a JSON state). It keeps the 1.04-style inventory protocol unchanged, so no client sees new item slots.

- **The Muun stays in the bag.** The active Muun is the bag item whose id is saved in the character's state row (feature `s9.muun`, `HnmoPlayerStateRowStoreRegistry`: the database on PostgreSQL, memory in tests). Equipping another one replaces it. If the item leaves the bag (traded, dropped, sold), it stops being active at the next recompute.
- **Level:** `Item.Level` (1..5, up to the option's highest non-zero level). **Evolved:** the evolved item number (the pet row has no further evolution).
- **Bonus:** one removable stat element per active Muun, recomputed on equip, unequip, entering the world and every periodic tick of the plug-in (the map, the total level, the hour and the weekday can change). The old element is always removed before a new one is added, so equipping again never stacks. The realm clock is Sydney time, as the other HNMO time rules are (BonusEvents, Economy, News), with a test clock.

| Option type | Stat |
|---|---|
| 1 (Attack/Wizardry/Curse +n) | `Stats.BaseDamageBonus` |
| 7 (Maximum Attack/Wizardry/Curse +n) | `Stats.BaseMaxDamageBonus` |
| 2 (Defense +n) | `Stats.DefenseBase` |
| 3 (Excellent Damage +n) | `Stats.ExcellentDamageBonus` |
| 4 (Critical Damage +n) | `Stats.CriticalDamageBonus` |
| 5 (Skill Damage +n) | `Stats.SkillDamageBonus` |
| 8 (Attack/Wizardry/Curse +n, non-PvP) | `Stats.BaseDamageBonusPvm` (monsters only) |
| 14 (Maximum Attack/Wizardry/Curse +n, non-PvP) | `Stats.BaseMaxDamageBonusPvm` (monsters only) |
| 10 (Excellent Damage +n, non-PvP) | `Stats.ExcellentDamageBonusPvm` (monsters only) |
| 11 (Critical Damage Chance +n %, non-PvP) | `Stats.CriticalDamageChancePvm` (n / 100, monsters only) |
| 12 (Skill Damage +n, non-PvP) | `Stats.SkillDamageBonusPvm` (monsters only) |
| 50, 53 (the Muun casts an attack skill) | not applied (no skill caster for companions yet) |

  The options marked non-PvP (8, 10, 11, 12, 14) land on PvM-only stats (`Stats.BaseDamageBonusPvm`,
  `BaseMaxDamageBonusPvm`, `ExcellentDamageBonusPvm`, `CriticalDamageChancePvm`, `SkillDamageBonusPvm`), which
  `AttackableExtensions.CalculateDamageAsync` adds only when the target isn't a player, next to the existing
  PvP-only `FinalDamageIncreasePvp` (owner's decision, 27 Nov: "chỉ đánh quái"). Tested in `PvmOnlyDamageBonusTests`.
- **Evolution:** the active Muun at its highest level plus one Evolution Stone (13/211) in the bag becomes the evolved item. The stone and the old item are taken in the same save as the new item.
- **Energy Converter:** a Muun that isn't active, plus one Muun Energy Converter (13/239), becomes energy points (`CalculateMuunEnergyConversionPoints(rank, level, MuunEvoPointMultiplier)`). The items are saved as taken before the points are written, so a stop in between loses points and never doubles them.
- **Level up:** costs energy, from the admin table `MuunLevelUpEnergyCosts` (entry 0: level 1 to 2, ...). HNMO's
  default is 10, 20, 30, 40 (`MuunLevelUpEnergyCost` times the level, which also covers a level without an entry). The
  owner chose the desktop client's values (27 Nov); they wait for the Season 9 pack (local Claude reads them).
- **Season gate:** every action and the bonus need `SeasonNineSystemsPlugIn.IsActive()` and `MuunEnabled`. In Season 6.3 and Arena, nothing applies and the window says it is off.
- **Packets** (XML plus generated code):
  - `HnmoMuunCommand` F5 2B (client → server): Action 1 = state, 2 = equip bag slot, 3 = unequip, 4 = evolve, 5 = convert bag slot, 6 = level up; Slot.
  - `HnmoMuunState` F5 2C (server → client): JSON with the active Muun, its bonus (or the unmet condition), the bag Muuns, energy, costs and an event text.
- **Web:** a Muun window (`client/web/src/ui/muunWindow.ts`), opened with Alt+U and from `/muun`, in EN and VI.

## Where each value comes from (coding rule 9; owner's decision 27 Nov: "Rà lại, làm theo desktop")

The desktop client has no Muun data (`client/src/source`, `client/src/bin/Data`: no Muun table), so no value has a
desktop source yet. Every value is either HNMO's own data or an admin setting; the pet and option tables are no longer
hidden constants: the admin page (S9 Systems) shows them and saves edits (`MuunPets`, `MuunOptions`); an unsaved table
uses the built-in one.

| Value | Source |
|---|---|
| Item group/number, size and slot of each Muun, the Evolution Stone and the Energy Converter | HNMO's own item data: `server/src/Persistence/Initialization/Updates/SeasonNine/SeasonNineOnlyItemsCatalog.cs:196-262` |
| Item names (EN, VI, DE, ZH) | HNMO resources: `server/src/Persistence/Initialization/Properties/SeasonNineItemNames*.resx` |
| Models of the Muuns (`Data/s9/Item/*pet*.bmd`) | the desktop client's data: `client/src/bin/Data/Items/Models/Group13_Helper.json` (numbers 211-262) |
| Pet table: rank, option index, add-option type and value, evolved item, skill delay | admin setting `MuunPets`; built-in default `SeasonNineSystemsCatalog.Muuns` (from #403) **waits for the S9 pack** |
| Option table: stat type, values evolved and level 1..5, condition type and values, text | admin setting `MuunOptions`; built-in default `SeasonNineSystemsCatalog.MuunOptions` (from #403) **waits for the S9 pack** |
| Option type to HNMO stat mapping | HNMO's choice (table above), from the option texts |
| Level-up energy | admin setting `MuunLevelUpEnergyCosts`, HNMO default 10/20/30/40, **waits for the S9 pack** |
| Energy of a conversion: (rank + level x 3) x multiplier, level 0 counting 4 | `SeasonNineSystemsEngine.CalculateMuunEnergyConversionPoints` (from #403) with the admin multiplier `MuunEvoPointMultiplier`; **waits for the S9 pack** |
| Realm clock for the hour/weekday conditions | HNMO default: Sydney time, as HNMO's other time rules |
| Highest level before evolving | derived: the option's last level with a value |

## Not in this branch: what is missing, precisely

- **The Muun attack skills (option types 50, 53: "Attack Skill (%d level)").** HNMO's data gives only a skill level per
  Muun level (4, 6, 8 at levels 1-3; 10 evolved) and, for 18 pets, a delay of 10 000 ms. Missing, and not in the
  desktop client either: which skill each Muun casts (number, damage type, element), its damage formula per skill
  level, its range and target rule (nearest monster? the hero's target?), its cooldown (is the 10 000 ms delay the
  cooldown?), and its effect/animation. A companion that attacks also needs an attacker object on the server (the hero's
  attributes, like a summon) and a packet to show the hit. Expected from the S9 pack: the desktop client's skill table
  for Muuns and its effect code.
- **The Muun next to the hero.** The models exist (`Data/s9/Item/*pet*.bmd`), but the desktop client has no code that
  places a companion: no follow offset, height, scale, idle/walk animation or attach rule to copy. Other players also
  can't know a hero's Muun: the appearance packet has no Muun field, so it needs a new view packet with the active Muun
  of each hero in range. Expected from the S9 pack: the desktop client's companion rendering (position, animations).
- **Desktop client UI** (window and slot): the desktop client has no Muun window; building one needs a Windows build.
- **Muun drops** (`MuunDropRatePermille`): no drop rule yet.

