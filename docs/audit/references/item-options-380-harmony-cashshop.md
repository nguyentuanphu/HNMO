# Reference: Lv380 Options, Buff Items, Cash Jewelry, Ancient Sets & Misc Items (S6 & S9 vs HNMO)

> Sanitized reference captured during the `server/src/Persistence/Initialization/VersionSeasonSix/Items (files 1)` audit (2026-11-24) so future checks do not need to re-read these files.

## 1. Level 380 Guardian Options (`S6` & `S9` Legacy Suites vs `GuardianOptions.cs`)
Both Season 6 and Season 9 legacy suite configurations (`IGC_ItemOptionManager.xml` Section 0 / `IGC_Item380Option.xml`) define the exact same values:
- **Weapons** (`0:22`, `0:23`, `2:14`, `4:21`, `5:12`, `5:19`, `0:35`):
  - `Option1 = 2` (Additional Damage PvP `+200`), `Option2 = 1` (Attack Success Rate PvP `+10`) — matches `GuardianOptions.cs` (`FinalDamageIncreasePvp +200`, `AttackRatePvp +10`).
- **Helms** (`7:29..31,33,43,73`):
  - `Option1 = 8` (SD Recovery Rate `+20`), `Option2 = 3` (Defense Success Rate PvP `+10`) — matches `GuardianOptions.cs` (`ShieldRecoveryMultiplier +20`, `DefenseRatePvp +10`).
- **Armors** (`8:29..33,43,73`):
  - `Option1 = 7` (SD Auto Recovery), `Option2 = 3` (`+10` Def Rate PvP) — matches `GuardianOptions.cs` (`ShieldRecoveryEverywhere +1`, `DefenseRatePvp +10`).
- **Pants** (`9:29..33,43,73`):
  - `Option1 = 4` (Defensive Skill PvP `+200`), `Option2 = 3` (`+10` Def Rate PvP) — `GuardianOptions.cs` adds `Stats.DefensePvp, 200 / 2` (`100`) and `DefenseRatePvp +10`.
- **Gloves** (`10:29..33,43`):
  - `Option1 = 5` (Max HP `+200`), `Option2 = 3` (`+10` Def Rate PvP) — matches `GuardianOptions.cs` (`MaximumHealth +200 AddFinal`, `DefenseRatePvp +10`).
- **Boots** (`11:29..33,43,73`):
  - `Option1 = 6` (**Max SD `+700`**), `Option2 = 3` (`+10` Def Rate PvP).
  - **Mismatch in `GuardianOptions.cs:106`**: sets `Stats.MaximumShield, 200` instead of `700`.

## 2. Buff Items (`S6` `IGC_ItemOptionManager.xml` vs `BuffItems.cs`)
- **Halloween Scrolls (`14:46..50`)** (`1800s`): Blessing (Atk Speed `+10`), Wrath (Dmg/Wiz `+25`), Cry (Def `+100`), Food (Max HP `+500`), Drink (Max Mana `+500`).
- **Seals (`13:43,44,45,62,63,93,94`)**:
  - `13:43` Seal of Ascension: Exp `+50%` (matching `ExperienceRate * 1.5f`).
  - `13:44` Seal of Wealth: Exp `+100%`, **Drop Rate `+150%`** (`BuffItems.cs` sets `ExperienceRate * 2f`; no drop rate modifier).
  - `13:45` Seal of Sustenance: Exp gain disabled (`0f`).
  - `13:62` Seal of Healing (`1800s`): Exp `+50%`, Auto HP Rec `+5%` (matches `BuffItems.cs`).
  - `13:63` Seal of Divinity (`1800s`): **Drop Rate `+150%`**, Auto Mana Rec `+5%` (`BuffItems.cs` only adds `ManaRecoveryMultiplier +0.05f`, no drop rate).
  - `13:93` Master Seal of Ascension (`1800s`): Master Exp `+50%` (matches `BuffItems.cs`).
  - `13:94` Master Seal of Wealth (`1800s`): Master Exp `+100%`, **Drop Rate `+150%`** (`BuffItems.cs` sets `MasterExperienceRate * 2f`; no drop rate).
- **Auras (`13:104,105`)** (`1800s`): Max AG `+Level * 1`, Max SD `+Level * 10` (both match `BuffItems.cs`; suite comment text mentions "level by 10" on 66/67, XML `Value1` is `1` and `10`).
- **Scrolls (`14:72..77, 97, 98, 140`)**:
  - Quickness: Atk Speed `+15`; Defense: Def `+60` (`DefenseFinal +30`); Wrath: Atk `+30`; Wizardry: Wiz `+30`; Health: Max HP `+200`; Mana: Max Mana `+300`; Battle: Crit Dmg `+20%`; Strength: Exc Dmg `+20%`; Healing: Auto HP Rec `+3%`, Max HP `+100` — all match `BuffItems.cs`.
- **Elixirs (`14:78..82`)** (`864000s` = 10 days): `+50` Str / Agi / Vit / Ene / Cmd — all match `BuffItems.cs`.

## 3. Cash Shop Rings & Necklaces (`13:109..115`)
- **Legacy S6 suite (`IGC_ItemOptionManager.xml` Sections 24–30)**: all have Auto HP Rec `+8%`, plus Sapphire Ring `+6%` dmg/wiz, Ruby Ring `+6` Max HP, Topaz Ring `+60%` zen, Amethyst Ring `6%` dmg absorb, Ruby Necklace `+15%` exc dmg, Emerald Necklace `+10` atk speed, Sapphire Necklace none.
- **HNMO `Jewelery.cs`**: intentionally follows client `Group13_Helper.json` values (`+3%` Auto HP Rec + `+4%` Mana / `+4%` HP / `+30%` Zen / `4%` DmgDec / `10%` ExcChance / `+7` AtkSpd / `+Mana/8` on kill).

## 4. Ancient Sets (`AncientSets.cs` vs `S6`/`S9` `IGC_ItemSetOption.xml`)
- **Kantata (`Set Index 9`)**: `FullOptIdx2 = 17, FullOptVal2 = 10` (`+10%` Excellent Damage Chance) and `FullOptIdx3 = 18, FullOptVal3 = 20` (`+20` Excellent Damage).
  - **`AncientSets.cs:199`** erroneously sets `(Stats.ExcellentDamageChance, 10.0f)` (`+1000%`) instead of `0.10f` (`+10%`). Every other set in `AncientSets.cs` correctly uses `0.05f` / `0.10f` / `0.15f` for `CriticalDamageChance` and `ExcellentDamageChance`.

## 5. S9 Suite Metadata for `MossGambleItemCatalog` & `Stardust`
- **`13:71..75` Moss Gamble Vouchers**: S9 suite item list has `Slot="8"`, `Width="2" Height="3"`, `Durability="255"`, `Dump="0" Transaction="0" PersonalStore="0" StoreWarehouse="0" SellToNPC="0" Repair="0"`. HNMO `ItemRules.cs` currently has no entries blocking `13:71..75`.
- **`14:153` Stardust**: S9 suite item list has `Width="1" Height="1"`, `Drop="0"`, `Money="60"`, `Overlap="20"`, `Dump="0" Transaction="0" PersonalStore="0" StoreWarehouse="0" Repair="0"`, `SellToNPC="1"`. HNMO `Misc.cs` sets `Durability = 1`, `Value = 60`; `ItemRules.cs` has no rule entry for `14:153`.
