# Season 6 & Season 9 Client / Server Import Spec

- **Date:** 2026-11-19 (Australia/Sydney)
- **Scope:** Authoritative local inspection of extracted Season 6 (`IGCN x6`) and Season 9 (`IGCN x9` EP2) client + server files outside git, mapping exact schemas into HNMO (`/import-server`, Wave 2, Waves 4B–4D) without committing any third-party files or binaries.

## 1. Client Comparison Summary

### 1.1 Season 6 Client vs. HNMO `client/src/bin/Data`
- HNMO (`14,891` files) already includes the full standard Season 6.3 Webzen client asset tree (`IGCN x6` client has `14,104` files).
- The `582` files present in the `IGCN x6` client folder but absent from HNMO are vendor-custom additions (`Object7/` custom Atlans textures `386`, `IGC/IGCUI/` launcher/HUD skins `110`, `IGC/Jewels/AncientGem.bmd`, `Local/` custom texts `37`, `World7/` `35`, test files `EncTerrainTest3.obj`, `World3.zip`, `mini_maptest.OZT`), not missing Webzen S6 base maps or models.

### 1.2 Season 9 Client
- Covered in [s9-asset-spec-2026-11-19.md](s9-asset-spec-2026-11-19.md): `3,578` `BMD:0x0e` models, `8` extra `World*`/`Object*` folders (`World83`, `92`, `94`, `95`, `96`, `99`, `101`, `103`), `"MAP\x01"`, `"ATT\x01"`, `.obj` v1 (`32`-byte stride), and `.ozp` -> `.png` unwrap — all decoded natively in Wave 4A (`modulusCipher.ts`, `formats.ts`, `GameFiles.cs`).

## 2. Server Config Formats (`IGCN x6` & `IGCN x9`)

### 2.1 Differences Corrected / Supported in `LegacyFileParsers.cs`
1. **`IGC_BonusEvent.xml`** (`x6` & `x9`):
   - Real schema: `<BonusEvent Enable="1" DisplayNotice="1"><Event DayOfWeek="-1" StartHour="8" EndHour="10" ExpMultiplier="1.5" MasterExpMultiplier="1.5" ItemDropBonus="15" ExcDropBonus="5" /></BonusEvent>`
   - Uses `ExpMultiplier` (e.g. `1.5` = `+50%`) and `ItemDropBonus` (not `ExpBonus`/`DropBonus`), plus `EndHour`, optional `StartMinute`, and `DayOfWeek` (`-1` daily, `0..6`).
2. **`IGC_MonsterSpawn.xml`** (`x9` replaces `MonsterSetBase.txt`):
   - Real schema: `<MonsterSpawn><Map Number="0" Name="..."><Spot Type="0|1|2|3" ...><Spawn Index="..." Distance="..." StartX="..." StartY="..." EndX="..." EndY="..." Dir="..." Count="..." Element="..." /></Spot></Map></MonsterSpawn>`
   - `Spot Type`: `0` = NPC/Trap, `1` = Multi-spawn (`EndX/EndY/Count`), `2` = Single spawn, `3` = Elemental spawn (`Element` `1..5` = Fire/Water/Earth/Wind/Darkness, `6` = Random).
3. **`ItemBags/*.xml`** (`x6` & `x9` weighted multi-pool bags, `73+` files):
   - Real schema: `<ItemBag><BagConfig Name="..." ItemRate="10000" SetItemRate="0" MoneyDrop="..." BagUseRate="10000" /><DropAllow Wizard="1" ... PlayerMinLevel="1" PlayerMaxLevel="800|MAX" MapNumber="-1"><Drop Rate="100" Count="1"><Item Cat="..." Index="..." ItemMinLevel="0" ItemMaxLevel="0" Skill="0" Luck="0" Option="0" Exc="-1" SetItem="0" SocketCount="0" ElementalItem="0" /></Drop></DropAllow></ItemBag>`
4. **`IGC_SetItemDropManager.xml`** (`x6` & `x9` Ancient drops):
   - Real schema: `<SetItemDropManager DropUseRate="10000"><Rule MonsterIndex="-1" MonsterMinLevel="1" MonsterMaxLevel="150" PlayerMinLevel="1" PlayerMaxLevel="MAX" MapNumber="31"><ItemDrop Cat="-1" MinLevel="0" MaxLevel="0" Count="1" Skill="-1" Luck="-1" Option="0" Exc="0" DropRate="1" /></Rule></SetItemDropManager>`
