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

1. ✅ **Top races ("đua top")**, built 2026-11-16 (Sydney): races set in the admin panel (*Top races*): resets done, levels
   gained, highest level reached, boss-hunt points, PK kills, event wins (Blood Castle, Devil Square, Chaos Castle or any),
   guild points (guilds; every member gets the prize), monsters, time in the game; all classes, one class or a table per
   class; daily, weekly (Monday), monthly or between two Sydney dates (daylight saving handled); a minimum and prizes per
   rank range (coins with coin type, zen, items), paid once at the end with unique references, zen and items waiting for
   room in the bag. Live tables, coming races and past winners in the admin panel, on the website (Top races section) and
   in the web game's top races window (F9); a golden notice in the game and community posts (Discord, website highlights)
   when a race starts and ends. Defaults: weekly resets, weekly boss hunt, monthly levels per class, daily monsters,
   weekly event wins, a weekly PK race (off). State in `save/top-races.json` (no database change). The weekly ranking
   rewards of the world bosses (section 12.4) stay as they are. Still to check on a server running this code: a whole
   race's end and payout with real players (the rules are unit-tested; the web parts are tested with fed packets).
2. ✅ **Gift codes**, built 2026-11-16 (Sydney): the admin panel's *Gift codes* page makes codes or batches of random
   single-use codes (CSV download) with a reward (coins with coin type, zen, items with level and options), uses in total,
   once per account or per character, Sydney start/end (daylight saving handled), level/reset/class limits, on/off, public,
   a note; it shows the uses and the redemption log. Players enter them in the game (`/code CODE`, or the gift code window:
   `/code` alone, the community window's button, the touch "more" menu) or on the account page (account name and password,
   a character). Case doesn't matter; wrong codes are limited per account (5 per hour by default); coins are paid once by
   reference, zen and items wait for room in the bag. Public codes on the website's home page. State in
   `save/gift-codes.json` (no database change). Still to check on a server running this code: a real redemption end to end
   (the rules, the admin page and the website API are unit-tested; the web parts are tested with fed packets).
