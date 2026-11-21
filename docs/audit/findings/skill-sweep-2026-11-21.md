# Skill sweep, 21 Nov 2026 (Task B)

How: `(local probe)` (new; fxprobe's per-call effect count plus the server's hits per target from
ObjectHitExtended, hero/target cells before and after, monsters within 3 cells of the target). Web client built from
`origin/claude/hnmo-main-handover-54co2m` in `HNMO-sweep`, served by my own bridge on 8085, against the demo server on
8095 (that process was started from the `HNMO-job9` worktree, branch `s9-stardust`, not from main). Lorencia fields,
headless Chromium. Raw rows: `(local probe)`, logs `(local probe)`,
summary script `(local probe)`.

Reading the columns:
- **Damage seen** is the server's health damage per hit on monsters (not the hero) during 3.5 s after the cast.
  The hit packet has no attacker, so hits by a summoned monster or another player nearby are counted too.
  **Expected** is: attack skill -> hits a target (area skills: up to the monsters near it); buff / summon / curse -> no
  damage. HNMO has no reference damage numbers per skill, so the size of the number was only checked for being in
  the same range as the class's other skills.
- **Effects** = effect objects the cast made (createEffect/Particle/Sprite/Joint calls above the idle rate).
  Arrows (shootArrow) are not counted, so bow skills show only the hit's blood (10).
- Positions: every skill was cast next to the target (distance 1, or the probe walked in first); "moved" is only the
  walk into range unless noted.

## Season 6

### Elf (testgmElf, bow 4:15 + arrows, Fenrir)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 24 Triple Shot | 1424 on 1 (1 near) / hit | 10 | OK | arrows not counted |
| 26 Heal | none / none | 131 | OK | |
| 27 Greater Defense | none / none | 136 | OK | |
| 28 Greater Damage | none / none | 131 | OK | |
| 30-36 Summon Goblin ... Soldier | none / none | 131 each | OK | all seven show the same magicGround2 + 60 healing joints; not compared with the desktop |
| 46 Starfall | 3054 on 1 / hit | 98 | OK | |
| 51 Ice Arrow | 2156 on 1 / hit | 10 | OK | one arrow (not counted) |
| 52 Penetration | 2314-2734 on 2 of 2 / hits all in line | 71 | OK | |
| 76 Plasma Storm | 2481-2530 on 3 / hits | 20 | OK | |
| 77 Infinity Arrow | none / none | 6 | OK | |
| 234 Recovery | none / none | 145 | OK | |
| 235 Multi-Shot | 1267 on 1 of 2 / hit | 44 | OK | |
| 708 Phoenix Arrow | 1149 on 1 / hit | 89 | OK | |
| 709 Frost Arrow Rain | 1245 on 1 / hit | 126 | OK | cast from 4 cells, aimed at the target's cell |
| 710 Forest Blessing | none / none | 271 | OK | |

### Dark Lord (testgmDl, sceptre 2:13, Dark Horse)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 60 Force | 18637 on 1 / hit | 22 | OK | walked 4 cells into range |
| 61 Fire Burst | 19101 on 1 / hit | 309 | OK | |
| 62 Earthshake | 17684-21275 on 3 of 3 / all near | 202 | OK | |
| 63 Summon | none / none | 74 | OK | |
| 64 Increase Critical Damage | none / none | 672 | OK | |
| 65 Electric Spike | 13566-13958 on 2 of 3 / hits | 14 | OK | |
| 72 Abolish Magic | **4510 on 1, killed it** / **none** | 0 | **Fixed #205** | direct hit skill without a damage type; the suites have Damage 0, isDamage 0 |
| 74 Fire Blast | 14974 on 1 / hit | 41 | OK | |
| 78 Fire Scream | 16897 on 1 / hit | 137 | OK | |
| 238 Chaotic Diseier | 22726 on 1 / hit | 807 | OK | |
| 716 Shadow Ravens | 13114 on 1 / hit | 45 | OK | |
| 717 War Command | none / none | 37 | OK | |
| 718 Earth Stomp | 14666 and 29332 (a double) on 2 of 2 / hits | 198 | OK | |

