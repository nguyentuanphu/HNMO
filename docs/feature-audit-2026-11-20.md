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
