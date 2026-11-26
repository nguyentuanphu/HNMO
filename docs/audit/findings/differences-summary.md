# Audit Differences Summary (HNMO vs Season 6 & Season 9 Server Suites)

Synthesizes every `"differs"`, `"missing in HNMO"`, real code bug, and feature/data gap recorded across all `docs/audit/data/diff-*.txt` tables and all `docs/audit/findings/unit-*.md` and `parser-*.md` reports (baseline `origin/claude/hnmo-main-handover-54co2m`, including audit gap review `#370`).

**Recommendation Legend:**
- **Bug to fix** — clear logic, state, persistence, or formula defect; queued for dedicated one-area Draft PRs with failing-before tests.
- **Owner choice -> `questions-for-owner.md`** — ambiguous balance, schedule, retail-vs-custom behavior, or Season 9 feature gap needing owner direction (excludes items already answered on 21–22 Nov 2026).
- **Keep HNMO rule** — intentional HNMO design, custom feature, anti-exploit guard, or previously owner-confirmed rule (`owner-answers-game-data-2026-11-21.md`, `owner-answers-monsters-2026-11-21.md`, `ledger-player-actions.md`).

---

## 1. Clear Code & Data Bugs (`Bug to fix`)

### 1A. Core `server/src/GameLogic` (`unit-server-src-GameLogic-files-1.md`, `files-4.md`, `files-5.md`)
1. **Summoner Fenrir skill damage check order (`AttackableExtensions.cs:855-863`)**: `isSummonerSkill` is evaluated *before* `damageType = skill.DamageType;` assigns `DamageType.Fenrir`, causing Fenrir skills cast by a Summoner to take the wrong damage multiplier/skill branch. -> **Bug to fix**
2. **Spawn gate bypasses `GetRandomPoint()` (`CharacterInitialization.cs:90-94`)**: Uses `Rand.NextInt(spawnGate.X1, spawnGate.X2)` directly instead of `spawnGate.GetRandomPoint()`. -> **Bug to fix**
3. **Dropped stack pickup race condition (`DroppedItem.cs:115, 298-311`)**: `TryStackOnItemAsync` checks `CanCompletelyStackOn` outside `_pickupLock`, then unconditionally adds `Durability` inside `_pickupLock` without rechecking `MaximumStackSize()`. -> **Bug to fix**
4. **Storage transfer rollback evicts unrelated items (`Storage.cs:222-251`)**: `TryTakeAllAsync` leaves source `item.Item.ItemSlot` unchanged when `AddItemAsync` returns `false`, and `finally` calls `RemoveItemAsync` unconditionally by slot index without checking `ReferenceEquals(ItemArray[slot], item)`, which can wipe an existing destination item sharing that slot. -> **Bug to fix**
5. **Solo vs Party Master EXP catch-up / Season Plan gate mismatch (`PlayerExperience.cs:176-192` vs `Party.cs:386-400`)**: Solo checks `CatchupCapPlugIn.GetEffectiveMaximumLevel` and `SeasonPlanPlugIn.IsFeatureUnlocked(MasterLevels)`, whereas `Party.AwardExperienceAsync` checks only `Stats.Level == MaximumLevel`. -> **Bug to fix**
6. **`SkillList.SkillCount` byte cast truncation (`SkillList.cs:68`)**: Unchecked `(byte)this._availableSkills.Count` wraps silently if configured >255 skills. -> **Bug to fix (defensive clamp/check)**

### 1B. `PlayerActions`, `Quests` & `Persistence/Initialization`
7. **`IncreaseStatsAction.cs:44-50` `ushort` underflow**: When `current > maximumValue`, `maximumValue - current` is negative and `(ushort)` wraps to ~`65535`. -> **Bug to fix**
8. **`Kantata` Ancient Set Excellent Damage Chance = `1000%` (`AncientSets.cs:199`)**: Adds `10.0f` instead of `0.10f` (`10%`) `AddRaw` fraction used elsewhere in `AncientSets.cs`. -> **Bug to fix**
9. **Blue Chocolate Box & Deep Blue Candy Box missing normal/jewel pools (`BoxOfLuck.cs:924-1052`)**: `14:34` levels `0`/`1` register `blueBox`/`deepBlueBox` item sets without calling `DropItemGroups.Add(...)` (only Exc + Zen fallback ever drop). -> **Bug to fix**
10. **Guardian Lv380 Boots option Max SD `+200` vs `+700` (`GuardianOptions.cs:106`)**: Sets `Stats.MaximumShield, 200` instead of S6/S9 suite `+700`. -> **Bug to fix**
11. **`GameMaster.cs:63,65` duplicate `PendantSlot` (`63`)**: `CreateDarkLord()` inserts two pendants into the same `InventoryConstants.PendantSlot`. -> **Bug to fix**
12. **`MerchantStores.cs:133, 141-142` (`Version075`)**: `Mace` (`2:0`) uses `(Scepters, 1)` (`Morning Star` is `2:1`), and `Gladius` (`0:6`) + `Falchion` (`0:7`) collide at `itemSlot = 73`. -> **Bug to fix**

