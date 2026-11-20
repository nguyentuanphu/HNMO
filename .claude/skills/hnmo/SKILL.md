---
name: hnmo
description: How the HNMO owner works and how to work on HNMO (server, web game, packaging). Use at the start of any HNMO task, and update it when you learn a new preference.
---

# Working on HNMO

## What the owner wants

- One HNMO folder with everything (server, admin panel, web game, save database)
  that starts with **one launch**; friends play in the browser, together over
  Tailscale or offline alone. Progress is saved in the folder.
- Everything named HNMO; nothing about the original game, publisher or base projects
  in anything pushed to GitHub (commit messages, PRs, docs, code). Keep license and
  copyright notices.
- Free cash shop coins (no real money); a simple account page to register and get coins.
- The web game should match the desktop game feature for feature (see
  `client/web/PARITY.md`), built in small, tested steps.
- The owner sometimes writes in Vietnamese: answer in Vietnamese then.
- The web client must have **everything** the desktop client has, 1:1 (the owner said so
  explicitly); never call a feature done while the desktop one does more.
- Auto-play: the desktop helper's full feature set first, plus extras the owner approved
  (2026-11-13): go back to town when the bag is full or potions run out (buy potions, return to
  the hunting spot), sell junk to NPCs, a stop timer, alerts on death / full bag. The owner
  also said: add other useful extras when they fit — mark them as HNMO extras in PARITY.md.
- An (i) button in the game opens a guide page: what to play and what each feature does.
- Times in Sydney time. Short, plain reports: what works, what's verified, what's next,
  and what the owner has to do (with links).

- **Decisions in a pop-up:** when a choice is the owner's (e.g. which cash shop items are "power" items), ask with the pop-up question tool when that task comes up, not in a chat message.
- **Schedule:** a resume task runs every 5 hours from 11:00 Sydney (11, 16, 21, 02, 07); one background agent at a time.

- **Everything the owner may want to tune is in the admin panel:** coin rewards (each source on/off, coin type, amounts, chances, caps, schedules), events, bosses, missions. No hard-coded amounts.

## How to work

1. Read `CLAUDE.md`, then `client/web/PARITY.md` for what's done and what's next.
2. Make the change; build (`dotnet build server/src/HNMO.sln -c Release`) and run the
   tests that cover it.
3. Test in the real game: package with `client/web/tools/package-hnmo.sh`, run it as a
   non-root user, drive the browser with Playwright (headless Chromium with
   `--use-gl=angle --use-angle=swiftshader`), take screenshots of what changed.
4. Commit with an HNMO-only message; push; check the **HNMO package** workflow run and
   give the owner the download link.

## Things learned

- The desktop client's windows are RML documents with RCSS themes in
  `client/src/bin/Data/Interface/RmlUi` (default theme `legacy`). The web client shows them
  as they are with `client/web/src/ui/rml.ts` (sprite sheets, tokens, data bindings), so a
  new window is: open its document, fill its data model like the desktop's `*RmlModel`, and do
  the hit tests in JS (the themes leave pointer events off; the desktop does them in C++).
- Items come in the server's extended format (`ItemSerializerExtended`, 5-15 bytes); the web
  client decodes them in `src/game/inventory.ts`. Items are drawn with their 3D model and the
  `inventory` rotation of `Data/Items/Models` (`src/ui/itemIcons.ts`).
- For quick browser tests run the web client from source: build `client/web/bridge` and run
  `dotnet bin/Release/net10.0/HNMO.dll --contentRoot <bridge dir>` (port 8081) next to a
  running server; the login may show a server list (click `#servers .choice`), and an account
  stays logged in for a while after a test browser closes.

- The connect server answers on 44406; the game server sends own player id 0x0200.
- Packet definitions: `server/src/Network/Packets/*.xml`; regenerate the web client's
  `packets.generated.js` with `client/web/tools/generate-packets.mjs` after changes.
- A fresh save has the test accounts; `test1` has four characters, `test300` one.
- The account page needs a one-time API key from the admin panel (see the folder's
  README.txt); handlers taking only `HttpContext` must be cast to
  `Func<HttpContext, Task<IResult>>`.
- Inside this sandbox, run local HTTP tests without the proxy
  (`env -u HTTPS_PROXY -u HTTP_PROXY -u https_proxy -u http_proxy`).
- `test1`'s first character (test1Dk) carries healing, mana and shield potions (good for item-use
  tests); the `/item` chat command needs a game master account.
- The in-game guide is `client/web/public/guide.html` (the (i) button): update its feature table
  whenever a feature starts working.
- `dotnet` lives in `~/.dotnet` in the cloud sandbox (`export PATH=$HOME/.dotnet:$PATH`).
- Night work blocks: when a block finishes early, start the next block at once; don't wait for
  its scheduled message (owner, 2026-11-14).
- `server/licenses/LICENSE.txt` (MIT notice, moved out of sight on the owner's request) stays exactly as it is, holder's name included: the license requires
  it and the packaging copies it into `licenses/`. Commit messages about it name nobody.
- Browser tests share accounts with each other and with earlier runs: a test that moves a
  character, joins a party or uses items leaves that state behind. Start each test from a known
  place (`/move lorencia`, walk to the NPC), leave parties first, close extra browser contexts.
- The server has a potion cooldown (0.5 s): two potions back to back are refused (C1 26 FD).
- A local server running for hours can run out of monsters; restart it (`start-hnmo.sh` as the
  `hnmo` user) before blaming the game. GitHub's "Web game tests" start from a fresh save.
- Packet handlers must not block (no `window.confirm` / `prompt` inside one): use
  `ui/confirmDialog.ts` (`confirmInGame`).
- Parity rule (owner, 2026-11-14): the owner decides what the web takes from the desktop and how.
  New content beyond the desktop (e.g. new skills for every class) goes in the server and the web
  only; don't change the desktop client for it unless asked. New skills reuse the game's own
  effect textures/models and sound files, defined in data (one entry per skill).
- The web's own data files go in `client/web/public/data/...` and are served at `/data/...` before
  the game's files of the same path (the bridge's /data route looks there first). HNMO's skill
  sounds are `public/data/Sound/hnmo/<skill>.mp3` (made with ElevenLabs sound effects, 50 credits
  each; the free plan sometimes says "busy" or hits a per-minute limit — try again later).
