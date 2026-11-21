# Ledger: game data (Season 6/9), 2026-11-21

Base commit read: `cbf4593e` (origin/claude/hnmo-main-handover-54co2m). Worktree `HNMO-data`.

Method: HNMO's real data was dumped from a fresh in-memory Season 6 database (throwaway test
`ZzDataDump`, kept at `data-audit/ZzDataDump.cs.txt`, CSVs in `data-audit/hnmo/`). The
suite tables were parsed by throwaway node scripts (`data-audit/js/`) and diffed table by table; the
diffs are in `data-audit/diff-*.txt`. Where a table is not in the suites' files (it is compiled
into their game server), it says so.

HNMO's Season 9 program uses the same Season 6 data (`VersionSeasonSix.DataInitialization`, key `season6`);
there is no separate Season 9 data set. So every table was compared with the Season 6 suite and then with the
Season 9 suite; a Season 9 difference is a question of whether the Season 9 program should get its own data.

Suite files: S6 = `the Season 6 suite/.../Files`, S9 = `the Season 9 suite/.../FileServer` (read-only).

"Read in full" below means: every rule-bearing value the unit produces was compared through the dump, and the
files named were read line by line where a difference had to be explained. "Not read" gives the reason.

PR numbers: #A = plus-mix rates (#217, closed: owner kept HNMO 20 Nov), #B = #218 380 requirements, #C = #220 socket critical damage, #D = #222 Dark Wizard
Teleport/Soul Barrier, #E = #223 Raklion/Swamp gate levels, #F = #230 Blood Castle play time.

