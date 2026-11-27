# Unit Audit: `client/web/src/game (files 5)`

- **Unit**: `client/web/src/game (files 5)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `12` (`3,201` lines): `questData.ts`, `quests.ts`, `resets.ts`, `skillCast.ts`, `skillTooltip.ts`, `skills.ts`, `social.ts`, `sparkleGoal.ts`, `spawnEffects.ts`, `stats.ts`, `textFormat.ts`, `warps.ts`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Desktop / Server / Suite Reference | Match |
|---|---|---|---|---|
| Warp level discount & map gear checks (`M` / `/move`) | `warps.ts:10-56` | Reads `MoveReq_eng.bmd` (`84` B xor); `warpLevel`: `[3, 4, 6]` (MG/DL/RF) need `floor(level * 2 / 3)` except level `400`; `canWarp` enforces `outlaw` ban, Icarus flight eq (`wings`, `Dinorant`, `Fenrir`, bans `Uniria`), Atlans bans `Uniria`/`Dinorant` | Desktop `CMoveCommandWindow::SettingCanMoveMap` & `warps-and-npc-dialogs-s6-s9.md` | Same (note `main.ts:1887` caller passes `outlaw: false`) |
| Quest log packet state & dark/legacy NPC rules | `quests.ts:47-141` | Handles `0xf6` quest state packets (`Etc`/`Progress`), class index mapping, `QUEST_COMBO = 3`, legacy kill/item state tracking | Desktop `CSQuest` / `CQuestMng` | Same |
| Reset / Relife / Delegated AutoReset packet state | `resets.ts:13-101` | Decodes `HnmoResetsState` (`C2 F5 1E`) and dispatches `HnmoResetsAction` (`Reset=1, Relife=2, DelegatedOn=3, DelegatedOff=4, Info=5`); formats VIP & daily counts | `server/src/GameLogic/Resets` (`unit-server-src-GameLogic-Resets.md`) | HNMO custom |
| Skill cast routing, equipment reqs & Nova / Rage attacks | `skillCast.ts:156-288+` | Routes `Teleport` (`EnterGateRequest`), `TeleportAlly`, `NovaBegin/Release` (`58->40`), `RageAttack` (`C3 4A` + hit frames), `DarkSide` (`C1 4B`), Chaos Castle / Stadium auto-attack override, `rotationScale` `256` vs `255` | Desktop `ZzzInterface` (`AttackWizard`, `AttackKnight`, `MonkSystem`) | Same |
| Skill tooltip combo/siege/level/stat req formatting | `skillTooltip.ts:14-51+` | Marks `COMBO_SKILLS = [19..23, 41..43]` for BK level `>= 220`, `CASTLE_SIEGE_ONLY` skills `[44..46, 56, 73, 74, 269]`, displays stat/level requirements from `skill_eng.bmd` | Desktop `CNewUISkillList` / `SkillAttribute` | Same |
| Skill BMD loader & SkillUseType enum | `skills.ts:12-65` | `RECORD = 108` (`MAX_SKILLS = 650`) xor `0xFC/CF/AB`; `SkillUseType` `NONE=0..MASTERACTIVE=4` | Desktop `SkillManager` | Same |
| Friend system minimum level & chat command guards | `social.ts:24-25` | `FRIEND_LEVEL = 6`, `MAX_NAME = 10` | Server `FriendServer` & Desktop `FriendWindow` | Same |
| Character stats sync & EXP next-level progress | `stats.ts:28-111` | Updates `level`, `levelUpPoints`, `experience`, `nextExperience = experienceForLevel(level + 1)` from `CurrentStatsExtended`, `CharacterLevelUpdateExtended`, `ExperienceGainedExtended` | Server `ExperienceTable` & Desktop `WSclient.cpp` | Same |
| Binary quest loader, text helpers & visuals | `questData.ts`, `sparkleGoal.ts`, `spawnEffects.ts`, `textFormat.ts` | BUX quest script reader, onboarding target highlights, spawn FX & string formatters | Data / visual presentation | No game rules |

## Re-check 2026-11-27 (changes 8cb8539b..a108e846)

Commits read (every changed file read in full):

- 98f231d4 fix(client): fix buff value time assignment, outlaw warp check, and extended bag space check

Evidence:

- stats.ts: hero state from CharacterInformation and from HeroStateChanged (F3 08) for the own id only (0x7FFF mask); outlaw = state >= 5 (PK1) - same as the server (PlayerKiller1stStage) and the desktop move window.
