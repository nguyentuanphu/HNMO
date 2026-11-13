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
dropping items; browser tests and CI for all of it; strict TypeScript build.

## 1. Effects and sound
1. Particle, sprite and effect system (the desktop's `ZzzEffect*`, `CreateEffect`, `CreateSprite`,
   `CreateParticle`, joints/trails, lights) — needed by skills, items and maps.
2. Sounds and music: the files are in `client/src/bin/Data/Sound` (808; 103 were damaged in the
   archive, listed in `Sound/MISSING.txt`) and `Data/Music` (47); port the desktop client's sound
   playback (`Audio/`, `Data/Monsters/Sounds.json`, map music) with the Web Audio API.

## 2. Skills and buffs
1. Skill list from the server (`SkillListUpdate`, `SkillAdded/Removed`), the HUD skill bar,
   hotkeys 1-5, the skill list popup, mana/AG costs and cooldowns.
2. Casting: targeted (`TargetedSkill`), area (`AreaSkill`, `AreaSkillHit`), the skill animations
   and effects per skill, skill arrows and multi-shot fans.
3. Buffs and debuffs (`MagicEffectStatus`), the buff strip (`buff_strip.rml`), their visual effects.
4. Master level and skill tree (`master_level.rml`).

## 3. Using items
1. Potions on Q W E R (`ConsumeItemRequest`), right-click to use, HP/MP refill.
2. Jewels on items (Bless, Soul, Life, Chaos …), fruits, scrolls/skill books, town portal, pets.
3. Repair (inventory button and NPCs), durability tints and the durability window.

## 4. Character
1. Character window C (`character_info.rml`): stats, adding level-up points.
2. Running after walking a while (`canRun`, run delays), swimming maps.
3. Pets and mounts drawn (Guardian Angel, Imp, Dark Horse, Fenrir …), capes as cloth.

## 5. NPCs, shops and quests
1. NPC dialogs (`npc_dialogue.rml`), shops (`npc_shop.rml`): buy, sell, repair.
2. Quests (`npc_quest.rml`, `my_quest_info.rml`, quest progress), class change quests.
3. Move window M / gates / warps (`move_command.rml`, `gateman.rml`), mini map, map name banner.

## 6. Trading and crafting
Trade (`trade.rml`), personal store (`my_shop`, `PurchaseShopInventory`), chaos machine and every
mix (`mix_inventory.rml`), Jewel of Harmony, sockets, lucky items, united market.

## 7. Social
Party (`party_info.rml`, `party_list.rml`), guild (`guild_info.rml`, `guild_make.rml`), friends,
messenger and letters, whisper/party/guild chat, duels, Gens.

## 8. Auto play
1. The desktop helper (`mu_helper_*.rml`): hunting range, main and 2 activation skills with
   conditions, combo, counter-attacks, potions and heal, buffs and party support, Dark Spirit,
   pick-up rules and item list, repair, auto-accept, the helper bar and its zen cost.
2. Offline leveling (the server already does it).
3. **(extra)** back to town when the bag is full or potions run out, buy potions, return.
4. **(extra)** sell junk to NPCs; **(extra)** stop timer; **(extra)** alerts on death / full bag.

## 9. Events
Blood Castle, Devil Square, Chaos Castle, Illusion Temple, Kalima, Crywolf, Castle Siege,
Doppelganger, Imperial Guardian, Kanturu, Raklion, lucky coins, golden bowman — enter windows,
timers, scores, rewards.

## 10. Shell and interface
1. **(extra)** The (i) button: an in-game guide of what to play and what each feature does.
2. Cash shop with the free coins (`in_game_shop.rml` and the gift/package windows).
3. Login, server and character select as the game's own RML scenes; option window (auto attack,
   sounds, theme), system menu, help, command window, translations, themes and UI scale.
4. Shadows, weather, animated map objects, water, BMD v14 and GLB models.

## 11. Release
Windows build tried on a real PC, performance on a normal laptop, the friends guide updated with
each release.

## Tonight (2026-11-13/14, Sydney time)

| Time | Work |
|---|---|
| 23:30–00:30 | 10.1 the (i) guide; 3.1 potions on Q W E R and right-click use |
| 00:30–01:30 | 2.1 skill list, skill bar and hotkeys |
| 01:30–02:30 | 1.1 effect system basics; 2.2 casting with skill animations and effects |
| 02:30–03:30 | 4.1 character window and stat points; 5.1 NPC shops (buy, sell, repair) |
| 03:30–04:30 | 8.1 auto play core (hunt, potions, pick up) with its bar; report, package link |

Order after tonight: the rest of 2, 3, 8, then 5-7, 4, 9, 10, 11.