| unit id | commit | suite files compared | evidence (one line per table) | status |
|---|---|---|---|---|
| `server/src/Persistence/Initialization (files 1)` | cbf4593e | S6 `Scripts/Misc/ExpCalc.lua`, `ExpSystem.xml`; S9 same | EXP table: HNMO `10*(L+8)*(L-1)^2 (+1000*(L-247)*(L-256)^2 over 255)` equals the suites' `SetExpTable_Normal` shifted by one level; master EXP `505L^3+228045L^2+35278500L` equals `SetExpTable_Master` for ML 1..200 (checked 1, 2, 10, 100, 200) - same. Max level 400 / master 200 - same. EXP rates in `ExpSystem.xml` are a server's rate setting (x1000), not a rule - not compared. `ItemOptionDefinitionNumbers`, `MiniGameConstants` (enter 5 min - same as suites' BC/DS/CC/IT "open 5"), `WingsInitializerBase`, `StatsExtensions` checked through the item/option dumps. | read in full (data outputs); `BaseMapInitializer`, `TerrainUpdateHelper`, `GuidHelper`, `I*` interfaces hold no rule values |
| `.../Captions` | cbf4593e | none | names and captions only, no rules | not read: no game rules |
| `.../CharacterClasses` | cbf4593e | S6/S9 `Scripts/Character/CalcCharacter.lua`, S6/S9 `MuOnline.sql` (DefaultClassType) | Base stats (7 classes): Str/Agi/Vit/Ene/Cmd, points per level - same. Combat formulas (damage by stat, magic damage, attack speed, magic speed, attack rate PvM/PvP, defense rate PvM/PvP, defense, elf bow/melee, RF vitality, DL command): all equal to `CalcCharacter.lua` - same. Max HP/mana: differs for DW (+1), DK (+2 HP, +0.5 mana), DL (-1 mana), Summoner (HP base 39 vs 33, mana 1.7/energy vs 1.5), RF (HP base 57 vs 48.7, mana base 7 vs 13) against `DefaultClassType` - owner question Q7 (the suites' table itself has a swapped DL life/mana row, so it is not a clean reference). | read in full |
| `.../Items` | cbf4593e | S6 `Data/Items/ItemList.xml`, S9 `Data/Items/ItemList.xml` | Item base classes (ArmorInitializerBase, WeaponInitializer...): class rank mapping treats rank 2 like rank 1 (`DetermineCharacterClasses`, `classValue < 3`) - 104 items (2nd-class sets: Dark Phoenix, Grand Soul, Divine, Great Dragon, Venom Mist, Sylphid Ray, Demonic, Storm Blitz, Eternal Wing...) are wearable by 1st class in HNMO, 2nd class+ in both suites - owner question Q1. Gloves overload has no energy parameter - fixed #B. | read in full |
| `.../PlugIns` | cbf4593e | none | default plug-in configurations; Happy Hour, PK, party EXP checked by earlier PRs (verified: unchanged since) | read: no suite table to diff |
| `.../Skills` | cbf4593e | S6/S9 `Skills/SkillList.xml` (403 rows, S6 = S9) | 282 skills compared: damage, distance, mana, AG, energy - same except: Teleport/Soul Barrier classes (fixed #D); Abolish Magic all classes in suites, Lords only in HNMO (Q9); Lance DW/Summoner rank 1 in suites, SM+ in HNMO (item skill, Q9); distance of Evil Spirit 6/7, Cometfall 6/3, Hellfire/Inferno 0/4 (HNMO uses distance as the area radius for these; not changed, Q9); Nova AG 45/0 and Chain Drive Strengthener AG 20/22 (Q9). Level requirement: HNMO folds it into the energy requirement exactly as the classic formula `reqEnergy*reqLevel*4/100+20` (Lightning 72, Aqua Beam 345...) - same. Master skills (121 suite rows, 34 HNMO-only rows): not diffed row by row - see the master tree note below. | read in full (normal skills); master tree not diffed |
| `.../Updates (files 1)` | cbf4593e | as the tables they patch | Update plug-ins: their end state is what the dump shows (fresh DB = initializer + `RunsOnNewDatabase` ones). Each plug-in's "old value" guard read for the tables changed here. Added #B-#F. | read in full for the touched tables; the others were not reread (their result is in the dump) |
| `.../Updates (files 2)` | cbf4593e | same | same; `DevilSquarePlayerLimit` (15 = suite `MaxPlayerPerRoom` 15) - same; `ExcellentDropChance` verified (earlier PR) | same |
| `.../Updates (files 3)` | cbf4593e | same | same; `FixItemRequirementsPlugIn` (elf bows) - checked, values equal ItemList | same |
| `.../Updates (files 4)` | cbf4593e | same | `JewelNpcPricesUpdatePlugIn` verified: Bless 3 M, Life 16 M, Creation 15 M, Guardian 15 M = `PriceSettings.ini` - same; Soul 6 M = suite 6 M - same; Chaos: HNMO 810,000? see Q12; `GroundDropDuration`, `HappyHourSchedule` verified unchanged | same |
| `.../Version075 (files 1)`, `Version075/Items`, `Version075/Maps`, `Version075/TestAccounts` | cbf4593e | none | version 0.75 data: no 0.75 suite exists | not read: no reference for this version (and not a played season) |
| `.../Version095d` | cbf4593e | none | version 0.95d data: no reference. Noted in passing: 0.95d Teleport is `DarkWizard` only (no Soul Master) | not read: no reference |
| `.../Version097d` | cbf4593e | none | 53 lines, version 0.97d | not read: no reference |
| `.../VersionSeasonSix (files 1)` | cbf4593e | S6/S9 `ChaosBox.ini`, `Warps/GateSettings.xml`, `MoveReq.xml`, `MoveLevel.xml`, `ShopList.xml`, `Shops/*.xml`, `PriceSettings.ini` | Chaos mixes (42 recipes): +12 55 vs 60, +14 50 vs 55 - different, owner kept HNMO (20 Nov; draft #217 closed, branch removed); +10/+11/+13/+15, luck +25, enhanced -10, socket -20, wings 2 max 90, cape 90, 3rd wing stage 1 max 60 / stage 2 max 40 - same; 3rd wings luck 5 % vs suite `Wing3LuckRate` 10 (S6 only) - Q5; socket weapon mix (40 %, 1 M) - HNMO has none - Q6. Gates (341 suite, 102 enter gates same): Raklion 286/288/290/292 240 vs 270 and Swamp 274-285 250 vs 400 - fixed #E; gate 7 target one tile shorter - left. Warp list (39 rows): levels and costs same; HNMO has Crywolf (26, lvl 190) and Loren Market (44) the suites don't list, S6 has Crywolf 48 lvl 300, Stadium 49 - Q8; Arena/Tarkan/Kanturu 3 target areas differ slightly - left. Shops (27 NPCs): Caren, Zienna, Harold, Pasi same; Elbeland NPCs 415/416/417 hold different lists than the suites' (HNMO = classic roles) - Q10; Castle Siege contracts 13-7 missing at 7 merchants, Old Scroll/Covenant +6 at Lumen, 0-41 at Bolo, shops for 239/379/492/583/604 - Q10; shop item levels/options small differences (Lala 10-10/11-10 +0 vs +3, Eo/Izabel shield option level) - Q10. Jewel NPC prices verified (earlier PR). | read in full |
| `.../VersionSeasonSix (files 2)` | cbf4593e | S6/S9 `SkillList.xml`, `MasterSkillTree.*`, `ClassQuest.xml` | SkillsInitializer: see Skills row; fixed #D. Quests.cs: class quests not diffed (suite `ClassQuest.xml` format differs; time). | read in full (skills); quests not read: time |
| `.../VersionSeasonSix/Events` | cbf4593e | S6 `Data/Events/*.dat`, `Data/Events/*.ini|xml`; S9 `Events/*/*.xml` | Blood Castle: open 5 / play **20** / close 1 vs suites 5 / 15 / 1 - fixed #F; statue/gate HP are monster data (other agent). Devil Square: 5 / 20, 15 players - same; DS monster waves per square (S6 dat) - monster data, other agent. Chaos Castle: 5 / 10 / 1, min 2 players - same (S9 min 1 - Q11). Illusion Temple: 5 / 15, min 2 - same; town spawn 4 min vs exit 1 - left. Level ranges of BC/DS/CC/IT are compiled into the suites' game server - not in files, not compared. Ticket drops: BC scroll/bone and DS eye/key 1 % vs suite 0.8 % (80/10000) - Q4. Kanturu, Doppelganger, Imperial Guardian, Crywolf, Castle Siege: timings only (monster data skipped). | read in full (timings) |
| `.../VersionSeasonSix/Items (files 1)` | cbf4593e | S6/S9 ItemList, `ExcellentOptions.xml`, `HarmonyItemOption.ini`, `ItemSetOption.xml` | Items (817 HNMO, 1003 S6 / 1260 S9 rows): damage, speed, defense, durability, size, drop level same for all base items except: 380 items without level 380, gloves without energy, Phoenix Soul without vitality 85 - fixed #B; set 44 (HNMO "Lilium", suites "Eternal Wing", different stats) - Q2; Divine Archangel weapons 220-230 etc. = S9 values, S6 suite 120-130 - Q3; gloves/boots 62-72 without attack/walk speed - Q13; trade/drop/store/sell/repair flags (~300 rows, e.g. wings not droppable in the suites) - Q13. Excellent options: values same (zen +40 % vs suite 30 - Q12). Harmony: values same up to item +13; the suites list +14/+15 values HNMO lacks - Q14. Orb/scroll energy is stored as the computed requirement - same by formula. | read in full |
| `.../VersionSeasonSix/Items (files 2)` | cbf4593e | S6 `SocketOptionSettings.ini`, S9 `SocketSystem/SocketOption.xml`, ItemList | Socket options (27 + 6 bonus + 2 package): same except lightning "critical damage increase" 15/20/25/30/40 vs suites 30/32/35/40/50 - fixed #C; water "shield protection" level 5: HNMO 30 = S9 30, S6 20 - Q15; S9 wind "max AG"/"AG value" changed in S9 - Q15. Wings: 2nd wing options HP/MP/ignore same; suites add +AG 50 and +speed 5 on 2nd wings - Q12. 3rd wing options same. Weapons: see items. Pets/potions/scrolls: values in the item diff. | read in full |
| `.../VersionSeasonSix/Maps (files 1)`, `(files 2)`, `(files 3)` | cbf4593e | gates/warps above | map files hold spawns (monster data - the other agent) and safe zones; gates are in `Gates.cs` (compared). Map EXP multipliers and requirements dumped (`maps.csv`), no suite counterpart. | not read: spawns belong to the monster agent |
| `.../VersionSeasonSix/TestAccounts` | cbf4593e | none | test characters only | not read: no game rules |

Master skill tree: HNMO keeps it in `MasterSkillTree.xml` + `SkillsInitializer.AddMasterSkillDefinition`; the S6
suite has a binary `.dat`, S9 an xml for the 330-level S9 tree. Not diffed (different formats, Season 9's is a
different tree) - listed as not done.

Drops: HNMO uses one jewel group (0.1 %, Bless/Soul/Chaos/Life/Creation and pets) and the 30 % random item group;
the suites use a per-monster-level table (`MonsterItemDropRate.xml`, e.g. Life from level 13 at about 1 %).
Different design - Q16, not diffed number by number.

## Owner questions (Q numbers used above)

- Q1 Item class ranks: suites' rank 2 = second class and up; HNMO lets the first class wear 104 such items (DetermineCharacterClasses treats 2 like 1). Options: follow the suites (code change in the helper + update plug-in; first-class characters can't equip them any more) / keep. Recommend: follow the suites.
- Q2 Item set 44 (helm..boots): HNMO "Lilium" (no level req, drop 82-113) vs suites "Eternal Wing" (380, energy 170, other stats). Which client item data is shown decides; recommend check the client's item.bmd first, then follow it.
- Q3 Divine (Archangel) weapons 0-19/2-13/4-18/5-10: HNMO 220-230 etc. = S9 suite; S6 suite 120-130. Recommend keep HNMO (matches S9 and the classic client).
- Q4 BC scroll/bone and DS eye/key drop 1 % vs suite 0.8 %. Recommend follow the suites (small) or keep - owner's taste.
- Q5 3rd wings luck 5 % vs S6 `Wing3LuckRate` 10 % (S9 no key). Recommend 10 %.
- Q6 Socket weapon mix (40 %, 1 M zen) missing in HNMO. Feature gap; recommend add later.
- Q7 Max HP/mana base values per class differ slightly (DW +1, DK +2, Summoner 39 vs 33 and mana 1.7/energy vs 1.5, RF 57 vs 48.7, mana base 7 vs 13). The suites' table has an obviously swapped DL row, so it's not clean. Recommend keep HNMO unless the owner wants exact suite values.
- Q8 Warp list: HNMO Crywolf (lvl 190) and Loren Market; S6 Crywolf lvl 300, S9 none; Stadium entry. Recommend keep HNMO.
- Q9 Skills: Abolish Magic (suites: all classes; HNMO: Lords), Lance (suites: DW/Summoner rank 1), distances Evil Spirit/Cometfall/Hellfire/Inferno (HNMO area radii), Nova AG 45. Recommend keep (HNMO's area model), ask only about Abolish Magic.
- Q10 Shops: Elbeland NPC roles (415/416/417), Castle Siege contracts (item not in HNMO), IT +6 tickets at Lumen, shops for 239/379/583/604, Moss gamble. Recommend keep HNMO (classic roles), add IT +6 tickets.
- Q11 Chaos Castle minimum players: S9 suite 1, S6 2 (HNMO 2, owner chose 2). Done.
- Q12 Excellent zen +40 % (HNMO, classic client) vs suite 30 %; Jewel of Chaos 810,000 vs 800,000; Loch's Feather/Crest 180,000 vs 100,000 (price change needs itemValue.ts); 2nd wings: suites add +AG 50 and +attack speed 5 options. Recommend keep HNMO for zen and prices; ask about the 2nd wing options.
- Q13 Item flags (trade/drop/store/sell/repair) differ on ~300 rows (e.g. suites: wings not droppable, jewel bundles not repairable is the same idea); gloves/boots 62-72 have no attack/walk speed. Recommend keep HNMO flags; add the speeds.
- Q14 Harmony options stop at item +13; suites list +14/+15 values. Recommend add the two levels.
- Q15 Socket water "shield protection" level 5: HNMO/S9 30, S6 20; S9 wind AG options changed. Recommend keep HNMO.
- Q16 Drops: HNMO single jewel group 0.1 % vs suites' per-level jewel table. Recommend keep HNMO (owner already tuned drops).
- Q17 Season 9 program: uses the Season 6 data; S9 suite changes 210 shared item rows (archangel weapons, Guardian Staff...), 281 S9-only items, S9 sockets, no Crywolf warp. Recommend a separate S9 data set later.
- Blood Castle play time (#F): the owner kept "event timetables" (start times) on 20 Nov; the play length is a different setting, so it is a draft PR - close it if the owner meant the whole event timing.

## Owner's answers (21 Nov 2026) and what was done

Keep HNMO (no PR): event ticket drops 1 % - different, owner kept HNMO (21 Nov); Archangel weapons (S9 values) -
different, owner kept HNMO (21 Nov); base HP/mana of DW, DK, Summoner, RF - different, owner kept HNMO (21 Nov);
item prices (excellent zen +40 %, Jewel of Chaos 810,000, Loch's Feather/Crest 180,000) - different, owner kept HNMO
(21 Nov); item trade/drop/store/sell/repair flags - different, owner kept HNMO (21 Nov); skill class lists (Abolish
Magic, Lance) - different, owner kept HNMO (21 Nov). Socket shield protection level 5 for Season 9 (30 %) - kept
for a future Season 9 data set.

Done as draft PRs (numbers in the final report): rank 2 items, 3rd wings luck 10 %, Crywolf warp 300, shops
(Elbeland, Natasha, Lumen +6), 2nd wing AG/speed options, lucky gloves/boots speeds, harmony +14/+15, jewel drops by
monster level, Eternal Wing set (both clients' item data show "Eternal Wing"), socket shield protection 20 % (S6).

Not done, with the reason:
- Area-skill distances: HNMO uses `Skill.Range` as the area radius of Evil Spirit, Hellfire, Inferno, Cometfall
  (AreaSkillTargetSelector.GetQueryCenterAndRange); the suites' "Distance" is the cast distance. Copying 0 to Hellfire
  and Inferno would make them hit nothing. It needs a code change (cast distance and area radius apart), not data.
- Nova AG: already the same in effect. HNMO splits Nova into Nova (Start) (58), which costs the suites' 45 AG, and
  Nova (40), which costs 180 / 12 mana per stage.
- Socket weapon mix: the suites have only its rate (40 %) and zen (1,000,000); the recipe is compiled into their
  server program, and the clients' mix.bmd has no such recipe either.
- Shops: Arena Guard (239) and the Jin NPCs (583/604) are NPCs HNMO doesn't have (Jin stands on maps 91/92, which
  Season 6 HNMO has no data for); Moss (492) sells gamble items 13-71..75 that HNMO has no definitions for.
  Natasha (379) gets the shop; whether she is spawned in Devias was not checked.
- Noted: HNMO's 13-50 is named "Scroll of Blood" (2x2) where the suites have the Illusion Sorcerer Covenant (1x1) and
  13-51 the Scroll of Blood; Lumen sells 13-50 as the covenant. Item data question, not changed.
- Noted: Scroll of Fire Scream (12,35) - suites let the Rage Fighter read it too; HNMO only the Dark Lord.