### Magic Gladiator (testgmMg, Explosion Blade 0:23, Fenrir, 26 energy)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 1 Poison, 2 Meteorite, 3 Lightning, 4 Fire Ball, 5 Flame, 7 Ice, 8 Twister, 9 Evil Spirit, 10 Hellfire, 11 Power Wave, 12 Aqua Beam, 13 Cometfall, 14 Inferno | **none** / none (energy too low: 40-724 needed) | 9-1757 (cast ~8 times each) | **Fixed #206** | the web game sent and drew each cast and auto attack repeated it; the desktop's CanExecuteSkill drops it (AreSkillAttributeRequirementsMet), the server refuses it |
| 17 Energy Ball | 222-250, 3 hits on 1 / hit | 193 | OK | |
| 41 Twisting Slash | 2432 on 1 of 3 / hit | 753 | OK | |
| 47 Impale | 1952 on 1 / **desktop: no cast without a spear and a mount** | 72 | **Found** | the web plan says equipmentOk=false, but Combat only enforces equipmentOk for the Rage Fighter; the server doesn't check the spear either |
| 55 Fire Slash | 2412 on 1 / hit | 301 | OK | |
| 56 Power Slash | 1776 on 1 / **desktop: not while riding; needs 100 energy** | 715 | **Found** | same equipmentOk gap (riding a Fenrir); server has no energy requirement for it (the suites: ReqEnergy 100). After #206 the web refuses it below 100 energy |
| 57 Spiral Slash | 2442 on 1 / hit | 130 | OK | |
| 73 Mana Rays | 1280 on 1 / hit? | 53 | **Found (unclear)** | web plan (UseSkillWizard AT_SKILL_DEATH_CANNON) wants a staff or book: equipmentOk=false with a sword; the server hits. Which one is right for the MG needs the desktop/suite rule |
| 76 Plasma Storm | 2169 on 1 / hit | 20 | OK | |
| 236 Flame Strike | 3258 on 1 / hit | 32 | OK | |
| 237 Gigantic Storm | none / none (needs 1058 energy) | 0 | OK after #206 | walked into range, never cast in the watch |
| 712 Chain Lightning Blade | 2266 on 1 / hit | 49 | OK | |
| 713 Flame Whirl | 2230-2554 on 2 of 2 / hits | 195 | OK | |
| 714 Twin Power | none / none | 37 | OK | |

### Summoner (testgm2Sum, a summoned Devil was out)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 2 Meteorite | 1570 on 1 / hit | 90 | OK | |
| 4 Fire Ball | 1850 on 1 / hit | 31 | OK | |
| 7 Ice | 6552 on 1 / hit | 25 | OK | |
| 11 Power Wave | 3260 on 1 / hit | 106 | OK | |
| 45 Lance | 3822 on 1 / hit | 42 | OK | |
| 76 Plasma Storm | 13916 on 1 / hit | 17 | OK | high next to the others; Fenrir damage, not checked further |
| 214 Drain Life | 2854 on 1 / hit | 1 | OK | one aliceDrainLife effect |
| 215 Chain Lightning | 2376 and 3614 on the same target / up to 3 targets | 190 | **Found (unclear)** | only one monster near; the server sent the chain twice (two AreaSkillAnimation, two hits on one target). Classic rule with fewer targets not checked |
| 217 Damage Reflection | none / none | 3178 | OK | big buff effect (1980 shiny2 sprites) |
| 218 Berserker | none / none | 3224 | OK | |
| 219 Sleep | 2925 on 1 / none | 26 | Not a skill bug | the hit is from the summoned Devil (the hit packet has no attacker); the server's Buff branch only applies the effect |
| 221 Weakness | 5026 on 1 / none | 107 | Not a skill bug | same: AreaSkillAttackAction Buff -> ApplyMagicEffect only |
| 222 Innovation | 5222 on 1 / none | 105 | Not a skill bug | same |
| 225 Pollution | none / hit | 0 | **Found** | the web client has no cast plan for the book summons 223-225 (skillCastPlan returns null: not castable). The server has PollutionSkillPlugIn. Missing feature, not a quick fix |
| 230 Lightning Shock | 1985 on 1 / hit | 303 | OK | |
| 720 Poison Soul | 3288 on 1 / hit | 360 | OK | |
| 721 Little Demon | 191 on 1 / summon | 108 | OK | the hit is likely the demon's |
| 722 Weakening Curse | 0 (miss) and 180 on 2 / curse | 155 | OK | the hits are the summon's |

