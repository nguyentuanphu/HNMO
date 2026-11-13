// HNMO monster sounds from Data/Monsters/Sounds.json, as the desktop plays them
// (client/src/source/GameLogic/Monsters/MonsterSoundEvents.cpp, Engine/Object/ZzzCharacter.cpp).
//
// Which set: a monster's entry in Data/Monsters/Monsters.json names it ("soundSet"); a random
// variant that appears with another model may name its own (GetSoundSetWithModel). Players have
// none.
//
// When the desktop asks a monster's set for sounds:
//  - moments ("on" in the file), whatever the animation:
//      'attack' SetPlayerAttack at frame 0 · 'hit' SetPlayerShock at frame 0 · 'death'
//      SetPlayerDie at frame 0 · 'stand' SetPlayerStop (a walk ends, the server stops it) ·
//      'walk' SetPlayerWalk, each step;
//  - "animations": PlayMonsterSound in the same places (start of an attack, a hit, the death, each
//    step of a walk) and at the end of each loop of stop1 and stop2, with the monster's
//    animation (stop1, stop2, walk, attack1..attack5, shock, die, appear, run);
//  - "cues": the frames a cue of its model (Data/Monsters/Models.json) names, in that animation;
//  - "during" / "once" / "duringCues": each frame the monster is drawn in one of those
//    animations (once: the first such frame until it stands or walks again).
// An event plays only within 500 units of the hero and on the set's maps unless "heardAnywhere";
// "chance" draws one in N ("randFps" draws are per call at 25 frames a second here), "pick"
// plays the first sound on a one-in-two draw, else the second; without "pick" all its sounds
// play. "atMonster": PlayBuffer with the monster as the object, i.e. `anchored: true` (the copy
// does not start again while it plays).
// Model timers ("timers", one set) are not ported.

export type MonsterSoundMoment = 'attack' | 'hit' | 'death' | 'stand' | 'walk';

/** One sound to play: `file` (pass to AudioSystem.play with `channels`), and whether at the monster. */
export interface MonsterSoundPlay {
  file: string;
  channels: number;
  /** PlayBuffer's object: play with `{ anchored: true }`. */
  atMonster: boolean;
}

export interface MonsterSoundSituation {
  /** A moment of the game (the "on" events). */
  moment?: MonsterSoundMoment;
  /** The monster's animation by name (the "animations" and "cues" events). */
  animation?: string;
  /** The animation frame, for cues. */
  frame?: number;
  /** A frame the monster is drawn ("during", "once", "duringCues"); then moment and animation events do not play. */
  drawn?: boolean;
  /** A once event played since it last stood or walked. */
  oncePlayed?: boolean;
  /** Distance from the hero on the ground (MU units); unknown counts as heard. */
  heroDistance?: number;
  /** The server map shown, for the set's maps; unknown counts as named. */
  serverMap?: number;
  /** OBJECT::LifeTime == 100: the monster's death effect is armed. */
  deathEffectArmed?: boolean;
  /** The caller drew the chance already (the hero's walk, one in 64): the events' own is not drawn. */
  chanceDrawn?: boolean;
  /** The model slot it appears with ("MONSTER_MODEL_..."), for a random variant's set. Default: the entry's model. */
  model?: string;
}

interface SoundChance {
  oneIn: number;
  draw?: 'randFps' | 'rand';
}

interface SoundEventEntry {
  on?: MonsterSoundMoment[];
  animations?: string[];
  cues?: string[];
  during?: string[];
  once?: string[];
  duringCues?: string[];
  timers?: string[];
  deathEffectArmed?: boolean;
  heardAnywhere?: boolean;
  atMonster?: boolean;
  chance?: SoundChance;
  pick?: string;
  sounds: string[];
}

type MapRef = number | { map: number; stage?: number };

interface SoundSetEntry {
  name: string;
  sounds: { file: string; id?: string; channels?: number }[];
  soundEvents?: SoundEventEntry[];
  maps?: MapRef[];
}

