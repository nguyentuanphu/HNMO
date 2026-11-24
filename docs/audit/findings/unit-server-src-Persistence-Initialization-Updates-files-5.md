# Audit Findings: `server/src/Persistence/Initialization/Updates (files 5)`

Audited unit: `server/src/Persistence/Initialization/Updates (files 5)` (46 files, 3,141 lines; `PkClearCostUpdatePlugIn.cs` through `ZenLossBagOnlyUpdatePlugIn.cs`).

## Rule Comparison Against Reference Suites

| Update Plug-In (`file:line`) | Applied Rule Value | Suite Role & Value | Result |
| --- | --- | --- | --- |
| `PkClearCostUpdatePlugIn.cs:29-31,70-73` | Updates `ZenCostPerKill` `10,000,000 -> 1,000,000` | Season 6 `CommonServer.cfg:59` (`PkClearCost=1000000`) | `same` (`1,000,000` zen/kill) |
| `RaklionSwampGateLevelsUpdatePlugIn.cs:28-42,60-71` | Updates Swamp gates (`274..285`) `250 -> 400`, Raklion gates (`286,288,290,292`) `240 -> 270` | Season 6/9 gate settings (`gate.bmd`) | `same` (`400` Swamp, `270` Raklion) |
| `SkillEnergyRequirementsUpdatePlugIn.cs:45-60` | Sets `Recovery` (`234`) Energy `37 -> 168`, `PowerSlash` (`56`) Energy `0 -> 100` | Season 6 `SkillList.xml` | Aligns skill energy requirements |
| `SkillRangesAndNovaAgUpdatePlugIn.cs:48-59` | Updates ranges: `EvilSpirit 7->6`, `Cometfall 3->6`, `BlastStr 3->6`; `Hellfire` range `4` (`effectRange=2`); `Inferno` range `4` (`effectRange=4`); `Nova` AG `0 -> 45` | Season 6 `SkillList.xml` (`Distance=6`, Nova `BPUsage=45`) | `same` skill distances & Nova AG |
| `SocketCriticalDamageUpdatePlugIn.cs:32,71-75` | Sets Lightning Critical Damage Increase Lv `1..5` `+15/+20/+25/+30/+40 -> +30/+32/+35/+40/+50` | Season 6/9 socket option table | `same` (`30/32/35/40/50`) |
| `SocketShieldProtectionUpdatePlugIn.cs:54-59` | Sets Water Shield Protection Lv `5` `0.30f -> 0.20f` (S6 default; S9 update overrides to `0.30f`) | Season 6 (`20%`) vs Season 9 (`30%`) socket option table | `same` for Season 6 (`20%`) |
| `StrongerHnmoSkillsUpdatePlugIn.cs:14-48` | Applies `HnmoSkillValues` (`+20%` damage/effect values on `700..727`) & `200` kills / `5` Chaos trials | Custom HNMO extension | Audited in `Initialization/Skills` |
| `SwellLifeDurationUpdatePlugIn.cs:59-61` | Removes `180f` duration cap (`MaximumValue = null`) on `GreaterFortitude` / `GreaterFortitudeProficiency` | Season 6/9 `RegularSkillCalc.lua:57` (`SkillTime = Energy/10 + 60`, uncapped) | `same` (removes unfaithful `180s` cap) |
| `ThirdWingsLuckUpdatePlugIn.cs:49-51` | Bumps 3rd wing Chaos Mix `ResultItemLuckOptionChance` `5 -> 10` (`10%`) | Season 6 `ChaosBox.ini` (`diff-suite-rules.txt:39`: `Wing3LuckRate=10`) | `same` (`10%`) |
| `ZenLossBagOnlyUpdatePlugIn.cs:97` | Sets `VaultMoneyLossPercentage = 0` on existing saves (inventory bag loss only) | Classic/Season 6 death zen penalty | `same` (vault zen protected) |

Remaining files (`PotionShopStorageBuilder`, `RageFighterResistanceMasterSkillsUpdatePlugIn`, `RegenerationsRefactorPlugIn*`, `RemoveDuplicateStatAttributesPlugIn*`, `RemoveJewelDropLevelGapPlugIn*`, `RenumberArenaHeraldUpdatePlugIn`, `ScopeExtraProjectilesPlugIn`, `Season6ShopListsUpdatePlugIn`, `SecondClassItemsUpdatePlugIn`, `SecondWingOptionsUpdatePlugIn`, `SetIntroducedGameVersionsPlugIn*`, `SocketWeaponMixUpdatePlugIn`, `SpawnFixesUpdatePlugIn`, `StandardizeEventEnterDurationPlugIn*` [`5m`], `SystemConfigurationAddedPlugIn*`, `UpdateDependency`, `UpdatePlugInBase`, `VaultPagesDefaultsUpdatePlugIn`, `WizEnhanceAndRagefighterSkillsUpdatePlugIn`) apply NPC shops, NPC spawns, second-class items, `IntroducedIn` tags, and internal attribute/plugin schema migrations.
