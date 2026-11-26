# Unit Audit: `client/web/src/game (files 1)`

- **Unit**: `client/web/src/game (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `12` (`5,492` lines): `accountSafety.ts`, `autoPlay.ts`, `autoPlayConfig.ts`, `autoPlayExtras.ts`, `autoPlaySparkles.ts`, `buffs.ts`, `cashShopScript.ts`, `castleSiege.ts`, `characterCalc.ts`, `characters.ts`, `chat.ts`, `chatExtras.ts`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Desktop / Server / Suite Reference | Match |
|---|---|---|---|---|
| Character base attack damage, speed & PvP/PvE rating | `characterCalc.ts:200-243,320-371,447-476` | DW/Knight/Elf/MG/DL/Sum/RF weapon/ammunition damage formulas, dual-wield (`55%` DK/MG, `60%/65%` RF), `AttackRating` (`Lv*5 + Agi*3/2 + Str/4` base; DL `Agi*5/2 + Str/6 + Cmd/10`; RF `Lv*3 + Agi*5/4 + Str/6`), PK attack/block ratings, blocking (`Agi / divisor` + shield) | Desktop `CharacterInfo.cpp` & `common-server-and-character-rules-s6-s9.md` | Same |
| Wizardry & Summoner Curse damage formulas | `characterCalc.ts:246-318,482-496` | `floor(Ene/9)..floor(Ene/4)` + wing/staff/book options minus wear; Summoner curse damage separated cleanly (`SUMMON_EXPLOSION..SUMMON_REQUIEM` `223..224`) | Desktop `CalculateMagicDamage` & `CalculateCurseDamage` | Same |
| Defense & +9/+10..+15 full-set bonus | `characterCalc.ts:374-422` | `floor(Agi / divisor)` (`10` Elf, `3` DK/Sum, `4` DW, `7` DL, `8` RF, `5` MG) + armor/wing minus wear + full-set `+9` requirement & `[0.05..0.30]` bonus for `+10..+15`; excludes RF Gloves (`376`) and MG non-Gladiator armors (`393`) | Desktop `CalculateDefense` & `FullSetOf` | Same |
| MUHelper defaults, timings & serialization | `autoPlay.ts:16-23`, `autoPlayConfig.ts:55-115` | `AUTO_PLAY_DATA_LENGTH = 257`; hunting range `6`, obtain range `8`, away `10s`, repair threshold `50%`, pickup retry `1s`/`3s` (`3` max), target retry `2s`, max distance `10` | Server `MuHelperConfiguration` / Desktop `MUHelper` (`257`-byte config) | Same |
| AutoPlay Extras Free / VIP / Higher VIP gating | `autoPlayExtras.ts:51-67,111-160,221-223` | `defaultPolicy`: Free (`sparkleGuide`, `presetsStats`, base loop, bag-full/out-of-potions return to Lorencia NPC `253` at `127,88`); VIP/Higher VIP gates (`manaSdPotions`, `jewelDeposit`, `targetPriority`, `patrol`, `survival`, `bossFocus`) synced with `HnmoUltimateAutoPlay` server policy | Server `AutoPlayPlugin` (PR #367 tier policy); client preset `defaultExtrasSettings()` (`useManaPotion: true`, `autoDepositJewels: true`) assumes policy check before execution | HNMO custom (matches PR #367) |
| Castle Siege declare level, NPC HP/Def tables & gates | `castleSiege.ts:25-36,74-75` | Map `30`, `DECLARE_LEVEL = 200`, `MAX_GUILDS = 100`, `GATE_NPC = 277`, `STATUE_NPC = 283`, 6 gate coords, Crown Switches `217..`; Gate HP `[1.9M, 2.5M, 3.5M, 5.2M]` & Def `[100, 180, 300, 520]`; Statue HP `[1.5M..5.0M]` & Def `[80..520]` | Server `CastleSiege` configuration (`castle-siege-s6-s9.md`) | Same |
| Account Safety vault/item lock state & 4-digit PIN | `accountSafety.ts:32-35,84,130-150` | Tracks vault unlock & per-slot locks via `HnmoAccountSafetyState`; prompts 4-digit PIN creation/verification | `server/src/GameLogic/PlugIns/AccountSafety` | HNMO custom |
| Buff strip family dedup | `buffs.ts:24-61` | Loads `/data/Buffs/Buffs.json` and merges `HnmoCustomBuff`; keeps strongest buff per mutual-exclusion family on HUD strip | Server `MagicEffectSystem` / Desktop `w_Buff` | Same |
| Chat limits, cooldown & mute/filter | `chat.ts:38-49`, `chatExtras.ts:8-48` | `CHAT_MAX = 60`, `NAME_MAX = 10`, `MAX_LINES = 200`, `SYSTEM_LINES = 6`, `COOLDOWN_MS = 1000`, `FADE_MS = 10s`, channel prefixes `['','~','@','$']`; local blocklist & profanity filter | Desktop `ChatLogWindow` & `SystemLogBox` | Same |
| CashShop script parser & walk speed | `cashShopScript.ts:31-67`, `characters.ts:45-75` | `PACKAGES_PER_PAGE = 9`, `GIFT_ENABLED = 184`; walk speed `WALK_CELLS_PER_SECOND` synced with 25 FPS | Desktop `CShopListManager` & `ZzzCharacter` | Same |

## Re-check 2026-11-27 (changes 8cb8539b..a108e846)

Commits read (every changed file read in full):

- ba5139b1 feat(web): make chat, whisper and messenger windows resizable with localStorage persistence

Evidence:

- chat.ts / chatExtras.ts: chat, whisper and messenger windows resizable (width 260-640, 3-24 lines) and remembered per browser. DIFFERS from the desktop client, whose chat height moves in 3-line steps with a fixed width (the web used to do the same): a web-only convenience, owner to confirm (rule "Same as the desktop"). Tooltips are translated through i18n.ts (title attributes, texts in vi-overrides.json).
