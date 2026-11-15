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
3. 🟡 Kalima, Crywolf, Castle Siege (`game/kalima.ts`, `game/crywolf.ts`, `game/castleSiege.ts`,
   `ui/castleSiegeWindows.ts`, `ui/fieldEventWindows.ts`): Kalima gates and tooltips, the Crywolf HUD, altars,
   result and terrain, the siege's guardsman, senior, gatekeeper, gate switch, catapult, HUD and notices done;
   the gate / catapult animations, the battle skills and the event monsters' effects next.
4. Kanturu, Raklion, lucky coins, golden bowman — enter windows, timers, scores, rewards.

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
6. ✅ **(extra)** The game's name players see (default "HenryN Mu Online") is set in the admin panel:
   Plugins, "Game name" (`GameNamePlugIn`); the web game reads it through `/game-name.json`
   (`src/gameName.ts`, test `game-name.spec.ts`).
7. **(extra)** Admin panel redesign, and a check that every game setting can be changed there.
8. ✅ **(extra, server)** Monster amount per map ×0.5 to ×5 on the admin panel's map editor
   (`MonsterAmountPlugIn`, applied at once to the running maps; the saved quantities stay).
9. ✅ **(extra, server)** Chat: offline whispers kept (20 per receiver, 7 days, file
   `offline-whispers.json` in the server folder) and delivered "(offline)" on entering; a whisper reaches
   other game servers of the same process; the admin panel's chat log (Players, Chat log: the last 2000
   lines, search, live refresh, mute 10 min / 1 h / 1 day for operators, open the account); a spam guard
   (the same text more than 3 times in 10 s or more than 8 lines in 5 s: "Please don't spam.", GMs exempt).

## 11. Release
- ✅ **(extra)** Player limit: 10 at once without a license; a signed `license.hnmo` (owner's
  Ed25519 key, `tools/hnmo/sign-license.sh`) raises it, with an optional end date and computer
  (`server/src/Integrity/PlayerLicense.cs`, `GameLogic/LicensedPlayerLimit.cs`, checked in the login).

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

## 12. Making HNMO stand out (the owner asked on 2026-11-15, Sydney time)

One background agent at a time, in this order; each with tests, docs, English and Vietnamese texts.

**Every coin reward is set in the admin panel** (the owner, 2026-11-15): for each source (missions,
online time, milestones, invites, login calendar, bosses, drops, events, rankings, community) an
on/off switch, the coin type (earned Goblin Points or paid W Coin), the amounts, the chances, the
daily and per-account caps, and the schedules; changes apply without a restart where possible, and
the coins history shows every grant.

1. ✅ **Phones** (idea 4): fight button, skill and potion buttons, "more" windows, full screen in
   landscape, the turn hint, the home screen manifest, and the website's "Play on your phone" part.
2. 🔄 **Chat on the server** (with the map monster multiplier): offline whispers, the GMs' chat log
   with mute, the spam guard, the Discord check. (Agent running.)
3. ✅ **Daily and weekly missions** (idea 2), built 2026-11-15/16 (Sydney): the mission board (kill N monsters,
   optionally of a monster and/or on a map; take part in or win Blood Castle, Devil Square, Chaos Castle; successful
   chaos machine mixes; zen picked up; levels; minutes in the game), rewards (zen, items, coins) claimed in the web
   game's missions window (J) with a full bag keeping them claimable, a daily streak (bonus on day 7, a missed day
   starts over), all set in the admin panel (*Missions and rewards*), and a Missions section on the website. With
   them, the **earned coins** (Goblin Points by default, each source on/off with its coin type and amounts): online
   time (per full hour, auto play half, capped per day), one-time rewards at level 100/200/300/400 and per reset (per
   character), invite a friend (code or character name, paid at level 150; same known address refused), a monthly
   login calendar (bigger on days 7, 14, 28), a daily cap of 300 earned coins per account (one-time rewards over it
   come on later days), every grant in the coins history. Progress per character, earned coins per account, in
   `save/missions.json` (no database change). The phones' "Play on your phone" part of the website is done too.
   Still to check on a server running this code: a whole day of play (the in-game browser test feeds the packets,
   the server rules are unit-tested); web players' addresses are the relay's, so the same-address check only
   works for desktop players.
