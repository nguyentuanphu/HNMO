// HNMO map music, ambient loops and map object sounds, ported from the desktop client:
//  - music: ManageBackgroundMusic (client/src/source/Scenes/SceneManager.cpp) with the maps'
//    PlayBGM / ChangeBackGroundMusic (World/GameMaps/*.cpp, GameLogic/Events/w_CursedTemple.cpp,
//    GameLogic/Quests/GM3rdChangeUp.cpp), track names from Core/Globals/_enum.h;
//  - ambient loops: PlayWorldAmbientSounds and StopInactiveAmbientSounds (SceneManager.cpp);
//  - map object sounds: Data/Maps/ObjectSounds.json (World/MapInfra/MapObjectSounds.cpp).
//
// The desktop runs these every frame of the main scene as a list of PlayMp3 / StopMp3 and
// PlayBuffer / StopBuffer calls, and the calls are what makes the rule: a track keeps playing
// until something stops it or plays another. Lorencia's theme, for one, plays when the hero is
// in the safe zone and goes on when he walks out (only leaving Lorencia stops it); Santa Town's
// and the Duel Arena's tracks are never stopped. So this file returns the calls, and
// AudioSystem.applyMusicCalls / applySoundCalls run them with the desktop's rules (asking for
// the track that plays does nothing; a stop stops only the track it names).
//
// "world" is the client's map number (WD_*), the same as the server's map number for the maps.

import type { MusicCall, SoundCall } from './audioSystem.js';

/** The music tracks (Core/Globals/_enum.h MUSIC_*), as files under Data/Music. */
export const Music = {
  PUB: 'Data/Music/Pub.mp3',
  MUTHEME: 'Data/Music/MuTheme.mp3',
  CHURCH: 'Data/Music/Church.mp3',
  DEVIAS: 'Data/Music/Devias.mp3',
  NORIA: 'Data/Music/Noria.mp3',
  DUNGEON: 'Data/Music/Dungeon.mp3',
  ATLANS: 'Data/Music/atlans.mp3',
  ICARUS: 'Data/Music/icarus.mp3',
  TARKAN: 'Data/Music/tarkan.mp3',
  LOSTTOWER_A: 'Data/Music/lost_tower_a.mp3',
  LOSTTOWER_B: 'Data/Music/lost_tower_b.mp3',
  KALIMA: 'Data/Music/kalima.mp3',
  CASTLE_PEACE: 'Data/Music/castle.mp3',
  CASTLE_BATTLE_START: 'Data/Music/charge.mp3',
  CASTLE_BATTLE_ING: 'Data/Music/lastend.mp3',
  BC_HUNTINGGROUND: 'Data/Music/huntingground.mp3',
  BC_AIDA: 'Data/Music/Aida.mp3',
  BC_CRYWOLF_1ST: 'Data/Music/crywolf1st.mp3',
  CRYWOLF_READY: 'Data/Music/crywolf_ready-02.ogg',
  CRYWOLF_BEFORE: 'Data/Music/crywolf_before-01.ogg',
  CRYWOLF_BACK: 'Data/Music/crywolf_back-03.ogg',
  MAIN_THEME: 'Data/Music/main_theme.mp3',
  KANTURU_1ST: 'Data/Music/kanturu_1st.mp3',
  KANTURU_2ND: 'Data/Music/kanturu_2nd.mp3',
  KANTURU_MAYA_BATTLE: 'Data/Music/KanturuMayaBattle.mp3',
  KANTURU_NIGHTMARE_BATTLE: 'Data/Music/KanturuNightmareBattle.mp3',
  KANTURU_TOWER: 'Data/Music/KanturuTower.mp3',
  BALGAS_BARRACK: 'Data/Music/BalgasBarrack.mp3',
  BALGAS_REFUGE: 'Data/Music/BalgasRefuge.mp3',
  CURSEDTEMPLE_WAIT: 'Data/Music/cursedtemplewait.mp3',
  CURSEDTEMPLE_GAME: 'Data/Music/cursedtempleplay.mp3',
  ELBELAND: 'Data/Music/elbeland.mp3',
  LOGIN_THEME: 'Data/Music/login_theme.mp3',
  SWAMP_OF_QUIET: 'Data/Music/SwampOfCalmness.mp3',
  RAKLION: 'Data/Music/Raklion.mp3',
  RAKLION_BOSS: 'Data/Music/Raklion_Hatchery.mp3',
  SANTA_TOWN: 'Data/Music/Santa_Village.mp3',
  DUEL_ARENA: 'Data/Music/DuelArena.mp3',
  PK_FIELD: 'Data/Music/PK_Field.mp3',
  EMPIRE_GUARDIAN: 'Data/Music/ImperialGuardianFort.mp3',
  DOPPELGANGER: 'Data/Music/iDoppelganger.mp3',
  KARUTAN1: 'Data/Music/Karutan_A.mp3',
  KARUTAN2: 'Data/Music/Karutan_B.mp3',
} as const;