### Rage Fighter (testgm2Rf, glove 0:35; helper taken off for the casts with UNMOUNT=1)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 76 Plasma Storm | - / not without a Fenrir | 0 | OK | unmounted: no cast (the 1352 hit counted was not this cast's) |
| 262 Chain Drive | 3150 on 1 / hit | 73 | OK | |
| 263 Dark Side | 6319-7100 on 2 of 2 / hits | 25 | OK | rerun in Noria (Lorencia was empty) |
| 264 Dragon Roar | 3092 on 1 / hit | 203 | OK | rerun in Noria |
| 265 Dragon Slasher | 8917 on 1 / hit | 190 | OK | rerun in Noria |
| 269 Charge | 3530 on 1 / hit | 37 | OK | rerun in Noria; walked 3 cells into range |
| 270 Phoenix Shot | 3052 on 1 / hit | 262 | OK | rerun in Noria |
| 266 Ignore Defense, 267 Increase Health, 268 Increase Block | none / none | 110 / 66 / 25 | OK | |
| 724 Dragon Fist | 3475 on 1 / hit | 36 | OK | rerun in Noria |
| 725 Whirl Kicks | 4330 on 1 / hit | 48 | OK | rerun in Noria |
| 726 Iron Body | none / none | 31 | OK | |

Dark Knight and Dark Wizard: Season 6 damage was already filled by the 20 Nov sweep (skillsweep-s6-1/-2,
fxb-dk/dw); not run again.

## Season 9 (world switched with s9world.mjs; Noria fields, since Lorencia's fields were empty)

The client served on 8085 for all Season 9 runs (and the Season 6 Summoner and Rage Fighter runs) was the first commit of #206: skills without their stat requirements are not cast (rows with castable=false); the Blood Slash diagnostic used #213's build.

### Dark Knight (testgmDk, two swords 0:19/0:22, Fenrir)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 22 Cyclone | 2373 on 1 of 1 near / hit | 31 | OK | |
| 41 Twisting Slash | 2373 on 1 of 1 near / hit | 769 | OK | |
| 42 Rageful Blow | 2319 on 1 of 1 near / hit | 109 | OK | |
| 43 Death Stab | 2685 on 1 of 2 near / hit | 185 | OK | |
| 44 Crescent Moon Slash | 2826 on 1 of 1 near / hit | 129 | OK | |
| 47 Impale | 2094 on 1 of 1 near / hit | 38 | **Found** | cast and hit without a spear (equipmentOk false not enforced outside the Rage Fighter) |
| 48 Swell Life | none / none | 481 | OK | |
| 76 Plasma Storm | 2151 on 1 of 1 near / hit | 20 | OK | |
| 232 Strike of Destruction | 2930 on 1 of 1 near / hit | 278 | OK | |
| 700 Earth Splitter | 2502 on 1 of 2 near / hit | 61 | OK | |
| 701 Blood Slash | none / hit | 78 | **Fixed #213** | sent as TargetedSkill; the server has it as an explicit-target area skill, so it never hit |
| 702 Iron Guard | none / none | 33 | OK | |