3. ✅ **Reset system with options**, built 2026-11-16 (Sydney): the admin panel's *Resets and relife* page: reset tiers by
   reset count (level needed, level after, zen (× the reset number if set), coins with coin type, items, points per reset
   given as a total, added or none, stats cleared or kept, resets per Sydney day); VIP accounts (the account's "Is VIP"
   value, also given for N days from the page) reset at a lower level, with discounts, extra daily resets and points;
   relife (grand reset) after N resets with its own level, costs, items, a kept share of the resets, permanent bonus
   points per relife, a maximum and a daily limit; delegated reset ("ủy thác reset", `/autoreset on` or `/uythac on`, or
   the website): while offline leveling the server resets the character at the level, within the limits, paying the
   costs plus an extra fee, keeping its build and hunting spot, with a log. The existing reset NPC and `/reset` follow
   these rules (`/reset info`, `/relife`). Website: the account page resets, relifes, adds and clears stat points and
   switches the delegated reset (only while the account is offline; account name and password through the bridge), and
   the home page shows this and last month's reset ranking, the relife ranking and the rules. The web game tells resets,
   relifes and the delegated resets in the chat and keeps the character window's reset count right. Top races see the
   resets at once. State in `save/resets.json` (no database change). Still to check on a server running this code: a
   reset with the NPC and a delegated reset during real offline leveling (the rules, the admin page and the website API
   are unit-tested; the web parts are tested with fed packets and answered requests). Not done: VIP days as a gift code
   or cash shop reward (the admin page gives VIP days; the account editor's attribute grants too).
4. ✅ **Account safety on the website**, built 2026-11-16 (Sydney): the second password is the game's vault PIN (4
   digits, kept hashed): set, changed (with the old one) and cleared on the account page, or at the vault in the game; the
   admin panel's *Account safety* page chooses what needs it (the vault, trades, the personal store, dropping or selling
   valuable items), how long it stays entered, the wrong tries and the lockout, and resets it for an account. It's entered
   once per session: the vault's lock (now also in the web game, as on the desktop), `/unlock 1234`, or the web game's box
   when the server asks. Item lock (account page, `/lockitem`, `/unlockitem`, Alt + right click in the web game): locked
   items can't be traded, sold, dropped, put in a personal store or mixed; an unlock needs the second password and waits
   (24 h by default). Login history (Sydney and local time, the address partly hidden on the website and whole in the
   admin panel, game or website, success or failure; kept 30 days) on the account page, and a notice in the game when the
   last login came from another address (the web bridge now tells the game server the player's real address). Trade
   history (items and zen, with whom, personal store sales too): the last 20 on the account page, all in the admin panel.
   State in `save/account-safety.json` (no database change; the PIN stays in the account's vault password). Still to check
   on a server running this code: a real trade, store sale and login from two addresses (the rules, the admin page and the
   website API are unit-tested; the web parts are tested with fed packets and answered requests).
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
- Live specs share test accounts (account-safety and missions-live both use test5): run them with --workers=1,
  or the second login is refused ("already logged in"). Once, after such a parallel run, test5 stayed
  "already logged in" with no open connection until the server restarted; not reproduced since - watch for it.
- Server tests on GitHub: a different admin-panel test fails on each run since 2026-11-16 (map controller disposal,
  bUnit render after disposal, map groups, packet order); they pass locally. Make them independent of timing.

## Owner decisions (2026-11-16, Sydney time) — to build
1. Phoenix Shot: the web game sends it to the server (an area skill with real damage). Note the difference from the desktop in PARITY.md.
2. Guild buff tooltips: English and Vietnamese for every guild buff.
3. Guild ladder rewards: (already built, checked 2026-11-16) paid to everyone who is a member at payout time, offline members included.
4. Zalo coins: (already built, checked 2026-11-16) credited only after an admin approves (an approval list in the admin panel).
5. Screenshots: the player chooses in the settings (3D view only, or with the interface).
6. K key: (already built, checked 2026-11-16) like the desktop, only while the bag is closed.
7. Top races: (already built, checked 2026-11-16) F9 opens them.
8. ✅ Gift codes: optional per-code network/device limits, salted identity claims and counts, shared browser device ids, and bilingual refusals. Legacy clients without a device id retain network checks. Live deployment verification remains.
9. Resets: the character is updated in place by default; the admin switch to log out instead stays.
10. Reset tiers: 350/380/400 with 20/10/5 per day as the default; the admin can change them.
11. VIP: bought with coins in the game; the admin can still grant it; players see and manage their VIP on the website's account page.

## Owner requests (2026-11-16, Sydney time) — approved, build on Thursday 2026-11-19 from 13:00 (first)
- Log in on the website first; the game then opens at the server choice without asking for the password again.
- Top-up lives on the home page and in the account page, not on the game's login box.
- The game's login box keeps only the guide link (no "create one", "top up", test accounts line).
- Player sign-in/registration and the admin sign-in are separate.
- The admin panel explains itself: a hint on hover for every button/field and a "How to use this page" button on
  every page (EN+VI), starting with the API keys page.
- The owner hit "This account is already logged in" for test5 on their own computer: the stuck-login follow-up is urgent.
- Approved proposal: website login -> "Play now" opens the game at the server choice with a one-time ticket
  (about a minute) instead of the password; the game without a web login goes to the website's login; the
  desktop client keeps its password login; the admin panel stays on its own address, no link from the website.
- The player's account page (after the web login) has everything a player needs, EN+VI:
  overview (characters with level/resets/map, online or not), coins balance + history + top-up, VIP (buy with
  coins, time left), gift codes, missions/daily rewards and login calendar, resets, account safety (second
  password, item locks, vault PIN, login history), change password and e-mail, invite-a-friend link,
  community links (Discord/Zalo), play now / download, log out. Today it has only: setup, create account,
  coins, gift code, resets and stats, account safety.
- Give the owner the guide for testing HNMO with their own Claude API key again with the Thursday report.

## The owner's performance reports (2026-11-16 09:55–09:58 Sydney; RTX 3060 Ti, 16 threads, Chrome 157, 1953x1289 at 1.2x)
Four 15 s reports (Lorencia with and without monsters, Devias, Noria/map 7): 60 fps steady, every frame 16.4–17 ms,
no long tasks, no slow frames, the game's own work 2.1–2.5 ms a frame (render 1.4–1.8 ms). So the browser draws
smoothly: the jerkiness the owner sees is in the MOVEMENT, not the frame rate. Suspects to check first:
- Character.advance (game/characters.ts): the angle snaps at once to each next cell (no turning over time), so
  grid zig-zag paths jerk; compare the desktop's turning.
- Monsters walk cell by cell from the server: when a step ends before the next packet arrives, the path is empty
  for a moment -> the stop animation and a pause, then walk again (stop/walk flicker). Keep walking / blend.
- A new path whose start isn't the current position makes the model jump; check how online moves set the path.
- Heap sawtooth ~25 MB every ~2 s (allocations per frame, e.g. `new THREE.Vector2` in advance): no frame drops
  now, but worth removing.
Next step: record per-frame positions/angles/actions of the hero and the nearest monsters (add to the perf report)
and compare with the desktop's movement.

## Other AIs' pull requests to review (snapshot 2026-11-16, Sydney)
- #2 chatgpt-login-hint (login box keeps only the guide link) -> main.
- #3 chatgpt-website-themes (five homepage themes chosen in the admin panel) -> main, draft; CI server tests failed (flaky web tests).
- #4 chatgpt-vietnamese (3,560 server resource translations, 8,053 web dictionary entries, shared language choice) -> BASE #3; machine-assisted, needs editorial review.
- #5 chatgpt-apikeys-help (API keys page help + hover hints, 14 EN/VI keys, 4 bUnit tests) -> main.
- #6 chatgpt-buffs-vi (Vietnamese for all 158 buffs + 7 skill buffs, durations, native dialogs) -> main; check buff 64 and 130 wording.
- #7 antigravity-flaky-web-tests (the 4 flaky HNMO.Web.Tests + NavMenu disposal fix) -> main. The CI failures on #3/#4/#5 are the tests #7 fixes: merge #7 first, then re-run CI on the others.
- #8 chatgpt-editor-vi (Vietnamese for the native editor, 2,021 entries + 142 English keys) -> main, draft.
- #9 chatgpt-phoenix-shot (Phoenix Shot sent as AreaSkill 270 at the hero's tile with the target as extra target; 5 files) -> main; live damage not yet checked.
- #12 chatgpt-giftcode-limit (one claim per network / per browser device, salted hashes only, new packet F5 23) -> main; next free F5 packet is 24; live redemption through the bridge not yet checked.
- #13 antigravity-web-login-ticket (website "Play now" with a one-time ticket, RequireWebsiteLogin setting) -> main.
  CONFLICT: #13 also uses F5 23 (HnmoTicketLogin), like #12 (HnmoGiftCodeRedeemExtended). When merging, keep #12 on F5 23
  and move #13 to F5 24 (XML, regenerate C#/web packets, docs, tests). Next free after that: F5 25.
- #14 antigravity-rage-fighter-test (deterministic effect sampling; passed 3 runs live) -> main.
- #15 antigravity-data-packs (HPK1 packs served by the bridge with ETag/immutable/304/206, web allowlist, item icons in one
  zip, no .br/.gz copies, single-file bridge): Windows package 25,000 files -> 194; Data 14,891 files / 1,066 MB -> 48
  packs / 738 MB. To check on merge: ffmpeg Opus packaging, macOS/Linux packages, admin panel icons live.
- #16 chatgpt-shop-editor (new /merchants with catalogue picker, 8x15 grid, first-fit, drag, bulk add, auto-arrange,
  undo/redo, save summary; /merchants/legacy kept; 87 EN/VI keys appended) -> main. Price override not done (needs model
  change). Recheck icons together with #15's packed item icons.