/** The maps the rules name (World/MapInfra/MapManager.h ENUM_WORLD). */
export const World = {
  LORENCIA: 0, DUNGEON: 1, DEVIAS: 2, NORIA: 3, LOST_TOWER: 4, UNKNOWN: 5, ATLANS: 7, TARKAN: 8,
  DEVIL_SQUARE: 9, ICARUS: 10, BATTLE_CASTLE: 30, HUNTING_GROUND: 31, AIDA: 33, CRYWOLF: 34,
  KALIMA_7: 36, KANTURU_1: 37, KANTURU_2: 38, KANTURU_3: 39, BALGAS_BARRACK: 41, BALGAS_REFUGE: 42,
  ELBELAND: 51, SWAMP_OF_QUIET: 56, RAKLION: 57, RAKLION_BOSS: 58, SANTA_TOWN: 62, PK_FIELD: 63,
  DUEL_ARENA: 64, MARKET_PLACE: 79, KARUTAN1: 80, KARUTAN2: 81,
} as const;

const inKalima = (world: number): boolean => (world >= 24 && world <= 29) || world === World.KALIMA_7;
const inCursedTemple = (world: number): boolean => world >= 45 && world <= 50;
const inDoppelGanger = (world: number): boolean => world >= 65 && world <= 68;
/** IsEmpireGuardian1..4; the fourth also counts the new login and character scenes (73, 74). */
const empireGuardianOf = (world: number): number => (world >= 69 && world <= 72 ? world - 68 : world === 73 || world === 74 ? 4 : 0);

/** What the music and ambience rules read, as the desktop knows it each frame. */
export interface MapAudioState {
  /** The map (WD_*). */
  world: number;
  /** Hero->SafeZone. */
  safeZone: boolean;
  /** HeroTile: the hero's ground texture, TerrainMappingLayer1[y * 256 + x]. */
  heroTile: number;
  /** The hero's tile (Hero->PositionX / PositionY). */
  heroX: number;
  heroY: number;
  /** RainCurrent > 0 (Lorencia's rain loop). */
  raining?: boolean;
  /** Raklion: the boss map's own music is on (CGM_Raklion::m_bMusicBossMap). */
  raklionBossMusic?: boolean;
  /** Crywolf's event state (M34CryWolf1st::m_CrywolfState). */
  crywolfState?: 'none' | 'notify2' | 'ready' | 'start' | 'end';
  /** Kanturu 3rd's state (g_Direction.m_CKanturu.m_iKanturuState). */
  kanturuState?: 'none' | 'standby' | 'mayaBattle' | 'nightmareBattle';
  /** Illusion Temple's state (CursedTemple::m_CursedTempleState). */
  cursedTempleState?: 'none' | 'wait' | 'ready' | 'play';
  /** Doppelganger: the event runs (g_pDoppelGangerFrame->IsDoppelGangerEnabled). */
  doppelGangerEnabled?: boolean;
  /** Battle Castle: the siege runs (IsBattleCastleStart). */
  battleCastleStarted?: boolean;
}

/**
 * The PlayMp3 / StopMp3 calls of one frame of ManageBackgroundMusic (with the maps' PlayBGM),
 * in the desktop's order. Run them with AudioSystem.applyMusicCalls each frame, or whenever the
 * map, the safe zone, the hero's tile or an event state changes.
 */
