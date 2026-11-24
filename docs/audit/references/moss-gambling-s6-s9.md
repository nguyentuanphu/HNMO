# Season 6 & Season 9 Moss Merchant Gambling Reference

Extracted from Season 6 and Season 9 suite `GamblingLottery.xml` and NPC Shop 492 configs. Both Season 6 and Season 9 use identical tables and rates.

## 1. General Settings
- **NPC**: `492` (Elbeland), selling 5 vouchers (`Cat=13, Index=71..75`, serialized with `Skill=1, Luck=1, Option=3`).
- **`Enable`**: `1`
- **`GamblingPrice`**: `1,000,000` (base multiplier; shop tax factor applied in suite)
- **`ShoppingDelayTime`**: `120` s

## 2. Tier & Option Rates (per 1,000,000)
Same 5 tiers across all 5 Rare Item voucher categories (`71`: Sword/Mace/Spear, `72`: Staff, `73`: Bow/Crossbow, `74`: Scepter, `75`: Stick):
- **Tier 1** (low-tier Exc, Lv 0–3): `ItemChance = 560,000`, `Skill = 100,000`, `Luck = 50,000`, `Option = 120,000`, `Exc = 5,000`
- **Tier 2** (mid-tier Exc, Lv 0–3): `ItemChance = 380,000`, `Skill = 100,000`, `Luck = 50,000`, `Option = 100,000`, `Exc = 3,000`
- **Tier 3** (high-tier Exc/normal, Lv 0–3): `ItemChance = 50,000`, `Skill = 50,000`, `Luck = 50,000`, `Option = 50,000`, `Exc = 1,000`
- **Tier 4** (380/high non-Exc, Lv 0–3): `ItemChance = 9,980`, `Skill = 30,000`, `Luck = 10,000`, `Option = 10,000`, `Exc = 0`
- **Tier 5** (Talisman of Chaos Assembly `14:96`, Lv 0): `ItemChance = 20`, no options

## 3. Additional Option & Excellent Selection Weights (per 1,000,000)
- **`ItemOptionChance`**: `+4` = `850,000` (`85%`), `+8` = `100,000` (`10%`), `+12` = `50,000` (`5%`), `+16..+28` = `0`.
- **`ExcOptionCountChance`**: `Count2 = 900,000` (`90%`), `Count3 = 100,000` (`10%`), others `0`.
- **`ExcOptionSelectionRate`**: bits 1..6 weighted `[100000, 200000, 200000, 200000, 200000, 100000]`.
- **`RareItemRewardChance`**: `3 / 1,000,000` per voucher type.