- New server data reaches existing saves through an update plug-in (Persistence/Initialization/
  Updates, found by its [PlugIn]/[Guid]); fresh saves through the initializer. To try server
  changes in the local test folder, copy `server/src/Startup/bin/Release/HNMO.*.dll` (and the
  language folders' HNMO resource DLLs) into `/home/hnmo/HNMO-clip/server/` and restart it.
- Accounts the browser tests use besides test0-9/test300/test400: `ancient` (level 330 Blade
  Knight, red Fenrir), `testgm` (game master: /startbc, /startds, /startcc, /skipwait), `quest1`.
- Packets can be fed in tests with `window.__game.connection.dispatch(bytes)`.
- Skill effects (`client/web/src/render/effects/`): the core and the wizard's types are in effectSystem.ts,
  the other classes' types register in extraEffects.ts's tables (knightEffects.ts, elfEffects.ts,
  lordEffects.ts, summonerEffects.ts, rageEffects.ts; extra particle types in EXTRA_PARTICLES); the skills' timelines are in skillEffects.ts (the Rage Fighter's effects start at the cast, rageCreateEffect; Chain Lightning comes from its own packet, playChainLightning). `tests/e2e/skill-effects.spec.ts` plays
  each skill through Combat's onSkill hook and freezes the effect ticks for its pictures (headless
  Chromium draws only a few frames a second); SKILL_GROUPS / SKILLS / SHOTS_DIR pick what it runs. The test machine loads a new model's textures very slowly (tens of seconds): a case's `warm` loads them before the cast and `model` waits for them before the picture.
  Don't name an env variable GROUPS in bash (it's a shell builtin).
- Parallel work: agents in git worktrees, each with its own bridge port (8082-8091) and BASE_URL;
  merge their branches one at a time and resolve PARITY.md / guide.html conflicts by keeping both.
- Owner wants (2026-11-14), to do AFTER the protection/keypair agent lands its public key:
  1. A default limit of 10 concurrent players (owner set 10; they will adjust); a `license.hnmo` file signed with the owner's
     private key (reuse the integrity keypair) raises it. The server already has a MaximumPlayers
     config field (ServerConfiguration.MaximumPlayers, used in BotServerPartition). Enforce 10 by
     default on login; a valid signed license lifts it. Give the owner a small command to sign a license.
  2. Ship Ubuntu (linux-x64) and macOS (osx-arm64/osx-x64) release packages too, not only Windows.
     package-hnmo.sh already takes the runtime arg; start-hnmo.sh already exists for non-Windows.
     The extra work is bundling the right PostgreSQL per OS (or relying on a system pg via POSTGRES_BIN).
     Update .github/workflows/hnmo-package.yml to a per-OS matrix.
- Per-person server licensing (owner, 2026-11-14): default limit 10 concurrent players. When the owner
  shares the server to several hosts, each host gets their OWN license.hnmo that the owner signs with
  the private key, setting THAT host's max players (and optionally an expiry and a machine binding so it
  can't be re-shared). No license => default 10. Provide a signing command the owner runs locally.
- Usage: the owner conserves usage (5-hour reset windows). Run at most ONE background agent at a time;
  prefer doing work directly. A recurring trigger resumes at 8pm Sydney and every 5h (01,06,11,16,20).

## 2026-11-14 (later)
- Visible game name: "HenryN Mu Online" by default, changeable in the admin panel (Plugins, "Game name").
  Web: `gameName()` / `data-game-name`; server: `GameNamePlugIn.GetName(context)`; admin API `api/game-name`.
- The owner wants the admin panel redesigned (better looking) and every game setting editable there.
- the Vietnamese client archive can be used for assets the game data is missing (99 sounds came from it); extract only
  the files needed, never run anything from it.
- Deploying the server to the test folder: `dotnet publish Startup -c Release -r linux-x64 --self-contained
  -o /home/hnmo/HNMO-clip/server` (keep its ConnectionSettings.xml); copying loose DLLs and deps.json from a
  plain build breaks the self-contained start ("Could not resolve CoreCLR path").