5. **`IGC_InvasionManager.xml`** (`x9`) & **`EventsManager.dat`** (`x6`):
   - `IGC_InvasionManager.xml`: `<InvasionManager Enable="1"><Invasion Type="0" Name="Red Dragon Invasion"><Start DayOfWeek="-1" Hour="1" Minute="0" />...</Invasion></InvasionManager>`
   - `EventsManager.dat`: section `0` lines with `<Type> <Hour> <Minute>`.
6. **Event XML Schedules** (`IGC_BloodCastle.xml`, `IGC_ChaosCastle.xml`, `IGC_DevilSquare_Classic.xml`, `IGC_ArcaBattle.xml`, `IGC_AcheronGuardian.xml`, `IGC_TormentedSquare.xml`, `IGC_ChaosCastle_Survival.xml`, `IGC_IllusionTemple_Renewal.xml`):
   - Real schedule nodes: `<Schedule><Start Hour="..." Minute="..." /></Schedule>` or `<EventSchedule><Start Hour="..." Minute="..." /></EventSchedule>` (note: real `IGC_IllusionTemple_Renewal.xml` uses typo `Monute="..."` on hours `1–9` and `Minute="..."` on `10–21`).
7. **`IGC_ChaosBox.ini`** (`x6` & `x9`):
   - Separate upgrade rate prefixes: `Mix10Rate_Normal..Mix15Rate_Normal` (+ `MixAddLuckRate_Normal`), `Mix*_Enhanced` (excellent/ancient), `Mix*_Socket`, and `Mix*_Pentagram`, plus `DQChaosSuccessRateLevel1..7`.
8. **`IGC_ExpSystem.xml`** (`x6` & `x9`):
   - `<ExpSystem CalcType="0"><StaticExp Normal="1000.0" Master="1000.0" Event="0.0" Quest="1.0" /><DynamicExpRangeList><Range MinReset="..." MaxReset="..." MinLevel="..." MaxLevel="..." NormalExp="..." MasterExp="..." /></DynamicExpRangeList></ExpSystem>`

## 3. Season 9 Complete Map, Catalog & System Schemas (Wave 4B–4D)

### 3.1 `IGC_MapList.xml` & `MapTerrains/*.att` High-ID Table
| Map ID | Server Terrain File (`IGCData/MapTerrains/`) | Client Folder (`World{ID+1}`) | Event / Mode |
| --- | --- | --- | --- |
| `79` | `79_LorenMarket.att` | `World80` | S6/S9 Market |
| `80` / `81` | `80_Karutan1.att` / `81_Karutan2.att` | `World81` / `World82` | Karutan 1 & 2 |
| `82..90`, `93..94` | `Null.att` | (`World83`, `World94`, `World95` unused on x9 GS) | Reserved / test |
| `91` | `91_Acheron.att` | `World92` | Acheron main hunting zone |
| `92` | `91_Acheron.att` | Shares `World92` | Acheron Arca / Guardian obelisks (Alkmaar / Ubaid) |
| `95` | `95_Debenter.att` | `World96` | Debenter |
| `96` | `96_Debenter_ArcaBattle.att` | `World96` | Debenter Arca Battle / Acheron Guardian obelisk #3 |
| `97` | `97_ChaosCastleSurvival.att` | Shared CC | Chaos Castle Survival of the Fittest |
| `98` / `99` | `98_IllussionTempleLeague1.att` / `99_IllussionTempleLeague2.att` | `World99` | Illusion Temple League / Renewal |
| `100` | `100_UrkMontain.att` | `World101` | Urk Mountain (`101` reserved event twin) |
| `102` | `102_TormentedSquare.att` | `World103` | Tormented Square |

*(Note: Crywolf also switches between `34_Crywolf_Peace.att`, `Occupied.att`, `War.att` by `OccupationState="0|1|2"`, and Kanturu Boss switches between `39_KanturuBoss_Close.att` and `Open.att` by `MayaState="0|1"`.)*