### Dark Wizard (testgmDw, staff 5:9 + book 6:15)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 1 Poison | 1383 on 1 of 1 near / hit | 21 | OK | |
| 2 Meteorite | 847 on 1 of 2 near / hit | 39 | OK | |
| 3 Lightning | 1442 on 1 of 1 near / hit | 99 | OK | |
| 4 Fire Ball | 1093 on 1 of 1 near / hit | 71 | OK | |
| 5 Flame | 1490 on 1 of 1 near / hit | 387 | OK | |
| 6 Teleport | none / none | 0 | Not checked | teleport needs a ground click; the probe has none |
| 7 Ice | 1120 on 1 of 1 near / hit | 40 | OK | |
| 8 Twister | 1554 on 1 of 4 near / hit | 139 | OK | |
| 9 Evil Spirit | 870-1615 on 3 of 3 near / hit | 218 | OK | |
| 10 Hellfire | 1682-2492 on 2 of 2 near / hit | 20 | OK | |
| 11 Power Wave | 756 on 1 of 1 near / hit | 91 | OK | |
| 12 Aqua Beam | 1717 on 1 of 1 near / hit | 391 | OK | |
| 13 Cometfall | 2108 on 1 of 2 near / hit | 58 | OK | |
| 14 Inferno | 1961 on 1 of 1 near / hit | 207 | OK | |
| 15 Teleport Ally | none / none | 0 | Not checked | needs a party member |
| 16 Soul Barrier | none / none | 840 | OK | |
| 17 Energy Ball | 797 on 1 of 1 near / hit | 51 | OK | |
| 38 Decay | 1301-1922 on 2 of 2 near / hit | 155 | OK | |
| 39 Ice Storm | 1349 on 1 of 1 near / hit | 427 | OK | |
| 40 Nova | 369 on 1 of 1 near / hit | 1084 | OK | released at once (charge 0), so a small hit |
| 45 Lance | 1885 on 1 of 1 near / hit | 33 | OK | |
| 76 Plasma Storm | 2510-2567 on 2 of 1 near / hit | 20 | OK | |
| 233 Expansion of Wizardry | none / none | 3355 | OK | |
| 704 Ice Dragon | 1898 on 1 of 1 near / hit | 74 | OK | |
| 705 Meteor Rain | none (no monster) / hit | 354 | Not checked | no monster in view |
| 706 Thunder Cage | none (no monster) / hit | 2 | OK | |

