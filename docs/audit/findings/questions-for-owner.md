# Questions for Owner (Remaining Audit Decisions)

These are the remaining unresolved `"differs"` and `"missing in HNMO"` items from the Season 6 / Season 9 audit that require an owner choice before changing game balance, schedules, or adding large S9 feature sets.

*(Items already decided on 21–22 Nov 2026 in `owner-answers-game-data-2026-11-21.md`, `owner-answers-monsters-2026-11-21.md`, and `open-points-2026-11-22.md` are NOT repeated here — clear bugs go straight to fix PRs.)*

---

## Q1. Master Skill Tree Missing Nodes (`187` missing) & Master Level Cap
- **HNMO**: `200` master skill nodes (`SkillsInitializer.cs`), `MaxMasterLevel = 200`, Web/Desktop client `MasterSkillTreeData.bmd` = `293` nodes (S6 layout).
- **Suites**: S6 `.dat` = `474` rows (`293` UI nodes across multi-class bits, shared passive skills `314..324` like `Cast Invincibility`, `Armor Set Bonus`, `Vengeance`, base Stat/Wing nodes); S9 `.xml` = `480` rows (`MaxMasterLevel = 330/400`, client `472` nodes).
- **Options**:
  1. **(Recommended for S6)** Add only the missing S6 passives (`314..324` etc.) that match the existing `293`-node S6 client UI, and leave S9's `330`-level expanded tree for the dedicated Season 9 dataset.
  2. Keep HNMO's current `200` server nodes and fix only the `34` differing formulas.
  3. Expand both S6 and S9 to full `472` nodes / `330` Master Levels.

## Q2. 3rd-Class Quest Drop Rates & Reward Points (`ClassQuest 4 & 5`)
- **HNMO**: Quest 4 (`Certificate of Strength`, `Quests.cs:175-197`) drops items at `3% / 4% / 5%` and rewards `20` points; Quest 5 (`Infiltration of Barrack`) rewards `10` points (`20 + 10 = 30` total).
- **S6/S9 Suites**: `ClassQuest.xml` sets Quest 4 drops at `10%` (`1000/10000`) with `10` points, and Quest 5 with `20` points (`10 + 20 = 30` total).
- **Options**:
  1. **(Recommended)** Match suites' `10%` drop rates and `10 / 20` point split so Quest 4 doesn't feel unnecessarily grindy, while total points stay `30`.
  2. Keep HNMO's `3%/4%/5%` drop rates and `20 / 10` point split.

## Q3. Episode / Shadow Phantom Quests (`247` HNMO vs `456` Suite Steps; `146` Differ)
- **HNMO**: `Quests.cs:358-2326` implements `247` Shadow Phantom/Episode steps; missing `209` steps (`Ep1` early tutorial/talk steps, `Ep2` dialogue steps) and differs on `146` steps (`Ep19` EXP/Zen values, Lv ranges). Also `QuestSystem.ini` #1 (`The Mighty Spider`) is missing.
- **Suites**: `Scripts/Quests/Quest_Ep*.lua` (`456` steps, identical S6 & S9).
- **Options**:
  1. **(Recommended)** Import missing `Ep1`/`Ep2` progression steps and sync `Ep19` EXP/Zen rewards cleanly with the client `QuestProgress.bmd` (`915` steps).
  2. Keep HNMO's trimmed `247`-step Shadow Phantom list (assuming players mostly grind/reset rather than run low-level tutorial quests).

