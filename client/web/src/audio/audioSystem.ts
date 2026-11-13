// HNMO sound and music playback on the Web Audio API, as the desktop client plays them
// (client/src/source/Audio/DSPlaySound.cpp, AudioPlayer.cpp and the backend in
// Core/Platform/Audio/MiniAudioBackend.cpp).
//
// What the desktop does and this file copies:
//  - Sound effects come from Data/Audio/SoundFiles.json: a SOUND_* id names a file and its
//    channels, the copies of it that can play at once. PlayBuffer takes the next channel in
//    turn (round robin), stops it and starts it from the beginning; a sound with one
//    channel, one played at an object, or a looped one does not start again while a copy
//    still plays (MiniAudioBackend::StartSound).
//  - Effects play in 2D: the desktop never placed them in 3D ("spatial" in the data is
//    read and ignored). The one sound whose volume follows distance is a map object's
//    (World/MapInfra/MapObjectSounds.cpp, DistanceLevel): full within 300 units (3 tiles),
//    then down to a quarter at 1500 units and beyond. `play(id, { at })` does that and plays
//    on a free channel only (PlayBufferOnFreeChannel).
//  - Effect volume and music volume are levels 0..10 (the option window's sliders; default 5),
//    applied as level / 10. HNMO adds a master volume and a mute switch on top.
//  - Music: one looping track at a time; asking for the track that plays does nothing, a
//    stop names the track it stops and does nothing for another (MiniAudioBackend::PlayMusic
//    / StopMusic). A track that failed to load is not tried again unless forced.
//  - A file that is missing (Data/Sound/MISSING.txt) or does not decode stays silent.
//
// Browsers keep audio off until the page gets a click or key press: the system listens for
// the first one, resumes its context and starts the music asked for meanwhile.

export interface Vec3 {
  x: number;
  y: number;
  z: number;
}

export interface PlayOptions {
  /**
   * Where the sound is in the world (MU units, 100 per tile). Plays like a map object's sound:
   * on a free channel only, at DistanceLevel of its distance from the listener.
   */
  at?: Vec3;
  /**
   * Played at a character or object (PlayBuffer's object argument): not started again while a
   * copy of it plays. Full volume, as on the desktop.
   */
  anchored?: boolean;
  /** Loops until stop(id) (the desktop's ambient loops). Not started again while it plays. */
  loop?: boolean;
  /** A level 0..1 for this copy, times the effect volume. Default 1. */
  volume?: number;
  /** For a file not in SoundFiles.json (a monster sound set's file): its channels. Default 4. */
  channels?: number;
}

export interface AudioSettings {
  /** 0..1, HNMO's master volume. */
  master: number;
  /** 0..10, the desktop's sound volume level. */
  effectsLevel: number;
  /** 0..10, the desktop's music volume level. */
  musicLevel: number;
  muted: boolean;
}

interface SoundFileEntry {
  id: string;
  file: string;
  channels: number;
  spatial?: boolean;
}

interface SoundFilesJson {
  sounds: SoundFileEntry[];
}

/** One loaded sound and its channels. */
interface LoadedSound {
  buffer: AudioBuffer;
  channels: (AudioBufferSourceNode | null)[];
  next: number;
}

/** The highest number of channels a sound gets (MAX_CHANNEL). */
export const MAX_CHANNEL = 4;
/** LoadWaveFile's default channels for a file without an entry. */
const DEFAULT_CHANNELS = 4;
/** The desktop's volume level scale. */
export const MAX_VOLUME_LEVEL = 10;
export const DEFAULT_VOLUME_LEVEL = 5;
/** A sound that was still loading longer than this when it became ready does not start (it would come late). */
const LATE_START_MS = 600;
const STORAGE_KEY = 'hnmo.audio';

// MapObjectSounds.cpp: the distances of DistanceLevel and its lowest level.
const FULL_VOLUME_DISTANCE = 300;
const QUIETEST_DISTANCE = 1500;
const QUIETEST_LEVEL = 0.25;

/**
 * The volume of a map object's sound by its distance from the hero on the ground
 * (MapObjectSounds::DistanceLevel): 1 within 300 units, then linearly down to 0.25 at 1500
 * units and beyond.
 */
