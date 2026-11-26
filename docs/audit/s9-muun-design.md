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
| 1, 8 (Attack/Wizardry/Curse +n) | `Stats.BaseDamageBonus` |
| 7, 14 (Maximum Attack/Wizardry/Curse +n) | `Stats.BaseMaxDamageBonus` |
| 2 (Defense +n) | `Stats.DefenseBase` |
| 3, 10 (Excellent Damage +n) | `Stats.ExcellentDamageBonus` |
| 4 (Critical Damage +n) | `Stats.CriticalDamageBonus` |
| 11 (Critical Damage Chance +n %) | `Stats.CriticalDamageChance` (n / 100) |
| 5, 12 (Skill Damage +n) | `Stats.SkillDamageBonus` |
| 50, 53 (the Muun casts an attack skill) | not applied (no skill caster for companions yet) |

  The "non-PvP" options (8, 10, 11, 12, 14) apply in PvP too: HNMO has no PvM-only stat for them yet. This is noted, not hidden.
- **Evolution:** the active Muun at its highest level plus one Evolution Stone (13/211) in the bag becomes the evolved item. The stone and the old item are taken in the same save as the new item.
- **Energy Converter:** a Muun that isn't active, plus one Muun Energy Converter (13/239), becomes energy points (`CalculateMuunEnergyConversionPoints(rank, level, MuunEvoPointMultiplier)`). The items are saved as taken before the points are written, so a stop in between loses points and never doubles them.
- **Level up:** costs energy. HNMO's data has no cost table, so it is a setting, `MuunLevelUpEnergyCost` (energy per current level; default 10; 0 in an existing database reads as 10). This needs the owner's confirmation.
- **Season gate:** every action and the bonus need `SeasonNineSystemsPlugIn.IsActive()` and `MuunEnabled`. In Season 6.3 and Arena, nothing applies and the window says it is off.
- **Packets** (XML plus generated code):
  - `HnmoMuunCommand` F5 2B (client → server): Action 1 = state, 2 = equip bag slot, 3 = unequip, 4 = evolve, 5 = convert bag slot, 6 = level up; Slot.
  - `HnmoMuunState` F5 2C (server → client): JSON with the active Muun, its bonus (or the unmet condition), the bag Muuns, energy, costs and an event text.
- **Web:** a Muun window (`client/web/src/ui/muunWindow.ts`), opened with Alt+U and from `/muun`, in EN and VI.

## Not in this branch
- **Desktop client UI:** the desktop client has no Muun at all, and its window code can't be built here (Windows).
- **The Muun's attack skill** (option types 50 and 53).
- **Muun drops** (`MuunDropRatePermille`).
- **The Muun shown next to the hero** (a model in the 3D scene).