## Q4. Castle Siege Cadence, Friendly Fire, Damage Reduction & Lord Mix Cap
- **HNMO**: **7-day** weekly cycle (`Saturday 20:00–22:00`), `FriendlyFire` disabled (same-side attacks cancelled at `CastleSiegePlugIn.cs:147-154`), no PvP `DamageReduction`, no `LordMixDailyLimit`.
- **S6/S9 Suites**: **14-day** default cycle (S6 battle `20:00–22:00`, S9 `08:00–10:00`), `FriendlyFire = 1`, `DamageReduction Opposite = 40% / Self = 20%`, `LordMixDailyLimit = 1`.
- **Questions**:
  - **Cadence**: Keep HNMO's **7-day Saturday 20:00–22:00** weekly war (recommended for active servers) or switch to **14-day**? (Keep `20:00–22:00` instead of S9 `08:00`?)
  - **Combat rules**: Enable `FriendlyFire = 1` with `40% / 20%` PvP damage reduction & `LordMixDailyLimit = 1`, or keep no-friendly-fire / full damage?

## Q5. Mini-Game Mechanics & Rewards (Blood Castle, Chaos Castle, Doppelganger, Imperial Guardian, Illusion Temple)
- **Blood Castle Winner**: HNMO gives `1x Jewel of Chaos`; S6/S9 suites drop `6` items (`13:14 Loch, 13:52 Flame, 14:13 Bless, 14:14 Soul, 14:16 Life, 12:15 Chaos`) + `300,000 Zen`. Follow suites' 6-item pool or keep Chaos-only?
- **Chaos Castle**: Keep max players `70` (HNMO) or raise to `100` (suites)? Keep static Bless/Soul jewel table (`0/2..2/3`) or wire XML reward bags (`117` items / ancient mix)? Add missing Dark Horse entry/mount ban in CC?
- **Doppelganger**: Raise EnterDuration `30s -> 60s` to match suites? Keep HNMO's fixed chest drops (`100%/90%` jewels, `8%` Feather, `2%` Crest) or use suite ItemBags?
- **Imperial Guardian**: Keep HNMO `50m` overall timer + `100k` fallback EXP, or align to suite `180s` standby + `600s` per zone + `20s` loot timer + `1,000,000 Zen` mix cost & full stage EXP table?
- **Illusion Temple**: Add suite `MinUseSkillCount = 3` anti-AFK check and `KillPts = 5/2`? For Season 9, keep Classic rules or switch to Renewal (`min 5 in party`)?

## Q6. Missing Events, Invasions & Custom Exchanges
- **Missing S6/S9 events**: Which (if any) of these missing suite events/exchanges do you want built next?
  - `BonusEvent` (scheduled `1.5x` EXP hours)
  - `XMasAttack` (Santa Village invasion + Santa NPC buffs/gifts)
  - `LorenDeep` & `RingAttack` (White Wizard / Orc ring invasion)
  - `LastManStanding` (Arena survival event)
  - `MiningSystem` (Pandora Pick + Bless mining) & `MuRummy` (S9 card mini-game)
  - `Delgado Lucky Coin` (`10/20/30` exchange) & `Cherry Blossom` (`White/Red/Gold` branch mix)
- **Timed vs Static Invasions**: `GoldenInvasion`, `WhiteWizardAttack`, `DragonEvent`, `Crywolf`, `Raklion` use static/plugin schedules in HNMO instead of suite exact clock schedules (`Crywolf` `02:10/04:10` vs HNMO schedule). Keep HNMO flexible schedules or mirror exact suite clocks?
- **Moss Merchant (`MossGamblePlugIn`)**: Keep HNMO `2,000,000 Zen` + uniform Excellent odds, or match `GamblingLottery.xml` (`1,000,000 Zen` + `10/20/20/20/20/10%` weighted Exc bits)?

## Q7. VIP Day/Night Server Tiers & Quest NPC Verification
- **VIP EXP/Drop Tiers**: Suites define 4 configurable VIP Day/Night multiplier tiers in `VipSettings.xml`. Keep HNMO's account/autoplay subscription perks without separate server VIP EXP tiers, or add `VipSettings` tiers?
- **Quest Completion Location (`ledger-player-actions.md` Q7)**: Suites require speaking to the quest NPC to complete a quest; HNMO currently allows quest completion via packet from anywhere. Enforce "must be talking to quest NPC" or keep remote completion?