export function distanceLevel(distance: number): number {
  if (distance <= FULL_VOLUME_DISTANCE) return 1;
  if (distance >= QUIETEST_DISTANCE) return QUIETEST_LEVEL;
  const along = (distance - FULL_VOLUME_DISTANCE) / (QUIETEST_DISTANCE - FULL_VOLUME_DISTANCE);
  return 1 - along * (1 - QUIETEST_LEVEL);
}

/**
 * The URL of a data file named as the desktop names it ("Data/Sound/a.wav",
 * "data\\music\\Pub.mp3"): served under /data/. The server matches names ignoring case
 * (bridge GameFiles.cs), so the data's spelling works as it is.
 */
export function dataUrl(file: string, dataRoot = '/data'): string {
  const parts = file.replace(/\\/g, '/').split('/').filter((part) => part.length > 0);
  if (parts.length > 0 && parts[0].toLowerCase() === 'data') parts.shift();
  return `${dataRoot}/${parts.map((part) => encodeURIComponent(part)).join('/')}`;
}

/** A file name as the desktop compares them: forward slashes, lower case. */
function fileKey(file: string): string {
  return file.replace(/\\/g, '/').toLowerCase();
}

function clamp(value: number, low: number, high: number): number {
  return Number.isFinite(value) ? Math.min(high, Math.max(low, value)) : low;
}

function defaultSettings(): AudioSettings {
  return { master: 1, effectsLevel: DEFAULT_VOLUME_LEVEL, musicLevel: DEFAULT_VOLUME_LEVEL, muted: false };
}

function readSettings(): AudioSettings {
  const settings = defaultSettings();
  try {
    const text = localStorage.getItem(STORAGE_KEY);
    if (text === null) return settings;
    const saved = JSON.parse(text) as Partial<Record<keyof AudioSettings, unknown>>;
    if (typeof saved.master === 'number') settings.master = clamp(saved.master, 0, 1);
    if (typeof saved.effectsLevel === 'number') settings.effectsLevel = Math.round(clamp(saved.effectsLevel, 0, MAX_VOLUME_LEVEL));
    if (typeof saved.musicLevel === 'number') settings.musicLevel = Math.round(clamp(saved.musicLevel, 0, MAX_VOLUME_LEVEL));
    if (typeof saved.muted === 'boolean') settings.muted = saved.muted;
  } catch {
    /* storage unavailable or damaged: defaults */
  }
  return settings;
}

function writeSettings(settings: AudioSettings): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
  } catch {
    /* private window */
  }
}

type AudioContextConstructor = new () => AudioContext;

function findAudioContext(): AudioContextConstructor | null {
  if (typeof window === 'undefined') return null;
  const scope = window as unknown as { AudioContext?: AudioContextConstructor; webkitAudioContext?: AudioContextConstructor };
  return scope.AudioContext ?? scope.webkitAudioContext ?? null;
}

export class AudioSystem {
  private readonly dataRoot: string;
  private readonly context: AudioContext | null;
  private readonly masterGain: GainNode | null;
  private readonly effectsGain: GainNode | null;
  private settings: AudioSettings;

  private soundFiles: Map<string, SoundFileEntry> | null = null;
  private soundFilesLoading: Promise<void> | null = null;
  /** Decoded sounds by file key; null: missing or undecodable (silent from then on). */
  private readonly sounds = new Map<string, LoadedSound | null>();
  private readonly loading = new Map<string, Promise<LoadedSound | null>>();
  /** Channels per file key, as the first play or the data gave them. */
  private readonly channelCounts = new Map<string, number>();

  private listener: Vec3 = { x: 0, y: 0, z: 0 };
  private listenerFacing = 0;

  private music: HTMLAudioElement | null = null;
  /** The track asked for (as named), whether or not the browser lets it play yet. */
  private musicName: string | null = null;
  private readonly failedMusic = new Set<string>();
  private unlocked = false;
  private readonly unlockHandler = (): void => this.unlock();

  constructor(options: { dataRoot?: string } = {}) {
    this.dataRoot = options.dataRoot ?? '/data';
    this.settings = readSettings();
    const Context = findAudioContext();
    let context: AudioContext | null = null;
    try {
      context = Context !== null ? new Context() : null;
    } catch {
      context = null;
    }
    this.context = context;
    if (context !== null) {
      this.masterGain = context.createGain();
      this.effectsGain = context.createGain();
      this.effectsGain.connect(this.masterGain);
      this.masterGain.connect(context.destination);
    } else {
      this.masterGain = null;
      this.effectsGain = null;
    }
    this.applyVolumes();
    if (typeof window !== 'undefined') {
      for (const type of ['pointerdown', 'keydown', 'touchstart', 'mousedown']) {
        window.addEventListener(type, this.unlockHandler, { capture: true, passive: true });
      }
    }
  }