### 3.2 Season 9 Catalog Counts & Key IDs (`IGCN x9` vs `IGCN x6`)
- **`IGC_ItemList.xml`**: `1,284` items in x9 vs `1,003` in x6 (`+281` items).
  - Mithril & bundles: `12:144` (Mithril Fragment), `12:145` (Mithril), `12:148` (Mithril Fragment Bunch), `12:149` (Elixir Fragment Bunch).
  - Pentagrams (category `12`): `12:200..207` Common (e.g. Muren's Book of Magic, Scroll of Etramu, Lorencia Knights' Iron Shield, Hero Elixir, Gladiator's Dagger), `12:208..211` Unique (Kundun's Madness Blade, Kundun's Magic Spell Scroll, Empire Guardians' Stronghold, Ancient Icarus Scroll), `12:212..214` Rare (Arca's Prophecy, Antonia's Sword, Kundun's Seal Scroll).
  - Errtels (category `12`): `12:221` (Errtel of Anger), `12:231` (Blessing), `12:241` (Integrity), `12:251` (Divinity), `12:261` (Gale).
  - Muun & evolution items (category `13`): `13:211` (Evolution Stone), `13:239` (Muun Energy Converter), `13:212+` (Muun pets + evolved variants), `13:276` (Chaos Castle Survival FinalAward).
- **`IGC_MonsterList.xml`**: `659` monsters in x9 vs `639` in x6 (`+20` core S9/elemental/boss entries beyond shared S6 base).
- **`IGC_SkillList.xml`**: `618: Spell of Protection`, `619: Spell of Restriction`, `620: Spell of Pursuit`, `621: Shied-Burn` added beyond S6 skills (`IGC_SkillList_s6.xml`).

### 3.3 Season 9 New System XML Schemas (Waves 4B–4D Reference)
- **Pentagram / Errtel (`Items/IGC_Pentagram*.xml`)**:
  - `IGC_PentagramOption.xml`: `<PentagramSystem><OptionList><Pentagram ItemCat="12" ItemIndex="200..214" Grade="1|2|3" OptionNum1..7="..." Name="..." /></OptionList><OptionSetting>...</OptionSetting></PentagramSystem>`
  - `IGC_PentagramMixNeedSource.xml`: `<Combination Index="..." SuccessRate="..." ItemCat1..4 ItemIndex1..4 ItemCount1..4 ReqMoney="..." />`
  - `IGC_PentagramItemDropRate.xml`, `IGC_PentagramJewelOutRate.xml`, `IGC_PentagramJewelOptionValue.xml`
- **Muun Companion System (`MuunSystem/IGC_Muun*.xml`)**:
  - `IGC_MuunInfo.xml`: `<Muun ID="..." ItemCat="13" ItemIndex="..." Type="1..4" Rank="1..5" OptionIndex="..." AddOptionType="..." AddOptionValue="..." OptionStartDate="..." OptionEndDate="..." EvolutionItemCat="..." EvolutionItemIndex="..." SkillDelayTime="..." Name="..." />`
  - `IGC_MuunOption.xml`: `<Option Index="..." Type="..." Value0..5="..." ConditionType="1=Map,2=LevelRange,3=TimeWindow,4=DayOfWeek" ConditionValue1=".." ConditionValue2=".." Name="..." Description="..." />`
  - `IGC_MuunExchange.xml`: `<EnergyConvertSettings StoneOfEvolutionPoint="2"><Muun Rank="..." RankPoint="..." Level="..." LevelPoint="..." /></EnergyConvertSettings>` + `<RequiredItemsExchange>` + `<BagListSettings>`
- **Extended Master Skill Tree (`IGC_MasterSkillTree.xml`) & `IGC_Common.ini`**:
  - `MaxMasterLevel = 330`, `MasterPointPerLevel = 1`, `MonsterMinLevelForMLExp = 95`.
  - `<MasterSkillTree><Class ID="1.."><Tree Type="0|1|2"><Skill Index="..." ReqMinPoint="..." MaxPoint="20" ParentSkill1="..." ParentSkill2="..." MagicNumber="300+" Name="..." /></Tree></Class></MasterSkillTree>`
- **Stat Specialization (`IGC_StatSpecialize.xml`)**:
  - `<StatSpecialize><Option ID="1..12" DK="0|1" DW="0|1" Elf="0|1" MG="0|1" DL="0|1" SU="0|1" RF="0|1" GL="0|1" /></StatSpecialize>`
- **Season 9 Events (`Events/`)**:
  - `IGC_ArcaBattle.xml` (guild Battle for Alkmaar/Ubaid/Debenter obelisks, maps `92` & `96`, Wednesday `22:00`), `IGC_AcheronGuardian.xml` (daily except Wed `23:00`), `IGC_TormentedSquare.xml` (tournament cycle days `17..25`, 3 stage types), `IGC_ChaosCastle_Survival.xml` (`MinLevel="280"`, tournament cycle days `16..21`), `IGC_IllusionTemple_Renewal.xml` (hourly `<EventSchedule>`), `IGC_BattleCore.ini`.
