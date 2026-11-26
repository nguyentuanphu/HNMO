# Plan for the owner's answers Q1-Q13 (26 Nov 2026, 14:40 Sydney)

One draft PR per area (handover rule 4). Status checked in the code on 26 Nov.

| Area | Answers | Status |
|---|---|---|
| Gens | Q8 | Draft PR #386: same gens for guild/alliance by default, normal chat hidden from the opposite gens. Missing: PK pays 2x warp Zen. |
| Quest NPC, personal store | Q7 (quest NPC), Q12 | Already in the code: `QuestCompletionAction` needs the quest giver's dialog open, `OpenStoreAction.MinimumLevel = 6`. No PR. |
| Quests and master skills | Q1, Q3 (Q2 and Q4, Q5, Q6b, Q7 VIP, Q9: keep, no code) | Not started. Q1: master nodes 314-324 of every class with their rank / max / parents / values are in `docs/audit/data/diff-master-skills.txt` (e.g. 1:314 Cast Invincibility, 315 Armor Set Bonus Increase, 316 Vengeance, 317-320 stat increases, 321-324 wing power-ups); each needs its effect in `SkillsInitializer.cs`, an update plug-in for existing saves and the client tree. Q3: Ep1/Ep2 steps and Ep19 rewards in `docs/audit/data/diff-quests.txt`. |
| Bless potion | Q10 | Not started. The x1.2 is on the general `AttackDamageIncrease` (`BlessPotionEffectInitializer`); restricting it to Castle Siege gates and statues needs its own stat read in `AttackableExtensions` and an update plug-in. |
| Chaos Card mixes | Q11 | Not started (20 recipes S6, 14 S9 at Chaos Card Master #375). |
| Events | Q6 | Not started: BonusEvent, XMasAttack, LorenDeep, RingAttack, LastManStanding, MiningSystem, MuRummy, Delgado Lucky Coin, Cherry Blossom. Split into a few PRs by kind (timed events, invasions, exchanges, mini-games). |
| Season 9 data set | Q13 | Not started: three PRs (items, events, combat and progression systems). |

Each PR: tests of what it touches, then GitHub runs the suites; main runs the full suites once per merge batch.
