// HNMO audio: sound effects, music, ambient loops and monster sounds for the web game, ported
// from the desktop client (client/src/source/Audio, Core/Platform/Audio/MiniAudioBackend.cpp,
// Scenes/SceneManager.cpp, GameLogic/Monsters/MonsterSoundEvents.cpp).
//
// ============================================================================================
// API
// ============================================================================================
//
//   import { audioSystem, heroFootstepSound, ... } from './audio/index.js';
//   const audio = audioSystem();        // one per page; listens for the first click/key to unlock
//   await audio.init();                 // optional: loads Data/Audio/SoundFiles.json up front
//
// AudioSystem (audioSystem.ts)
//   play(id, { at?, anchored?, loop?, volume?, channels? })
//       id: a SOUND_* id of SoundFiles.json or a file ("Data/Sound/mBull1.wav").
//       PlayBuffer's rules: round robin over the id's channels (max copies at once); a sound with
//       one channel, `anchored` (PlayBuffer with an object) or `loop` does not restart while it
//       plays. `at` (MU units): a map object's sound, free channel only, volume by distance from
//       the listener (distanceLevel: 1 within 300, down to 0.25 at 1500+). Missing or bad files
//       are silent. A one-shot still loading more than 0.6 s after the call is dropped.
//   stop(id) · stopAll() · isPlaying(id) · preload(ids)
//   setListener({x,y,z}, facingDegrees)   hero position for `at` (the desktop pans nothing)
//   playMusic(file, force?) · stopMusic(file?) · currentMusic
//       one looping track; same track = no-op; stopMusic(file) only stops that track.
//   applyMusicCalls(backgroundMusicCalls(state))   the desktop's per-frame music rule
//   applySoundCalls(ambientCalls(state))           the desktop's per-frame ambient loops
//   setMasterVolume(0..1) · setEffectsLevel(0..10) · setMusicLevel(0..10) · setMuted(bool)
//   getSettings() / setSettings({...})   saved in localStorage "hnmo.audio" (defaults 1, 5, 5, false)
//   unlock()                              call from a click handler if needed (automatic otherwise)
//
// Map audio (mapAudio.ts)
//   backgroundMusicCalls(state) · musicForMap(world, inSafeZone, heroTile, heroX, heroY)
//   loginMusic(creditsShown) · ambientCalls(state, rand) · ambientForMap(world, heroTile, raining)
//   loadMapObjectSounds() · mapObjectSound(serverMap, objectType) · mapWideSounds(serverMap)
//   Music.* track files · World.* map numbers
//   state = { world, safeZone, heroTile (TerrainMapping layer1[y*256+x]), heroX, heroY, raining?,
//             crywolfState?, kanturuState?, cursedTempleState?, raklionBossMusic?,
//             doppelGangerEnabled?, battleCastleStarted? }
//
// The music rule (ManageBackgroundMusic, every frame of the main scene):
//   Lorencia (0): in the safe zone the main theme, or the pub track on ground tile 4; walking out
//     of the safe zone does NOT stop it (only leaving Lorencia does). Devias (2): in the safe zone
//     Devias, or Church inside tiles x 205-214, y 13-31. Noria (3): in the safe zone. Dungeon (1)
//     and map 5: Dungeon. Atlans 7, Icarus 10, Tarkan 8, Lost Tower 4 (track A), Kalima 24-29 and
//     36, Hunting Ground 31, Aida 33, Kanturu 37/38, Elbeland 51, Swamp 56, Raklion 57 (+58 boss
//     music when on), PK Field 63, Karutan 80/81, Balgas 41/42: their track anywhere on the map,
//     stopped when leaving it. Crywolf 34, Kanturu 39, Illusion Temple 45-50, Doppelganger 65-68,
//     Battle Castle 30: by event state. Empire Guardian 69-72: one shared track. Santa Town 62
//     and Duel Arena 64 play theirs and never stop it. Other maps (Devil Square, Blood Castle,
//     Chaos Castle, Market Place...) ask for nothing: the previous track goes on.
//   Login screen: login_theme (MuTheme while the credits show); the character select keeps it;
//   entering the world (LoadingScene) stops it: audio.stopMusic(Music.LOGIN_THEME).
//
// Sound events (soundEvents.ts, Data/Audio/SoundEvents.json)
//   loadSoundEvents() · pickEventSound(event, context, rand)
//   heroFootstepSound(world, tile, safeZone) · attackSwingSound(weapon) · heldWeaponOf(right, left, bowType)
//   interfaceClickSound(kind) · characterSelectedSound()
//
// Monster sounds (monsterSounds.ts, Data/Monsters/Sounds.json)
//   await loadMonsterSounds()
//   monsterSound(number, 'stand'|'walk'|'attack'|'hit'|'death', rand, animation?) -> file | null
//   monsterSoundPlays(number, { moment?, animation?, frame?, drawn?, heroDistance?, serverMap?, ... }, rand)
//       -> [{ file, channels, atMonster }]; play each with audio.play(file, { channels, anchored: atMonster })
//   monsterSoundSetName(number, model?) · monsterSoundFiles(number)
//
// ============================================================================================
// Where the desktop plays what (for wiring; ids are SoundFiles.json SOUND_* ids)
// ============================================================================================
//
// Walking (hero only)       heroFootstepSound(world, HeroTile, safeZone) at walk/run animation
//                           frames 1.5 and 4.5, not flying; play(id). PlayWalkSound / MoveCharacter
//                           (Engine/Object/ZzzCharacter.cpp).
// Normal attack, player     attackSwingSound(heldWeaponOf(...)) at frame 0 of the attack; play(id,
//                           {anchored:true}): bow SOUND_BOW01, crossbow SOUND_CROSSBOW01, bow items
//                           13-14 SOUND_MAGIC, Light Saber/Spear SOUND_BRANDISH_SWORD03, other weapon
//                           SOUND_BRANDISH_SWORD01|02, bare hands nothing. SetPlayerAttack.
//                           A skeleton-shaped player: SOUND_BRANDISH_SWORD01|02 at random.
// Getting hit, player       SetPlayerShock frame 0, play(id,{anchored:true}): male
//                           SOUND_HUMAN_SCREAM01..03 at random (Dark Lord: SOUND_DARKLORD_PAIN 4 in
//                           5), female SOUND_FEMALE_SCREAM01|02. Skeleton shape: SOUND_BONE1.
// Death, player             SetPlayerDie frame 0: male SOUND_HUMAN_SCREAM04 (Dark Lord
//                           SOUND_DARKLORD_DEAD), female SOUND_FEMALE_SCREAM02, + SOUND_FENRIR_DEATH
//                           on a Fenrir. In Chaos Castle SOUND_CHAOS_MOB_BOOM01|02 instead.
// Monster idle              SetPlayerStop: monsterSoundPlays(n,{moment:'stand',animation:'stop1'});
//                           end of each stop1/stop2 loop: {animation:'stop1'|'stop2'}.
// Monster walk              SetPlayerWalk each step: {moment:'walk', animation:'walk'} (the sets'
//                           idle events, one in 16).
// Monster attack            SetPlayerAttack frame 0: {moment:'attack', animation:'attack1'...}.
// Monster hit               SetPlayerShock frame 0: {moment:'hit', animation:'shock'}.
// Monster death             SetPlayerDie frame 0: {moment:'death', animation:'die'}.
// Monster drawn frames      UpdateDrawnCharacterEffects: {drawn:true, animation, frame, oncePlayed}.
// Melee hit sparks          SOUND_ATTACK_MELEE_HIT1+5.. (ZzzEffect.cpp) - effect code, not ported.
// Item picked up            ReceiveGetItem (Network/Server/WSclient.cpp): jewel SOUND_JEWEL01,
//                           Gemstone SOUND_JEWEL02, other item or zen SOUND_GET_ITEM01; anchored.
// Item dropped (lands)      CreateItemDrop (Engine/Object/ZzzObject.cpp), a fresh drop: jewel
//                           SOUND_JEWEL01, Gemstone SOUND_JEWEL02, else SOUND_DROP_ITEM01 (anchored at
//                           the item); zen CreateMoneyDrop: SOUND_DROP_GOLD01.
// Hero sits / poses on an   Action (Engine/Object/ZzzInterface.cpp), arriving at a sit or pose
//   object                  spot: SOUND_DROP_ITEM01, anchored at the hero.
// Potion / food used        CInventoryActionController::TryConsumeItem: apple SOUND_EAT_APPLE01,
//                           potions SOUND_DRINK01 (UI/Inventory/InventoryActionController.cpp).
// Buy / sell / equip        ReceiveBuyExtended, ReceiveSell, ReceiveEquipmentItemExtended:
//                           SOUND_GET_ITEM01. Repair: ReceiveRepair SOUND_REPAIR.
// Low life                  MainFrameWindow: SOUND_HEART each update below 20% life (one channel,
//                           so it repeats as it ends).
// Level up                  ReceiveLevelUp: SOUND_LEVEL_UP (also ReceiveDisplayEffectViewport).
// Class change              SOUND_CHANGE_UP (WSclient quest rewards).
// Whisper received          SOUND_WHISPER (ReceiveChat whisper, ChatLogWindow).
// NPC talk / window open    ReceiveTalk: SOUND_CLICK01 + SOUND_INTERFACE01; UIManager SOUND_INTERFACE01.
// UI click                  interfaceClickSound(kind) -> SOUND_CLICK01 (RmlClickSounds.cpp); many
//                           old windows PlayBuffer(SOUND_CLICK01) directly. Character select click:
//                           characterSelectedSound().
// Entering a map            no one-shot sound: music and ambience follow from the per-frame
//                           backgroundMusicCalls / ambientCalls; map object sounds from
//                           mapObjectSound for each drawn object.
// Teleport                  CreateTeleportBegin / CreateTeleportEnd: SOUND_MAGIC.
// Skills (hard-coded, not data-driven): the sound plays where the cast is received or its effect
//   starts. ReceiveMagic (WSclient.cpp): Falling Slash SOUND_SKILL_SWORD1, Lunge SOUND_SKILL_SWORD2,
//   Uppercut SOUND_SKILL_SWORD3, Cyclone/Slash/Power Slash SOUND_SKILL_SWORD4, Fire Burst
//   SOUND_ATTACK_FIRE_BUST, Combo SOUND_COMBO, Nova start SOUND_NUKE1, Lightning SOUND_THUNDER01,
//   Infinity Arrow SOUND_INFINITYARROW, Recover SOUND_SKILL_RECOVER, Teleport Ally
//   SOUND_TELEKINESIS. AttackEffect (ZzzCharacter.cpp): Energy Ball / Power Wave SOUND_MAGIC,
//   Fire Ball / Meteor SOUND_METEORITE01, Flame SOUND_FLAME, Ice SOUND_ICE, Twister SOUND_STORM,
//   Evil Spirit SOUND_EVIL, Hell Fire SOUND_HELLFIRE, Aqua Flash SOUND_FLASH, Ice Storm
//   SOUND_SUDDEN_ICE1, Nova SOUND_NUKE2, Death Stab SOUND_SKILL_SWORD2, Impale SOUND_RIDINGSPEAR,
//   Penetration SOUND_PIERCING, Fire Scream SOUND_FIRE_SCREAM, Chain Lightning
//   SOUND_SKILL_CHAIN_LIGHTNING, Drain Life SOUND_SKILL_DRAIN_LIFE, Greater Fortitude / Swell Life
//   SOUND_SWELLLIFE (MoveCharacter), Soul Barrier SOUND_SOULBARRIER, Critical Damage SOUND_CRITICAL.
//   Arrows: CreateArrow SOUND_BOW01 (SOUND_ICEARROW for Ice Arrow). Check the C++ case for the
//   exact skill numbers before wiring each.