### 1C. `GameLogic/PlugIns` & `Seasons`
13. **`JewelBankPlugIn.cs:188-198` stacked jewel deposit destroys up to 254 jewels**: Hardcodes quantity `1` regardless of `item.Durability` before deleting the item stack; also `ClearMasterSkillsAsync` (`lines 354-355`) ignores `SaveProgressAsync()` failure and does not roll back `CashShopBalanceReceipt`. -> **Bug to fix**
14. **`CombiningLeftoversEngine.cs:107-155` & `CombiningLeftoversPlugIn.cs:297-316` stacked items destroyed/uncounted**: `RemoveBagSlotsAsync` deletes entire stack (`Durability` ignored) instead of consuming exact count, and `PlanExchange` counts a stacked slot as `1`. -> **Bug to fix**
15. **`SeasonPlanStoreData.cs` & `CombiningLeftoversStore.cs:142` (`MaxBoxLevel`)**: `SeasonPlanStoreData` never serializes `MaxBoxLevel`, so `CombiningLeftovers` stage box caps (`2..5`) never activate. -> **Bug to fix**
16. **`MarriagePlugIn.cs:32` vs `MountChainPlugIn.cs:36` Buff ID `242` collision + stale buff (`MarriagePlugIn.cs:504-507`)**: Both plugins share `BuffEffectNumber = 242`, overwriting each other; separating couples retain buff `242` until expiry. -> **Bug to fix**
17. **`PartyOptionsPlugIn.cs:144-147, 254` `/partypass` blocked when Spot Finder is off**: `JoinLeaderPartyAsync` calls `PlanSpotJoin` which fails if `AllowAutoSpotParty == false` even when `AllowPasswordJoin == true`. -> **Bug to fix**
18. **`MarketPlugIn.cs:468-474` & `ItemStackPlugIn.cs:200-207` orphaned `CashShopBalanceReceipt`**: When `SaveProgressAsync()` returns `false`, coins are refunded in memory without removing the staged `CashShopBalanceReceipt`. -> **Bug to fix**
19. **`EconomyStore.cs:84-87` (`CountJewelPieces`)**: Legacy helper treats every `Group == 12` item with `"Jewel"` in name as bundle `(Level + 1) * 10`, counting single `Jewel of Chaos` (`12:15`) as `10` jewels (note: `ConfiguredJewelCounter.cs` added at `8cb8539b`, verify callers). -> **Bug to fix**
20. **`AntiCheatPlugIn.cs:166, 175`**: Checks `Stats.AttackSpeed` only (misses higher `Stats.MagicSpeed` on caster spells) and divides raw timestamps by `Stopwatch.Frequency` instead of `this._timeProvider.GetElapsedTime(...)`. -> **Bug to fix**
21. **`MountChainPlugIn.cs:352-355` "PvE-only" spec mismatch**: `PveDefenseBonus` adds `Stats.DefenseFinal`, applying to PvP as well (decide PvE-only filter or update wording). -> **Bug to fix**
22. **`SeasonSharedAccounts.cs:89-99` coin sync race**: Background save awaits `store.AddCoinsAsync` and overwrites `account` coins via `shared.ApplyTo(account)`, dropping concurrent in-game coin deltas. -> **Bug to fix**

### 1D. Web & Desktop Client Bugs
23. **Desktop `w_BuffStateValueControl.cpp:60-61, 66-67`**: Assigns `valueinfo.s_Value2 = Item_data.m_Time;` (and `s_Value2 = 0` twice) instead of `valueinfo.s_Time`. -> **Bug to fix**
24. **Web `client/web/src/main.ts:1885-1891` `outlaw: false` hardcoded**: `MoveWindow` ignores character PK Murderer state, showing warpable maps before server rejects them. -> **Bug to fix**
25. **Web `client/web/src/main.ts:1936-1978` bag space check**: Scans only base slots `12..75`, ignoring unlocked extended inventory slots `76..203` during quest reward claim checks. -> **Bug to fix**

