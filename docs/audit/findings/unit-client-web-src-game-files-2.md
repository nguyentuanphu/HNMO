# Unit Audit: `client/web/src/game (files 2)`

- **Unit**: `client/web/src/game (files 2)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `13` (`5,968` lines): `combat.ts`, `combatEffects.ts`, `combatLook.ts`, `crywolf.ts`, `customRanks.ts`, `darkSideGhosts.ts`, `duel.ts`, `events.ts`, `gameSounds.ts`, `gates.ts`, `gens.ts`, `groundItems.ts`, `guild.ts`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Desktop / Server / Suite Reference | Match |
|---|---|---|---|---|
| Duel eligibility & restricted maps | `duel.ts:11-14,127-154` | `DUEL_MIN_LEVEL = 30`; blocks challenge on Doppelganger (`65..68`) and Loren Market (`79`), blocks command in Chaos Castle (`18..23, 53`); `ROOM_COUNT = 4` colosseum spectator channels; spectator buff ID `98` | Desktop `DuelMgr` & server `DuelRequestAction` | Same |
| Gate collision & map mount/wing entry checks | `gates.ts:13-17,45-67` | Parses 512 records (`14` B xor `0xFC/CF/AB`) from `gate.bmd`; `RESEND_MS = 3000`; MG/DL/RF `floor(target.level * 2 / 3)`; Atlans gates (`45..49, 55, 56`) ban Uniria/Dinorant (`13:2, 13:3`); Icarus gates (`62..65`) require alive wings/Dinorant/Fenrir and ban Uniria | Desktop `ZzzInterface` `CheckGate` & `warps-and-npc-dialogs-s6-s9.md` | Same (note `gateAt` line `52` applies special gate-index `28` check) |
| Crywolf event maps, altar range & Elf eligibility | `crywolf.ts:19-71`, plus contract logic | `CRYWOLF_MAP = 34`; Altars `205..209`; decodes server class progression (`serverClass / 4` base + `% 4` quest step); enforces > `350` Elf contract preconditions & occupation states (`PEACE`, `OCCUPIED`, `WAR`) | Server `Crywolf` (`crywolf-s6-s9.md`) & Desktop `GMCrywolf1st` | Same |
| Event map IDs (BC / CC / DS / Market) | `events.ts:24-30` | `BLOOD_CASTLE_MAPS = [11..17, 52]`, `CHAOS_CASTLE_MAPS = [18..23, 53]`, `DEVIL_SQUARE_MAPS = [9, 32]`, `UNITED_MARKET_PLACE = 79` | Server map IDs (`minigames-bc-cc-ds-it-s6-s9.md`) | Same |
| Gens rank titles & emblem cells | `gens.ts:10-41` | `GENS_NONE=0, GENS_DUPRIAN=1, GENS_VANERT=2`, stewards `543/544`; clamps rank to `1..14` for `gensMarkCell` emblem offset | Desktop `CGensRanking` & `gens-system-s6-s9.md` | Same |
| Guild role masks, emblem size & status codes | `guild.ts:14-75` | `NONE=0xff, PERSON=0, BATTLE_MASTER=32, SUB_MASTER=64, MASTER=128`, `MARK_CELLS = 64` (`8x8`); maps join/leave/create/assign/relationship server error codes | Desktop `GuildConstants` & `GuildServer` | Same |
| Ground item pickup range & Zen display | `groundItems.ts:22-25,65` | `PICKUP_REACH = 2` cells, `ZEN = { group: 14, number: 15 }`, `LABEL_RAISE = 15` | Desktop `Pickup.h` & server pickup distance | Same |
| Combat target, mount & weapon animation rules | `combat.ts:28-115`, `combatLook.ts:12-429`, `darkSideGhosts.ts:13-80` | `ANIMATION_FRAMES_PER_SECOND = 25`, `TARGET_HIT_SKILLS = [264, 560]`, dual-sword/spear/poleaxe animation tables, RF `MAX_COPIES = 6` Dark Side ghosts | Desktop `ZzzCharacter` & `MonkSystem` | Same |
| Custom Ranks, audio/visual helpers | `customRanks.ts`, `combatEffects.ts`, `gameSounds.ts` | Custom rank badge lookup (`HnmoCustomRanksState`), Three.js skill effect & sound triggers | Client visual/audio/HNMO display | No game rules |
