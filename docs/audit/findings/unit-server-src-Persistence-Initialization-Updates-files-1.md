# Audit Findings: `server/src/Persistence/Initialization/Updates (files 1)`

Audited unit: `server/src/Persistence/Initialization/Updates (files 1)` (86 files, 5,979 lines; `AbolishMagicNoDamageUpdatePlugIn.cs` through `AddSmallWingsUpdatePlugIn.cs`).

## Scope & Findings Summary

All 86 files are idempotent `UpdatePlugInBase` migrations backfilling configuration data, attributes, Cash Shop products, events, skills, items, or spawn data onto existing saves (`0.75`, `0.95d`, `Season 6`, or `HNMO`). Key rule-bearing updates compared against reference suites:

| Update Plug-In (`file:line`) | Applied Rule Value | Suite Role & Value | Result |
| --- | --- | --- | --- |
| `AbolishMagicNoDamageUpdatePlugIn.cs:46-48` | Skill `72` (Abolish Magic) changed from `DirectHit` to `SkillType.Other` | Season 6 suite `SkillList.xml` (non-damaging skill) | `same` |
| `AddAreaSkillSettingsUpdatePlugInBase.cs:57-86` | Frustum/projectile settings (`AquaBeam`, `FireSlash`, `PowerSlash`, `ForceWave`, `Stun`, `FireScream`, `FlameStrike`, `ChaoticDiseier`, `TripleShot=3`, `MultiShot=5`) | Matches `VersionSeasonSix/SkillsInitializer.cs` | `same` |
| `AddCrestOfMonarchDropGroupUpdateSeason6.cs:63-65` | `Chance=0.001` (`10/10000`), `MinMonsterLv=82` in Icarus | Season 6 suite `CommonServer.cfg:18-19` (`CrestOfMonarchDropRate=10` `/10000`, `CrestOfMonarchDropLevel=86`) | `same` rate (`0.001`); `differs` min monster level (`82` vs S6 `86`) |
| `AddCrystalSwordIceChanceUpdatePlugInBase.cs:52` | `CrystalSwordIceChance = 1/20` (`5%`) | Crystal Sword freeze proc | Explicit weapon rule |
| `AddDefenseReductionChancePlugIn.cs:26,59` | `PowerSlashStreng` (`482`) def reduction `Chance = 0.45f` (`45%`) | Matches `DefenseReductionEffectInitializer.cs:41` | `same` internal Fire Slash proc |
| `AddDragonSlasherShieldDamagePlugIn.cs:58-60` | `DragonSlasher` SD chance `0 + Energy/10000` (`Ene/100%`), SD dmg rate `0 + Energy/3000` (`Ene/30%`) | Season 6 skill calc (`RegularSkillCalc.lua:420-421`: `SuccessRate = Ene/100 + 10`, `DecreasePercent = Ene/30 + 10`) | `same` energy divisors; `differs` (`+10%` flat base in S6 Lua omitted here) |
| `AddDuelConfigurationPlugIn.cs:58-87` | `MaxScore=10`, `MinCharLv=30`, Duel Arena `64` / Vulcanus `63` | Season 6/9 Duel Arena (`MaxScore=10`, Lv `30` Vulcanus matches S9 MoveReq vs S6 `250`) | `same` score rule & S9 Vulcanus min level |
| `AddElfSoldierBuffPlugIn.cs:74-99` | Duration `3600s` (`60m`), Def `+50 + Lv/5`, Dmg `+45 + Lv/3` | Season 6/9 Elf Soldier NPC helper buff (classic `50+Lv/5`, `45+Lv/3`, `60m`) | `same` |
| `AddGlobalMoneyAmountRateAttributePlugInBase.cs:50` | Global `MoneyAmountRate = 1f` | Default base zen multiplier | `same` base `1.0` |
| `AddHitShockSettingsPlugInBase.cs:37-38` | `HitShockChance = 0.25f`, `HitShockDamageThreshold = 5f` (with Uniria) | Matches `GameConfigurationInitializerBase.cs:67-68` | `same` |
| `AddItemDropGroupForJewelsUpdate075.cs:121` | Jewel drop group `Chance = 0.001` (`0.1%`) | Matches `GameConfigurationInitializerBase.cs:212` | `same` |
| `AddKalimaPlugIn.cs:95-104` | Symbol of Kundun `1..7` monster level tiers `[(25,46)..(108,255)]`, `Chance=0.003` (`0.3%`), `Durability=5` | Season 6 Kalima Lost Map stack/drop tiers | `same` 5-stack Lost Map rule |
| `AddKanturuDataUpdatePlugIn.cs:40-42,68-105` | Migrates Kanturu `MaximumPlayerCount` `10 -> 15` | Season 6/9 suite `Kanturu.ini` (`MaxPlayers=15`) | `same` (`15` matches S6/S9) |
| `AddMaximumAllianceSizeUpdatePlugInSeason6.cs:64` | `MaximumAllianceSize = 5f` | Season 6 suite guild alliance max (`5` guilds) | `same` |
| `AddRaklionEventUpdatePlugIn.cs:125-128` | Selupan skill multipliers: Poison `2.0f`, IceStorm `2.2f`, IceStrike `2.3f`, Fall `2.5f` + Hatchery wave spawns | Season 6/9 Raklion Selupan event | Explicit Selupan scaling |
| `AddRandomExperienceConfigAttributesPlugInBase.cs:63-68` | `RandomExperienceMinMultiplier = 0.8f`, `Max = 1.2f` | HNMO exp variance config (not in S6/S9 suites) | HNMO custom rule |
| `AddRenaItemUpdatePlugIn.cs:84-87` | Rena `Chance = 0.01` (`1%`), `Lv 30..255` | Golden Archer event drop | Explicit drop config |
| `AddShieldRatioAttributesPlugIn.cs:60` | `OverShieldBurstDamageRatio = 0.2f` (`20%`) | HNMO custom over-shield mechanic | HNMO custom rule |

Remaining files (`AddBuffItemsUpdatePlugIn`, `AddCashShop*`, `AddCastleSiegeDataUpdatePlugIn`, `AddCraftingBonusPercentage*`, `AddCraftingTalismansUpdatePlugIn`, `AddCrywolfEventUpdatePlugIn`, `AddDarkHorseCanFlyPlugIn`, `AddDoppelgangerDataUpdatePlugIn`, `AddElitePotionsUpdatePlugIn`, `AddFenrirMaterialDropGroupsUpdateSeason6`, `AddFreeTicketItemsUpdatePlugIn`, `AddGensRewardItemsUpdatePlugIn`, `AddGoblinBoxesUpdatePlugIn`, `AddGuardsDataPlugIn`, `AddHarmonyOptionWeightsUpdateSeason6`, `AddHnmoSkillsUpdatePlugIn`, `AddImperialGuardianDataUpdatePlugIn`, `AddIronKnightStabUpdatePlugIn`, `AddItemPriceDefinitions*`, `AddItemRegistrationAttributes*`, `AddItemRuleFlags*`, `AddItemStatProfiles*`, `AddKalimaTicket*`, `AddLorenMarket*`, `AddLuckyItems*`, `AddMasterSkillPassivePowerUps*`, `AddMissingMerchantStores*`, `AddMovementSpeedAttributes*`, `AddPointsPerResetAttribute*`, `AddProtectionTalismans*`, `AddQuestItemLimit*`, `AddRageFighterAncientSets*`, `AddResetFruits*`, `AddSantaVillageInvitation*`, `AddSmallWings*`) apply schema/initializer data already audited under their respective `VersionSeasonSix` / `Initialization` modules.