export function backgroundMusicCalls(state: MapAudioState): MusicCall[] {
  const calls: MusicCall[] = [];
  const play = (file: string): void => { calls.push({ play: file }); };
  const stop = (file: string): void => { calls.push({ stop: file }); };
  const playOnly = (when: boolean, file: string): void => { if (when) play(file); else stop(file); };
  const { world, safeZone } = state;

  if (world === World.LORENCIA) {
    if (safeZone) play(state.heroTile === 4 ? Music.PUB : Music.MAIN_THEME);
  } else {
    stop(Music.PUB);
    stop(Music.MAIN_THEME);
  }
  if (world === World.DEVIAS) {
    if (safeZone) {
      const inChurch = state.heroX >= 205 && state.heroX <= 214 && state.heroY >= 13 && state.heroY <= 31;
      play(inChurch ? Music.CHURCH : Music.DEVIAS);
    }
  } else {
    stop(Music.CHURCH);
    stop(Music.DEVIAS);
  }
  if (world === World.NORIA) {
    if (safeZone) play(Music.NORIA);
  } else {
    stop(Music.NORIA);
  }
  playOnly(world === World.DUNGEON || world === World.UNKNOWN, Music.DUNGEON);
  playOnly(world === World.ATLANS, Music.ATLANS);
  playOnly(world === World.ICARUS, Music.ICARUS);
  playOnly(world === World.TARKAN, Music.TARKAN);
  playOnly(world === World.LOST_TOWER, Music.LOSTTOWER_A);
  playOnly(inKalima(world), Music.KALIMA);
  playOnly(world === World.HUNTING_GROUND, Music.BC_HUNTINGGROUND);
  playOnly(world === World.AIDA, Music.BC_AIDA);

  // M34CryWolf1st::ChangeBackGroundMusic
  if (world === World.CRYWOLF) {
    switch (state.crywolfState ?? 'none') {
      case 'notify2': stop(Music.BC_CRYWOLF_1ST); play(Music.CRYWOLF_BEFORE); break;
      case 'ready': stop(Music.CRYWOLF_BEFORE); play(Music.CRYWOLF_READY); break;
      case 'start': stop(Music.CRYWOLF_READY); play(Music.CRYWOLF_BACK); break;
      // The end plays SOUND_CRY1ST_SUCCESS / SOUND_CRY1ST_FAILED (a sound, not music).
      case 'end': stop(Music.CRYWOLF_READY); stop(Music.CRYWOLF_BACK); break;
      default: play(Music.BC_CRYWOLF_1ST); break;
    }
  } else {
    stop(Music.BC_CRYWOLF_1ST);
  }
  // M39Kanturu3rd::ChangeBackGroundMusic
  if (world === World.KANTURU_3) {
    const kanturu = state.kanturuState ?? 'none';
    if (kanturu === 'mayaBattle' || kanturu === 'standby') {
      stop(Music.KANTURU_TOWER); play(Music.KANTURU_MAYA_BATTLE);
    } else if (kanturu === 'nightmareBattle') {
      stop(Music.KANTURU_TOWER); stop(Music.KANTURU_MAYA_BATTLE); play(Music.KANTURU_NIGHTMARE_BATTLE);
    } else {
      stop(Music.KANTURU_MAYA_BATTLE); stop(Music.KANTURU_NIGHTMARE_BATTLE); play(Music.KANTURU_TOWER);
    }
  } else {
    stop(Music.KANTURU_MAYA_BATTLE);
    stop(Music.KANTURU_NIGHTMARE_BATTLE);
    stop(Music.KANTURU_TOWER);
  }
  playOnly(world === World.KANTURU_1, Music.KANTURU_1ST);
  playOnly(world === World.KANTURU_2, Music.KANTURU_2ND);
  // CGM3rdChangeUp::PlayBGM
  if (world === World.BALGAS_BARRACK) play(Music.BALGAS_BARRACK);
  else if (world === World.BALGAS_REFUGE) play(Music.BALGAS_REFUGE);
  else { stop(Music.BALGAS_BARRACK); stop(Music.BALGAS_REFUGE); }
  // CursedTemple::PlayBGM, only in the temple (leaving it stops nothing).
  if (inCursedTemple(world)) {
    const temple = state.cursedTempleState ?? 'none';
    if (temple === 'wait') play(Music.CURSEDTEMPLE_WAIT);
    else if (temple === 'ready' || temple === 'play') { stop(Music.CURSEDTEMPLE_WAIT); play(Music.CURSEDTEMPLE_GAME); }
    else { stop(Music.CURSEDTEMPLE_WAIT); stop(Music.CURSEDTEMPLE_GAME); }
  }
  playOnly(world === World.ELBELAND, Music.ELBELAND);
  playOnly(world === World.SWAMP_OF_QUIET, Music.SWAMP_OF_QUIET);
  // CGM_Raklion::PlayBGM
  playOnly(world === World.RAKLION, Music.RAKLION);
  playOnly(world === World.RAKLION_BOSS && state.raklionBossMusic === true, Music.RAKLION_BOSS);
  // Santa Town and the Duel Arena play their track and never stop it.
  if (world === World.SANTA_TOWN) play(Music.SANTA_TOWN);
  playOnly(world === World.PK_FIELD, Music.PK_FIELD);
  // CGMDoppelGanger1::PlayBGM: in its maps, while the event runs.
  if (inDoppelGanger(world)) {
    if (state.doppelGangerEnabled === true) play(Music.DOPPELGANGER);
    else stop(Music.DOPPELGANGER);
  }
  // GMEmpireGuardian1..4::PlayBGM: one track, stopped only when leaving them all.
  if (empireGuardianOf(world) !== 0) play(Music.EMPIRE_GUARDIAN);
  else stop(Music.EMPIRE_GUARDIAN);
  // CGMKarutan1::PlayBGM
  playOnly(world === World.KARUTAN1, Music.KARUTAN1);
  playOnly(world === World.KARUTAN2, Music.KARUTAN2);
  if (world === World.DUEL_ARENA) play(Music.DUEL_ARENA);
  // Battle Castle (MoveBattleCastleObjectSetting, checked every 30 minutes on the desktop).
  if (world === World.BATTLE_CASTLE) {
    if (state.battleCastleStarted === true) { stop(Music.CASTLE_PEACE); play(Music.CASTLE_BATTLE_START); }
    else { stop(Music.CASTLE_BATTLE_START); play(Music.CASTLE_PEACE); }
  }
  return calls;
}

