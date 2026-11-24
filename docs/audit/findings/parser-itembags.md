# Parser Findings: Item Bags & Drop Boxes (`diff-itembags.txt`)

- **Checked on**: 2026-11-24
- **Outputs**: [diff-itembags.txt](../data/diff-itembags.txt) (174 lines), [s9/diff-itembags.txt](../data/s9/diff-itembags.txt) (174 lines)
- **Sources**: all 112 Season 6 item bag XMLs, all 118 Season 9 item bag XMLs, and HNMO `server/src/Persistence/Initialization/VersionSeasonSix/Items/BoxOfLuck.cs`, `BloodCastleInitializer.cs:329`, `ChaosCastleInitializer.cs`

## Fix Summary

`parseSuiteItemBags` previously counted `<Item>` tags per bag and omitted levels, options, excellent/ancient flags, rates, and HNMO comparison. The updated parser reads every `<Item>` attribute (`Cat`, `Index`, `ItemMinLevel`, `ItemMaxLevel`, `Skill`, `Luck`, `Option`, `Exc`, `SetItem`, `SocketCount`, `ElementalItem`), `<BagConfig>` (`ItemRate`, `MoneyDrop`), and `<Drop Rate>`, and compares them directly against HNMO's `BoxOfLuck.cs` (`ItemDropItemGroup`, `AddDropItem`, `AddArmorSet`, `AddMoneyDropFallback`) and mini-game reward drop groups.

## Hand-Verified Sample Values

1. **Box of Kundun +1 (`Item_(14,11,8)_Kundun_Box+1.xml`)**:
   - Hand check in both suites' XML: `ItemRate="10000"`, `MoneyDrop="0"`, `<Drop Rate="10000" Count="1">`, items have `ItemMinLevel="0" ItemMaxLevel="0" Skill="1" Luck="1" Option="1" Exc="-1"`; includes `0:0` Kris, `0:3` Katana, `0:13`, `0:9`, `0:12`, `7:5..11:5` (Leather), `7:9..11:9` (Plate).
   - HNMO `BoxOfLuck.cs:362-407`: `ItemType = Excellent`, `Chance = 0.2`, `MoneyAmount = 50000`, `47` items (adds Leather/Pad/Vine/Bronze/Silk/Violent Wind/Red Wing sets and early weapons) -> **differs** (`Chance 20% + Zen fallback` vs suite `100%`; distinct item lists).
2. **Box of Kundun +3 (`Item_(14,11,10)_Kundun_Box+3.xml`)**:
   - Hand check in the Season 6 suite's XML: contains only 5 jewel/talisman rows (`12:15` @ rate `3000`, `14:13/14:14/14:42` @ rate `4000`, `12:140` @ rate `100`, `MoneyDrop="7000"`) — Season 6 suite packaging defect vs Season 9 suite's 63 Excellent items (`Skill=1, Luck=1, Opt=1, Exc=1`) and HNMO `BoxOfLuck.cs:459` (`72` Excellent items @ `20%`).
3. **Red Ribbon Box (`Item_(12,32,0)_Red_Ribbon_Box.xml`)**:
   - Hand check in both suites' XML: `47` items (`Lv6-9`, `Skill=1, Luck=1, Option=1, Exc=0`, `ItemRate=1000`, `MoneyDrop=30000`).
   - HNMO `BoxOfLuck.cs:1075`: exact same `47` item keys (`same items`), `Lv7-9`, `Chance=50%`, `MoneyAmount=10000`.
4. **Blood Castle 1–8 Rewards (`Event_BloodCastle(1..8)_Reward.xml`)**:
   - Hand check in both suites' XML: `6` items (`13:14`, `13:52`, `14:13`, `14:14`, `14:16`, `12:15`) + `300,000` Zen vs HNMO `BloodCastleInitializer.cs:329` (`12:15 Jewel of Chaos` only) -> **differs**.
