// An EffectOwner view of a web client character (game/characters.ts), so effects can follow
// it: one view per character, so DeleteJoint / SearchJoint find the joints made for it.

import * as THREE from 'three';
import type { Character } from '../../game/characters.js';
import type { EffectOwner } from './types.js';

/** c->Weapon[0].LinkBone and c->Weapon[1].LinkBone of the player model (ZzzCharacter.cpp). */
export const PLAYER_WEAPON_BONES: readonly [number, number] = [33, 42];
const RADIANS_TO_DEGREES = 180 / Math.PI;

export interface CharacterOwnerOptions {
  /** The bones the weapons hang on; the player model's by default. */
  weaponBones?: readonly [number, number];
  /** Whether it has a buff that keeps aura joints alive (eBuff_Attack, eBuff_Defense or eBuff_HelpNpc). */
  hasAura?: () => boolean;
}

const owners = new WeakMap<Character, EffectOwner>();

/** The character as an effect owner (the same object for the same character). */
export function ownerFromCharacter(character: Character, options: CharacterOwnerOptions = {}): EffectOwner {
  const known = owners.get(character);
  if (known) return known;
  const position = new THREE.Vector3();
  const angle = new THREE.Vector3();
  const light = new THREE.Vector3();
  const bones = options.weaponBones ?? PLAYER_WEAPON_BONES;
  const owner: EffectOwner = {
    get position(): THREE.Vector3 {
      return position.set(character.position.x, character.position.y, character.group.position.z);
    },
    get angle(): THREE.Vector3 {
      return angle.set(0, 0, character.angle * RADIANS_TO_DEGREES);
    },
    get light(): THREE.Vector3 {
      return light.set(character.light.r, character.light.g, character.light.b);
    },
    isLive: () => !character.dead && character.group.visible && character.group.parent !== null,
    weaponPosition: (hand) => {
      const bone = character.model.matrices[bones[hand]];
      if (!bone) return null;
      character.group.updateMatrix();
      return new THREE.Vector3().applyMatrix4(bone).applyMatrix4(character.group.matrix);
    },
    hasAura: options.hasAura,
  };
  owners.set(character, owner);
  return owner;
}
