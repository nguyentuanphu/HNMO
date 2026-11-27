# Audit Findings: `server/src/Persistence/Initialization/Skills`

Audited unit: `server/src/Persistence/Initialization/Skills` (42 files, 4,109 lines).

## Scope

- 26 `MagicEffectDefinition` initializers (regular buffs/debuffs, potion effects, master strengthener/proficiency/mastery effect variants)
- `SkillsInitializerBase.cs`, `MagicEffectNumber.cs`, `SkillNumber.cs`
- Custom HNMO extensions: `HnmoSkillsInitializer.cs`, `HnmoSkillValues.cs`, `HnmoTrialQuestsInitializer.cs`

## Rule Comparison Against Reference Suites

| Effect / Skill | HNMO (`file:line`) | Suite Role & Value | Result |
| --- | --- | --- | --- |
| Soul Barrier (`16`) | `SoulBarrierEffectInitializer.cs:39-64` (`Dur=60+Ene/40`; `10% + Agi/50 + Ene/200`, no `MaximumValue` cap; mana toll `2% MaxMana`) | Season 6/9 skill calc (`RegularSkillCalc.lua:26-36`: `10 + Dex/50 + Ene/200`, `SkillTime = Ene/40 + 60`, capped at `50%`) | `same` formula; `differs` cap (`HNMO` omits the `50%` cap on base `SoulBarrierReceiveDecrement`) |
| Swell Life (`48`) & Proficiency | `LifeSwellEffectInitializer.cs:40-76` (`Dur=60+Ene/10`; `12% + Ene/20 + Vit/100 + Party*1%`, `max=200%`); `LifeSwellProficiencyEffectInitializer.cs:36-48` (`+1%/party Mana`) | Season 9 skill calc (`RegularSkillCalc.lua:54-58`: `Vit/100 + 12 + Ene/20 + PartyBonus`, `60+Ene/10`); Season 6 (`RegularSkillCalc.lua:55-59`: `Vit/540 + 12 + Ene/108 + PartyBonus`) | `same` as S9 (S6 uses weaker `Vit/540 + Ene/108`; `200%` cap added in HNMO) |
| Heal (`26`), Greater Damage (`28`), Greater Defense (`27`) | `HealEffectInitializer.cs:43-50` (`5+Ene/5`); `GreaterDamageEffectInitializer.cs:39-53` (`60s`, `3+Ene/7`); `GreaterDefenseEffectInitializer.cs:39-53` (`60s`, `2+Ene/8`) | Season 6 skill calc (`RegularSkillCalc.lua:77-157`: `Ene/5+5`, `60s / 3+Ene/7`, `60s / 2+Ene/8`) | `same` |
| Shield Recovery (`234`) | `ShieldRecoverEffectInitializer.cs:43-56` (`Level + Ene/4`) | Season 6 skill calc (`RegularSkillCalc.lua:160-163`: `Ene/4 + PlayerLevel`) | `same` |
| Critical Damage Increase (`64`) & Mastery | `CriticalDamageIncreaseEffectInitializer.cs:40-67` (`Dur=60+Ene/10` max `180s`; `Ene/30 + Cmd/25`); `CriticalDamageIncreaseMasteryEffectInitializer.cs:35-41` (adds crit chance placeholder) | Season 6 skill calc (`RegularSkillCalc.lua:201-206`: `Cmd/25 + Ene/30`, `Time=Ene/10+60`, no 180s cap in Lua) | `same` formula; `differs` (`180s` max cap added in HNMO) |
| Damage Reflection (`217`) | `ReflectionEffectInitializer.cs:41-62` (`Dur=30+Ene/24` max `180s`; `30% + Ene/42` max `60%`) | Season 6 skill calc (`RegularSkillCalc.lua:249-258`: `30 + Ene/42` capped at `25%` [always `25%`], `Time=30 + Ene/25`) | `differs` (HNMO allows up to `60%` reflect vs S6 `25%` cap, and uses `/24` max `180s` for duration) |
| Berserker (`218`) | `BerserkerEffectInitializer.cs:39-98` (`Dur=30+Ene/20` max `180s`; `Mana/Dmg +Ene/30%` max `100%`; `HP -40% + Ene/60%`; `Min/MaxPhys = 140+(Str+Agi)/50`, `160+(Str+Agi)/30`) | Season 6 skill calc (`RegularSkillCalc.lua:261-275`: `UP=Ene/30`, `DOWN=Ene/60`, `Time=Ene/20+30`, `AttackMin=(Str+Dex)/7`, `AttackMax=(Str+Dex)/4`) | `same` duration/mana/HP scaling; `differs` physical dmg formula (`140+(S+A)/50` / `160+(S+A)/30` vs suite `(S+A)/7` / `(S+A)/4`) & HNMO caps (`180s`, `100%`) |
| Sleep (`219`), Weakness (`221`), Innovation (`222`) | `SleepEffectInitializer.cs:39-100`; `WeaknessSummonerEffectInitializer.cs:39-128`; `InnovationEffectInitializer.cs:39-128` (exact PvM/PvP chance & effect/duration equations + level divisors) | Season 6 skill calc (`RegularSkillCalc.lua:294-343`: identical equations; no max duration/effect clamps in Lua) | `same` core PvM/PvP formulas; `differs` (`HNMO` adds max caps, e.g. Sleep `20s/10s`, Weakness `73%/46%`, Innovation `64%/48%`) |
| Explosion (`223`) & Requiem (`224`) DoT | `ExplosionEffectInitializer.cs:40`; `RequiemEffectInitializer.cs:40` (`Duration=5s`) | Season 6 skill calc (`RegularSkillCalc.lua:346-365`: `Time=5`) | `same` |
| Shield Defense (`18`) | `DefenseEffectInitializer.cs:39-48` (`4s`, `0.50f Multiplicate`) | Season 6 skill calc (`RegularSkillCalc.lua:475-479`: `InDamage / 2`) | `same` |
| Expansion of Wizardry (`233`), Str/Mast | `WizardryEnhanceEffectInitializer.cs:39-52` (`1800s`, `MinWiz + Ene/45` max `100`); `...Strengthener`/`...Mastery` | Season 6 buff table (`BuffEffectManager.xml:117,170-171`, `Index=82,138,139`) & `SkillList.xml:147` (`BuffIndex=82`) | `same` slot/roles; base formula matches `Ene/9 * 0.2` capped at `100` |
| Rage Fighter Buffs/Debuffs (`260,261,266,267,268,270`) | `IgnoreDefenseEffectInitializer.cs` (`60+Ene/5` max `180s`, `-1.04%+Ene/100%` max `10%`); `IncreaseHealth` (`16.8+Ene/10` max `200`); `IncreaseBlock` (`2+Ene/10` max `100`); `Weakness` RF (`10%`, `10s`, `-5%`); `DefenseReductionBeastUppercut` (`10%`, `10s`, `x0.9`); `DecreaseBlock` (`10%`, `10s`, `-50 PvM / -20 PvP`) | Season 6 buff table (`BuffEffectManager.xml:161-164,185-187`, `Index=129-132,153-155`) | `same` buff indices & mechanics; `DecreaseBlock` & debuff proc chances (`10%`, Fire Slash `45%`) are explicit config values |
| Potions & Consumables | `PotionOfBless` (`10`, `120s`, `x1.2` all attacks — `TODO` L42 says Castle Siege gates/statues only); `PotionOfSoul` (`11`, `60s`, `+20 Spd`, `+8 AG`, `+50%` Light/Ice res); `Alcohol` (`201/sub54`, `80s`, `+20 Spd`) | Season 6 buff table (`BuffEffectManager.xml:45-46`: Bless `+20%` Siege-only, Soul `+8 AG / +50% Light+Ice / +20 Spd`) | `same` Potion of Soul; `differs` Bless potion scope (applies globally in HNMO due to `TODO` at L42) & Alcohol uses internal ID `201` (`sub54`) |
| Elemental status & base skill helpers | `SkillsInitializerBase.cs:171-306` (`Poison` `20s`, `Decay` `10s`, `Ice` `10s`, `IceArrow` `7s`, `Pollution` `2s`, `ChainDrive` `10s 40%`) | Season 6/9 skill attributes & elemental status durations | `same` structure; note `StunEffectInitializer` uses `Duration=2s` base |
| HNMO custom skills & trials (`700..727`) | `HnmoSkillsInitializer.cs:1-274`, `HnmoSkillValues.cs:1-177`, `HnmoTrialQuestsInitializer.cs:1-151` (`28` skills `700..727`, `7` effects `180..186`, `7` Trial quests Lv `250` = `200` kills + `5` Chaos) | HNMO custom rules; matched count `28/28` in `diff-client-tables.txt:358` (`Web_new_skills(700+)=28`) | Custom HNMO rules (`same` vs web client table) |

## Re-check 2026-11-27 (changes 9f72bccd..a108e846)

Commits read (every changed file read in full):

- 7a58c00b Potion of Bless: raise the damage only against Castle Siege gates and statues

Evidence:

- BlessPotionEffectInitializer: no damage power-up any more (the bonus is applied by the damage calculation, siege gates/statues only) - same as the owner decision of 26 Nov - fixed.
