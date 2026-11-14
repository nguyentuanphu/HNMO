# HNMO web game: the plan to full parity with the desktop client

Rule for every step: port from the desktop client's code and data (`client/src/source`,
`client/src/bin/Data`), show windows with the game's own RML documents (`src/ui/rml.ts`), strict
TypeScript, a browser test that plays the feature against a real server, update `PARITY.md`,
commit with an HNMO-only message. Owner-approved HNMO extras are marked **(extra)**.

## Done

Login, server and character select, create character; 36 maps; walking with pathfinding; other
players, monsters and NPCs; chat and `/move`; equipment, wings and item glow; inventory and vault
windows (the game's RML), moving and equipping items; item tooltips; HUD (life, mana, SD, AG,
experience); fighting (attack animations per weapon, ranges, auto attack, damage numbers, health
bar, monster attack data, arrows, death effects, stances); drops, ground labels, picking up and
dropping items; potions on Q W E R and right-click use of items (3.1); the (i) guide (10.1); browser
tests and CI for all of it; strict TypeScript build.

## 1. Effects and sound
1. 🟡 Particle, sprite and effect system (core done; more effect types with each skill; weapon trails, terrain light) (the desktop's `ZzzEffect*`, `CreateEffect`, `CreateSprite`,
   `CreateParticle`, joints/trails, lights) — needed by skills, items and maps.
2. 🟡 Sounds and music (wired: music, ambience, footsteps, combat, monsters, potions, skills; map object and event sounds, volume sliders left): the files are in `client/src/bin/Data/Sound` (808; 103 were damaged in the
   archive, listed in `Sound/MISSING.txt`) and `Data/Music` (47); port the desktop client's sound
   playback (`Audio/`, `Data/Monsters/Sounds.json`, map music) with the Web Audio API.

## 2. Skills and buffs
1. 🟡 Skill list from the server (`SkillListUpdate`, `SkillAdded/Removed`), the HUD skill bar,
   hotkeys 1-9/0, the skill list popup, the skill tooltip (range, mana, AG, cooldown,
   requirements, notes) (done). Cooldowns (done: CheckSkillDelay, the wipes on the bar and the list). Left: the tooltip's damage lines (with the character's damage,
   4.1), the buff/party/map checks of grey icons. Master skill icons (done: the master atlas on the bar and the list).
2. 🟡 Casting: targeted (`TargetedSkill`), area (`AreaSkill`), the skill animations (done for the
   classes' skills except the Rage Fighter's and master skills) and effects per skill (first skills
   done), skill arrows and multi-shot fans. (The desktop never sends `AreaSkillHit`; the server hits.)
3. 🟡 Buffs and debuffs (`MagicEffectStatus`), the buff strip (`buff_strip.rml`) (done), their visual effects.
4. 🟡 Master level and skill tree (`master_level.rml`, `ui/masterLevelWindow.ts`, `game/masterLevel.ts`): done: the master
   level, experience and points (F3 50/51, master experience), the HUD's experience bar past level 400, A and the
   character window's button, the class's tree from `MasterSkillTreeData.bmd` with its columns, ranks, icons, arrows,
   levels and hints from `MasterSkillTooltip_eng.bmd`, the desktop's checks and question before `AddMasterSkillPoint`,
   the answer (F3 52) updating the tree, the skill list and the HUD (test `master.spec.ts`). Left: master level-up
   flare effect, the master skills' effects on the character window's numbers, casting the strengthened skills.

## 3. Using items
1. ✅ Potions on Q W E R (`ConsumeItemRequest`), right-click to use. Left: the dialogs of fruits,
   reset fruits, event tickets and talismans; the elixir check against the buffs (with 2.3).
2. 🟡 Jewels on items (done: dropped on a bag item; the server refuses worn items), fruits, scrolls/skill books, town portal, pets.
3. Repair (inventory button and NPCs), durability tints and the durability window.

## 4. Character
1. ✅ Character window C (`character_info.rml`): stats, adding level-up points (buffs, set/socket options and master level count once those exist).
2. Running after walking a while (`canRun`, run delays), swimming maps.
3. 🟡 Pets and mounts drawn (done: Uniria, Dinorant, Dark Horse, Fenrir in its colours ridden; Guardian Angel, Imp, Dark Raven and the pet.bmd pets following; test `pets.spec.ts`). Left: pet attacks on command, zen pick-up by the collector pets, the effects the effect system lacks (see PARITY), capes as cloth.

## 5. NPCs, shops and quests
1. 🟡 NPC dialogs (`npc_dialogue.rml`, done), shops (`npc_shop.rml`): buy, sell, repair (done; tax rate, gamble shops left).
2. 🟡 Quests (`npc_quest.rml`, `my_quest_info.rml`, `quest_progress.rml`, done; test `quests.spec.ts`); left: the class change
   rewards' new class look (A3 handled otherwise), the condition item icons, tutorial client actions, the gens parts of the dialogue.
3. 🟡 Move window M (done) / gates by walking (done) / warps (`move_command.rml`, `gateman.rml`), mini map (done, Tab), map name banner (done).