/**
 * The track the desktop asks for on a map in this situation, or null when it asks for none
 * (then whatever plays goes on, unless the map's rule stops it: see backgroundMusicCalls,
 * which is the full rule). Event maps use their default state.
 */
export function musicForMap(world: number, inSafeZone = false, heroTile = 0, heroX = 0, heroY = 0): string | null {
  const calls = backgroundMusicCalls({ world, safeZone: inSafeZone, heroTile, heroX, heroY });
  let track: string | null = null;
  for (const call of calls) if (call.play !== undefined) track = call.play;
  return track;
}

/**
 * The login screen's music (PlayLogInSceneMusic): the login theme, or the credits' theme
 * while the credits show. The character select keeps it; the loading of the world stops it.
 */
export function loginMusic(creditsShown = false): string {
  return creditsShown ? Music.MUTHEME : Music.LOGIN_THEME;
}

/**
 * The PlayBuffer / StopBuffer calls of the map's ambience for one frame
 * (PlayWorldAmbientSounds, then StopInactiveAmbientSounds), in the desktop's order. `rand`
 * draws Noria's forest sound (rand_fps_check(512) per frame at 25 frames a second; pass the
 * frame's share for another rate). Run with AudioSystem.applySoundCalls.
 */
export function ambientCalls(state: MapAudioState, rand: () => number = Math.random, framesPer25 = 1): SoundCall[] {
  const calls: SoundCall[] = [];
  const loop = (id: string): void => { calls.push({ play: id, loop: true }); };
  const once = (id: string): void => { calls.push({ play: id }); };
  const stop = (id: string): void => { calls.push({ stop: id }); };
  const { world, heroTile } = state;

  switch (world) {
    case World.LORENCIA:
      if (heroTile === 4) { stop('SOUND_WIND01'); stop('SOUND_RAIN01'); }
      else { loop('SOUND_WIND01'); if (state.raining === true) loop('SOUND_RAIN01'); }
      break;
    case World.DUNGEON: loop('SOUND_DUNGEON01'); break;
    case World.DEVIAS:
      if (heroTile === 3 || heroTile >= 10) stop('SOUND_WIND01'); else loop('SOUND_WIND01');
      break;
    case World.NORIA:
      loop('SOUND_WIND01');
      if (rand() < framesPer25 / 512) once('SOUND_FOREST01');
      break;
    case World.LOST_TOWER: loop('SOUND_TOWER01'); break;
    case World.ATLANS: loop('SOUND_WATER01'); break;
    case World.TARKAN: loop('SOUND_DESERT01'); break;
    case World.ICARUS: loop('SOUND_HEAVEN01'); break;
    case World.RAKLION_BOSS: loop('SOUND_WIND01'); break;
    case World.MARKET_PLACE: loop('SOUND_WIND01'); loop('SOUND_RAIN01'); break;
    case World.KARUTAN1: loop('SOUND_KARUTAN_DESERT_ENV'); break;
    case World.KARUTAN2:
      if (heroTile === 12) { stop('SOUND_KARUTAN_DESERT_ENV'); loop('SOUND_KARUTAN_KARDAMAHAL_ENV'); }
      else { stop('SOUND_KARUTAN_KARDAMAHAL_ENV'); loop('SOUND_KARUTAN_DESERT_ENV'); }
      break;
    default: break;
  }

  const windMaps: readonly number[] = [World.LORENCIA, World.DEVIAS, World.NORIA, World.RAKLION_BOSS, World.MARKET_PLACE];
  if (!windMaps.includes(world)) stop('SOUND_WIND01');
  if (world !== World.LORENCIA && world !== World.DEVIL_SQUARE && world !== World.MARKET_PLACE) stop('SOUND_RAIN01');
  if (world !== World.DUNGEON) stop('SOUND_DUNGEON01');
  if (world !== World.NORIA) stop('SOUND_FOREST01');
  if (world !== World.LOST_TOWER) stop('SOUND_TOWER01');
  if (world !== World.ATLANS) stop('SOUND_WATER01');
  if (world !== World.TARKAN) stop('SOUND_DESERT01');
  if (world !== World.ICARUS) stop('SOUND_HEAVEN01');
  if (world !== World.ELBELAND) {
    for (const id of ['SOUND_ELBELAND_VILLAGEPROTECTION01', 'SOUND_ELBELAND_WATERFALLSMALL01', 'SOUND_ELBELAND_WATERWAY01',
      'SOUND_ELBELAND_ENTERDEVIAS01', 'SOUND_ELBELAND_WATERSMALL01', 'SOUND_ELBELAND_RAVINE01', 'SOUND_ELBELAND_ENTERATLANCE01']) stop(id);
  }
  if (world !== World.KARUTAN1 && world !== World.KARUTAN2) stop('SOUND_KARUTAN_DESERT_ENV');
  if (world !== World.KARUTAN1) stop('SOUND_KARUTAN_INSECT_ENV');
  if (world !== World.KARUTAN2) stop('SOUND_KARUTAN_KARDAMAHAL_ENV');
  return calls;
}

