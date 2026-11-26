# Season 9 part (c): evidence table (27 Nov 2026, Sydney time)

Base: PR #403 at `a1b730bd`. Branch: `local/s9c-gameplay` (local only). Each row traces one feature through: packet and
handler → game logic → attributes/damage → database → reply (view plug-in) → web client.

Status:
- **wired**: works end-to-end;
- **engine**: calculation code only, nothing calls it in play;
- **item**: item definitions only;
- **missing**: not there.

## Muun

| Step | Evidence | Status |
|---|---|---|
| Item definitions | 76 of the 90 catalogue Muun items are defined in `SeasonNineOnlyItemsCatalog.cs:197` (e.g. 13/212 Luki), all with slot `-1` (bag only) | item |
| Equipment location | No Muun inventory or slot: `InventoryConstants.cs:15-85` has slots 0-11 only, and no Muun packet exists in `server/src/Network/Packets/*.xml` | missing |
| Bonus by map, level, hour and weekday | `SeasonNineSystemsEngine.cs:69` (`IsMuunConditionMet`), `:120` (`EvaluateMuunBonus`); its only callers are the tests | engine |
| Evolution, energy converter | `SeasonNineSystemsEngine.cs:168`; no mix, NPC or item action uses it | engine |
| Settings | `SeasonNineSystemsConfiguration.cs:29` (evolution multiplier), `:32` (strict conditions), `:35` (drop rate): only `/muun` prints them | engine |
| Persistence across restart | Nothing to persist (no Muun state) | missing |
| Desktop client | No Muun code (`client/src/source`) | missing |
| Web client | No Muun code (`client/web/src`) | missing |

## Pentagram / Errtel

| Step | Evidence | Status |
|---|---|---|
| Item definitions | Pentagram and Errtel items, e.g. `SeasonNineOnlyItemsCatalog.cs:137` (12/200), `:152` (12/221 Errtel of Anger), slot `-1` | item |
| Pentagram slot, elemental affinity | No slot; no elemental attribute in the damage path (`AttackableExtensions.cs` has only the old element resistances, `:871`) | missing |
| Elemental multiplier | `SeasonNineSystemsEngine.cs:181`; not called by any damage code | engine |
| Errtel attach, rank, level, mix, extraction | `SeasonNineSystemsEngine.cs:220` (extraction rate); there is no socket-attach action, mix or extraction NPC | engine |
| Settings | `SeasonNineSystemsConfiguration.cs:41`, `:47`, `:50`: only `/pentagram` prints them | engine |
| Desktop client | Only effect enums `EFFECT_PENTAGRAM_JEWEL_*` (`_enum.h:4162`), no UI | missing |
| Web client | None | missing |

## Skills 618–621

| Step | Evidence | Status |
|---|---|---|
| Skill data | `SeasonNineSystemsUpdatePlugIn.cs` (S9 realm only; runs on new S9 databases). Version 2: no Rage Fighter line; 618-620 cost no mana; 621 costs 30 | wired (data) |
| Learning source | No skill item, master tree row, level or quest teaches them in HNMO's data (`master-tree.json` has no 618-621; the item catalogues name no such skill) | **missing: owner decision** |
| Cast 621 Shield-Burn | `TargetedSkillHandlerPlugIn.cs:50` → `ShieldBurnSkillPlugIn` (new) → `Player.BurnShieldAsync` (new): burns SD and never HP; same PvP rules as a hit (`Player.MayBeHitBy`); flag, season and safe-zone checks come before mana is spent | wired (server); browser cast not run |
| Cooldowns | `SkillDelays.cs:69-72` (618/619: 60 s, 620: 120 s, 621: 60 s), applied in `Player.TryConsumeForSkillAsync` | wired |
| Cast 618-620 | Default targeted handler: no magic effect is defined, so a cast does nothing, and now costs nothing | placeholder |
| Legacy 210–213 | Not touched | — |
| Web | `skills.ts:58` loads `s9-skills.json` in Season 9 (mana now 0 for 618-620) | wired (display) |

## Master Skill Tree and the level cap

| Step | Evidence | Status |
|---|---|---|
| The web's S9 tree (480 rows) against the server's master skills | `SeasonNineMasterTreeParityTest` (new). Every row whose skill exists on the server matches: maximum level, master prerequisites, class, and the F3:52/53 index (`MasterSkillExtensions.cs:22/377`) | wired |
| Rows the server can't learn | 108 skills the web tree draws are not server master skills in this base (passives of ranks 6-9 such as 314-324, and others; #411 adds some). The web merges them into the tree (`masterLevel.ts:99`), and a click on one is refused by the server | **missing** |
| 571 maximum level | Server 20, web tree 10 | **owner decision** |
| Cap: experience | `PlayerExperience.cs:197`; tables from `GameContext.cs:144/727`. The runtime cap change now rebuilds the tables (`GameContext.RefreshExperienceTables`); before the fix, a character past 200 read outside the table | wired (fixed) |
| Cap: plug-in off | The realm's own cap comes back; characters above it keep their level and gain no master experience | wired (fixed) |
| Cap: importer | `LegacyServerImportEngine.cs:41` (400; Season 9: 330) | wired |
| Cap: admin | `S9Systems.razor:63` (200-400, clamped in the plug-in) | wired |
| Cap: UI | The server sends the cap (`UpdateLevelPlugIn.cs:73`). `maxMasterLevelForSeason()` (`masterLevel.ts:80`) is unused | wired; one unused export |

## Blocked in Season 6.3 and Arena

| Step | Evidence | Status |
|---|---|---|
| Plug-in | `SeasonNineSystemsPlugIn.IsActive()` (`:52`): only in a Season 9 realm, whatever the stored "Season 9 only" flag says (that toggle is off the admin page) | wired (fixed) |
| Data | `SeasonNineUpdatePlugInBase`: S9 updates are no-ops outside S9 | wired |
| Web | S9 data is loaded only when `currentSeason() === 's9'` (`skills.ts:58`, `masterLevel.ts:99`, `miniMapData.ts`) | wired |

## Admin S9 pages and minimap

| Step | Evidence | Status |
|---|---|---|
| Save → configuration → game | The page saves the plug-in configuration; the plug-in reads it on its next run (cap) or cast (621) | wired |
| Settings that do nothing in play | `MuunEnabled`, `MuunDropRatePermille`, `MuunStrictConditions`, `MuunEvoPointMultiplier`, `PentagramEnabled`, `PentagramElementalDamageMultiplier`, `PentagramErrtelMixBonusPct`, `PentagramErrtelExtractBonusPct`, `SpellOfProtectionDefensePct`, `SeasonNineMinimapEnabled` | **toggles without effect** |
| Minimap | `miniMapData.ts` reads `Season9/minimap.json` in S9; the server's `SeasonNineMinimapEnabled` doesn't reach the browser; 31 maps and season switching in one tab were not run in a browser | engine/display; not run |

## Decisions for the owner
1. How players learn 618–621: skill item, master tree, level or quest.
2. The effects of 618–620 (Spell of Protection, Restriction, Pursuit). Until then they are no-cost placeholders.
3. Muun and Pentagram: a protocol, inventory location, desktop and web UI are needed before the engine can be wired. Is
   this wanted? The desktop client has no such UI, and the web follows the desktop.
4. Equipped Weapon Mastery (571): 20 or 10 levels.
5. The ten admin settings without effect: hide them until wired, or keep them?
