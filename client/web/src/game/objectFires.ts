// The fires and smoke of Lorencia's lamps, torches, bridge, dungeon gate and bonfire (ZzzObject.cpp, the
// WD_0LORENCIA case of the object animation: CreateFire on MODEL_LIGHT01..+2, MODEL_BRIDGE, MODEL_DUNGEON_GATE,
// MODEL_FIRE_LIGHT01..+1 and MODEL_BONFIRE; CreateFire is in ZzzEffectFireLeave.cpp). The web game drew the
// models but not their flames.
import * as THREE from 'three';
import type { WorldObject } from '../formats.js';
import { rotateByAngle } from '../render/effects/core.js';
import type { EffectSystem } from '../render/effects/effectSystem.js';

/** CreateFire's kinds: 0 a flame that lights the ground, 1 and 2 smoke. */
type FireKind = 0 | 1 | 2;

/** The fires of each object type: kind and the offset from the object (turned with it). By map number. */
const OBJECT_FIRES: Record<number, Record<number, [FireKind, number, number, number][]>> = {
  0: {
    130: [[0, 0, 0, 0]],                         // MODEL_LIGHT01
    131: [[1, 0, 0, 0]],                         // MODEL_LIGHT01 + 1
    132: [[2, 0, 0, 0]],                         // MODEL_LIGHT01 + 2
    80: [[0, 90, -200, 30], [0, 90, 200, 30]],   // MODEL_BRIDGE
    55: [[0, -150, -150, 140], [0, 150, -150, 140]], // MODEL_DUNGEON_GATE
    50: [[0, 0, 0, 200]],                        // MODEL_FIRE_LIGHT01
    51: [[0, 0, -30, 60]],                       // MODEL_FIRE_LIGHT01 + 1
    52: [[0, 0, 0, 60]],                         // MODEL_BONFIRE
  },
};

/** The desktop animates only the objects in view; here, those within this distance of the hero. */
const FIRE_RANGE = 2000;

export interface ObjectFire {
  kind: FireKind;
  position: THREE.Vector3;
  angle: THREE.Vector3;
  /** The object's light (o->Light), which the smoke takes. */
  light: THREE.Vector3;
}

/** The fires of a map's objects. `lightAt` gives the light map's colour at a world position. */
export function objectFires(mapNumber: number, objects: readonly WorldObject[], lightAt: (x: number, y: number) => THREE.Vector3): ObjectFire[] {
  const table = OBJECT_FIRES[mapNumber];
  if (!table) return [];
  const fires: ObjectFire[] = [];
  for (const object of objects) {
    for (const [kind, x, y, z] of table[object.type] ?? []) {
      const angle = new THREE.Vector3(object.angle[0], object.angle[1], object.angle[2]);
      const position = rotateByAngle(new THREE.Vector3(x, y, z), angle).add(new THREE.Vector3(object.position[0], object.position[1], object.position[2]));
      fires.push({ kind, position, angle, light: lightAt(object.position[0], object.position[1]) });
    }
  }
  return fires;
}

const flameLight = new THREE.Vector3();
const at = new THREE.Vector3();

/** One frame of CreateFire for each fire near (x, y). */
export function tickObjectFires(effects: EffectSystem, fires: readonly ObjectFire[], x: number, y: number): void {
  for (const fire of fires) {
    if (Math.abs(fire.position.x - x) > FIRE_RANGE || Math.abs(fire.position.y - y) > FIRE_RANGE) continue;
    at.set(fire.position.x + randomRange(-8, 7), fire.position.y + randomRange(-8, 7), fire.position.z + randomRange(-8, 7));
    const chance = Math.random() < 0.5; // rand_fps_check(2)
    if (fire.kind === 0) {
      const luminosity = randomRange(6, 11) * 0.1;
      flameLight.set(luminosity, luminosity * 0.6, luminosity * 0.4);
      if (chance) effects.createParticle('fire', at, fire.angle, flameLight, Math.floor(Math.random() * 4));
      effects.addTerrainLight(at.x, at.y, flameLight, 4);
    } else if (chance) {
      effects.createParticle('smoke', at, fire.angle, fire.light, fire.kind === 2 ? 2 : 0);
    }
  }
}

/** Random::RangeFloat(min, max). */
function randomRange(min: number, max: number): number {
  return min + Math.random() * (max - min);
}