## 6. Trading and crafting
🟡 Trade (`trade.rml`, done), personal store (`my_shop`, `purchase_shop`, done; left: prices in other currencies, the command window's Purchase), chaos machine (`mix_inventory.rml`,
done for the Chaos Goblin, the refinery, restore, chaos card and cherry blossom NPCs; left: the mix sparkle, the method
menu as RML, the pet trainer, Elpis, seed master / researcher menus and the socket list, the 380 option lines, the
harmony restore price), Jewel of Harmony, lucky items, united market.

## 7. Social
🟡 Party (`party_list.rml`, `party_info.rml`, done), guild (`guild_info.rml`, `guild_make.rml`, done: create,
join, members and positions, leave / disband / release, alliances, tags over heads), friends, messenger
and letters (`friend_shell.rml`, `letter_read.rml`, `letter_write.rml`, done), whisper / party / guild
chat (done). Left: friend chat rooms (the chat server), letter portraits, the guild mark on the chest,
guild wars and Battle Soccer.
🟡 Duels (`game/duel.ts`, `ui/duelWindows.ts`): the challenge (`/DuelChallenge`), the question over the duel art, the score
window, the end and the result, Doorkeeper Titus and the spectator HUD done; the command window's Duel button and the
duellists' capes next.
🟡 Gens (`game/gens.ts`, `ui/gensWindow.ts`): joining and leaving at the stewards, the reward answers, the Gens Info window (B),
contribution, the family mark on the pointed player done; the marks in every player's name box and the strife-map rules next.

## 8. Auto play
1. ✅ The desktop helper (`mu_helper_*.rml`) — core loop, bar, start/stop, settings windows, buffs, heal, Drain Life, party support and the Dark Raven done (the master skill variants and the Rage Fighter's buffs wait for their casting): hunting range, main and 2 activation skills with
   conditions, combo, counter-attacks, potions and heal, buffs and party support, Dark Spirit,
   pick-up rules and item list, repair, auto-accept, the helper bar and its zen cost.
2. Offline leveling (the server already does it).
3. ✅ **(extra)** back to town when the bag is full or potions run out, buy potions, return.
4. ✅ **(extra)** sell junk to NPCs; **(extra)** stop timer; **(extra)** alerts on death / full bag.

## 9. Events
1. 🟡 Blood Castle, Devil Square, Chaos Castle (`game/events.ts`, `ui/eventWindows.ts`): entry windows,
   tickets, refusals, the time window, countdowns, terrain changes, results, NPC lines done; the gate and
   bridge animations, the weapon on its carrier, the rush pose, free tickets and on-screen golden notices next.
2. 🟡 Illusion Temple, Doppelganger, Imperial Guardian (`game/instanceEvents.ts`, `ui/instanceEventWindows.ts`):
   entry windows, tickets, the temple's HUD (time, mini map, points, score, tutorial, skills with Shift + right
   click), the sacred item's progress box, the temple's result and reward, the Doppelganger frame, the Imperial
   Guardian timer and boxes done; the temple skills' effects, the sacred item's model, team outfits and the
   Imperial Guardian's weather next.
3. Kalima, Crywolf, Castle Siege, Kanturu, Raklion, lucky coins, golden bowman — enter windows, timers,
   scores, rewards.

## 10. Shell and interface
1. ✅ **(extra)** The (i) button: an in-game guide of what to play and what each feature does
   (`public/guide.html`; keep its feature table up to date with each step).
2. ✅ Cash shop with the free coins (`in_game_shop.rml`, `igs_buy_package.rml`, `igs_buy_select.rml`, done; left: event packages, the buff warning on use, `igs_send_gift.rml` as the game's own window).
3. Login, server and character select as the game's own RML scenes; the option window's other rows
   (auto attack, whisper sound, theme, the Enhanced look, shadows), system menu, help, command
   window, translations, themes and UI scale.
4. Shadows, weather, animated map objects, water, BMD v14 and GLB models.
5. ✅ Option window (`O` or ⚙, `ui/settingsWindow.ts`): FPS limit, sound and music volume,
   windowed mode, effects off, FPS counter; **(extra)** render scale, text size, mute, view
   distance and a low-spec mode for slow PCs (`render/quality.ts`).

## 11. Release
Windows build tried on a real PC, performance on a normal laptop, the friends guide updated with
each release.

## Tonight (2026-11-13/14, Sydney time)

| Time | Work |
|---|---|
| 23:30–00:30 | ✅ 10.1 the (i) guide; ✅ 3.1 potions on Q W E R and right-click use |
| 00:30–01:30 | ✅ 2.1 skill list, skill bar, hotkeys and tooltip (cooldown wipes come with casting) |
| 01:30–02:30 | 🟡 1.1 effect system basics; 2.2 casting with skill animations and first effects |
| 02:30–03:30 | ✅ 4.1 character window and stat points; ✅ 5.1 NPC shops (buy, sell, repair) |
| 03:30–04:30 | 🟡 8.1 auto play core (hunt, potions, pick up) with its bar (done); settings windows; report |

Order after tonight: the rest of 2, 3, 8, then 5-7, 4, 9, 10, 11.
