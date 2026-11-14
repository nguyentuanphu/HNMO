// Monster and NPC looks from the client's data files (Data/Monsters/Monsters.json and
// Models.json, see docs/monster-data.md): which model a type number uses, its scale
// and its animation speeds.

import type { MonsterAttacksData } from './combatLook.js';

const FRAMES_PER_SECOND = 25; // animation speeds in the data are keys per 25 FPS frame (ADR 0001)
const DEFAULT_PLAY_SPEED = 0.25;
const MONSTER_ACTIONS = ['stop1', 'stop2', 'walk', 'attack1', 'attack2', 'shock', 'die'];
export const MonsterAction = { STOP: 0, WALK: 2 };

// The parts of Monsters.json and Models.json this file reads.
interface MonsterEntry {
  name?: { en?: string; [language: string]: string | undefined };
  kind?: string;
  /** A model slot of Models.json, or 'player'. */
  model?: string;
  /** Its size, or 'character': the size a player has (docs/monster-data.md). */
  scale?: number | 'character';
  numbers: { number: number; maps?: number[] }[];
  /** How it attacks (which animation for which skill), read by combatLook's monsterAttackAction. */
  attacks?: MonsterAttacksData;
}

interface MonstersFile {
  unknownMonsters: { model: string; scale?: number };
  monsters: MonsterEntry[];
}

interface ModelEntry {
  slot: string;
  file: string;
  textureFolders?: string[];
  /** Keys per frame by action name. */
  animations?: Record<string, number>;
  scale?: number;
}

interface ModelsFile {
  models: ModelEntry[];
}

export interface MonsterCatalog {
  byNumber: Map<number, MonsterEntry>;
  modelsBySlot: Map<string, ModelEntry>;
  unknown: MonstersFile['unknownMonsters'];
}

/** A type drawn with the player model (guards, some NPCs). */
export interface PlayerModelMonster {
  name: string;
  usesPlayerModel: true;
  scale: number;
  kind: string | undefined;
  attacks: MonsterAttacksData | null;
}

/** A type drawn with its own model. */
export interface ModelMonster {
  name: string;
  kind: string;
  usesPlayerModel: false;
  dir: string;
  fileName: string;
  textureDir: string[];
  scale: number;
  /** Animation speed per action (MONSTER_ACTIONS order), in keys per second. */
  keysPerSecond: number[];
  attacks: MonsterAttacksData | null;
}

export type MonsterDescription = PlayerModelMonster | ModelMonster;

let catalog: MonsterCatalog | null = null;

/** A type's size: 'character' (and anything else that isn't a number) is a player's size, 1. */
function sizeOf(scale: MonsterEntry['scale'], fallback?: number): number {
  if (typeof scale === 'number' && Number.isFinite(scale)) return scale;
  if (scale === undefined && typeof fallback === 'number' && Number.isFinite(fallback)) return fallback;
  return 1;
}

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status} ${url}`);
  return response.json() as Promise<T>;
}

export async function loadMonsterCatalog(): Promise<MonsterCatalog> {
  if (catalog) return catalog;
  const [monsters, models] = await Promise.all([fetchJson<MonstersFile>('/data/Monsters/Monsters.json'), fetchJson<ModelsFile>('/data/Monsters/Models.json')]);
  const modelsBySlot = new Map(models.models.map((model) => [model.slot, model]));
  const byNumber = new Map<number, MonsterEntry>();
  for (const monster of monsters.monsters) {
    for (const entry of monster.numbers) byNumber.set(entry.number, monster);
  }
  catalog = { byNumber, modelsBySlot, unknown: monsters.unknownMonsters };
  return catalog;
}

// Where to load a type's model from, and how to play it. Returns null for types drawn
// with the player model (guards, some NPCs), which need the player's body parts.
export function describeMonster(number: number): MonsterDescription {
  if (!catalog) throw new Error('The monster catalog is not loaded yet (loadMonsterCatalog).');
  const monster = catalog.byNumber.get(number);
  const slot = monster?.model ?? catalog.unknown.model;
  const name = monster?.name?.en ?? `Monster ${number}`;
  if (slot === 'player') return { name, usesPlayerModel: true, scale: sizeOf(monster?.scale), kind: monster?.kind, attacks: monster?.attacks ?? null };
  // The unknown monsters' model is always in Models.json.
  const model = catalog.modelsBySlot.get(slot) ?? catalog.modelsBySlot.get(catalog.unknown.model)!;
  const file = model.file.replace(/^Data\//, '');
  const dir = file.slice(0, file.lastIndexOf('/'));
  const speeds = MONSTER_ACTIONS.map((action) => (model.animations?.[action] ?? DEFAULT_PLAY_SPEED) * FRAMES_PER_SECOND);
  return {
    name,
    kind: monster?.kind ?? 'monster',
    usesPlayerModel: false,
    dir,
    fileName: file.slice(dir.length + 1),
    textureDir: model.textureFolders?.length ? model.textureFolders : [dir],
    scale: sizeOf(monster?.scale, model.scale),
    keysPerSecond: speeds,
    attacks: monster?.attacks ?? null,
  };
}