## Player license (2026-11-15)
- 10 players at once by default (all game servers together, per account; bots/templates don't count),
  enforced in LoginAction (not a plug-in, so a host can't switch it off).
- `license.hnmo` next to "Start HNMO" (or in server/), signed with the integrity key:
  `HNMO_SIGNING_KEY_FILE=<key> client/web/tools/hnmo/sign-license.sh "<licensee>" <max> [YYYY-MM-DD|-] [machine id|-]`.
  The server logs "HNMO license: …" and "this computer's id" at startup. sign-license.sh is never packaged.
- Website work (PR #1, branch codex/bilingual-server-website) isn't in main yet: don't deploy main's GameLogic
  next to the website branch's admin panel on the test server (type load error). Publish one branch fully.

## Learned 2026-11-15

- **modern-web-guidance** (Google Chrome's guides, installed in `.agents/skills/modern-web-guidance`,
  linked from `.claude/skills`; Apache-2.0, its LICENSE kept there): use it for the website, the
  account / top-up pages and the admin panel (forms, accessibility, page speed, phone layout, the
  installable app). Run its searches with `DISABLE_TELEMETRY=1` (the owner's data stays private).
  Not for the game's own windows: those copy the desktop client 1:1. Update: `npx skills update`.
- **Slow container:** the HUD and chat appear ~25 s after entering the world while builds or other
  agents run; tests wait for `__game.chat.input` / `__game.hud.doc` instead of fixed sleeps.
  "ERR_INSUFFICIENT_RESOURCES" in a test = reload + goto at once; leave with `about:blank`, then log in
  again in a `toPass` loop (the server refuses an account still logged in).
- **Test server restart:** stop `HNMO.Startup`, the old web (`./HNMO` in HNMO-clip/web) and PostgreSQL
  (`su hnmo -c "/usr/lib/postgresql/16/bin/pg_ctl -D /home/hnmo/HNMO-clip/save/database stop -m fast"`)
  before a full publish, or the publish fails on locked files / port 8080 is taken.
- **Early packets:** packets that arrive while the map loads are buffered (`earlyPackets` in main.ts);
  a handler set up later must replay them (the offline whispers came in then and were lost).
- **Window scale:** use `uiScale()` / `hudScale()` / `dockTop()` from `ui/nativeText.ts` (the desktop's
  CappedUniformScale with the UI size option), never `innerHeight / 480`.
- **Synthetic keys:** the game reads `event.code` for the number keys (`Digit1`): a dispatched
  KeyboardEvent needs `code` too.
- **Flaky server tests on GitHub:** timing tests must wait for the full end state (e.g. a respawned
  monster is alive before it's back on the map).
- **ERR_INSUFFICIENT_RESOURCES / "Failed to fetch" in browser tests = the disk is full** (`df -h /`):
  Chromium can't store downloads. Remove merged worktrees and old package folders in the scratchpad.
- **Plug-in settings saved by the admin panel** use `$id` / `$values` markers; read them with
  `PlugInSettings.ReadActiveAsync` (drops them). A plug-in with no saved row is active with its defaults.
- **Owner choices:** every coin reward is set in the admin panel; decisions in a pop-up. The cash shop
  stays as it is: fairness comes from every coin also being earnable by playing (answered 2026-11-15).
  Sign-up: the normal website registration stays; Google sign-in is optional, never required.

## Missions (2026-11-15)
- HNMO's own client/server messages live in the F5 group (`ChatCommandGroupHandlerPlugIn.GroupKey`): Discord 02-07,
  missions 08 (request), 09 (state as ASCII JSON, C2), 0A (action). Unasked updates only go to a client which asked once.
- Missions and earned coins: `GameLogic/PlugIns/Missions` (engine is pure and unit-tested; the store is
  `save/missions.json`); coins are cash shop coin grants with a reference (`MissionCoinGranter`). Admin page
  `/missions-rewards`; website `/website/missions`.
- A web window can be tested without a game server: import `/js/ui/<window>.js`, construct it with fake callbacks
  and feed it packets built with `write()` from `/js/net/packets.js` (see `tests/e2e/missions.spec.ts`).

## World bosses (2026-11-15)
- `GameLogic/PlugIns/WorldBosses`: `WorldBossSchedule` (wall-clock Sydney times; a time missing on the DST day moves
  to the first valid minute, a repeated one counts once), `WorldBossEngine` (pure: plan, damage, kill, tiers, weekly
  rankings, pay-once), `WorldBossStore` (`save/world-bosses.json`, one shared instance per file in the process via
  `WorldBossStore.Open`, so the admin panel and website read the live state), `WorldBossesPlugIn` (spawns like the
  invasions, hosts each boss on the lowest game server id of the process unless set). Tests `WorldBossEngineTest`.
- F5 codes now: Discord 02-07, missions 08-0A, world bosses 0B (request; the web game sends it on entering the world),
  0C (state JSON), 0D (take waiting rewards). Next free: 0E.
- Discord posts go through `IGameServerContext.EventPublisher` with a `GameEvent` record (`WorldBossAnnouncedEvent`) and
  a case in `DiscordMessageFormatter`; nothing is posted when the bridge isn't configured.
- `MissionCoinGranter.GrantAsync(..., grantedBy)` names the source in the coins history ("HNMO world bosses").
- Admin page `/world-bosses-rankings` (spawn now uses `IList<IManageableServer>` → `IGameServerContextProvider.Context`);
  website `/website/world-bosses`; web window N (`ui/worldBossWindow.ts`). An RML `data-if` hides with display:none,
  so Playwright checks `toBeHidden()`, not `toHaveCount(0)`.
- Website copy keys must exist in both `copy.en` and `copy.vi` (website.ts), or tsc fails; a literal % in them is "%", not "%%".

## Onboarding (2026-11-15)
- `GameLogic/PlugIns/Onboarding`: `OnboardingEngine` (pure, `OnboardingEngineTest`: new characters, ordered steps counted by
  events or checked by looking (`OnboardingFacts`, every second), optional steps, skip, final reward, starter kit per character
  or account), `OnboardingStore` (`save/onboarding.json`, `OnboardingStore.Open` shared per file; its `ActivatedAtUtc` decides
  "created after the plug-in was turned on"), `OnboardingPlugIn` (rewards wait as owed entries; coins granted at once by
  `MissionCoinGranter` with "HNMO onboarding"; kit items built with `GameMasterItemFactory`, so options work).
- `INpcTalkStartedPlugIn` (new plug-in point, called by `TalkNpcAction` for every NPC): `IPlayerTalkToNpcPlugIn` only fires for
  NPCs without a window. The missions request handler tells the guide that the missions window was opened.
- F5 codes now: Discord 02-07, missions 08-0A, world bosses 0B-0D, onboarding 0E (request), 0F (state JSON), 10 (action).
  Next free: 11.
- Admin page `/onboarding-starter-kit`; website `/website/onboarding`; web panel Y (`ui/onboardingWindow.ts`), also opened by the
  (i) guide's button (`hnmo-onboarding-open` window event). In a game test, an RML window can take a minute to appear while the
  map loads: wait up to 90 s.
- vi.json keeps its texts under `"texts"`; add keys there (not at the top level).

## Monster looks (2026-11-15)
- `game/monsterData.ts` `describeMonster(number, { map })` is the web's Setup::Create: a number's own values over its
  monster's (null clears), map variants by the server map, random variants, model list or other-area models
  (HellGate, ball, cannon trap, `mapObject N`), "none". `createMonsterCharacter({ id, type, map })` builds it; pass the map.
- Player-shaped monsters: the skin (`subType` 244-246 Skeleton01-03; other skins only for players) or worn items, nothing on
  an empty slot (no default body); class index from `class` (default Dark Wizard). Combat treats `character.playerShaped`
  as a player for attacks, flinches and death; skeletons (`skeletonShape`) burst into bones.
- Characters and worn items draw with shared GPU-skinned geometry (`bmd.ts` `sharedGeometries`, `AnimatedModel({ skinned })`,
  `overlay()` for glow passes; `itemGlow`'s shader has the skinning chunks). Call `character.release()` when one leaves.
  Window/ground item models keep CPU posing (icons need posed bounds).
- `game/prewarm.ts`: `online.prepare` warms each new character; the map's monsters are pre-warmed after a map change.
- Dev viewer / audit without a server: `src/dev/monsterViewer.ts` (`auditAll`, `showRow`), used by
  `tests/e2e/monster-looks.spec.ts` (needs only the bridge: `dotnet bin/Release/net10.0/HNMO.dll --contentRoot <bridge>
  --urls http://127.0.0.1:8088`). Its in-game part: `IN_GAME=1 LOCAL_JS=$PWD/public/js BASE_URL=http://127.0.0.1:8080`
  (routes this checkout's client into the running HNMO folder's page), or the dev bridge's own relay with BASE_URL 8088.
  "The server closed the connection" at login = no game server running (check `ps aux | grep HNMO.Startup`).
- To compare GL calls with an older build: `git archive <commit> client/web/src client/web/tsconfig.json`, `tsc --outDir`,
  route `/js/**` to it, count calls with a `WebGL2RenderingContext.prototype` wrapper.

## Skill effects (2026-11-15)
- Effect models come from a pool (`modelPool` in `render/effects/effectSystem.ts`): built once, reused per cast. A
  pooled model's scrolling texture is a clone that shares the GPU texture: bump `version`, never set `needsUpdate`
  (that re-uploads the image). `EffectSystem.prewarm(warm)` builds one of each effect model and every bitmap batch
  a few seconds after entering the world (main.ts).
- `effects.groundLight / addTerrainLight` = AddTerrainLight (the terrain shader adds the tick's lights, at most 32);
  `effects.setEarthQuakeAt` = the camera shake (main.ts's updateCamera pitches the camera by `effects.earthQuake`).
- An EXTRA_EFFECTS `move` must not count `lifeTime` down itself: the shared tail does (a double countdown halves it).
- Joints can draw themselves (`ExtraJoint.draw`), lay tails with a bone matrix (`layTailMatrix`); effects can ride a
  joint (`extra.joint`) and carry CreateEffect's Skill argument (`extra.kind`, o->Kind).
- Buff looks: `render/effects/buffLooks.ts` (`debuffTint` → `Character.bodyTint`, `playBuffLook`), reached from
  main.ts's `buffs.onChange` for players and monsters. Tests set buffs with `__game.buffs.replace(id, [...])`.
- Skills timed by an action's keys read `caster.currentAction()` / `animationFrame()`; the test plays the action
  with `hero.playOnce(action)` (case `action`), else the timing counts the action's play speed per tick.
- Second pass (2026-11-15): wizardEffects.ts (Cometfall, Expansion of Wizardry and its buff), the rest went into the class
  modules. An effect type whose name is in Data/Effects/EffectTypes.json gets that row's creation values before its
  `create` hook: name new types exactly as the catalogue does (e.g. `arrowsre6`, `twlight`, `knightPlancrackB`), or the
  life time stays 0 and it never shows. Skills released at an action's key go in skillEffects.ts's KEYED_RELEASES.
  Sprite sizes match the desktop (texture width x scale; the desktop shrinks upscaled "MUX" textures back to their
  original size, the game data has none), so a very bright effect (Spiral Slash) is the desktop's own look.
  The test's `other: true` aims a skill at another character (cancels skip the caster).
- Third pass (2026-11-15): shared particles in `sharedParticles.ts` (BITMAP_EXPLOTION+1, ADV_SMOKE+1, BLUE_BLUR, LIGHTNING+1,
  the rising HIK flames and the Flame Strike debuff), the Berserker look in `berserkerEffects.ts`; Starfall, Plasma Storm and
  the cancels' skin shards in `darkLordEffects.ts`, Fire Breath in `elfEffects.ts`. Loads an effect needs before its first use
  (a model's bones, not drawn) go in `PREWARM_LOADS` (extraEffects.ts), run by `EffectSystem.prewarm`: the first cast of a
  test found the shadow body not loaded yet. A hidden effect model that must still animate (its bones lead something):
  `alwaysModel: true` (the shared tail only advances `animationFrame` when the model exists). `playSkillEffect` now gets
  `casterIsHero` and `fenrirColor` from main.ts.
- Item glow (`game/itemGlow.ts`): pass materials are shared per (mode, texture, colour, kind) and moved on the world's clock
  (`performance.now()`), never per character; icons get their own copies (`itemIcons.ts` changes their blending). Debuffs and
  the cloak: `Character.setItemLooks` (main.ts's buffs.onChange) → `setItemGlowState` / `setCloaked`. +15 decorations are
  `Attachment`s with slot `ornament` (PartModels.json). `tests/e2e/item-glow.spec.ts` builds a row of players with
  `createPlayerCharacter` and puts them in `online.characters` (ids 0x7e00+) so the game updates and draws them.
- Fourth pass (2026-11-15): `rageStrikeEffects.ts` (Chain Drive, Dragon Roar; Dragon Roar's volcanoes come per struck target
  through Combat's `onSkillHit` → `playSkillHit`, the hero's own SkillAnimation included), `summonerSummons.ts` (casting
  circles, Requiem, Pollution; `castSummon` from playSkillEffect with `summonBookLevel`), `objectBlurs.ts` (CreateObjectBlur,
  `system.objectBlurs`, census key `blur:object`). An ExtraEffect can draw its meshes in another texture (`skin`, `skins`
  for the prewarm), paint each mesh (`paint`: colour, visibility, scroll, blending — never a change that rebuilds a
  program) and name the BlendMesh its creation sets (`prewarmBlendMesh`, so the prewarm builds the pooled copy it uses).
  Owners can pose at another key (`bonePositionAt`, `boneMatrixAt`) for trails laid between ticks. Effects timed by
  an action's keys need the test's `actionSpeed` (the action played slowly and held): the test machine's few frames
  a second step over the keys otherwise. `sampleModelLight` (terrain.ts) adds the frame's effect lights to a model's
  light. Item looks: `MeshDraw.lightAt` / `scrollAt` move on the world's clock, `reflection` draws via
  `itemGlow.reflectionMaterial`. The server sends no Dark Side target list (C1 4B) or fighter attack (4A).

## Performance (2026-11-15)
- The owner's lag can't be seen on the headless SwiftShader machine: ask for a **performance report** (Options,
  Graphics, "Performance report", or Ctrl+Shift+P; 15 s; "Download" saves `hnmo-performance-*.json`). Its
  `frames.slowOutsideGame` says whether slow frames were the game's own work or the browser / driver / other programs;
  `profile.topSelf` names the functions (the package bundle keeps names: esbuild without identifier minifying).
- `__game.debug` (dev build and bundle alike): `renderer`, `createPlayerCharacter`, `createMonsterCharacter`, `drawItem`,
  `glowMaterialCount`, `benchFrames(n, { draw })` (the frame loop's parts in ms, back to back: the CPU cost without
  SwiftShader's frame rate), `setCulling(on)`. Tests should use it instead of `import('/js/...')`, which fails on the bundle.
- Per-frame code must not read layout after writes: use `ui/layoutCache.ts` `cachedRect()` for boxes that only move on
  resize. Characters out of view are culled (`Character.culled`, main.ts `cullCharacters`): their group stays visible
  (effects and arrows read `group.visible`), only its children are hidden.
- In a crowd (60 characters) the CPU cost is mostly three.js's draw calls (~8 µs each in headless); fewer meshes per
  character is the next big win.

## Rage Fighter casting (2026-11-15)
- `testgm2`'s second character (`testgm2Rf`) is a Rage Fighter with every skill (pick it with
  `#characters .choice:has-text("testgm2Rf")`). It rides a Fenrir, which blocks the close skills as on the desktop;
  `tests/e2e/rage-fighter.spec.ts` takes the horn off on the client side only (`inventory.items.delete(8)`) and reloads.
- The server already handles the fighter attack (C3 4A, `RageSkillAttackHandlerPlugIn`) and Dark Side's target list
  (C1 4B, `RageSkillAttackRequestHandlerPlugIn` → `RageSkillAttackAction` → `ShowRageAttackRangePlugIn`); the 4B
  response is 16 bytes (the desktop struct is unpacked: the skill at offset 4), ids little endian, 10000 = none.
- Headless Chromium runs a few frames a second: an action can pass its hit keys between two updates (combat.ts counts
  passed keys at the action's end), and Dark Side's dash is tested by wrapping `effects.step` to watch every tick.

## Guilds (2026-11-16)
- `GameLogic/PlugIns/Guilds`: `GuildEngine` (pure, `GuildEngineTest`: XP with a daily cap per member, levels, buffs, the weekly
  ladder with Sydney weeks incl. DST, pay-once references `hnmo-guild-week:{week}:{guild}:{member}`), `GuildStore`
  (`save/guilds.json`, `GuildStore.Open` shared per file), `GuildsPlugIn` (monster kills, mini games, castle siege, a 1-minute tick
  for online time, buffs and the Monday payout; missions and world bosses call `GuildsPlugIn.Get(context)` directly).
- A player's persistent guild: `player.GuildStatus.GuildId` (uint) → `IGameServerContext.GuildServer.GetPersistentGuildIdAsync`;
  an offline character's: `CreateNewGuildContext().GetGuildMembershipAsync(name)`.
- Server-only buffs: a `MagicEffect` with an in-memory `MagicEffectDefinition` numbered 200+ (`MagicEffectsList` sends nothing
  for those; guild buff = 240) and `ConstantElement`s (`BonusExperienceRate` AddRaw, `MoneyAmountRate` Multiplicate,
  `DefenseFinal` AddFinal). The web shows HNMO buffs on the strip with `setHnmoBuff` (game/buffs.ts; guild buff = 190).
- F5 codes now: ... onboarding 0E-10, guilds 11 (request), 12 (state JSON), 13 (take waiting rewards). Next free: 14.
- Admin page `/guilds-levels-ladder`; website `/website/guilds`; web panel opens with the guild window (G)
  (`ui/guildLevelsWindow.ts`, `SocialOptions.guildWindowToggled`).
- Tests can feed an HNMO JSON state without importing /js modules: build the C2 F5 xx bytes in Node (escape non-ASCII as
  \uXXXX like the server does) and call `__game.connection.dispatch` (see `tests/e2e/guilds.spec.ts`).
- The worktree can borrow the main checkout's `client/web/node_modules` with a symlink (same lock file; saves disk).

## Community (2026-11-16)
- `GameLogic/PlugIns/Community`: `CommunityEngine` (pure, `CommunityEngineTest`: Discord coins once per account and Discord
  user, Zalo claims approved by the admin, event codes once per account, winners once each, Sydney wall-clock dates with
  DST, drop filter, post texts, highlights), `CommunityStore` (`save/community.json`, `CommunityStore.Open` shared per file,
  the admin page writes Zalo decisions into it), `CommunityPlugIn` (pays approved Zalo claims, records winners and pays coins
  on a 1-minute tick on the lowest server id; `PostAsync` → highlights + `CommunityHighlightEvent` for Discord). World bosses
  and guilds call `CommunityPlugIn.Get(context)` for their posts; `GameEventPublisherPlugIn` leaves drop posts to it when
  its rare drops go to Discord.
- Discord linking reuses `AccountLinkService` (the bot's /link); the web window asks for a code with the Discord packets
  (F5 04 → F5 05). Zalo: claim code `Z-XXXXXX`, admin approves (no bot API for Zalo groups).
- F5 codes now: ... guilds 11-13, community 14 (request), 15 (state JSON), 16 (action). Next free: 17.
- Admin page `/community`; website `/website/community` (section `#social`); web window K (not while the inventory is
  open: K is the inventory extension there, as on the desktop), `ui/communityWindow.ts`; screenshots `ui/screenshot.ts`
  (Print Screen like the desktop: `Screen(MM_DD-HH_MM)-NNNN.jpg`; share panel via the `hnmo-share-open` window event).
- A WebGL picture without preserveDrawingBuffer is read by rendering and copying in the same task (`capture` in main.ts).
- vi.json: add keys as single lines after `"texts": {` (re-serializing the file rewrites all 2800 lines).

## Top races (2026-11-16)
- `GameLogic/PlugIns/TopRaces`: `TopRaceEngine` (pure, `TopRaceEngineTest`: periods with Sydney DST, custom wall-clock dates (a
  skipped time moves to the first valid minute, a repeated one is the first), values, ties to whoever got there first,
  per-class tables (class = character class number / 4), the end with prizes paid once, owed prizes, history),
  `TopRaceStore` (`save/top-races.json`, `TopRaceStore.Open` shared per file), `TopRacesPlugIn` (1-minute tick: online
  minutes, level/resets observed; starts, ends and payouts on the lowest server id). World bosses call
  `TopRacesPlugIn.Get(ctx)?.WorldBossKilledAsync`, guilds `GuildPointsEarned`; community posts `TopRaceStarted` / `TopRaceWinner`.
- F5 codes now: ... community 14-16, top races 17 (request), 18 (state JSON), 19 (action). Next free: 1A.
- Admin page `/top-races` (its live standings use a copy of the configuration: an unsaved edit must not end a race);
  website `/website/top-races` (section `#races`); web window **F9** (`ui/topRacesWindow.ts`): every letter key is taken by the
  desktop or HNMO (desktop leaves N, O, Y free; HNMO uses N, O (options), Y), F9 is free on the desktop and in browsers.

## Gift codes (2026-11-16)
- `GameLogic/PlugIns/GiftCodes`: `GiftCodeEngine` (pure, `GiftCodeEngineTest`), `GiftCodeStore` (`save/gift-codes.json`, shared per
  file with `GiftCodeStore.Open`; the codes themselves live there, not in the plug-in settings, so the admin page edits the store and
  calls `CodesChanged()`), `GiftCodesPlugIn` (20 s tick: pending coins on the lowest server id, waiting goods for online players),
  `GiftCodeRewards` (coin grants without a game context, goods with options). Chat command `/code` (`GiftCodeChatCommandPlugIn`).
- F5 codes now: ... top races 17-19, gift codes 1A (request), 1B (state JSON), 1C (action: 1 redeem, 2 take). Next free: 1D.
- Admin page `/gift-codes` (CSV as a data: link); website `GET /website/gift-codes` (public codes) and the account page's
  `POST /portal/gift-codes/characters|redeem` (bridge `Portal.cs`, API key; the admin API checks the account's password with
  `GetAccountByLoginNameAsync(login, password)`). The account page has no login session: the name and password go with each call.
- Web window `ui/giftCodesWindow.ts` (no hotkey: `/code` alone, community window button, touch menu 🎁). An e2e test that must not
  send test codes to the shared server records `HnmoGiftCodesAction` without forwarding it.

## Resets (2026-11-16)
- `GameLogic/PlugIns/ResetOptions`: `ResetEngine` (pure, `ResetEngineTest`: tiers, VIP, relife, Sydney daily limits, delegated
  limits, monthly ranking), `ResetStore` (`save/resets.json`, `ResetStore.Open` shared per file), `ResetApplier` (puts a plan into a
  character: in the game via the attribute system, on the website via the DB entities), `ResetOptionsPlugIn` (active by default;
  `ResetCharacterAction` hands over to it, so the old NPC and `/reset` follow the tiers; bots keep `BotResetHandler`). Delegated
  resets run on a tick over `OfflinePlayerManager.OfflinePlayers`.
- Relifes live in the store, not in a character attribute. VIP = `Stats.IsVip` from account attributes / `AccountAttributeGrant`s.
- Website changes to a character: refuse when the login server's snapshot has the account, when any game context's
  `OfflinePlayerManager.IsActive(login)` (offline leveling logs the account off the login server!), then
  `ILoginServer.TryReserveAccountAsync` around the save (`WebsiteResetsController`).
- Top races: call `TopRacesPlugIn.ObserveCharacter` before and after a change made outside the observed game players.
- F5 codes now: ... gift codes 1A-1C, resets 1D (request), 1E (state JSON), 1F (action). Next free: 20.
- Admin page `/resets-relife` uses an inline `T(en, vi)` helper instead of 100+ resource keys (only the page name is a resource).
- A Playwright check of a Vietnamese chat notice: re-feed inside `expect(...).toPass()` (vi.json loads after the click).

## Account safety (2026-11-16)
- `GameLogic/PlugIns/AccountSafety`: `AccountSafetyEngine` (pure, `AccountSafetyTest`: PINs, wrong-try lockout, item locks with the
  unlock wait, login/trade history), `AccountSafetyStore` (`save/account-safety.json`, `Open` shared per file), `AccountSafetyPlugIn`
  (sessions in a ConditionalWeakTable; `RefuseAsync(player, SafetyAction)`, `RefuseLockedItemAsync`, `RefuseDropOrSaleAsync` are called
  from MoveItemAction, SellItemToNpcAction, BuyRequestAction, TradeRequest/TradeButtonAction, ItemCraftAction, the store-open handler).
- The second password IS `Account.VaultPassword` (the desktop's 4-digit vault PIN), now a BCrypt hash ("$2..."); plain old PINs still
  match (`AccountSafetyEngine.PinMatches`). Never send an unasked VaultProtectionInformation 12 to the desktop: it then moves its
  backed-up item, which may not exist. Website/admin PIN changes write the live `player.Account` when the account is in a game of
  this process (`WebsiteAccountSafetyController.WritePinAsync`), else the DB under a login server reservation.
- Real web player addresses: the bridge's relay writes a `PROXY TCP4 …` line first (`Network/ProxyHeader.cs`), the `Listener` reads it
  only from loopback (100 ms wait). Against a server without this code run the bridge with `--Relay:SendClientAddress=false`.
- F5 codes now: ... resets 1D-1F, account safety 20 (request), 21 (state JSON), 22 (action). Next free: 23.
- In-memory persistence shares entity objects between contexts: don't set a fake value on an entity "just for the answer".

## Owner decisions of 2026-11-16 (Sydney)
See ROADMAP.md "Owner decisions (2026-11-16)": Phoenix Shot sent to the server; guild buff tooltips EN+VI;
guild ladder rewards to members at payout; Zalo coins need admin approval; screenshots: player chooses
(3D only or with the interface); K only with the bag closed; F9 opens top races; gift codes also limited
per address/device; resets in place (admin switch); reset tiers 350/380/400, 20/10/5 per day, editable;
VIP bought with coins, admin can grant, players manage it on the website's account page.

## Web only for now (owner, 2026-11-16)
No Android/iOS store apps and no desktop client: everything ships as the web game (phones use the browser or
"Add to Home Screen"). Don't plan or build native apps or desktop work unless the owner asks again.
The desktop client is dropped for now: HNMO is played through the web game. New features get web windows only.
The desktop code stays the reference for how the classic game behaves (parity rule unchanged).

## Handover and re-audit (2026-11-19)
- Roles: main Claude audits, fixes, merges into `main`, ships; Antigravity does the Windows-only work on
  `antigravity-*` branches with full prompts; the first helper stopped on 2026-11-18. See
  `docs/HANDOVER-2026-11-19.md`, `docs/WORKBOARD.md`, `docs/audit-2026-11-19.md`.
- A fresh cloud container has no .NET and no `hnmo` user: install with `dotnet-install.sh --channel 10.0
  --install-dir ~/.dotnet`, `useradd -m hnmo`, package to `/home/hnmo/HNMO`, `chown -R hnmo`, start with
  `su hnmo -c ./start-hnmo.sh` (ready in under a minute). PostgreSQL 16 and ffmpeg are there.
- A full `dotnet test` of the solution takes about 12 minutes (the initialization tests are 11 of them); run it
  in the background and never edit-and-build the same projects meanwhile.
- Tests that import `/js/...` modules or start `dotnet` (control.spec, party-options.spec, buffs-vi-language)
  only work against the developer web client (`bridge` on its own port), not against the packaged folder.
- Helpers' code: don't trust their own audits. What they got wrong most: features that take items or money
  without thinking of the trade / crafting inventory backup, a lock per wrapper over one shared file,
  `innerHTML` with player text, "local connection = the host", saved files in the program folder.
- Never name a vendor or the original game in the repository, local disk paths included.
- Only use the GitHub tools for GitHub (no direct API calls).

## Seasons (2026-11-19)
- A world has a season (`WorldDefinition.Season`, S6.3 default). The website and the game's server list show
  it; the game loads `/data/...` for S6.3 and `/data/s9/...` for S9 (`client/web/src/dataRoot.ts`,
  `bridge/SeasonData.cs`: only maps, models, effects and sounds come from the Season 9 pack in `Data_S9`).
- The server runs one game configuration for all worlds. Plan for Season 9 content: add it to that one
  configuration and switch it on per game server by its world's season (maps in the server's map list,
  season-only features behind a season check), not a second configuration or database.
- A world's rates are its servers' own rates (the per-server rates of the Servers page).

## Owner decisions 2026-11-20 (morning)
- Release (merge #85 into main, HNMO.zip): wait for local Claude's additional work, then check again with the owner.
- Vault pages: 3 free, VIP +1, buy more with coins; page buttons in the vault window. Stacks: potions, event
  tickets and keys stack free; jewels, boxes/quest items and crafting materials stack after a coin unlock; up to
  255 per slot; in the vault AND the bag. Plan: `docs/plans/vault-stacking-pages-2026-11-20.md`.
- When the owner says "give me more options", list more choices in the next pop-up instead of deciding.
- (07:10) Stack unlocks per kind, 500 WCoinC each; extra vault page 200 WCoinC; web only (desktop later).
- "MU Helper" texts: rename all to "Auto play" (VI "Tự động chơi").
- Admin path through the website: a Host setting; a fresh folder gets a random hard-to-guess path shown to the host.
- Market/escrow (audit 1.4): do the full fix, keeping the real item in a system storage (like vault pages).
- Season 9 new classes: local Claude checks the owner's Season 9 data for them.
- (08:30) Vault pages: 2 free (not 3). Anti-dupe: item origin serials + hourly scan + freeze + admin alert.
  Market: one-off access fee 1,000 WCoinC + 5% tax per sale (listing fee stays). Arena across servers/seasons:
  same template per class, all 4 modes (1v1 always; 5v5, battle royale, guild war scheduled), rewards = titles,
  arena points shop on home servers, website leaderboard (no coins). Plans: docs/plans/arena-2026-11-20.md,
  docs/plans/anti-dupe-market-2026-11-20.md.
- Messages for local Claude (owner, 2026-11-20): the owner relays them by hand and can miss some. Send ONE combined,
  copyable message that replaces all earlier unsent ones, and ask in a pop-up whether it was sent. Don't add a new
  message per small change: fold it into the next combined one.
- (09:20) The owner calls HNMO's server and game proprietary and will share them free, but not to be cracked or
  misused. Protections chosen: obfuscate HNMO's own server code in the package, refuse to start when a signed
  package's files were changed, obfuscate the web game in the package. (Not chosen: licence required to host.)
  Keep third-party license notices; game data isn't the owner's. Remind about multi-player licenses
  (license.hnmo, sign-license.sh) on 2026-11-21 09:00 Sydney (a scheduled reminder exists).
- (10:20) Release plan: ONE release with everything, the Arena (all 4 modes) included. Until then keep merging
  verified work into the working branch; main and HNMO.zip wait for the owner's OK after all of it is done.

## Vault pages and item stacks (2026-11-20)
- F5 codes: vault pages 4E (command: Action, Page) / 4F (state JSON); item stacks 50 (command: Action 0 state,
  1 split, 2 unlock; Storage 0 bag / 1 vault; Slot; Count; Kind) / 51 (state JSON). Next free: 52.
- Stack limits come from `HNMO.DataModel.ItemExtensions.StackLimit` (set by `ItemStackRules.Configure`); the player is
  `ItemStackRules.CurrentPlayer` (set in `RemotePlayer.PacketReceivedAsync`). Kinds in `ItemStackConfiguration`.
- test300 has no WCoin (C): browser tests can't buy unlocks with it. They check the refusal ("needs 500") and use free
  kinds (potions); paid paths are covered by server tests (`ItemStackRulesTest`).

## Database changes (2026-11-20)
- New column: add the property to `DataModel/Entities/*.cs` (and to `AssignValues` if copies keep it), build, then
  `dotnet ef migrations add <Name> --context EntityDataContext --configuration Release --no-build` in
  `server/src/Persistence/EntityFramework`, and the compiled models with
  `dotnet ef dbcontext optimize -c <AccountContext|TradeContext|EntityDataContext> -o CompiledModels/<ctx> -n HNMO.Persistence.EntityFramework.CompiledModels.For<ctx> --startup-project ../EntityFramework.DesignTime --configuration Release --no-build`
  (the DesignTime project keeps the fully qualified names; without it every file changes). Then
  `HNMO_UPDATE_GENERATED_CODE=1 dotnet test tests/HNMO.Tests --filter FullyQualifiedName~GameConfigurationLoaderGeneratorTest`.
  Strip the BOM the tool adds and keep `[DbContext(typeof(HNMO.Persistence.EntityFramework.EntityDataContext))]`.
  The tool leaves a folder literally named `bin\Release` in EntityFramework.DesignTime: delete it, never commit it.
- A live PostgreSQL for tests: `su hnmo -c "initdb -D /home/hnmo/pgtest -U postgres --auth=trust; pg_ctl -D /home/hnmo/pgtest -o '-p 5432 -k /tmp' start"`,
  then the server from `server/src/Startup/bin/Release` with `-reinit -autostart` (its ConnectionSettings use port 5432).
  Plug-ins that are off by default can be switched on with SQL on `config."PlugInConfiguration"` ("IsActive").
- Duplicate check: `Item.Serial`, `DuplicateItemsPlugIn` (hourly), admin page `/duplicate-items`; frozen items are refused
  by `AccountSafetyPlugIn.RefuseLockedItemAsync` and the item move.
- Market (owner 2026-11-20): one market per season; fees per account per season: buying 200 WCoinC, selling 1,000
  WCoinC, 5% tax paid by the seller; the website's market has a Season 6 / Season 9 choice. Friends work across the
  servers of one season; friends across seasons come with the Arena (owner's choice).
- F5 codes: Arena travel 52 (request: Action 0 state, 1 travel, 2/3 join/leave the 1v1 queue, 4 buy shop item `Index`) / 53
  (state JSON: ticket, `ranked` in the Arena, `shop` at home). Next free: 56 (54/55 are bug reports). Arena points and ratings live in the shared
  database (`ArenaShared.Current`, table `data."HnmoArenaAccount"`); test them by setting points with SQL.
- Bug reports (owner 2026-11-20): F5 54/55 (next free: 56), Alt+B / 🐞, admin page `/bug-reports`. Reports reach the
  owner (HNMO developer) through the owner's own Discord webhook, which each server owner pastes into the plug-in's
  settings; no screenshots; rewards are each server owner's choice (off by default).
- Developer hub (owner 2026-11-20, `docs/plans/developer-hub-2026-11-20.md`): host bug reports go to GitHub like the
  owner's other game (web function + hourly Action → issues in a private repo, one per bug); licence in Host settings;
  a Developer page only where the private key is; unlicensed servers may report with a daily limit; bug robot later.
- Update plug-ins that read saved plug-in settings must drop the admin editor's "$id"/"$values" markers first (a
  saved list is `{"$values": [...]}`); an update that throws stops the server from starting.
  The Arena program: `HNMO_SEASON=arena` (ports +200, database `hnmo_arena`, save `save/arena/`, admin panel 8097); start
  it next to Season 6.3 for tests like Season 9 (`ASPNETCORE_URLS=http://127.0.0.1:8097`), and a bridge with
  `--Launcher:ArenaEnabled=true` to see the Arena column.