## Q8. Gens Social & Battle Zone Restrictions
- **HNMO (`GensConfiguration.cs`)**: `GuildRequiresGens = false`, `AllianceRequiresSameGens = false`, regular chat visible across opposite Gens, normal warp Zen for PKs in Battle Zone (`Vulcanus`).
- **S6/S9 Suites**: Opposite Gens cannot join the same Guild (`0`) or Alliance (`0`), cannot see opposite-Gens regular chat (`CanSeeOppositeGensChat = 0`), and PK characters pay `2x` Zen (`PkPenaltyAddNeedZenMapMove = 2`).
- **Recommendation**: Enforce matching Gens for Guild/Alliance (`true`) and `CanSeeOppositeGensChat = false` so Gens rivalry actually matters on live servers, unless HNMO prefers looser social grouping for smaller populations.

## Q9. Cash Shop Seals `+150%` Drop Rate (`BuffItems.cs`)
- **HNMO**: `Seal of Wealth` (`13:44`), `Master Seal of Wealth` (`13:94`), and `Seal of Divinity` (`13:63`) do NOT include `+150%` item drop rate (`DropRate`).
- **S6/S9 Suites**: All three seals grant `Option = 7, Value = 150` (`+150%` Drop Rate) in `ItemAddOption` / option manager.
- **Options**: Add `+150%` drop rate to match retail Cash Shop seals, or keep HNMO values if economy drop boost is unwanted?

## Q10. Skill Formula & Consumable Clarifications (`SkillsInitializer` & Effects)
- **Bless Potion (`14:10`, `PotionOfBless`)**: Currently boosts damage `x1.2` against **all** monsters & players (`TODO` notes retail restricts it to Castle Siege gates & statues only). Keep global `x1.2` PvM/PvP or restrict to Castle Siege structures?
- **Summoner Damage Reflection (`217`)**: S6/S9 Lua comments/caps conflict internally (`capped at 25%` vs `60%` in HNMO `ReflectionEffectInitializer.cs:41-62`), and **Soul Barrier (`16`)** / **Berserker (`218`)** caps differ slightly across S6 vs S9 (`FixSkillMultipliersPlugIn.cs` sets Soul Barrier cap `70%`). Confirm whether HNMO's `60%` Reflect / `70%` Soul Barrier caps should stand.

## Q11. Chaos Mix Rate Differences (`ChaosMixes.cs`)
- Note: Owner confirmed on 20 Nov (`#217` closed) to keep `+12 = 55%` and `+14 = 50%` HNMO rates and (`#21` Nov) changed 3rd Wing luck to `10%`.
- Confirm whether `Chaos Card` mixes (`20` S6 / `14` S9 recipes at Chaos Card Master `#375`) are desired later (`missing in HNMO`).

## Q12. Personal Store Min Level & Player Action Safeguards (`ledger-player-actions.md` Q8)
- Opening Personal Store currently lacks a server-side Level `>= 6` check (suites require Level 6 to set prices/open). Add Level `>= 6` server check? (Repair-all without NPC at `2.5x` already decided by owner: keep `2.5x`, only strip false `"cheater"` warning log.)

## Q13. Dedicated Season 9 Data Set & Features (Longer-Term Roadmap)
- Confirmed in `owner-answers-game-data-2026-11-21.md`: HNMO Season 9 server still boots `VersionSeasonSix` core tables. When building the dedicated Season 9 dataset, confirm priority across:
  1. `210` shared S9 item overrides + `281` S9-only items (Note mixes, Lv400 weapons, wings `12:262..268`, sockets) synced into Web `Data/Items/` & `Monsters/Season9.json` (`35 -> 615`).
  2. S9 events (`AcheronGuardian`, `ArcaBattle`, `ChaosCastleSurvival`, `TormentedSquare`, `InvasionManager`, `Uruk Mountain` monsters `#669..729`).
  3. S9 combat/progression systems (`Muun` pets, `Pentagram` elemental system, `4` S9-only skills `#618..621`, `330`-level Master Tree, `Minimap_eng.bmd`).
