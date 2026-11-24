# Unit: server/src/DataModel/Entities

Checked against: Season 6 & Season 9 entity state/status definitions.

Scope: `server/src/DataModel/Entities` (`30` files, `2062` lines).

## Summary

- Core runtime entity models (`Account`, `Character`, `Item`, `ItemStorage`, `SkillEntry`, `CastleSiegeData`, `Guild`, etc.).

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `Character.cs:14-71` | `HeroState` (`New, Hero, LightHero, Normal, PlayerKillWarning, PK1st, PK2nd`) & `CharacterStatus` (`Normal=0, Banned=1, GameMaster=32`) | Season 6/9 PK status enum & GM CtlCode `32` | Same |
| `Item.cs:62-88` | Adds `StorePriceCoinType`, `StorePriceItemDefinition`, and `Guid? Serial` for HNMO multi-currency shops & UUID duplicate detection | Single-zen store + sequential int serial in classic suite | HNMO custom |
| `SkillEntry.cs:14-105` | Transient attribute/buff cache per learned skill | Runtime state | No hardcoded rules |
