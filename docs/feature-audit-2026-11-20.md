# Feature audit, groups 5 to 13 (20 Nov 2026)

Continues the audits of EXP, drops, zen, chaos machine, jewels, resets, character creation
([docs/feature-audit-shops-events-2026-11-20.md](feature-audit-shops-events-2026-11-20.md) covers shops, money and events).

Reference: classic MU Season 6. Where HNMO differs, the owner chose (20 Nov 2026) to follow the Season 6/9 server
suites (their character formulas and `CommonServer` settings); both suites agree on every rule checked here.

Result key: **OK** = matches the reference or is a sound HNMO rule; **Fixed** = bug, fixed in the linked draft PR
with a test that fails before the fix; **Owner** = the owner decided; **Note** = difference kept, worth knowing.

## 5. Combat

| Check | Result | Notes |
|---|---|---|
| Physical / wizardry / curse damage order (base, defense, excellent x1.2, critical, skill, final) | OK | Same order as classic. |
| Double wield (MG, DK, RF) | OK | Each weapon 55 %, both added = 110 %. |
| Damage against a monster whose defense rate is above the attack rate | OK | 30 % of the damage (classic). |
| Minimum damage | OK | Level / 10. |
| Chaos Castle PvP | OK | Halved (once more only without SD). |
| Duel damage | OK | 60 % without SD. |
| Damage reflection between two reflecting players | **Fixed** | [#162](https://github.com/nguyentuanphu/HNMO/pull/162): the returned hit was reflected again and bounced back and forth. |
| SD (shield) against monster hits | **Fixed** | [#163](https://github.com/nguyentuanphu/HNMO/pull/163): monster hits went 90 % to the SD; the 90/10 split is PvP only. |
| SD/HP split in PvP | OK | 90 / 10 (`DamageDevideToSD/HP` of the suites); SD decrease options and bypass chance applied. |
| PvP hit chance | **Fixed** | [#164](https://github.com/nguyentuanphu/HNMO/pull/164): was `1 - D/A` (3 % for equal players); now `2 * A/(A+D) * La/(La+Ld)` with the 100/200/300-level penalties. |
| PvM hit chance | OK | `1 - D/A`, at least 3 %. |
| PvP attack and defense rates, all classes | OK | Match the suites' formulas. |
| PvM attack rate, defense rate, defense, attack and magic speed, base damage, all classes | OK except two | Match the suites' formulas, except: |
| Magic Gladiator defense | **Fixed** | [#165](https://github.com/nguyentuanphu/HNMO/pull/165): agility / 4 (was / 5), with an update for existing databases. |
| Dark Lord attack rate (PvM) | **Fixed** | [#166](https://github.com/nguyentuanphu/HNMO/pull/166): agility * 3 + strength / 4 (was * 2.5 and / 6), with an update. |
| Class damage rates (`RateTuning` class PvP/PvM %) | OK; Note | Applied once, after all other modifiers, also to the raven. The suites have an attacker-by-defender class table for PvP; HNMO has one PvP rate per attacking class. |
| Combat limits (max hit, PvP %, Castle Siege %) | OK; Note | Off by default. The Castle Siege reduction applies to every hit on Valley of Loren, monsters' hits too. |
| Per-map PvP switch (`MapRules`) | OK | Cancels the attack before damage. |
| Excellent hit also adds the critical damage bonus (physical only) | Note | Kept from the base engine; wizardry excellent hits don't add it. |
| Combat settings no code reads | OK | None found in the unused-settings scan. |

## 6. Skills

| Check | Result | Notes |
|---|---|---|
| Damage, mana, AG and range of every regular skill (Season 6 data vs the suites' skill list) | OK | All equal except: wizard spells have no level requirement on the skill (it is on the scroll, as intended) and the ranges of Evil Spirit (7 vs 6) and Cometfall (3 vs 6, HNMO uses it as the area radius). |
| Skill delays (cooldowns) on the server | **Fixed** | [#167](https://github.com/nguyentuanphu/HNMO/pull/167): only the client checked them; the server now refuses a cast before 80 % of the suites' delay (Earthshake 10 s, Iron Defense 60 s, Blood Storm 10 s, ...). Chain Drive is left out because one cast sends two hits. |
| Dark Side mana | **Fixed** | [#168](https://github.com/nguyentuanphu/HNMO/pull/168): it was free (the rage range request never charged the skill). |
| Mana / AG paid once per cast, refused casts free | OK | `Player.TryConsumeForSkillAsync` for all skill actions, bots and offline play. |
| Client skips its delay check for Rage Fighter skills | Note | Same as the desktop client; the server check covers it now. |
| Buff and debuff durations, master tree values | Not checked in depth | The suites keep most of these inside the program; spot checks only. |

## Settings that differ from the Season 6/9 server suites

Compared on 20 Nov 2026 (defaults of a new Season 6 database vs the suites' text files). None changed yet: most are balance choices for the owner.

| Setting | HNMO | Suites | Kind |
|---|---|---|---|
| EXP rate | 1x | 1000x | host setting |
| Party EXP bonus | x1.05 per extra member (608 % for 5) | 160-220 %, set party 230-300 % | code fallback; RateTuning table can replace it |
| Excellent item drop | 0.01 % per kill | 0.2 % | data |
| Jewel of Life | up to +16 | up to +28 | data / RateTuning |
| Luck on dropped items | 25 % | 50 % | data |
| Items and zen on the ground | 60 s | S6 10 s (S9 items 20 s, zen 15 s) | host setting |
| Blood Castle / Chaos Castle / Illusion Temple timetables | every 4 h / 4 h / 2 h; Chaos Castle starts with 1 player | every 2 h / odd hours / 7 a day; 2 players | host settings; CC minimum in code |
| PK clear cost | 10 M zen per kill | 1 M per kill | host setting |
| NPC jewel prices (Bless / Life / Creation / Guardian) | 9 M / 45 M / 36 M / 60 M | 3 M / 16 M / 15 M / 15 M | data |
| Guild: creation level, size cap, alliance size, opposite Gens | 100, no cap, 5 guilds, allowed | 150, 80, 3 guilds, not allowed | code / host settings |
| +12 / +14 chaos mix | 55 % / 50 % | 60 % / 55 % | data |
| Trading harmony and full-excellent items | allowed | not allowed | - |
| Duel damage reduction | only without SD (so never in Season 6) | 60 % for all duel hits | code (group 11) |
| Devil Square players | 10 | 15 | data |
| Happy hour | 4 h, EXP only | 8 h, EXP and drops | host setting |
| Extra vault pages | on | off | host setting |
| MU helper | from level 1 | from level 80 | host setting |

Equal: Jewel of Soul / Life / Harmony rates, refining, wing and Fenrir mixes, the 380 mix, EXP tables, maximum levels, sell divisor, per-class damage rates.

### Owner decisions on these settings (20 Nov 2026)

Change to the suites' value: party EXP bonus (160-220 %, set party 230-300 %); excellent drop 0.2 %; Jewel of Life up to +28;
items / zen on the ground 20 s / 15 s (Season 9 values); PK clear 1 M zen per kill; NPC jewel prices; Chaos Castle needs 2
players; duel damage 60 % for every hit; Devil Square 15 players; happy hour 8 h with EXP and drop bonus.

Change differently: guild size capped at 80 (creation level 100, 5-guild alliance and mixed Gens stay as HNMO); MU helper from
level 80, VIP accounts from level 1.

Keep HNMO: EXP rate 1x; luck on drops 25 %; event timetables; +12 / +14 mix rates; trading harmony and full-excellent items;
extra vault pages on.