4. ✅ **World bosses** (idea 5), built 2026-11-15 (Sydney): bosses on a schedule (every N hours or fixed Sydney times,
   days of the week) on a chosen map (spots or a random walkable spot of an area), announced before, at the spawn and at
   the kill in the game, on Discord (when the bridge is set up) and on the website; damage per player or party; rewards
   (zen, items, coins with a coin type) for rank 1, 2, 3, 4-10, everyone with at least X % (or X damage) and the last
   hit; no damage, no reward; rewards that don't fit the bag wait, coins come at once. A spawn-now button and the recent
   kills with damage tables in the admin panel (*World bosses and rankings*); a World bosses section on the website; a
   line on the screen, chat notices and a window (N) in the web game. With it, the **weekly ranking rewards** in coins
   (paid once per week with unique references): the top 10 of boss-hunt points, Blood Castle, Devil Square, Chaos
   Castle (points for being in it at the end and for winning) and the castle siege's winning guild (coins per member,
   extra for the master). State in `save/world-bosses.json` (no database change). Still to check on a server running
   this code: a real kill with several players and a week's payout (the rules are unit-tested; the web parts are tested
   with fed packets).
5. ✅ **Onboarding** (idea 3), built 2026-11-15 (Sydney): a guided first half hour for new characters (talk to a merchant,
   defeat monsters, a potion, levels, stat points, a skill, the missions window, a party, the cash shop; walk to a spot and
   enter a map as examples), every step checked by the server with its own reward and a final reward, optional steps,
   skippable (with a confirmation); a starter kit per new character or per account (items with options and class items,
   zen, coins, stat points, an EXP buff), each piece on/off; bag-full rewards wait. All in the admin panel (*Onboarding and
   starter kit*), the guide panel (Y) with an arrow and chat notices in the web game, and a New players section on the
   website. State in `save/onboarding.json` (no database change). Still to check on a server running this code: a new
   character's whole guide (the rules are unit-tested; the web parts are tested with fed packets).
6. ✅ **Guilds** (idea 6), built 2026-11-16 (Sydney): guild experience from the members' play (monsters, events
   finished and won, world boss kills and damage, missions, time in the game, the castle siege), each source on/off with
   its amount and a daily cap per member; 10 guild levels (editable) with small buffs for the online members (+EXP %,
   +zen drop %, +defense) shown on the web game's buff strip; a weekly guild ladder (Monday 00:00 Sydney) whose top
   guilds' members get coins (extra for the master), zen or items once, with the castle siege winner's ladder bonus.
   All in the admin panel (*Guilds: levels and ladder*), a panel next to the guild window (G) and chat notices in the
   web game, and a Guilds section on the website (ladder, last week's winners, guilds by level, castle siege results).
   State in `save/guilds.json` (no database change). Still to check on a server running this code: a week's payout
   and a real level-up with several members (the rules are unit-tested; the web parts are tested with fed packets).
7. ✅ **Community** (idea 7), built 2026-11-16 (Sydney): one-time coins for linking the account to Discord (the Discord
   bot's /link with a code from the game; once per account and per Discord user) and for joining the Zalo group (a claim
   code posted in the group that the admin approves in the admin panel: Zalo has no open bot API for groups), community
   events (a code word entered once per account while the event runs, or listed winners paid once; coins, zen, items;
   Sydney dates), automatic posts to Discord and the website's recent highlights (rare drops with a filter, world boss
   kills, guild level-ups and ladder winners, castle siege winners; each kind on/off with English and Vietnamese texts),
   Print Screen screenshots like the desktop's and a share panel (caption with the game's name, Web Share or download).
   All in the admin panel (*Community*), the community window (K) in the web game and a Community section on the
   website. State in `save/community.json` (no database change). Still to check on a server running this code: a real
   Discord link with the bot and a real post on Discord (the rules are unit-tested; the web parts are tested with fed
   packets); top-race winner posts come with the top races (section 13.1).
8. ✅ **Cash shop: decided by the owner (2026-11-15)** — the shop stays as it is; what makes it fair is that
   every coin can also be earned by playing (bosses, drops, events, missions, online time, milestones,
   invites, the login calendar, rankings, community), each source set in the admin panel. No items are
   taken out of the shop.

## 13. What the popular Vietnamese servers offer (the owner asked on 2026-11-15, Sydney time)

Looked at what the well-known Vietnamese servers advertise (feature pages, server listings, a big
licensed mobile release). Each item: every number and switch in the admin panel (English and
Vietnamese), and shown on the website. Done after section 12's items 4-7, in this order; item 8 of
section 12 (the cash shop) still waits for the owner's answers.

