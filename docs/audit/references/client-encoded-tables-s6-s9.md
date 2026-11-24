# Season 6 & Season 9 Reference: Client Encoded Data Tables & Remaining Suite Rules

Verified on `2026-11-24` using `tools/audit/suite-parsers/diff-all.mjs` and `tools/audit/suite-parsers/client-tables.mjs` against `LEGACY_S6_DIR`, `LEGACY_S9_DIR`, `LEGACY_S6_CLIENT_DIR`, and `LEGACY_S9_CLIENT_DIR` (no decoded client files committed).

## 1. Remaining Server Suite Rules (Part 4A)
- **Quests (`diff-quests.txt`)**:
  - `ClassQuest.xml`: `7` `QuestInfo` entries and `22` `Job` steps in both S6 and S9.
- **All Events & Invasions (`diff-events-all.txt`)**:
  - Shared across S6 & S9: Blood Castle, Bonus Event, Chaos Castle, CryWolf, Devil Square, DoppelGanger, Gambling Lottery, Golden Invasion, Illusion Temple (Classic in S6 -> Renewal in S9), Imperial Guardian, Kanturu, Last Man Standing, Loren Deep, Raklion, XMas Attack.
  - S6-only: Ring Attack (`RingAttackEvent.dat`).
  - S9-only: Acheron Guardian, Arca Battle, Chaos Castle Survival, Dragon Event, Invasion Manager, Tormented Square, White Wizard Attack.
  - HNMO `events.csv`: `40` configured event/difficulty rows.
- **Reward Bags (`diff-itembags.txt`)**:
  - `112` bags in S6 vs `118` bags in S9.
  - S9 adds Roomy Egg, Gift of Henchmen/Knights/High Knights, Minor/Normal/High Devil's Box, Guardsman's/Captain's Box, and Lord Silvester (`Monster 673`), and expands Kundun Box +3/+4/+5 pools from `5/10/10` items to `63` items each.
- **Master Skill Tree (`diff-master-skills.txt`, `MasterSkillTreeData.bmd`)**:
  - Client `MasterSkillTreeData.bmd`: `293` nodes in S6 vs `472` nodes in S9 (`+179` S9-only master skill nodes across all 7 classes).
- **Castle Siege (`diff-castle-siege.txt`)**:
  - Both S6 and S9 enable Castle Siege with `FriendlyFire=1`, `LordMixDailyLimit=1`, `TowerEntryWithoutAlliance=0`, and `24` NPC entries.
- **Gens System (`diff-gens.txt`)**:
  - S6 uses `GensSystem.ini` + `GensRanking.dat` (`13` rank tiers); S9 uses `GensSystem.xml` (`BlueMuMode=0`).
- **Common, PK, Party, Guild, Pet, Price, VIP (`diff-common-rules.txt`)**:
  - Diffs extracted between S6 and S9 `CommonServer.cfg` and `Common.ini` (`148` lines).

## 2. Client Encoded `.bmd` Data Tables (Part 4B, `diff-client-tables.txt`)
- **`MasterSkillTreeData.bmd`**: `293` nodes (S6) vs `472` nodes (S9).
- **`mix.bmd` / `Mix.bmd`**: `101` recipes (S6, 656-byte BUX records) vs `159` recipes (S9, expanded record layout).
- **`movereq_eng.bmd`**: `39` warps (S6) vs `37` warps (S9) vs `40` warps in HNMO `warps.csv`, with Kanturu Ruins 1/2/3, Kanturu Relics, and Vulcanus level/zen differences documented in `diff-client-tables.txt`.
- **Other Encoded `.bmd` Rule Tables (S6 vs S9 byte sizes verified)**:
  - `Item_eng.bmd` (`688,132` -> `786,264` bytes), `Skill_eng.bmd` (`57,204` -> `72,804` bytes), `BuffEffect_eng.bmd` (`24,972` -> `31,924` bytes), `Quest_eng.bmd` (`148,800` -> `148,800` bytes), `QuestProgress.bmd` (`37,515` -> `42,886` bytes), `NPCDialogue.bmd` (`5,104` -> `7,392` bytes), `SocketItem_eng.bmd` (`15,600` -> `16,200` bytes), `JewelOfHarmonyOption_eng.bmd` (`5,400` -> `135,000` bytes), `ItemSetOption_eng.bmd` (`7,044` -> `28,054` bytes), `ItemSetType.bmd` (`32,772` -> `49,156` bytes), `ItemAddOption.bmd` (`131,072` -> `131,072` bytes), `MonsterSkill.bmd` (`2,692` -> `3,652` bytes).