export {
  AudioSystem,
  audioSystem,
  dataUrl,
  distanceLevel,
  MAX_CHANNEL,
  MAX_VOLUME_LEVEL,
  DEFAULT_VOLUME_LEVEL,
} from './audioSystem.js';
export type { AudioSettings, MusicCall, PlayOptions, SoundCall, Vec3 } from './audioSystem.js';

export {
  ambientCalls,
  ambientForMap,
  backgroundMusicCalls,
  loadMapObjectSounds,
  loginMusic,
  mapObjectSound,
  mapWideSounds,
  Music,
  musicForMap,
  World,
} from './mapAudio.js';
export type { MapAudioState, MapObjectSound } from './mapAudio.js';

export {
  attackSwingSound,
  characterSelectedSound,
  heldWeaponOf,
  heroFootstepSound,
  interfaceClickSound,
  loadSoundEvents,
  pickEventSound,
} from './soundEvents.js';
export type { HeldItem, HeldWeapon, SoundEventContext } from './soundEvents.js';

export {
  HEARING_DISTANCE,
  loadMonsterSounds,
  monsterSound,
  monsterSoundFiles,
  monsterSoundPlays,
  monsterSoundSetName,
} from './monsterSounds.js';
export type { MonsterSoundMoment, MonsterSoundPlay, MonsterSoundSituation } from './monsterSounds.js';
