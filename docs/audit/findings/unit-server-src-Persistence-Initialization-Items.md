# Audit: `server/src/Persistence/Initialization/Items`

- **Unit**: `server/src/Persistence/Initialization/Items`
- **Files read in full**: 10 of 10 (2,125 lines total)
  - `ArmorInitializerBase.cs` (515), `BuiltInItemStatProfiles.cs` (271), `ExcellentOptions.cs` (167), `ItemGroups.cs` (98), `ItemHelper.cs` (233), `ItemPriceDefinitions.cs` (257), `ItemStatProfiles.cs` (340), `OptionExtensions.cs` (105), `OrbRequirements.cs` (63), `TransformationRingConfiguration.cs` (71)
- **Suite references**:
  - Season 6 & Season 9 `Data/Items/ItemList.xml` (and `docs/audit/data/diff-items.txt`, `docs/audit/data/s9/diff-items.txt`)

## Rules & Comparison

| Rule / Behaviour | HNMO | Season 6 / Season 9 Suite | Match |
|---|---|---|---|
| Armor & shield level tables + full-set bonuses (`ArmorInitializerBase`) | Armor defense bonus by level `[0,3,6,...,66]`; Shield defense `[0..15]`, Shield defense rate `[0,3,...,66]`; full set gives `DefenseRatePvm * 1.1` (any level) and `DefenseBase * (1 + (setLevel - 9) * 0.05)` (+5%..+30% for +10..+15) | Standard S6/S9 armor/shield upgrade tables & +10..+15 full set defense bonuses | Same |
| Excellent options (`ExcellentOptions`) | 6 Wizardry/Physical options (`Mana/8`, `HP/8`, `+7 Speed`, `+2% Dmg`, `+Lvl/20`, `+10% Exc`) & 6 Defense options (`+40% Zen`, `+10% DefRate`, `5% Ref`, `4% DD`, `+4% Mana`, `+4% HP`); `Max=2`, `Curse` unused | S6/S9 excellent option definitions (Summoner books use Wizardry Exc options) | Same |
| Item stat profiles (`BuiltInItemStatProfiles`, `ItemStatProfiles`) | Client-matching stat profiles (`Armor`, `Weapon`, `Shield`, `Book`, `Sceptre`, `Orb`, `Scroll`, `Helper`, `SmallWing`, `FirstTierWing`, `SecondTierCape`, `SecondTierWing`, `ThirdTierWing`) | Standard client/server item formula groups | **Difference (S9)**: `ItemStatProfiles.GetWingProfileName` omits S9 wings `12-262..268` (they fall back to `DefaultName`) |
| Item prices (`ItemPriceDefinitions`) | Special prices for Jewels (`Bless 3M`, `Soul 6M`, `Chaos 810k`, `Life 16M`, `Creation 15M`, `Guardian 15M`), Packed Jewels, Potions, Tickets, Moss Gamble Vouchers `13-71..75` (`2M`), Dark Raven (`lvl*1M`), Dark Horse (`lvl*2M`), `SkillsWithoutValue = [66,223,224,225]` | Standard S6/S9 shop/NPC prices | Same |
| Orb/Scroll requirements (`OrbRequirements`) | `20 + (multiplier * level * factor / 100)` (`4` for Energy, `3` for Str/Agi; uses `levelRequirement` if `> 0` for scrolls) | Standard client/server orb & scroll requirement formula | Same |
| Transformation Ring (`TransformationRingConfiguration`) | Level >= 3 requires char level 50; +5 (Death Cow) grants `+20%` Max HP | Standard S6/S9 transformation ring rules | Same |