interface MonsterEntryPart {
  model?: string;
  soundSet?: string;
  randomVariants?: { model: string; soundSet?: string }[];
  numbers: { number: number }[];
}

interface ModelCue {
  name: string;
  animation: string;
  frames: [number, number];
}

interface Catalog {
  sets: Map<string, SoundSetEntry>;
  monsters: Map<number, MonsterEntryPart>;
  cuesBySlot: Map<string, ModelCue[]>;
  soundFiles: Map<string, { file: string; channels: number }>;
}

/** The hooks' cut-off: no sounds of monsters farther from the hero (unless heard anywhere). */
export const HEARING_DISTANCE = 500;
const DEFAULT_CHANNELS = 4;

let catalog: Catalog | null = null;
let loading: Promise<void> | null = null;

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status} ${url}`);
  return (await response.json()) as T;
}

/** Loads Sounds.json, Monsters.json, Models.json (cues) and SoundFiles.json once. Lookups answer null until it finished. */
export function loadMonsterSounds(dataRoot = '/data'): Promise<void> {
  if (loading === null) {
    loading = Promise.all([
      fetchJson<{ soundSets: SoundSetEntry[] }>(`${dataRoot}/Monsters/Sounds.json`),
      fetchJson<{ monsters: MonsterEntryPart[] }>(`${dataRoot}/Monsters/Monsters.json`),
      fetchJson<{ models: { slot: string; cues?: ModelCue[] }[] }>(`${dataRoot}/Monsters/Models.json`).catch(() => ({ models: [] })),
      fetchJson<{ sounds: { id: string; file: string; channels: number }[] }>(`${dataRoot}/Audio/SoundFiles.json`).catch(() => ({ sounds: [] })),
    ])
      .then(([sounds, monsters, models, soundFiles]) => {
        const next: Catalog = { sets: new Map(), monsters: new Map(), cuesBySlot: new Map(), soundFiles: new Map() };
        for (const set of sounds.soundSets) next.sets.set(set.name, set);
        for (const monster of monsters.monsters) {
          for (const { number } of monster.numbers) if (!next.monsters.has(number)) next.monsters.set(number, monster);
        }
        for (const model of models.models) if (model.cues !== undefined) next.cuesBySlot.set(model.slot, model.cues);
        for (const sound of soundFiles.sounds) next.soundFiles.set(sound.id, { file: sound.file, channels: sound.channels });
        catalog = next;
      })
      .catch(() => {
        catalog = { sets: new Map(), monsters: new Map(), cuesBySlot: new Map(), soundFiles: new Map() };
      });
  }
  return loading;
}

/**
 * The name of the sound set the monster plays with the model (GetSoundSetWithModel): a random
 * variant's own set for its model, else the entry's; null for none or before loading.
 */
export function monsterSoundSetName(monsterNumber: number, model?: string): string | null {
  const monster = catalog?.monsters.get(monsterNumber);
  if (monster === undefined) return null;
  if (model !== undefined) {
    const variant = monster.randomVariants?.find((entry) => entry.model === model && entry.soundSet !== undefined);
    if (variant?.soundSet !== undefined) return variant.soundSet;
  }
  return monster.soundSet !== undefined && monster.soundSet.length > 0 ? monster.soundSet : null;
}

function resolveSound(set: SoundSetEntry, name: string, atMonster: boolean): MonsterSoundPlay | null {
  const own = set.sounds.find((sound) => sound.id === name || (sound.id === undefined && sound.file === name)) ?? set.sounds.find((sound) => sound.file === name);
  if (own !== undefined) return { file: own.file, channels: own.channels ?? DEFAULT_CHANNELS, atMonster };
  const shared = catalog?.soundFiles.get(name);
  if (shared !== undefined) return { file: shared.file, channels: shared.channels, atMonster };
  return name.includes('/') ? { file: name, channels: DEFAULT_CHANNELS, atMonster } : null;
}

function namesMap(maps: readonly MapRef[], serverMap: number): boolean {
  return maps.some((ref) => (typeof ref === 'number' ? ref === serverMap : ref.map === serverMap));
}

function onCue(names: readonly string[] | undefined, cues: readonly ModelCue[], situation: MonsterSoundSituation): boolean {
  if (names === undefined || situation.animation === undefined) return false;
  const frame = situation.frame ?? 0;
  return names.some((name) => cues.some((cue) => cue.name === name && cue.animation === situation.animation && frame >= cue.frames[0] && frame < cue.frames[1]));
}

function isWhen(event: SoundEventEntry, cues: readonly ModelCue[], situation: MonsterSoundSituation): boolean {
  const animation = situation.animation;
  if (situation.drawn === true) {
    return (animation !== undefined && event.during?.includes(animation) === true)
      || onCue(event.duringCues, cues, situation)
      || (situation.oncePlayed !== true && animation !== undefined && event.once?.includes(animation) === true);
  }
  if (situation.moment !== undefined && event.on?.includes(situation.moment) === true) return true;
  return (animation !== undefined && event.animations?.includes(animation) === true) || onCue(event.cues, cues, situation);
}

/**
 * All the sounds the monster's set plays in the situation, each event's chance drawn and its pick
 * picked with `rand` (0..1), in the file's order (MonsterSoundEvents::PlayEvents). A moment and an
 * animation given together answer both, as the desktop's two calls at an attack, a hit, a death
 * or a step do. Empty before loadMonsterSounds() finished.
 */
export function monsterSoundPlays(monsterNumber: number, situation: MonsterSoundSituation, rand: () => number = Math.random): MonsterSoundPlay[] {
  const name = monsterSoundSetName(monsterNumber, situation.model);
  const set = name !== null ? catalog?.sets.get(name) : undefined;
  if (set === undefined || set.soundEvents === undefined) return [];
  const model = situation.model ?? catalog?.monsters.get(monsterNumber)?.model;
  const cues = (model !== undefined ? catalog?.cuesBySlot.get(model) : undefined) ?? [];
  const heard = (situation.heroDistance === undefined || situation.heroDistance <= HEARING_DISTANCE)
    && (set.maps === undefined || set.maps.length === 0 || situation.serverMap === undefined || namesMap(set.maps, situation.serverMap));
  const plays: MonsterSoundPlay[] = [];
  for (const event of set.soundEvents) {
    if (!isWhen(event, cues, situation)) continue;
    if (event.deathEffectArmed === true && situation.deathEffectArmed !== true) continue;
    if (event.heardAnywhere !== true && !heard) continue;
    if (event.chance !== undefined && situation.chanceDrawn !== true && !(rand() * Math.max(1, event.chance.oneIn) < 1)) continue;
    const atMonster = event.atMonster === true;
    let names: string[];
    if (event.pick !== undefined) {
      if (rand() < 0.5) names = event.sounds.slice(0, 1);
      else names = event.sounds.length > 1 ? [event.sounds[1]] : [];
    } else {
      names = event.sounds;
    }
    for (const soundName of names) {
      const play = resolveSound(set, soundName, atMonster);
      if (play !== null) plays.push(play);
    }
  }
  return plays;
}

/**
 * The sound file the monster plays at a moment ('stand' | 'walk' | 'attack' | 'hit' | 'death'),
 * or null (none, the chance did not come up, or not loaded yet). With `animation` also the
 * events of that animation count (pass the monster's current one at an attack, a hit, a death
 * or a step). When an event plays several sounds this gives the first; monsterSoundPlays gives
 * all, with their channels and whether they play at the monster.
 */
export function monsterSound(monsterNumber: number, event: MonsterSoundMoment, rand: () => number = Math.random, animation?: string): string | null {
  const plays = monsterSoundPlays(monsterNumber, { moment: event, animation }, rand);
  return plays.length > 0 ? plays[0].file : null;
}

/** The files of the monster's set (to preload them when it appears); empty for none. */
export function monsterSoundFiles(monsterNumber: number, model?: string): string[] {
  const name = monsterSoundSetName(monsterNumber, model);
  const set = name !== null ? catalog?.sets.get(name) : undefined;
  return set !== undefined ? set.sounds.map((sound) => sound.file) : [];
}
