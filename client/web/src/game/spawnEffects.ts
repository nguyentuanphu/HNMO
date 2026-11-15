// A monster's spawn effects (Monsters.json "spawnEffects", MonsterSetupApply.cpp Spawn): the joints, effects and
// particles made with it when it appears, by the code's names (Data/Effects/JointTypes.json, EffectTypes.json and
// ParticleTypes.json give the web's name of each). A type or SubType the web's effect system has not ported makes
// nothing; `unsupportedSpawnEffects` lists them. Objects ({"object"}), eyes and rows at a bone are not made yet.

import * as THREE from 'three';
import type { EffectSystem, EffectOwner } from '../render/effects/index.js';
import type { EffectType, JointType, ParticleType } from '../render/effects/types.js';
import type { Character } from './characters.js';
import type { MonsterSpawnEffectData } from './monsterData.js';

type Kind = 'joint' | 'effect' | 'particle';
const names: Record<Kind, Map<string, string>> = { joint: new Map(), effect: new Map(), particle: new Map() };
let loading: Promise<void> | null = null;
const unsupported = new Set<string>();

/** Loads the code names of the effect types once. */
export function loadEffectTypeNames(): Promise<void> {
  if (!loading) {
    const files: [Kind, string][] = [['joint', 'JointTypes'], ['effect', 'EffectTypes'], ['particle', 'ParticleTypes']];
    loading = Promise.all(files.map(async ([kind, file]) => {
      const response = await fetch(`/data/Effects/${file}.json`);
      if (!response.ok) return;
      const data = await response.json() as { types: { name: string; code?: string }[] };
      for (const type of data.types) if (type.code) names[kind].set(type.code, type.name);
    })).then(() => undefined).catch(() => undefined);
  }
  return loading;
}

/** The spawn effect rows the web could not make, as "kind code subType" (for the audit and the parity list). */
export function unsupportedSpawnEffects(): string[] {
  return [...unsupported].sort();
}

function describe(row: MonsterSpawnEffectData): string {
  const kind = row.joint ? 'joint' : row.effect ? 'effect' : row.particle ? 'particle' : row.object ? 'object' : 'eyes';
  return `${kind} ${row.joint ?? row.effect ?? row.particle ?? row.object ?? ''} ${row.subType ?? 0}`.trim();
}

/** Makes a character's spawn effects on it (rows without a bone), as Spawn does; returns how many were made. */
export async function playSpawnEffects(effects: EffectSystem, character: Character, owner: EffectOwner): Promise<number> {
  const rows = character.look?.setup.spawnEffects ?? [];
  if (!rows.length) return 0;
  await loadEffectTypeNames();
  let made = 0;
  for (const row of rows) {
    if (row.bone !== undefined || row.object !== undefined || row.eyes !== undefined) { unsupported.add(describe(row)); continue; }
    const position = new THREE.Vector3(character.group.position.x, character.group.position.y, character.group.position.z);
    if (row.offset) position.add(new THREE.Vector3(...row.offset));
    const target = position.clone();
    if (row.targetOffset) target.add(new THREE.Vector3(...row.targetOffset));
    const angle = new THREE.Vector3(0, 0, THREE.MathUtils.radToDeg(character.angle));
    const light = row.color ? new THREE.Vector3(...row.color) : new THREE.Vector3(character.light.r, character.light.g, character.light.b);
    const scale = () => (row.scaleRandom ? (row.scale ?? 0) + Math.floor(Math.random() * row.scaleRandom) : row.scale ?? 0);
    let ok = false;
    for (let i = 0; i < (row.count ?? 1); i++) {
      if (row.joint) {
        const name = names.joint.get(row.joint) as JointType | undefined;
        ok = Boolean(name && effects.createJoint(name, position, target, angle, row.subType ?? 0, owner, scale(), 0, row.key ?? 0, row.color ? light : null));
      } else if (row.effect) {
        const name = names.effect.get(row.effect) as EffectType | undefined;
        ok = Boolean(name && effects.createEffect(name, position, angle, light, row.subType ?? 0, owner, 0, { pkKey: row.key }));
      } else if (row.particle) {
        const name = names.particle.get(row.particle) as ParticleType | undefined;
        const before = effects.counts().particles;
        if (name) effects.createParticle(name, position, angle, light, row.subType ?? 0, scale() || 1, owner);
        ok = effects.counts().particles > before;
      }
      if (!ok) break;
    }
    if (ok) made++;
    else unsupported.add(describe(row));
  }
  return made;
}