- As of 2026-11-16 16:17 #2, #3, #5, #6, #8, #9, #12 conflict with main: ChatGPT is rebasing its own branches.
- Coming (2026-11-16): antigravity host settings -> installer app -> one save file -> KTX2 (one PR each, based on #15);
  chatgpt rebases + account page + admin help everywhere + VIP.
- Earlier: antigravity-stuck-login (the "already logged in" leak), antigravity-smooth-movement (jerky movement), chatgpt-giftcode-limit.
Merged 2026-11-16: #7, #10, #11 (all server tests pass; one more flaky test seen: LeaderboardsPageTests.LateResultOfPreviousSelectionDoesNotOverwriteCurrentOne, passes 5/5 alone).
Merge order for the rest: then #2, #5, #6, #8, then #3, then #4 retargeted to main (resolve the translation JSON carefully: one "New here?", keep #6's durations and dialogs).

## Fewer files (the owner asked earlier; status 2026-11-16)
Done: the packaged server is ONE executable (PublishSingleFile) instead of ~80 DLLs; the web game's code is bundled and
minified into main.min.js / website.min.js. Not done (the packaged folder still has ~25,000 files):
- client Data: ~13,900 files (738 MB) the browser loads one by one -> pack into a few archive files (by folder) with an
  index, served by the bridge (byte ranges) and unpacked in the browser; the desktop client keeps its folder.
- server/wwwroot: ~8,300 files, 8,150 of them the item editor's static files (each with .br and .gz copies) -> drop the
  precompressed copies (the server compresses on the fly) or pack the item icons into one sprite/archive.
- the web bridge: ~370 files -> PublishSingleFile like the server.
- save/database: ~2,000 PostgreSQL files (created at first start; can't be packed).
- The source repository (33,500 files: 14,900 of them client/src/bin/Data) stays as files for git.

## Lighter and fewer files: the plan (owner asked 2026-11-16; approved direction, build after the PR merges)
Today's packaged folder: ~25,000 files. Game data 738 MB in 13,900 files (textures .ozj 276 MB + .ozt 184 MB,
models .bmd 198 MB). Target: about 50 files and roughly half the size.
1. Game data packs: per map one pack (its terrain, objects, monsters' and NPCs' models) + a few shared packs
   (interface, player, items, effects, skills, sounds). Each pack = one file with an index (name -> offset, size).
   The bridge serves a pack (HTTP range requests and long cache headers); the browser fetches what it needs,
   caches whole packs in Cache Storage, so a second visit loads from disk. Only the packs of the current map and the
   shared ones load at the start (faster first load). The builder (package-hnmo.sh) makes the packs; the source repo
   keeps the loose files.
2. Lighter textures: convert textures to GPU-compressed KTX2 (Basis Universal) for the web game: about 4-6x smaller
   on the graphics card and smaller downloads, faster loading, less stutter when a new map/monster appears. Keep a
   fallback to the original for any texture that looks wrong; compare screenshots before/after.
3. Leave out what the web game never loads (desktop-only files): the builder lists the files the web client really
   requests (from a full walk of every map in the tests) and drops the rest from the web download. The desktop
   client gets its own download with the full folder.
4. Admin panel files: drop the precompressed .br/.gz copies (the server compresses on the fly) and pack the item
   editor's ~2,700 icons into one sprite sheet: 8,300 files -> about 100.
5. The web bridge as ONE executable (PublishSingleFile), like the server; no .xml/.pdb anywhere.
6. Save files: the plug-ins' save/*.json (guilds, missions, top races, ...) into one save/hnmo-state.json (or the
   database), written safely (write to a temp file, then rename), so a backup is the database + one file.
