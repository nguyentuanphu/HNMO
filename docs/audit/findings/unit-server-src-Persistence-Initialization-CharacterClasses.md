# Audit Findings: `server/src/Persistence/Initialization/CharacterClasses`

- **Unit**: `server/src/Persistence/Initialization/CharacterClasses` (11 files, 1,715 lines)
- **Checked on**: 2026-11-24
- **Checked at**: `6ec9f6ab`
- **Status**: `Checked`

## Comparison against Season 6 / Season 9 Suite References

Compared all 11 files in full against the Season 6/9 suites' character calculation script (`docs/audit/references/attributes-and-stats-s6-s9.md`) and the Season 6/9 suites' database seed (`DefaultClassType` table in the suite SQL setup scripts).

## File-by-File Findings

### `CharacterClassInitialization.cs`, `CharacterClassHelper.cs`, `CharacterClasses.cs`, `CharacterClassNumber.cs`
- `CharacterClassNumber.cs:10-101` & `CharacterClasses.cs:10-197`: Define the 7 Season 6 base classes + 2nd/3rd evolutions (18 entries; Season 9's Grow Lancer class `112` is absent by design).
- `CharacterClassHelper.cs:50-132`: `DetermineCharacterClasses` maps class rank integers (`1`=1st+2nd+3rd, `2`=2nd+3rd, `3`=3rd) identically to Season 6/9 item/skill class flags (**same**).
- `CharacterClassInitialization.cs:94-195`: Common relationships (`TotalLevel`, `DefenseFinal = 0.5 * DefenseBase`, `ShieldRatio = 0.9`, `OverShieldBurstDamageRatio = 0.2`, `ShieldRecoveryMultiplier`, `MaximumGuildSize = 0.1 * Level`) match the Season 6/9 suites' shield/defense formulas (**same**).
- `CharacterClassInitialization.cs:201-215` (`AddDoubleWieldAttributeRelationships`): Applies a uniform `0.55f` (`55%` per hand, `110%` total) physical damage multiplier when two weapons are equipped (`DK`, `MG`, `RF`).
  - Suite character calc script (`CalcTwoSameWeaponBonus`, `CalcTwoDifferentWeaponBonus`, `CalcRageFighterTwoWeaponBonus`): uses `65%` per hand for two same weapons (`130%`), `55%` per hand for two different weapons (`110%`), and `60%` min / `65%` max per hand for Rage Fighter (**differs**).

### Per-Class Base Stats, Spawn Maps, Growth & Combat Formulas
- **Dark Wizard (`ClassDarkWizard.cs:27-137`)**:
  - `Str=18, Dex=18, Vit=15, Ene=30`, `HP=60, MP=60`, `PointsPerLevelUp=5`, `HomeMap=Lorencia (0)` (`ClassDarkWizard.cs:33-45`) -> suite DB seed `(18,18,15,30, Life=60, Mana=60, Map=0)` (**same**).
  - Formulas (`ClassDarkWizard.cs:54-118`): `Def=Dex/4`, `DefRatePvm=Dex/3`, `AtkRatePvm=5*Lvl+1.5*Dex+Str/4`, `AtkSpd=Dex/20`, `MagSpd=Dex/10`, `Phys=Str/8..Str/4`, `Wiz=Ene/9..Ene/4`, `PvpAtk=4*Dex+3*Lvl`, `PvpDef=Dex/4+2*Lvl`, `MaxMana=2*Lvl+2*Ene` -> **same** as suite character calc script.
  - `MaximumHealth` (`ClassDarkWizard.cs:71-72,114`): `30 + 1*Lvl + 2*Vit` (`61` at Lv1) vs suite SQL `VitalityToLife=1` (**differs** `+2/Vit` vs `+1/Vit` in SQL seed).
- **Dark Knight (`ClassDarkKnight.cs:27-131`)**:
  - `Str=28, Dex=20, Vit=25, Ene=10`, `HP=110, MP=20`, `PointsPerLevelUp=5`, `HomeMap=0`, growth `2*Lvl+3*Vit` HP / `0.5*Lvl+1*Ene` MP (`ClassDarkKnight.cs:33-45,70-73,110-111`) -> suite DB seed `(28,20,25,10, 110, 20, LevelLife=2, LevelMana=0.5, VitalityToLife=3, EnergyToMana=1)` (**same**).
  - Formulas (`ClassDarkKnight.cs:55-108`): `Def=Dex/3`, `DefRatePvm=Dex/3`, `AtkSpd=Dex/15`, `MagSpd=Dex/20`, `Phys=Str/6..Str/4`, `SkillMult=2+0.001*Ene`, `ComboBonus=0.5*(Str+Dex+Ene)`, `PvpAtk=4.5*Dex+3*Lvl`, `PvpDef=0.5*Dex+2*Lvl` -> **same**.
- **Fairy Elf (`ClassFairyElf.cs:27-144`)**:
  - `Str=22, Dex=25, Vit=20, Ene=15`, `HP=80, MP=30`, `HomeMap=Noria (3)` (`ClassFairyElf.cs:36-48`; suite DB seed has `Map=0` while client/server spawn Elf in Noria) -> base stats & growth (`1*Lvl+2*Vit`, `1.5*Lvl+1.5*Ene`) **same**.
  - Formulas (`ClassFairyElf.cs:60-120`): `Def=Dex/10`, `DefRatePvm=Dex/4`, `AtkSpd=MagSpd=Dex/50`, `Bow=Str/14+Dex/7 .. Str/8+Dex/4`, `Fist=(Str+Dex)/7 .. (Str+Dex)/4`, `PvpAtk=0.6*Dex+3*Lvl`, `PvpDef=0.1*Dex+2*Lvl` -> **same**.
- **Magic Gladiator (`ClassMagicGladiator.cs:27-146`)**:
  - `Str=26, Dex=26, Vit=26, Ene=26`, `HP=110, MP=60`, `CreationLvl=220`, `7` pts/lvl, `LevelWarpRequirementReductionPercent=34%` (`Math.Ceiling(100/3)`) (`ClassMagicGladiator.cs:38-48`) -> suite DB seed `(26,26,26,26, 110, 60)` & growth (`1*Lvl+2*Vit`, `1*Lvl+2*Ene`) **same**.
  - Formulas (`ClassMagicGladiator.cs:58-123`): `Def=Dex/4`, `DefRatePvm=Dex/3`, `AtkSpd=Dex/15`, `MagSpd=Dex/20`, `Phys=Str/6+Ene/12 .. Str/4+Ene/8`, `Wiz=Ene/9..Ene/4`, `PvpAtk=3.5*Dex+3*Lvl`, `PvpDef=0.25*Dex+2*Lvl` -> **same**.
- **Dark Lord (`ClassDarkLord.cs:18-149`)**:
  - `Str=26, Dex=20, Vit=20, Ene=15, Cmd=25`, `HP=90, MP=40`, `CreationLvl=250`, `7` pts/lvl (`ClassDarkLord.cs:31-43`) -> suite DB seed has `Leadership=24` (**differs** by 1 Command point; S9 SQL seed also has `Life=25, Mana=90` columns swapped).
  - Growth (`ClassDarkLord.cs:71-75,125-126`): `48.5+1.5*Lvl+2*Vit` HP, `38+1*Lvl+1.5*(Ene-15)` MP -> matches suite `LevelLife=1.5, LevelMana=1, VitalityToLife=2, EnergyToMana=1.5` (**same**).
  - Formulas (`ClassDarkLord.cs:54-123`): `Def=Dex/7`, `DefRatePvm=Dex/7`, `AtkRatePvm=5*Lvl+3*Dex+Str/4+Cmd/10`, `AtkSpd=MagSpd=Dex/10`, `Phys=Str/7+Ene/14 .. Str/5+Ene/10`, `SkillMult=2+0.0005*Ene`, `PvpAtk=4*Dex+3*Lvl`, `PvpDef=0.5*Dex+2*Lvl` -> **same**.
- **Summoner (`ClassSummoner.cs:18-168`)**:
  - `Str=21, Dex=21, Vit=18, Ene=23`, `HP=70, MP=40`, `HomeMap=Elvenland (51)` (`ClassSummoner.cs:35-48`) -> base stats **same**; growth `MaximumMana = 6 + 1.5*Lvl + 1.7*Ene` (`ClassSummoner.cs:90-91,141`) **differs** from suite SQL `EnergyToMana=1.5`.
  - Formulas (`ClassSummoner.cs:59-139`): `Def=Dex/3`, `DefRatePvm=Dex/4`, `AtkSpd=MagSpd=Dex/20`, `Fist=(Str+Dex)/7 .. (Str+Dex)/4`, `Wiz/Curse=Ene/9..Ene/4`, `PvpAtk=3.5*Dex+3*Lvl`, `PvpDef=0.5*Dex+2*Lvl` -> **same** (note `ClassSummoner.cs:100-101` applies `BaseMinDamageBonus`/`BaseMaxDamageBonus` only to Wizardry and not Curse `108-112`, and `ClassSummoner.cs:50,64-88` does not gate SD/PvP attributes by `!this.UseClassicPvp`).
- **Rage Fighter (`ClassRageFighter.cs:18-116`)**:
  - `Str=32, Dex=27, Vit=25, Ene=20`, `HP=100, MP=40`, `CreationLvl=150`, `7` pts/lvl (`ClassRageFighter.cs:29-39`), growth `1.3*Lvl+2*Vit` HP, `1*Lvl+1.3*Ene` MP -> suite DB seed `(32,27,25,20, 100, 40, LevelLife=1.3, LevelMana=1, VitalityToLife=2, EnergyToMana=1.3)` (**same**).
  - Formulas (`ClassRageFighter.cs:50-101`): `Def=Dex/8`, `DefRatePvm=Dex/10`, `AtkRatePvm=3*Lvl+1.25*Dex+Str/6`, `AtkSpd=MagSpd=Dex/9`, `Phys=Str/7+Vit/15 .. Str/5+Vit/12`, `SkillMult=0.5+0.001*Ene`, `VitSkillMult=0.5+0.001*Vit`, `PvpAtk=3.6*Dex+2.6*Lvl`, `PvpDef=0.2*Dex+1.5*Lvl` -> **same** (note `ClassRageFighter.cs:41,53-77` also omits `if (!this.UseClassicPvp)` guard on SD/PvP rates).