### Elf (testgmElf)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 24 Triple Shot | 1164 on 1 of 1 near / hit | 10 | OK | |
| 26 Heal | none / none | 131 | OK | |
| 27 Greater Defense | none / none | 136 | OK | |
| 28 Greater Damage | none / none | 131 | OK | |
| 30 Summon Goblin | none / none | 131 | OK | |
| 31 Summon Stone Golem | none / none | 131 | OK | |
| 32 Summon Assassin | none / none | 131 | OK | |
| 33 Summon Elite Yeti | none / none | 0 | OK | |
| 34 Summon Dark Knight | none / none | 0 | OK | |
| 35 Summon Bali | none / none | 0 | OK | |
| 36 Summon Soldier | none / none | 0 | OK | |
| 46 Starfall | 2612 on 1 of 3 near / hit | 98 | OK | |
| 51 Ice Arrow | 2224 on 1 of 2 near / hit | 10 | OK | |
| 52 Penetration | 2682 on 1 of 1 near / hit | 71 | OK | |
| 76 Plasma Storm | 2501 on 1 of 1 near / hit | 20 | OK | |
| 77 Infinity Arrow | none / none | 6 | OK | |
| 234 Recovery | none / none | 0 | OK (#206) | refused: the desktop formula wants 168 energy, the Elf has 120; the server asks only 37 (Found: server/desktop requirement differ) |
| 235 Multi-Shot | 1162 on 1 of 2 near / hit | 44 | OK | |
| 708 Phoenix Arrow | 1019 on 1 of 1 near / hit | 85 | OK | |
| 709 Frost Arrow Rain | 1025 on 1 of 2 near / hit | 123 | OK | |
| 710 Forest Blessing | none / none | 271 | OK | |

### Dark Lord (testgmDl)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 60 Force | 31034 on 1 of 1 near / hit | 23 | OK | |
| 61 Fire Burst | 14561 on 1 of 1 near / hit | 309 | OK | |
| 62 Earthshake | 20705-21275 on 2 of 1 near / hit | 196 | OK | |
| 63 Summon | none / none | 80 | OK | |
| 64 Increase Critical Damage | none / none | 672 | OK | |
| 65 Electric Spike | 16602 on 1 of 1 near / hit | 15 | OK | |
| 72 Abolish Magic | 4217 on 1 of 1 near / none | 0 | **Fixed #205** | hit for the lord's damage again (as in Season 6) |
| 74 Fire Blast | 13328 on 1 of 1 near / hit | 41 | OK | |
| 78 Fire Scream | 16897 on 1 of 1 near / hit | 136 | OK | |
| 238 Chaotic Diseier | 11672 on 1 of 1 near / hit | 799 | OK | |
| 716 Shadow Ravens | 14437 on 1 of 1 near / hit | 61 | OK | |
| 717 War Command | none / none | 37 | OK | |
| 718 Earth Stomp | 17443 on 1 of 1 near / hit | 221 | OK | |

### Magic Gladiator (testgmMg, 26 energy; client build with #206)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 1 Poison | 1032 on 1 of 2 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 2 Meteorite | 1032 on 1 of 2 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 3 Lightning | 858 on 1 of 1 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 4 Fire Ball | 1032 on 1 of 1 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 5 Flame | 1008 on 1 of 1 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 7 Ice | 1032 on 1 of 2 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 8 Twister | 873 on 1 of 1 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 9 Evil Spirit | 833 on 1 of 1 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 10 Hellfire | 890 on 1 of 1 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 11 Power Wave | 973 on 1 of 1 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 12 Aqua Beam | 1238 on 1 of 1 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 13 Cometfall | 846 on 1 of 1 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 14 Inferno | 828 on 1 of 1 near / hit | 0 | OK (#206) | not cast (26 energy); the hit counted is a plain attack the refused cast fell back to, removed by #206's second commit |
| 17 Energy Ball | 324 on 1 of 2 near / hit | 51 | OK | |
| 41 Twisting Slash | 2054-2064 on 2 of 1 near / hit | 784 | OK | |
| 47 Impale | 1878 on 1 of 1 near / hit | 38 | **Found** | cast and hit without a spear (equipmentOk false not enforced outside the Rage Fighter) |
| 55 Fire Slash | 2460 on 1 of 1 near / hit | 302 | OK | |
| 56 Power Slash | 968 on 1 of 1 near / none | 0 | **Found** | same equipmentOk gap (riding); after #206 refused below 100 energy as on the desktop |
| 57 Spiral Slash | 2440 on 1 of 1 near / hit | 130 | OK | |
| 73 Mana Rays | 966 on 1 of 1 near / hit | 53 | **Found (unclear)** | web plan wants a staff/book; the server hits |
| 76 Plasma Storm | 2114 on 1 of 1 near / hit | 20 | OK | |
| 236 Flame Strike | 2354 on 1 of 1 near / hit | 11 | OK | |
| 237 Gigantic Storm | 1032 on 1 of 1 near / hit | 0 | OK (#206) | same as the spells |
| 712 Chain Lightning Blade | 2234 on 1 of 1 near / hit | 38 | OK | |
| 713 Flame Whirl | 2288 on 1 of 1 near / hit | 237 | OK | |
| 714 Twin Power | none / none | 37 | OK | |

### Summoner (testgm2Sum, summon out)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 2 Meteorite | 3342 on 1 of 1 near / hit | 43 | OK | |
| 4 Fire Ball | 2717 on 1 of 1 near / hit | 71 | OK | |
| 7 Ice | 2729 on 1 of 1 near / hit | 34 | OK | |
| 11 Power Wave | 2514 on 1 of 1 near / hit | 91 | OK | |
| 45 Lance | 2671 on 1 of 1 near / hit | 45 | OK | |
| 76 Plasma Storm | 8401 on 1 of 1 near / hit | 20 | OK | |
| 214 Drain Life | 2876 on 1 of 1 near / hit | 11 | OK | |
| 215 Chain Lightning | 2587-3082 on 2 of 1 near / hit | 1 | **Found (unclear)** | 2 hits on 2 targets but only the base chainLightning effect, no thunder joints (Season 6: 56) |
| 217 Damage Reflection | none / none | 3426 | OK | |
| 218 Berserker | none / none | 3219 | OK | |
| 219 Sleep | 2511 on 1 of 1 near / none | 16 | Not a skill bug | the hit is the summoned monster's (Buff branch only applies the effect) |
| 221 Weakness | 3988 on 1 of 1 near / none | 106 | Not a skill bug | same |
| 222 Innovation | 1889 on 1 of 1 near / none | 106 | Not a skill bug | same |
| 225 Pollution | none / hit | 0 | **Found** | no cast plan in the web client for the book summons 223-225 |
| 230 Lightning Shock | 4610-11062 on 3 of 2 near / hit | 258 | OK | |
| 720 Poison Soul | 5286 on 1 of 1 near / hit | 397 | OK | |
| 721 Little Demon | none / none | 86 | OK | |
| 722 Weakening Curse | 0-191 on 2 of 1 near / none | 272 | OK | the hits are the summon's |

### Rage Fighter (testgm2Rf, unmounted for the casts)

| Skill | Damage seen vs expected | Effects | Result | Notes |
|---|---|---|---|---|
| 76 Plasma Storm | 1248 on 1 of 1 near / hit | 0 | OK | no cast without a Fenrir (hit counted is not this cast's) |
| 262 Chain Drive | 4055 on 1 of 1 near / hit | 64 | OK | |
| 263 Dark Side | 6215 on 1 of 1 near / hit | 25 | OK | |
| 264 Dragon Roar | 3365-4050 on 2 of 2 near / hit | 157 | OK | |
| 265 Dragon Slasher | 10095 on 1 of 2 near / hit | 436 | OK | |
| 266 Ignore Defense | none / none | 111 | OK | |
| 267 Increase Health | none / none | 66 | OK | |
| 268 Increase Block | none / none | 25 | OK | |
| 269 Charge | 4328 on 1 of 2 near / hit | 37 | OK | |
| 270 Phoenix Shot | 4465 on 1 of 1 near / hit | 264 | OK | |
| 724 Dragon Fist | 4020 on 1 of 1 near / hit | 41 | OK | |
| 725 Whirl Kicks | 3522 on 1 of 1 near / hit | 50 | OK | |
| 726 Iron Body | none / none | 31 | OK | |

Season 6 Dark Knight, added: Blood Slash (701) also did no damage in the 20 Nov Season 6 run (fxb-dk: onSkill 701 twice,
damage 0) -> **Fixed #213** for both seasons.

## Summary

### Draft PRs (base claude/hnmo-main-handover-54co2m)

| PR | Bug | Test |
|---|---|---|
| #205 Abolish Magic does no damage | DL skill 72 hit for 4,000-4,500 (direct hit skill without a damage type; the suites: Damage 0, isDamage 0) | AbolishMagicNoDamageTest (init tests): red 2/3 -> green; project 898 passed |
| #206 Skills: no cast without the skill's stat requirements | web client cast skills the hero lacks the stats for (MG with 26 energy: 13 wizard spells drawn and repeated, no hit); after the first fix the refused cast fell back to plain attacks | skill-requirements.spec.ts: red -> green, twice (one per commit) |
| #213 Blood Slash and Soul Drain: send them as area skills at the target | 701/723 sent as TargetedSkill, server has them as explicit-target area skills: never hit | blood-slash-hits.spec.ts: red (17 TargetedSkill, no hit) -> green |

### Found, not fixed (for main)

1. **equipmentOk is only enforced for the Rage Fighter** (web client `Combat.updateSkill`). Impale (47) is cast and hits
   without a spear (DK with swords, MG with a sword), Power Slash (56) while riding a Fenrir. The desktop drops these;
   the server doesn't check the spear either. Enforcing it for all classes touches every class's casting: wants its own
   change and a look at each plan's equipmentOk rule first.
2. **Mana Rays (73)**: the web plan (AT_SKILL_DEATH_CANNON) wants a staff or book, the server hits with the MG's sword.
   One of them is wrong; needs the desktop/suite rule.
3. **Book summons 223-225 (Explosion, Requiem, Pollution)**: no cast plan in the web client (`skillCastPlan` returns
   null), so a Summoner can't cast Pollution at all. The server has PollutionSkillPlugIn. Missing feature.
4. **Energy requirements differ between server and desktop formula**: Recovery (234) server 37 vs desktop 168; Summon
   Elite Yeti server 230 vs desktop 130 (and the other elf summons); Power Slash: server none vs desktop/suite 100. The
   web client follows the desktop (since #206); the server lets some through and refuses others.
5. **Web client stats have no item bonuses** (`stats.energy` etc. are base stats, no AddEnergy). The skill bar's grey
   icons and #206's check use them, so a hero whose items add energy can be refused a skill the server allows.
6. **Chain Lightning (215)**: Season 6, one monster near: the server sent the chain twice (two AreaSkillAnimation, two
   hits on the same target). Season 9, two monsters: hits on both but only the base `chainLightning` effect, no thunder
   joints (Season 6 drew 56). Classic rule for the jumps not checked; effect difference not explained.
7. **Expansion of Wizardry (233)** and other self buffs walk towards the selected monster when it is out of range
   (DW walked 5 cells). Probably the probe passing the monster as the target; not compared with the desktop.
8. **Monsters were gone from Lorencia's fields** on the demo server from about 07:40 to past 10:00 (no living monster at
   any of nine waypoints, in Season 6 and then Season 9), and Noria emptied after two classes; they came back later.
   Could be the sweeps themselves (GM one-hit kills) with a long respawn, or something else on that server.
9. The sweep's hit counter can't tell who hit (ObjectHitExtended has no attacker): Summoner curses (Sleep, Weakness,
   Innovation, Weakening Curse) showed hits that are the summoned monster's. Server code applies only the effect for
   Buff skills (AreaSkillAttackAction), so no bug there.

### Not checked / couldn't run

- (Rage Fighter Season 6 attack skills: done in a rerun in Noria after the world was back on Season 6, all OK.)
- Teleport (6) and Teleport Ally (15): the probe has no ground click / party member.
- Soul Drain (723) wasn't cast live (testgm2's Summoner doesn't know it).
- Damage size: no reference numbers per skill in HNMO; only "hits / doesn't hit" and the same range as the class's other
  skills were checked.
- The demo server on 8095 is another session's build (HNMO-job9, branch s9-stardust), not main; #205 was not checked in
  game after the change (unit tests only).
- Positions: no skill moved the hero or the target except the walk into range; Charge/Dark Side dashes were not
  measured beyond that.

### World

Switched the demo world to Season 9 with `s9world.mjs` (global for that server) for the Season 9 sweep and **restored it
to Season 6** afterwards (badge "S9" -> "S6.3", log (local probe)).