1. ⏳ **Top races ("đua top")** — the most advertised feature: races by day, week and month (resets, level,
   boss-hunt points, PK kills, event wins, guild points, per class), each with its own prizes (coins, items),
   start/end dates and rules. Live tables and past winners on the website; a notice in the game when a race
   starts and ends. Boss-hunt points come from section 12's world bosses (points per boss, not by power).
   Partly there (2026-11-15): the boss-hunt points (each boss's points shared by damage) and **weekly** races with
   coin prizes for the top 10 of boss hunt, Blood Castle, Devil Square, Chaos Castle and the castle siege winners,
   with this week's tables and last week's winners on the website. Still to do: daily and monthly races, the other
   race kinds (resets, level, PK, guild points, per class), item prizes and start/end dates.
2. ⏳ **Gift codes**: the admin creates codes (rewards, number of uses, one per account or per character,
   start/end, level/reset limits); players enter them on the website or in the game (/code).
3. ⏳ **Reset system with options**: reset tiers (level needed, zen/coins cost, points kept or given, a
   daily limit per tier), VIP reset at a lower level, relife (grand reset) after N resets, and "ủy thác
   reset": the server resets the character during offline leveling. Reset and stat points (add / clear)
   also on the website. Reset ranking per month.
4. ⏳ **Account safety on the website**: a second password for the vault and for trading, item lock, login
   history (time, address, game/website), trade history; the admin panel sets which are required.
5. ⏳ **Top-up milestones ("thưởng mốc nạp")**: rewards at total top-up steps, shown on the account page;
   QR bank transfer on the top-up page (the bank top-up exists).
6. ⏳ **Event and boss timetable**: every event, invasion and boss with its next start (Sydney and the
   player's time), its map and its drops, on the website and in the game (i). Extra invasions on a
   schedule (golden monsters, white wizard, rabbits, snowmen, "find the GM").
7. ⏳ **Daily lucky number**: pick a number on the website or in the game, a draw at a set time, coins or
   jewels for the winners; prizes and odds in the admin panel.
8. ⏳ **Marriage**: a proposal, a ceremony at an NPC, a couple title, a small couple bonus when hunting
   together, divorce rules; the couples list on the website.
9. ⏳ **Item market**: players list items for zen or coins (web and in-game NPC), with a fee; the
   admin panel sets the fee, the currencies and the banned items. Replaces risky trades between strangers.
10. ⏳ **Jewel bank**: deposit jewels (Bless, Soul, Life, Creation, Chaos, ...) as numbers on the account, take
    them out in the game or on the website; which jewels, limits and fees in the admin panel. Also clearing
    master skill points (with item 3's stat clearing).
11. ⏳ **Catch-up level cap** (a server option, off by default): once the first player reaches the top level,
    the cap for everyone rises N levels a day; with resets, a daily reset limit does the same job. Shown on
    the website (today's cap).
12. ⏳ **Season plan**: features open in stages (item level cap +9 first, then master levels, harmony, wing 2,
    the wolf, 380 items, wing 3), each stage opening on a date or when the server has done something (a boss
    killed, N players at a level). Stages, dates and conditions in the admin panel; the timeline on the website.
13. ⏳ **Combining leftovers**: chaos machine recipes the admin panel can add (e.g. 2 boxes of the same level +
    1 Jewel of Life + 1 Jewel of Creation -> a box one level higher, with a top box level per season stage),
    and an exchange NPC for surplus materials. Recipes shown in the game and on the website.
14. ⏳ **Mount chain**: golden bosses drop mount materials, duplicate mounts are material for the next step
    (horn -> wolf -> golden wolf/horse -> higher steps); a mount collection page on the website. Kept to looks
    and modest PvE strength.
15. ⏳ **Party options**: a party password (join without an invite) and auto party for hunting spots. A party of
    10 needs a new party window in the web and desktop clients (they show 5); later, if wanted.

## Follow-ups noted overnight (2026-11-16, Sydney)
- rage-fighter.spec.ts: the "Ignore Defense makes effect:windForce" check fails on repeated runs against the same
  test character (likely the buff is still active from the previous run, so it isn't re-applied); make the test
  remove or wait out the buff first. The Dark Side dash check passed live but once failed at 5 fps (timing).
- Phoenix Shot: the desktop casts it and sends nothing (the server supports it as an area skill). Kept 1:1 for now;
  ask the owner whether HNMO should send it.