---

## 2. Master Skill Tree (`diff-master-skills.txt`, `parser-master-skills.md`, `diff-client-tables.txt`)

- **Totals**: S6 = `474` nodes, S9 = `480` nodes vs HNMO `200` server nodes -> **`same = 259`, `differs = 34`, `missing_in_HNMO = 187`** (counted across S6/S9 class-bit + skill-ID rows). Client `MasterSkillTreeData.bmd`: HNMO/Web = `293` (matches S6 `293` 1:1), S9 = `472` (`179` S9-only rows missing in Web/Desktop client).
- **`differs` (`34` entries)**: Primarily formula scaling differences in `SkillsInitializer.cs` — notably `Formula502` (`5.02 / 32.39 / 44.08`) vs suite `stid=3` (`6.32 / 39.17 / 53.2`) or `stid=4` (`8.83 / 52.62 / 71.33`) on strengtheners (`Cyclone`, `Slash`, `Falling Slash`, `Lunge`, `Twisting Slash`, `Flame`, `Lightning`, `Blast`, `Evil Spirit`, `Triple Shot`, `Penetration`, `Bow`, `Fire Burst`, `Force Wave`), plus `SD Recovery Speed Inc` (`306`), `Defense Success Rate Inc` (`313`), `Command Attack Inc` (`533`), `Beast Uppercut Mastery` (`555`), `Equipped Weapon Mastery` (`571`, max `10` vs `20`), and RF `584`/`591`. -> **Bug to fix** (verify passive vs active formula units — e.g. fractional `0.01` vs percentage `1.01` vs real `5.02/6.32` discrepancy on Strengtheners).
- **`missing_in_HNMO` (`187` entries)**: Includes global/tier skills (`314` Cast Invincibility, `315` Armor Set Bonus, `316` Vengeance, `317–320` Stat Increases, `321–324` Wing Def/Atk tiers, S9 new class skills in Tier 7–9, and `S9-only` tree entries). Also S9 raises `MaxMasterLevel` from `200` (`diff-common-rules.txt:6,115`) to `330`/`400`. -> **Owner choice -> `questions-for-owner.md` (Q1)**

---

## 3. Quests & Progression (`diff-quests.txt`, `parser-quests.md`, `unit-server-src-GameLogic-PlayerActions-Quests.md`)

- **Class Quests (`0..6`)**:
  - Quests `0, 2, 3, 6` match (`ClassQuest 1` differs only in minor `MinLv=150` guard vs `0`).
  - **Quest 4 (`Certificate of Strength`, NPC `406`, `Quests.cs:175-197`)**: HNMO uses drop rates `3% / 4% / 5%` (`mob 63/309/77`) and `20` reward points vs suite `10%` (`1000/10000`) and `10` points.
  - **Quest 5 (`Infiltration of Barrack`, `Quests.cs:96-133`)**: HNMO grants `10` points vs suite `20` points (total `20+10 = 30` vs suite `10+20 = 30`). -> **Owner choice -> `questions-for-owner.md` (Q2)**
- **Episode Quests (`Scripts/Quests` Lua vs `Quests.cs:358-2326`)**:
  - `456` suite Lua episode steps vs `247` HNMO `CreateQuest` blocks -> **`same = 101`, `differs = 146`, `missing_in_HNMO = 209`** (mostly dialogue/hint steps, early Ep1 tutorial quests, and Ep19 EXP/Zen reward tiers), plus `QuestSystem.ini` custom #1 (`The Mighty Spider`). -> **Owner choice -> `questions-for-owner.md` (Q3)**
- **Action / Quest engine gaps**:
  - `QuestRewardType.GensAttribution` is `// not yet implemented.` (`QuestCompletionAction.cs:184-186`), blocking Gens quest rewards. -> **Bug to fix / feature completion**
  - `QuestSelectAction` / `QuestStartAction` do not validate prerequisite quest completion (`LastFinishedQuest` overwrites history per group). -> **Bug to fix**
  - `ClassQuest` steps with `Repeatable` risk & `MyQuestInfoWindow.cpp:545-557` / `questWindows.ts:588-592` hardcoding BC/IT `6/day`. -> **Bug to fix (if daily cap differs from 6)**
  - Completing quests anywhere via packet without NPC dialog check (`ledger-player-actions.md` Q7). -> **Owner choice -> `questions-for-owner.md`**

---