7. All configuration in the admin panel: a "Host settings" page (EN+VI, with help) for what still lives in files:
   ports (web 8080, admin 8095, connect 44406, database 55432), public address/domain, relay options, paths, the
   web bridge's appsettings, the database connection, the license; saved to ONE settings file the start scripts and
   the bridge read; a "Restart HNMO" button applies changes that need a restart. Check every plug-in has an admin
   page (list any that don't) and that nothing needs a hand-edited file.

## One app to install and run (owner asked 2026-11-16)
Today: unzip, then Start HNMO.bat / start-hnmo.sh (the web bridge's GameLauncher starts PostgreSQL and the server).
Plan:
- ONE app per system: Windows HNMO.exe (installer: Setup.exe with Start-menu/desktop icons and uninstall),
  macOS HNMO.app in a .dmg, Linux an AppImage (+ a .deb). Double-click -> the HNMO Control page opens in the browser
  (part of the admin panel, this computer only): Start / Stop / Restart the game, the status of the database, game
  server, web game and admin panel, live log, open the game, open the admin panel, "start HNMO when this computer
  starts", "install as a service" for a VPS (Windows service / systemd), check for updates, backup now / restore.
  A tray icon (Windows/macOS) while it runs. The .bat/.sh stay as a fallback.
- The first start runs a setup wizard on that page: admin password, game name, public address, ports (from the
  Host settings plan), language.
- Honest limits: without a paid code-signing certificate Windows SmartScreen warns once ("More info -> Run anyway"),
  and macOS needs an Apple developer account ($99/year) to notarise, else right-click -> Open the first time.
- Tests: the windows-start CI job installs and starts it; add macOS and Linux jobs.

## Shops in the admin panel (owner asked 2026-11-16)
Today: Admin -> Merchants lists the NPC shops by name; Edit shows the shop's grid; "create item" adds an empty item
and its kind is picked in the item editor; drag to place. Works, but slow with thousands of items.
New design (the "Shop editor"):
- Shop list: NPC name, map and position, number of items, a small preview of the grid; search; "copy shop to...".
- Two panes: LEFT the catalogue of ALL items (icons, virtual scrolling so thousands stay fast), with a search box
  (name in English or Vietnamese, or group:number), filters (category: swords/axes/.../jewels/potions/scrolls/wings,
  class, required level range, "only items not in this shop"), and sort; RIGHT the shop's 8x15 grid as in the game.
- Add by click (placed in the first free spot), by drag, or multi-select + "Add selected"; "Add whole category";
  "Auto-arrange" (largest items first, no gaps); a capacity bar (cells used / 120).
- Per item: level, skill, luck, option, excellent options, durability, and the price (computed as in the game,
  with an optional fixed price override and a shop-wide price multiplier).
- Undo/redo, "discard changes", save shows exactly what changed; changes reach players without a restart.
- Import/export a shop as a file; a change log (who changed what, Sydney time).
- EN+VI, hover hints and a "How to use this page" button. bUnit tests + a browser test.
Same picker for every place items are chosen (gift codes, missions, rewards, drops, cash shop catalogue).