  /** Whether the browser lets audio play (a user gesture came, or autoplay is allowed). */
  get isUnlocked(): boolean {
    return this.unlocked || this.context?.state === 'running';
  }

  /** Resumes audio after a user gesture; the listeners call it, a caller may too (from a click handler). */
  unlock(): void {
    const context = this.context;
    if (context !== null && context.state !== 'running') {
      context.resume().catch(() => undefined);
    }
    this.unlocked = true;
    if (typeof window !== 'undefined') {
      for (const type of ['pointerdown', 'keydown', 'touchstart', 'mousedown']) {
        window.removeEventListener(type, this.unlockHandler, { capture: true });
      }
    }
    if (this.music !== null && this.music.paused) {
      this.music.play().catch(() => undefined);
    }
  }

  // --- settings -----------------------------------------------------------------------------

  getSettings(): AudioSettings {
    return { ...this.settings };
  }

  /** Sets any of the settings, applies and saves them. */
  setSettings(change: Partial<AudioSettings>): void {
    const next = { ...this.settings };
    if (change.master !== undefined) next.master = clamp(change.master, 0, 1);
    if (change.effectsLevel !== undefined) next.effectsLevel = Math.round(clamp(change.effectsLevel, 0, MAX_VOLUME_LEVEL));
    if (change.musicLevel !== undefined) next.musicLevel = Math.round(clamp(change.musicLevel, 0, MAX_VOLUME_LEVEL));
    if (change.muted !== undefined) next.muted = change.muted;
    this.settings = next;
    this.applyVolumes();
    writeSettings(next);
  }

  setMasterVolume(volume: number): void { this.setSettings({ master: volume }); }
  /** The desktop's sound volume level, 0..10 (SetEffectVolumeLevel). */
  setEffectsLevel(level: number): void { this.setSettings({ effectsLevel: level }); }
  /** The desktop's music volume level, 0..10 (AudioPlayer::SetMusicVolume). */
  setMusicLevel(level: number): void { this.setSettings({ musicLevel: level }); }
  setMuted(muted: boolean): void { this.setSettings({ muted }); }

  private applyVolumes(): void {
    const master = this.settings.muted ? 0 : this.settings.master;
    if (this.masterGain !== null) this.masterGain.gain.value = master;
    if (this.effectsGain !== null) this.effectsGain.gain.value = this.settings.effectsLevel / MAX_VOLUME_LEVEL;
    if (this.music !== null) this.music.volume = clamp(master * (this.settings.musicLevel / MAX_VOLUME_LEVEL), 0, 1);
  }

  // --- listener -----------------------------------------------------------------------------

  /**
   * The hero's position (MU units) for the distance of `play(id, { at })`. The facing is kept
   * for callers that read it; the desktop pans nothing, so neither does this.
   */
  setListener(position: Vec3, facingDegrees = 0): void {
    this.listener = { x: position.x, y: position.y, z: position.z };
    this.listenerFacing = facingDegrees;
  }

  get listenerPosition(): Vec3 { return { ...this.listener }; }
  get listenerFacingDegrees(): number { return this.listenerFacing; }

  // --- sound effects ------------------------------------------------------------------------

  /** Loads Data/Audio/SoundFiles.json (play does it on its own; await it to preload). */
  init(): Promise<void> {
    if (this.soundFilesLoading === null) {
      this.soundFilesLoading = fetch(`${this.dataRoot}/Audio/SoundFiles.json`)
        .then(async (response) => {
          if (!response.ok) throw new Error(`${response.status}`);
          const data = (await response.json()) as SoundFilesJson;
          const map = new Map<string, SoundFileEntry>();
          for (const entry of data.sounds) map.set(entry.id, entry);
          this.soundFiles = map;
        })
        .catch(() => {
          this.soundFiles = new Map();
        });
    }
    return this.soundFilesLoading;
  }

  /** The file and channels of a SOUND_* id, once init() finished; null for an unknown id. */
  soundFile(id: string): { file: string; channels: number } | null {
    const entry = this.soundFiles?.get(id);
    return entry !== undefined ? { file: entry.file, channels: entry.channels } : null;
  }