## 4. Castle Siege (`diff-castle-siege.txt`, `parser-castle-siege.md`)

- **Schedule cycle**: HNMO runs a **7-day** weekly Saturday `20:00–22:00` cycle (`CastleSiegeSchedule.cs:20`), whereas S6/S9 suites default to **14-day** cycles (and S9 config sets battle `08:00–10:00` vs S6 `20:00–22:00`). -> **Owner choice -> `questions-for-owner.md` (Q4)**
- **FriendlyFire & PvP DamageReduction (`CastleSiegePlugIn.cs:147-154`)**: Suites enable `FriendlyFire = 1` with `DamageReduction Opposite = 40% / Self = 20%`; HNMO cancels same-side attacks and omits both siege PvP damage reductions. -> **Owner choice -> `questions-for-owner.md` (Q4)**
- **LordMixDailyLimit**: Suites enforce `1`/day (`CastleSiegeConfiguration` lacks limit). -> **Owner choice -> `questions-for-owner.md` (Q4)**
- **NPCs (`215..288`)**: HNMO spawns `216, 217, 218, 219 (x6)` vs S6/S9 suite table listing `215 (x1)` and `288 Canon Tower (x9)` (`diff-castle-siege.txt:36-45`). Also `CastleSiegeGate.cs:143-152` terrain collision rule vs suite. -> **Keep HNMO rule** unless owner wants Canon Towers `288` added.

---

## 5. Events, Timed Invasions & Reward Bags (`diff-events-all.txt`, `diff-itembags.txt`, `parser-events-all.md`, `parser-itembags.md`)

### 5A. Core Events
- **Blood Castle**: Timings fixed per owner answer `#230` (open 5m, play 15m, close 1m). However **Winner Reward (`BloodCastleInitializer.cs:329`)**: HNMO drops only `12:15 Jewel of Chaos`, whereas S6/S9 `Event_BloodCastle(1..8)_Reward` drop `6` items (`13:14, 13:52, 14:13, 14:14, 14:16, 12:15`) + `300,000 Zen`. -> **Owner choice -> `questions-for-owner.md` (Q5)**
- **Chaos Castle (`ChaosCastleInitializer.cs:90-93`, `ChaosCastleDropGenerator.cs:18-28`)**: Max `70` players vs suites `100` (Min `2` confirmed by owner over S9 `1`); hardcoded Bless/Soul jewel table (`0/2..2/3`) vs suite `Event_ChaosCastle(1..7)_Reward` XML (`117` items / ancient pools); missing Dark Horse ban check (`diff-events-all.txt` / suite rules). -> **Owner choice -> `questions-for-owner.md` (Q5)**
- **Doppelganger (`DoppelgangerInitializer.cs:51-54`, `DoppelgangerMonsters.cs`)**: `EnterDuration = 30s` vs suites `60s`; fixed `100%/90%` jewel & `8%/2%` feather/crest chest drops vs suite ItemBags. -> **Owner choice -> `questions-for-owner.md` (Q5)**
- **Imperial Guardian (`ImperialGuardianInitializer.cs:63-104`)**: Whole-event `50m` (`3000s`) timer + fallback `100,000` EXP vs suites' per-stage (`180s` standby + `600s` round + `20s` loot) timer, `Suspicious Scrap` Lv32 drop rule, and `1,000,000 Zen` mix fee. -> **Owner choice -> `questions-for-owner.md` (Q5)**
- **Illusion Temple (`IllusionTempleInitializer.cs`)**: Core `5m/15m` matches S6 Classic; missing `MinSkill=3` anti-AFK check and `KillPts` `5/2` vs S9 Party Renewal (`minUserInParty=5`). -> **Owner choice -> `questions-for-owner.md` (Q5)**

