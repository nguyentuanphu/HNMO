# Unit: server/src/GameLogic/PlugIns (files 1)

Checked against: the Season 6 suite's common settings and server config, and the Season 9 suite's common settings and server config.

Scope: `server/src/GameLogic/PlugIns (files 1)` (`123` files, `5930` lines; root plugin interfaces, gameplay/event/anti-cheat/ops plugins and configurations).

## Summary

- Plugin point interfaces (`IAreaSkillPlugIn.cs`..`IWhisperMessageReceivedPlugIn.cs`, ~`55` files) and infrastructure helpers (`HnmoStateStore.cs`, `WebsiteBrand.cs`) contain no game rules.
- Classic MU rules implemented here match the Season 6 and Season 9 suites: Kundun regen (`275`, `400/s`, `15,000 / 600s`), stack-to-ticket item merges (`14:110->14:111`, `14:101->14:102`, `14:29->14:28`), Pet EXP share (`20%`, split `10%` if dual pets), Guild War kill score (`2` GM / `1` member), Self-Defense (`1m`), and classic death EXP/Zen penalty bands.
- HNMO custom rules include custom skill auto-learning (`700..727` at Lv `80/150/220`), token-bucket speed/walk/ping anti-cheat thresholds, spawn multiplier `[0.5..5]`, monster attribute scaling defaults (`+25%`), IP cap (`3`), and website/bank top-up config bounds.

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `KundunHealthRefillConfiguration.cs:19-37`, `KundunHealthRefillPlugIn.cs:21-56` | Monster `275`, `400 HP/s`, `+15,000 HP` every `600s` | Season 6 & Season 9 server config (`KundunRefillHPSec=400`, `KundunRefillHP=15000`, `KundunRefillHPTime=600`) | Same |
| `KalimaConstants.cs:17-33`, `SignOfDimensionsStackedPlugIn.cs:19-41`, `SuspiciousScrapOfPaperStackedPlugIn.cs:19-41`, `SymbolOfKundunStackedPlugIn.cs:15-40` | Full stack converts `14:29->14:28` (Lost Map), `14:110->14:111` (Mirror of Dimensions), `14:101->14:102` (Gaion's Order) | Classic / Season 6 & Season 9 ticket stack merges | Same |
| `PetExperiencePlugIn.cs:28,41-46,85-87` | `PetShare = 0.2` (`20%` of kill EXP; halved to `10%` each when mount & attack pet equipped); enforces Dark Raven Leadership requirement per level | Classic MU Dark Lord pet EXP formula | Same |
| `GuildWarKillScorePlugIn.cs:31` | Guild War kill awards `2` points for Guild Master, `1` point for regular member | Classic Guild War scoring | Same |
| `PlayerLosesExperienceAfterDeathPlugIn.cs:90-102` | Normal loss `3%/2%/1%` (`Lv 11+/151+/221+`), Master `1%`; PK states `5%/10%/20%`; waived in mini-games or with `DeathPenaltyProtection` | Classic death EXP loss tiers | Same |
| `PlayerLosesMoneyAfterDeathPlugIn.cs:79-85` | Inventory zen loss `1%/2%/3%/4%` (`Lv 10+/150+/220+/Master`), Vault loss `0%`; waived in mini-games or with `DeathPenaltyProtection` | Classic death Zen loss (`0%` vault tuned in HNMO updates) | Same / Tuned |
| `SelfDefensePlugInConfiguration.cs:16`, `SelfDefensePlugIn.cs:101` | `SelfDefenseTimeOut = 1m` (refreshed on non-zero non-reflected damage outside free-PK mini-games) | Suite `SelfDefense` timer (`60s`) | Same |
| `BloodCastleArchangelTalkPlugIn.cs:23`, `KanturuGatewayPlugIn.cs:31`, `IllusionTempleStoneStatueTalkPlugIn.cs:23`, `IllusionTempleAllianceStorageTalkPlugIn.cs:22`, `IllusionTempleIllusionStorageTalkPlugIn.cs:22` | Event NPC mappings (`232` Archangel, `367` Gateway Machine, `380/383/384` IT statue/storages) | Suite NPC numbers | Same |
| `LearnSkillsByLevelPlugInConfiguration.cs:30-35`, `LearnSkillsByLevelPlugIn.cs:72-75` | Auto-teaches skills `700..727` (`700 + 4*class + 0..2`) at levels `80, 150, 220` | Custom HNMO progression skills | HNMO custom |
| `AttackSpeedCheck.cs:41-68`, `AttackSpeedModel.cs:23-31,103`, `WalkSpeedValidator.cs:26-48`, `ViolationPolicy.cs:19-73`, `SpeedHackDetectConfiguration.cs:50-223` | Token-bucket attack check (`450ms - speed*1.2`, min `60ms`, `5` tokens); walk `300ms` base step over `15s` window (`2000ms` tolerance, `5`-tile offset); `3` warnings in `1h` bans account | Custom HNMO server anti-cheat model | HNMO custom |
| `MonsterAmountConfiguration.cs:19-24`, `MonsterAttributeScalerConfiguration.cs:13-17`, `MaximumConnectionsPerIpPlugInConfiguration.cs:16`, `LoginTimeoutPlugInConfiguration.cs:16`, `LostConnectionDetectionConfiguration.cs:20`, `BankTopUpConfiguration.cs:41-48` | Spawn multiplier `[0.5..5]` (default `1`), monster scaler defaults `25%`, IP limit `3`, login timeout `5m`, heartbeat timeout `2m`, BankTopUp defaults `100 VND/coin`, min `10,000` | Server operator / HNMO settings | HNMO custom |