  /** Loads and decodes sounds ahead of their first play (SOUND_* ids or files). */
  async preload(ids: readonly string[]): Promise<void> {
    await this.init();
    await Promise.all(ids.map(async (id) => {
      const target = this.resolve(id, undefined);
      if (target !== null) await this.load(target.key, target.file, target.channels);
    }));
  }

  /**
   * Plays a sound: a SOUND_* id of SoundFiles.json, or a file ("Data/Sound/mBull1.wav", a
   * monster sound set's). PlayBuffer's rules, see the top of this file. Fails silently.
   */
  play(id: string, options: PlayOptions = {}): void {
    if (this.context === null) return;
    const requested = performance.now();
    if (this.soundFiles === null && !this.isFile(id)) {
      void this.init().then(() => this.playResolved(id, options, requested));
      return;
    }
    this.playResolved(id, options, requested);
  }

  private isFile(id: string): boolean {
    return id.includes('/') || id.includes('\\') || id.includes('.');
  }

  private resolve(id: string, channels: number | undefined): { key: string; file: string; channels: number } | null {
    if (this.isFile(id)) {
      return { key: fileKey(id), file: id, channels: clamp(channels ?? DEFAULT_CHANNELS, 1, MAX_CHANNEL) };
    }
    const entry = this.soundFiles?.get(id);
    if (entry === undefined) return null;
    return { key: fileKey(entry.file), file: entry.file, channels: clamp(entry.channels, 1, MAX_CHANNEL) };
  }

  private playResolved(id: string, options: PlayOptions, requested: number): void {
    const target = this.resolve(id, options.channels);
    if (target === null) return;
    const loaded = this.sounds.get(target.key);
    if (loaded === null) return;
    if (loaded !== undefined) {
      this.start(loaded, options);
      return;
    }
    void this.load(target.key, target.file, target.channels).then((sound) => {
      // A looped sound starts whenever it is ready; a one-shot only if it would not come late.
      if (sound !== null && (options.loop === true || performance.now() - requested <= LATE_START_MS)) {
        this.start(sound, options);
      }
    });
  }

  private load(key: string, file: string, channels: number): Promise<LoadedSound | null> {
    const known = this.sounds.get(key);
    if (known !== undefined) return Promise.resolve(known);
    const pending = this.loading.get(key);
    if (pending !== undefined) return pending;
    const context = this.context;
    if (context === null) return Promise.resolve(null);
    if (!this.channelCounts.has(key)) this.channelCounts.set(key, channels);
    const promise = fetch(dataUrl(file, this.dataRoot))
      .then(async (response) => {
        if (!response.ok) return null;
        const bytes = await response.arrayBuffer();
        const buffer = await context.decodeAudioData(bytes);
        const count = this.channelCounts.get(key) ?? channels;
        const sound: LoadedSound = { buffer, channels: new Array<AudioBufferSourceNode | null>(count).fill(null), next: 0 };
        return sound;
      })
      .catch(() => null)
      .then((sound) => {
        this.sounds.set(key, sound);
        this.loading.delete(key);
        return sound;
      });
    this.loading.set(key, promise);
    return promise;
  }

  private start(sound: LoadedSound, options: PlayOptions): void {
    const context = this.context;
    const output = this.effectsGain;
    if (context === null || output === null || context.state !== 'running') return;
    let level = clamp(options.volume ?? 1, 0, 1);
    const count = sound.channels.length;
    if (options.at !== undefined) {
      // PlayBufferOnFreeChannel: the next channel in turn; one that still plays keeps playing.
      const channel = sound.next;
      sound.next = (channel + 1) % count;
      if (sound.channels[channel] !== null) return;
      const dx = options.at.x - this.listener.x;
      const dy = options.at.y - this.listener.y;
      level *= distanceLevel(Math.sqrt(dx * dx + dy * dy));
      this.startChannel(sound, channel, false, level, output);
      return;
    }
    // StartSound: one channel, an object's sound or a loop does not start again while it plays.
    if (count === 1 || options.anchored === true || options.loop === true) {
      if (sound.channels.some((node) => node !== null)) return;
    }
    const channel = sound.next;
    sound.next = (channel + 1) % count;
    this.stopChannel(sound, channel);
    this.startChannel(sound, channel, options.loop === true, level, output);
  }