### 5B. Missing Events & Bags (`missing in HNMO`)
- **Shared S6/S9 Missing Events**: `BonusEvent` (Exp `1.5x` window), `LorenDeep`, `GamblingLottery` (Moss `GamblingLottery.xml`: `1M` Zen + weighted Exc bits vs HNMO `MossGamblePlugIn` `2M` Zen + uniform bits), `LastManStanding`, `XMasAttack` (`Santa Village` buff & lottery), `MiningSystem` (`24` twinkles), `MuRummy`, `RingAttack` (S6), plus **static vs timed** invasions (`GoldenInvasion`, `WhiteWizardAttack`, `DragonEvent`, `Crywolf`, `Raklion`). -> **Owner choice -> `questions-for-owner.md` (Q6)**
- **S9-only Missing Events (`diff-events-all.txt`, `open-points-2026-11-22.md`)**: `AcheronGuardian`, `ArcaBattle`, `ChaosCastleSurvival`, `InvasionManager`, `TormentedSquare`. -> **Owner choice -> `questions-for-owner.md` (Q13)**
- **ItemBags & BoxOfLuck (`diff-itembags.txt`, `parser-itembags.md`)**: S6 `112` / S9 `118` XML bags vs `27` HNMO `BoxOfLuck` groups; `BoxOfLuck` gives `20% Exc + 50,000 Zen` across `47` items vs suite bags; S6/S9 `Delgado Lucky Coin (10/20/30)`, `Cherry Blossom (White/Red/Gold)`, `SantaClaus`, `IllusionTemple` reward bags intentionally skipped by importer (`open-points-2026-11-22.md`). -> **Keep HNMO rule** (custom box groups) unless owner wants specific NPC exchanges (`Delgado`, `Cherry Blossom`) implemented (**Q6**).

---

## 6. Common Server Rules, VIP, Gens & Economy (`diff-common-rules.txt`, `diff-suite-rules.txt`, `diff-gens.txt`, `diff-shops.txt`)

- **VIP Tiers (`diff-common-rules.txt:2`, `parser-suite-rules.md:25`)**: S6/S9 have `4` VIP day/night EXP & drop tiers; HNMO has no VIP tier table in `GameConfigurationInitializer.cs` (only website/Autoplay membership). -> **Keep HNMO rule** (or confirm in **Q7** if VIP EXP tiers are desired).
- **Rate / Timer Constants**:
  - `ItemDropDuration = 20s` / `MoneyDropDuration = 15s` in HNMO matches S9 (`20s/15s`), differs from S6 (`10s/10s`) — -> **Keep HNMO rule** (already patched via `GroundDropDurationUpdatePlugIn`).
  - `OffLevelling`: S6/S9 `Enable=0` vs HNMO custom `OfflineLevellingPlugIn` -> **Keep HNMO rule**.
  - `Party` / `CashShop` / `ResetCMD`: HNMO uses custom configurable plugins (`ResetPlugIn` vs INI `Cost=10M/50M`, `488` CashShop items vs `373` S6 / `230` S9) -> **Keep HNMO rule**.
  - `AntiCheatConfiguration.HitHackMaxAgility = 32767` (opt-in `false`) vs S9 `23000` -> **Keep HNMO rule**.
- **Gens System (`diff-gens.txt`, `unit-server-src-GameLogic-Gens.md`)**:
  - Top 300 ranks `1..8` require `MinimumContribution = 10000` in HNMO (`0` in suite files): anti-low-pop guard -> **Keep HNMO rule**.
  - `RankingIntervalHours = 2` vs suite DS `>=5h`: -> **Keep HNMO rule**.
  - `GuildRequiresGens = false` / `AllianceRequiresSameGens = false` (`GensConfiguration.cs:172,180`) vs suite `0` (disallowed across opposite Gens); missing `CanSeeOppositeGensChat = 0`, `PkPenaltyAddNeedZenMapMove = 2`, per-map BattleZone EXP bonuses. -> **Owner choice -> `questions-for-owner.md` (Q8)**
- **Shops (`diff-shops.txt`)**:
  - `20` shop count mismatches mostly reflect earlier minor differences (`32` vs `34`, `6` vs `7`, `NPC 231` count), plus S9-only Loren Market (`Map 79`, NPCs `683..688`), `NPC 583/604 Jin` (Alkmar/Ubaid), and `NPC 239/379/492` -> follow S6 owner decision where applicable; **Part 8** covers S9 shop/map expansion (**Q13**).

---

## 7. Items, Skills, Monsters, Character Formulas & Client Tables (`diff-items.txt`, `diff-skills.txt`, `diff-monsters.txt`, `diff-client-tables.txt`, `ledger-game-data.md`)

