# Unit: server/src/DataModel/Configuration (files 1)

Checked against: Season 6 and Season 9 client/suite formula & global config roles.

Scope: `server/src/DataModel/Configuration (files 1)` (`66` files, `5251` lines).

## Summary

- Defines configuration model classes and documented defaults consumed by initialization & runtime plugins.

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `GameConfiguration.cs:42,112,119` | Property defaults: `MasterExperienceRate = 1.0f`, `ExcellentDropLevelBonus = 25`, `AncientDropLevelBonus = 30` | Client/suite item requirement & damage bonus drop-level offsets (`+25` exc / `+30` ancient) | Same |
| `GameConfiguration.cs:25-31,137-218` | Documented classic defaults: `MaximumLevel=400`, `MaximumMasterLevel=200`, `RecoveryInterval=3000ms`, `MaximumPartySize=5`, `MaximumCharactersPerAccount=5`, `LetterSendPrice=1000`, `DamagePerOneItemDurability=69`, `DamagePerOnePetDurability=100`, `HitsPerOneItemDurability=564` | Suite global caps & classic durability wear divisors | Same |
| `GameVersion.cs:24-100` | Version ordering enum (`0.75`, `0.95d`, Seasons) | Version metadata | Metadata |
| `DuelConfiguration.cs:13-45`, `SystemConfiguration.cs:17-95` | Duel & system deployment configuration schemas | Schema | No hardcoded rules |
