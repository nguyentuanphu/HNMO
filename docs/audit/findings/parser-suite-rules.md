# Parser Findings: Suite-Wide Gameplay Rules & Infra Sanitization (`diff-suite-rules.txt` & `diff-common-rules.txt`)

- **Checked on**: 2026-11-24
- **Outputs**: [diff-suite-rules.txt](../data/diff-suite-rules.txt), [diff-common-rules.txt](../data/diff-common-rules.txt), and `docs/audit/data/s9/` equivalents
- **Sources**: 170 Season 6 gameplay rule files and 200 Season 9 gameplay rule files compared against HNMO `docs/audit/data/hnmo/` & `docs/audit/data/hnmo-s9/` CSV dumps and HNMO `GameConfigurationInitializer.cs`, `CashShopInitializer.cs`, `PlayerKillPenaltyPlugIn.cs`, `OfflineLevellingPlugIn`, `ExcellentDropChanceUpdatePlugIn.cs`

## Fixes Applied

1. **Removed all database, connection, password, IP, port, CRC, and non-rule infra settings** from `parsers-all-rules.mjs` and `diff-all.mjs`:
   - Excluded `.sql` setup scripts, DataServer / ConnectServer directories, `AllowedIPList`, `ServerList`, `ConnectMember`, `IpList`, `MapServerInfo`, `LangBase`, `CashItem_Info`, and `README` files.
   - Filtered out all `[SQL]` / `[Database]` / `[AntiHack]` sections and all `IP`, `Port`, `User`, `Pass`, `DB`, `DSN`, `Path`, `ServerName`, `CRC`, `MachineID`, `WanIP`, and `EventChipServerIp` keys across `.ini` / `.cfg` diffs.
2. **Added direct HNMO comparison across remaining rule domains** (S6 vs S9 vs HNMO dumps & `file:line`):
   - Fixed `ExpSystem`, `ExcellentOptions`, `Item380Option`, `HarmonyOptions`, and `VipSettings` (`<Vip>` tags -> `S6=4, S9=4` tiers) lookups so HNMO counts and formulas are reported without zero mismatches or missing keys.

## Hand-Verified Sample Values

1. **ExpSystem (`IGC_ExpSystem.xml`)**:
   - Hand check in both suites' `ExpSystem.xml`: `<StaticExp Normal="1000.0" Master="1000.0" Event="0.0" Quest="1.0" />` + `36` `<Range>` rows (`MinReset`, `MaxReset`, `MinLevel`, `MaxLevel`, `NormalExp`, `MasterExp`).
   - HNMO `exp_and_globals.csv` & `GameConfigurationInitializer.cs`: `ExperienceRate=1`, polynomial `ExperienceFormula` and `MasterExperienceFormula` (**differs** — static/flat polynomial formulas vs suite's 1000x static multiplier + 36 reset/level dynamic ranges).
2. **ZenDrop (`IGC_ZenDrop.xml`)**:
   - Hand check in the Season 6 suite's XML: `<ZenDropSystem Enable="0" MultiplyByMonsterLevel="0" MultiplyChanceRate="4000" />` (`77` maps); Season 9 suite XML: `<ZenDropSystem Enable="1" MultiplyByMonsterLevel="1" ... />` (`79` maps).
   - HNMO `GameConfigurationInitializer.cs`: `1.0` default formula drops everywhere (**differs**).
3. **VIP Tiers (`IGC_VipSettings.xml`)**:
   - Hand check in both suites' XML: `4` `<Vip>` entries (`Bronze`, `Silver`, `Gold`, `Platinum`).
   - HNMO `GameConfigurationInitializer.cs`: no VIP tier table (**missing in HNMO**).
