// What the desktop draws on a character for its buffs and debuffs (the magic-effect state the server sends):
// the body's colour while a debuff lasts, and the effects that come with a buff and stay while it lasts.
//
// Followed C++ (client/src/source):
//   RenderPartObjectEffect (a player's parts: cloak, poison, freeze, stun, harden, Blow of Destruction colours)
//                                                                          Engine/Object/ZzzObject.cpp
//   RenderObject's monster body colours (poison, freeze, Blow of Destruction)  Engine/Object/ZzzObject.cpp
//   InsertBuffPhysicalEffect / ClearBuffPhysicalEffect                     Network/Server/WSclient.cpp
//   UpdateHeldWeaponEffects (eBuff_AddCriticalDamage)                      Engine/Object/ZzzCharacter.cpp

import * as THREE from 'three';
import type { EffectSystem } from './effectSystem.js';
import type { EffectOwner } from './types.js';
import { CRITICAL_DAMAGE_BUFF, criticalDamageSparks } from './darkLordEffects.js';
import { RageBuff } from './rageEffects.js';

/** The eBuffState numbers the looks read. */
export const Buff = {
  ATTACK: 1, DEFENSE: 2, WIZ_DEFENSE: 4, ADD_CRITICAL_DAMAGE: CRITICAL_DAMAGE_BUFF, LIFE: 8, CLOAKING: 18,
  POISON: 55, FREEZE: 56, HARDEN: 57, STUN: 61, ATTACK_DOWN: 76, DEFENSE_DOWN: 77,
  GIGANTIC_STORM_DAMAGE: 84, LIGHTNING_SHOCK_DAMAGE: 85, BLOW_OF_DESTRUCTION: 86,
  ATT_UP: RageBuff.ATT_UP, HP_UP: RageBuff.HP_UP, DEF_UP: RageBuff.DEF_UP,
} as const;

/**
 * The body colour a debuff gives (b->BodyLight instead of the light it stands in), or null. Players' parts and
 * monsters' bodies take different colours; the first that applies wins, as the desktop's else-if chain.
 */
export function debuffTint(has: (buff: number) => boolean, player: boolean): [number, number, number] | null {
  if (player) {
    if (has(Buff.CLOAKING)) return null; // drawn see-through in chrome instead (not on the web)
    if (has(Buff.POISON) && has(Buff.FREEZE)) return [0.3, 1, 1];
    if (has(Buff.BLOW_OF_DESTRUCTION)) return [0.3, 1, 1];
    if (has(Buff.POISON)) return [0.3, 1, 0.5];
    if (has(Buff.STUN)) return [0, 0, 1];
    if (has(Buff.FREEZE)) return [0.3, 0.5, 1];
    if (has(Buff.HARDEN)) return [0.3, 0.5, 1];
    return null;
  }
  if (has(Buff.POISON) && has(Buff.FREEZE)) return [0.3, 1, 0.8];
  if (has(Buff.POISON)) return [0.3, 1, 0.5];
  if (has(Buff.FREEZE)) return [0.3, 0.5, 1];
  if (has(Buff.BLOW_OF_DESTRUCTION)) return [0.3, 0.5, 1];
  return null;
}

const WHITE = new THREE.Vector3(1, 1, 1);
const sparking = new WeakSet<EffectOwner>();

/**
 * InsertBuffPhysicalEffect (on) and ClearBuffPhysicalEffect (off) for the buffs besides the ones skillEffects.ts's
 * playBuffEffect handles (Soul Barrier, Greater Defense, Swell Life). Returns whether it knew the buff.
 */
export function playBuffLook(system: EffectSystem, buff: number, owner: EffectOwner, on: boolean): boolean {
  switch (buff) {
    case Buff.ADD_CRITICAL_DAMAGE:
      if (on && !sparking.has(owner)) {
        sparking.add(owner);
        system.schedule(() => {
          const live = owner.isLive?.() ?? true;
          if (!live || !(owner.hasBuff?.(Buff.ADD_CRITICAL_DAMAGE) ?? false)) {
            sparking.delete(owner);
            return false;
          }
          if (!(owner.weaponsOnBack?.() ?? false) && !(owner.hasBuff?.(Buff.CLOAKING) ?? false)) {
            criticalDamageSparks(system, owner, owner.heldWeapons?.() ?? []);
          }
          return true;
        });
      }
      return true;
    case Buff.ATT_UP:
    case Buff.HP_UP:
    case Buff.DEF_UP: {
      // The glow that stays with the fighter's party buff on everyone it reaches (BITMAP_LIGHT_RED 1, 0, 2).
      const glow = buff === Buff.HP_UP ? 0 : buff === Buff.DEF_UP ? 2 : 1;
      if (on && !system.searchEffect('lightRed', owner, glow)) system.createEffect('lightRed', owner.position, owner.angle, owner.light ?? WHITE, glow, owner, 1.5);
      return true;
    }
    case Buff.ATTACK_DOWN:
    case Buff.DEFENSE_DOWN: {
      // Weakness / Innovation on a monster: shines and pins of light fall from its bones while it lasts.
      const sub = buff === Buff.ATTACK_DOWN ? 1 : 2;
      const light = buff === Buff.ATTACK_DOWN ? new THREE.Vector3(1.4, 0.2, 0.2) : new THREE.Vector3(0.25, 1, 0.7);
      if (on) {
        system.createEffect('shiny5', owner.position, owner.angle, light, sub, owner, 0.5);
        system.createEffect('pinLight', owner.position, owner.angle, light, sub, owner, 1);
      }
      return true;
    }
    case Buff.GIGANTIC_STORM_DAMAGE:
      if (on) system.createEffect('jointThunderEffect', owner.position, owner.angle, owner.light ?? WHITE, 0, owner);
      return true;
    case Buff.LIGHTNING_SHOCK_DAMAGE:
      if (on) system.createEffect('lightningShock', owner.position, owner.angle, owner.light ?? WHITE, 2, owner);
      return true;
    case Buff.BLOW_OF_DESTRUCTION:
      if (on) system.createEffect('ice', owner.position, owner.angle, owner.light ?? WHITE, 0, owner);
      return true;
  }
  return false;
}