- **Items (`diff-items.txt` — `848` HNMO vs `1003` S6 / `1260` S9)**:
  - `196` staff/scroll/orb/jewel `Durability = 1` or `20..182` vs suite `0`, `12-73/74` Spheres (`DropLevel 0` vs `142/152`), `12-100..129` Seed Spheres `DropLevel` `0..4` vs `1..5` (socket craft items only), `0-19/0-35/2-18/4-6/4-18/5-7/5-10` Archangel & Phoenix `Drop:0->1` flags -> **Keep HNMO rule** (already reviewed on 21 Nov; Orb/Scroll durability `1` avoids zero-durability UI warnings).
  - `170` S6-only absent rows (`-J` Lucky sets, `7..11-53` Pearl/Succubus duplicates) & `263` S9-only items (Note `14-191..203`, Mastery/Ruud/Socket sets, Muuns `13-180+`): S9 items exist partially in `SeasonNineOnlyItemsCatalog.cs` (`279` items on server vs missing in Web `Data/Items/Group*.json`) -> handled in **S9 data set / Part 8 (`Q13`)**.
  - `BuffItems.cs` (`Seal of Wealth` `13:44`, `Master Seal` `13:94`, `Seal of Divinity` `13:63`): Missing `+150%` drop rate option from suite item manager -> **Owner choice -> `questions-for-owner.md` (Q9)**.
  - `Potions.cs:485` Jack O'Lantern durations & `Jewelery.cs:73-137` Cash Shop jewelry (`+3%` tooltip match vs `+8%` legacy file) -> **Keep HNMO rule**.
  - `Wings.cs:107` (`Version075`): Unimplemented `+5` level requirement per wing upgrade (`TODO`) -> **Bug to fix** (verify if intentional or missing in S6 wings too).
- **Skills (`diff-skills.txt`, `unit-server-src-Persistence-Initialization-Skills.md`)**:
  - `13 Cometfall`, `14 Inferno`, `40 Nova`, `382/484 Blast Strengthener`, `482 Power Slash Strengthener` energy/distance/mana differences — energy folding matches classic `reqEnergy * reqLevel * 4 / 100 + 20`, `Dist` for area skills addressed via cast-vs-radius decision, `Nova` split `(58)`/`(40)` matches AG `45`. -> **Keep HNMO rule** (except `Soul Barrier` / `Reflection` / `Berserker` S6-vs-S9 formula nuances -> see **Q10**).
  - `PotionOfBless` (`10`): Applies `x1.2` dmg to *all* targets despite `TODO` saying Castle Siege gates/statues only. -> **Owner choice -> `questions-for-owner.md` (Q10)**
  - **Skills already deferred / custom in client**: S9-only `#618..621` (`Spell of Protection/Restriction/Pursuit`, `Shield-Burn`) missing in client & server (**Q13**); `Power Slash (56)` Web client missing "not while riding" check & Lv/Equipment checks (`skill-sweep-2026-11-21.md`) -> **Bug to fix**.
- **Monsters (`diff-monsters.txt`)**:
  - NPCs `215..492, 540..668` having `Lv=2, HP=50` vs `0` in suite: non-combat NPCs -> **Keep HNMO rule**.
  - Imperial Guardian bosses/gates `#504..528` (`HP=1.8M` vs suite placeholder `Lv=1` overridden at runtime by IG script) & traps `#100..103` -> **Keep HNMO rule** (except check trap `#103 Meteorite` `AtkRng=3 vs 0` if affecting SafeZone/LostTower hits).
  - `#412 Dark Elf` S6 (`10s`) vs S9 (`10800s`), plus S9 monsters `#669..729` (Uruk Mountain / Lord Silvester / Evomon / Goats) & `5` S9 models without 3D mesh (`580, 581, 584, 643, 689`) -> handled in **Part 8 Item 5 (`Q13`)**.
- **Client Tables & Cross-Client Rules (`diff-client-tables.txt`, `unit-client-*`)**:
  - Web client `public/data/Monsters/Season9.json` has `35` entries vs `615` server S9 monsters; Web missing S9-only `Gate.bmd` (`345..443`), `MoveReq`, `QuestProgress` (+131), `BuffEffect` (+44), `Item_eng` (`337` items), and `.bmd` tables (`Muun*`, `Pentagram*`, `MuRummy*`, `Minimap_eng`, `Excellent*Option`, `ItemTRSData`). -> **Part 8 / S9 dataset (`Q13`)**.
  - **UI / Action Gaps (`unit-client-*`, `ledger-player-actions.md`)**:
    - `ledger-player-actions.md` unfixed low-priority items: Repair-all without NPC `2.5x` (`Q6`, owner kept, remove cheater log only), Talisman consume / Mini-game state checks (if any remain), Party kick packet bounds check, Personal Store min level `6` & state check -> **Bug to fix (bounds & state safety)** / **Keep HNMO rule (Repair 2.5x)**.
    - Web vs Desktop visual HUD & Monster rendering/lag issues -> **Part 8 Items 5 & 6**.
