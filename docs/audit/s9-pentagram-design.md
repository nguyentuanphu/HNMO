# Season 9 Pentagram and Errtel: evidence and plan (27 Nov 2026, Sydney)

Owner's decision (27 Nov, popup): build Muun and Pentagram/Errtel in full. This file covers Pentagram/Errtel only.

## What HNMO had before this branch

| Part | Where | State |
|---|---|---|
| Item definitions: 15 pentagrams (12/200–214), 5 errtels (12/221, 231, 241, 251, 261) | `server/src/Persistence/Initialization/Updates/SeasonNine/SeasonNineOnlyItemsCatalog.cs:137-155` | item definition only (slot -1, no equip slot) |
| Pentagram grade per item, errtel options (60 rows: rank, option, values at level 0/5/10), extraction rates (55 rows) | `server/src/GameLogic/PlugIns/SeasonNineSystems/SeasonNineSystemsCatalog.cs:193-290` | data only |
| Element advantage (fire > darkness > wind > earth > water > fire), extraction rate | `SeasonNineSystemsEngine.cs:175-225` | calculation engine only, not called by combat |
| Settings: `PentagramEnabled`, `PentagramElementalDamageMultiplier`, `PentagramElementalAdvantageBonusPct`, `PentagramErrtelMixBonusPct`, `PentagramErrtelExtractBonusPct` | `SeasonNineSystemsConfiguration.cs:37-50` | no effect in play |
| `/pentagram` | `PentagramChatCommandPlugIn.cs` | shows the settings only |
| Desktop client | `client/src/source/Core/Globals/_enum.h:4162-4173` | buff effect ids for pentagram jewels only; no slot, window or packet |
| Web client | `client/web/public/data/lang/*.json` | texts only |
| Monster elements | none in HNMO's data | — |

The old socket seed system (`SocketCount`, seed spheres) is separate and untouched.

## What this branch adds (vertical slice)

- **Storage.** `Item.PentagramElement` (0 none, 1 fire, 2 water, 3 earth, 4 wind, 5 darkness) and `Item.PentagramErrtels` (text) on the item, with migration `20261126173519_AddPentagramToItem` and regenerated compiled models; `AssignValues` copies both. An errtel keeps its options (`index:level` per rank); a pentagram keeps its five slots (`number=element=options`, separated by `|`). No new configuration data, so no initializer or update plug-in is needed.
- **Slot.** `InventoryConstants.PentagramSlot` = 236 (after the personal store in the inventory's item storage), so no bag slot number changes. `PentagramActions.EquipAsync` and `UnequipAsync`. Unequipping needs room in the bag.
- **Errtels.** `PentagramActions.AttachErrtelAsync` and `RemoveErrtelAsync`:
  - an errtel must match the pentagram's element;
  - a pentagram has grade + 2 usable slots;
  - taking an errtel out needs room in the bag, else nothing changes;
  - each action is saved at once; a failed save puts everything back.
- **Damage.** `Pentagram.ElementalDamage`, added at the end of `AttackableExtensions.CalculateDamageAsync`:
  - attack = errtel "Elemental Dmg" (option 0), plus the "in PvP" options 20 and 120 in PvP;
  - defense = "Elemental Defense" (50), plus 70 and 170 in PvP;
  - the result (attack − defense) is multiplied by:
    - the advantage (`CalculateElementalMultiplier` with the admin settings);
    - "+% attack against an element" (10–14, 110–114);
    - "+% defense against an element" (60–64, 160–164);
    - in PvP: "+% damage" (140), "−% damage" (90) and "absorb" (190).
  - Option values come from HNMO's own data: at levels 0/5/10, linear in between.
- **Season gate.** Everything only when `SeasonNineSystemsPlugIn.IsActive()` (Season 9 realm) and `PentagramEnabled`.

## Still missing (next steps)

1. **Network.** Packets (XML in `server/src/Network/Packets`): the worn pentagram and its errtels, equip/unequip, attach/remove; the item serialization of element and errtels to the client; then regenerate the web packets.
2. **Web UI.** Pentagram slot in the character window, errtel window, element shown on items, elemental damage colour in the damage numbers.
3. **Desktop UI.** It has no pentagram window today: needs a new window (Windows build, not compiled here).
4. **Errtel sources and mixes.**
   - Where errtels and pentagrams come from: drops, an element given on drop.
   - The errtel level-up and rank mixes at the NPC (with `PentagramErrtelMixBonusPct`, all or nothing in one save).
   - Extraction from a pentagram with the existing rates and `PentagramErrtelExtractBonusPct`.
   - The data for the mix costs is not in HNMO yet: it needs the owner's numbers or the S9 client data.
5. **Monster elements.** HNMO's data has none, so against monsters only the attack counts. Needs a monster element list.
6. **Other options.** Success/critical/excellent rates (100, 150, 200, 230, 240, 40, 41) and the stat and max HP/MP/AG/SP options of errtel 261 (210–223) aren't applied yet. Neither are the raid-only options (21, 71, 121, 171, 191).