/** The looping ambient sounds (SOUND_* ids) the desktop keeps on for the map and tile. */
export function ambientForMap(world: number, heroTile = 0, raining = false): string[] {
  const loops: string[] = [];
  for (const call of ambientCalls({ world, safeZone: false, heroTile, heroX: 0, heroY: 0, raining }, () => 1)) {
    if (call.play !== undefined && call.loop === true && !loops.includes(call.play)) loops.push(call.play);
  }
  return loops;
}

// --- map object sounds (Data/Maps/ObjectSounds.json) -------------------------------------------

type MapRef = number | { map: number; stage?: number };

interface ObjectSoundEntry {
  maps: MapRef[];
  /** Object types; none: the map-wide sound any drawn object starts. */
  objects?: number[];
  sound: { file: string; channels?: number };
  plays: string;
}

export interface MapObjectSound {
  file: string;
  channels: number;
}

let objectSounds: ObjectSoundEntry[] | null = null;
let objectSoundsLoading: Promise<void> | null = null;

/** Loads Data/Maps/ObjectSounds.json (once). */
export function loadMapObjectSounds(dataRoot = '/data'): Promise<void> {
  if (objectSoundsLoading === null) {
    objectSoundsLoading = fetch(`${dataRoot}/Maps/ObjectSounds.json`)
      .then(async (response) => {
        if (!response.ok) throw new Error(`${response.status}`);
        objectSounds = ((await response.json()) as { objectSounds: ObjectSoundEntry[] }).objectSounds;
      })
      .catch(() => { objectSounds = []; });
  }
  return objectSoundsLoading;
}

function namesMap(maps: readonly MapRef[], serverMap: number, stage: number): boolean {
  return maps.some((ref) => (typeof ref === 'number' ? ref === serverMap : ref.map === serverMap && (ref.stage === undefined || ref.stage === stage)));
}

/**
 * The sound an object type plays on the map while it is drawn (MapObjectSounds::FindSound),
 * or null. Play it each frame the object is drawn with `audio.play(file, { at: objectPosition,
 * channels })`: on a free channel, at the distance level of the nearest such object.
 */
export function mapObjectSound(serverMap: number, objectType: number, stage = 0): MapObjectSound | null {
  let found: MapObjectSound | null = null;
  for (const entry of objectSounds ?? []) {
    if (entry.sound.file.length === 0 || !namesMap(entry.maps, serverMap, stage)) continue;
    if (entry.objects?.includes(objectType) === true) found = { file: entry.sound.file, channels: entry.sound.channels ?? 4 };
  }
  return found;
}

/** The map-wide sounds of the map (entries without objects), played at full volume once a frame while anything is drawn. */
export function mapWideSounds(serverMap: number, stage = 0): MapObjectSound[] {
  return (objectSounds ?? [])
    .filter((entry) => entry.objects === undefined && entry.sound.file.length > 0 && namesMap(entry.maps, serverMap, stage))
    .map((entry) => ({ file: entry.sound.file, channels: entry.sound.channels ?? 4 }));
}
