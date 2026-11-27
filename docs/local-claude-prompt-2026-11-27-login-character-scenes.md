# Prompt for local Claude: login, server select and character select like the desktop client (27 Nov 2026, Sydney)

```
From main cloud Claude, Fri 27 Nov (Sydney). Owner: the web game's login, server choice and character choice are plain
HTML (a form, a list of servers, a list of names). The original client shows a 3D login scene, a server window, then a
3D character stage where the account's characters stand and are clicked. Make the web match it (PARITY.md section 1:
"Login and character screens with the original 3D scenes and UI ⬜"). Rules: CLAUDE.md (HNMO_DEVELOPER_HUB=off in test
runs), strict TypeScript, the desktop is the reference (read its code and data; never name the original game or its
publisher in anything pushed), draft PRs into claude/hnmo-main-handover-54co2m, never main, Sydney times. Main merges.

## Desktop reference (read first)
- Scenes: client/src/source/Scenes/LoginScene.cpp (+ LoginSceneOverlay.cpp: logo, copyright lines), CharacterScene.cpp,
  Scenes/SceneManager.cpp; UI: UI/Windows/LoginMainWin.cpp, Character/CharMakeWin.cpp (create window with the 3D class
  preview), Network/Reconnect/*.
- RML documents (already shown by the web with ui/rml.ts): client/src/bin/Data/Interface/RmlUi/login_main.rml,
  login_scene.rml, server_select.rml, char_sel_main.rml, char_make.rml (+ themes/legacy|classic|modern).
- Worlds: WD_73NEW_LOGIN_SCENE = Data/World74, WD_74NEW_CHARACTER_SCENE = Data/World75 (both in the repo; the old
  login world 55 / Data/World56 is not). The login camera flies along WALK_PATHS (LoginCameraState); the character scene
  has its own camera, lighting (ApplySelectedCharacterLighting), the selected character's effects, click picking via
  CameraProjection::ScreenToWorldRay, and sounds (login theme, characterSelected click: already in audio/).

## What the web has to reuse
- client/web/src/world.ts + terrain.ts (map loading: `World${n}` folders), createPlayerCharacter / createMonsterCharacter
  (game/...), the Character class with animations and worn items, ui/rml.ts (RML windows, tokens, data bindings), the
  audio system. main.ts today: login() / fetchCharacters() / enterWorld(); index.html #login (#loginForm, #servers,
  #characters, language picker, login news, the (i) guide link); loginWithTicket (account page) must keep working.

## Plan (small steps; one draft PR each, each tested in the real packaged game)
1. CHARACTER STAGE first (the biggest visible gap): after the server is chosen and the account's characters arrive,
   load World75, put each character (class, level, worn items as the server lists them) on its stage spot with the idle
   animation, camera and lighting as CharacterScene; hover/click selects (click sound), the selected one plays its
   selection effect/animation; the window from char_sel_main.rml (Create, Connect, Delete, menu) with the same states
   (create disabled when the slots are full, connect disabled with none selected); Delete asks the account's second
   password / PIN like the desktop; Create opens char_make.rml with the 3D class preview and the class unlock rules the
   web already has (net/session.ts createCharacter, CLASSES).
2. LOGIN SCENE: World74 behind the login, the camera tour, the logo and copyright overlay (login_scene.rml), the login
   window from login_main.rml (account, password, language picker, news, guide link kept), then server_select.rml (server
   groups, rows with the load gauge from the server list; Season 6.3 / Season 9 / Arena groups as the web's list has them).
3. Polish: sounds and music of both scenes, touch (tap to select, the window sized for phones), "Reconnect" like
   Network/Reconnect, a setting "Plain login screen" (and an automatic fallback when WebGL isn't available or the data
   is still loading) that keeps today's HTML forms working, loading progress for World74/75, no layout break below 360 px.

## Don't break
- ~40 e2e specs log in through the plain DOM (`#servers .choice`, `#characters .choice:has-text(...)`, helpers in
  client/web/tests/e2e/). Either keep those elements as hidden, working hooks for tests, or change the shared helper and
  every spec in the same PR; the "Web game tests" workflow must stay green (known flaky specs aside).
- The account page / website ticket login, account auto-login after a test browser closes, the server list answer when
  only one server exists (skip the server window as the desktop does).
- Performance: load World75 first; share the characters' GPU-skinned geometry as in-game; release it on entering the world.

## Report on each PR
What ran, what was verified and what wasn't, with screenshots of the login scene, server window and character stage
next to the desktop's if you can run it (headless Chromium with --use-gl=angle --use-angle=swiftshader is fine for the
web), Sydney times. Update PARITY.md section 1 and guide.html when a step works.
```