  private startChannel(sound: LoadedSound, channel: number, loop: boolean, level: number, output: AudioNode): void {
    const context = this.context;
    if (context === null) return;
    const source = context.createBufferSource();
    source.buffer = sound.buffer;
    source.loop = loop;
    const gain = context.createGain();
    gain.gain.value = level;
    source.connect(gain);
    gain.connect(output);
    source.onended = () => {
      if (sound.channels[channel] === source) sound.channels[channel] = null;
      gain.disconnect();
    };
    sound.channels[channel] = source;
    source.start();
  }

  private stopChannel(sound: LoadedSound, channel: number): void {
    const node = sound.channels[channel];
    if (node === null) return;
    sound.channels[channel] = null;
    try {
      node.stop();
    } catch {
      /* already stopped */
    }
  }

  /** Stops every copy of a sound (StopBuffer). */
  stop(id: string): void {
    const target = this.resolve(id, undefined);
    if (target === null) return;
    const sound = this.sounds.get(target.key);
    if (sound === undefined || sound === null) return;
    for (let channel = 0; channel < sound.channels.length; channel++) this.stopChannel(sound, channel);
  }

  /** Whether a copy of the sound plays. */
  isPlaying(id: string): boolean {
    const target = this.resolve(id, undefined);
    if (target === null) return false;
    const sound = this.sounds.get(target.key);
    return sound !== undefined && sound !== null && sound.channels.some((node) => node !== null);
  }

  /** Stops all sound effects (AllStopSound); the music goes on. */
  stopAll(): void {
    for (const sound of this.sounds.values()) {
      if (sound === null) continue;
      for (let channel = 0; channel < sound.channels.length; channel++) this.stopChannel(sound, channel);
    }
  }

  // --- music --------------------------------------------------------------------------------

  /** The track asked for last (as named), or null. */
  get currentMusic(): string | null {
    return this.musicName;
  }

  /**
   * Plays a looping music track (PlayMp3): "Data/Music/Noria.mp3" or the desktop's
   * "data\\music\\Noria.mp3". Asking for the track that plays does nothing; a track that
   * failed is not tried again unless `force`.
   */
  playMusic(file: string, force = false): void {
    const key = fileKey(file);
    if (!force && this.musicName !== null && fileKey(this.musicName) === key) return;
    this.releaseMusic();
    if (!force && this.failedMusic.has(key)) return;
    if (typeof Audio === 'undefined') return;
    const element = new Audio(dataUrl(file, this.dataRoot));
    element.loop = true;
    element.preload = 'auto';
    element.addEventListener('error', () => {
      this.failedMusic.add(key);
      if (this.music === element) this.releaseMusic();
    });
    this.music = element;
    this.musicName = file;
    this.failedMusic.delete(key);
    this.applyVolumes();
    // Before a user gesture the browser refuses; unlock() starts it then.
    element.play().catch(() => undefined);
  }

  /**
   * Stops the music (StopMp3). With a track named, only when that track plays; without one,
   * whatever plays (StopMusic).
   */
  stopMusic(file?: string): void {
    if (file !== undefined && (this.musicName === null || fileKey(this.musicName) !== fileKey(file))) return;
    this.releaseMusic();
  }

  /** Applies PlayMp3 / StopMp3 calls in order (see mapAudio.ts backgroundMusicCalls). */
  applyMusicCalls(calls: readonly MusicCall[]): void {
    for (const call of calls) {
      if (call.play !== undefined) this.playMusic(call.play);
      else if (call.stop !== undefined) this.stopMusic(call.stop);
    }
  }

  /** Applies PlayBuffer / StopBuffer calls in order (see mapAudio.ts ambientCalls). */
  applySoundCalls(calls: readonly SoundCall[]): void {
    for (const call of calls) {
      if (call.stop !== undefined) this.stop(call.stop);
      else if (call.play !== undefined) this.play(call.play, { loop: call.loop === true });
    }
  }

  private releaseMusic(): void {
    const element = this.music;
    this.music = null;
    this.musicName = null;
    if (element !== null) {
      element.pause();
      element.removeAttribute('src');
      element.load();
    }
  }
}

/** One PlayMp3 or StopMp3 call. */
export interface MusicCall {
  play?: string;
  stop?: string;
}

/** One PlayBuffer or StopBuffer call of a SOUND_* id. */
export interface SoundCall {
  play?: string;
  stop?: string;
  loop?: boolean;
}

let shared: AudioSystem | null = null;

/** The page's one audio system (created on first use). */
export function audioSystem(): AudioSystem {
  if (shared === null) shared = new AudioSystem();
  return shared;
}
