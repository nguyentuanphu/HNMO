// HNMO effect system: a port of the desktop client's effect core (Render/Effects) for the web
// client, so skills show their effects.
//
// Usage
// -----
//   import { EffectSystem, playSkillEffect, ownerFromCharacter, SkillEffect } from './render/effects/index.js';
//
//   // Once, with the scene's MU-space root (x, y on the ground, z up; main.ts's `root`):
//   const effects = new EffectSystem(root, { camera, heightAt: (x, y) => sampleHeight(world.heights, x, y) });
//   // Every frame (seconds); it steps in whole 40 ms ticks like the desktop and redraws:
//   effects.update(dt);
//
//   // When a skill is cast (the attack packet / the own cast), with the caster and its target:
//   playSkillEffect(effects, SkillEffect.ENERGY_BALL, ownerFromCharacter(caster), ownerFromCharacter(target));
//   // It runs the desktop's timeline: per-tick effects while AttackTime counts 1..14, then the
//   // release effects (SKILL_RELEASE_DELAY_SECONDS after the cast). Options: `hit` (blood),
//   // `slashTwoHandAction`, `targetHadDefense`, `casterCloaked`, and for Triple Shot `shootArrow`
//   // (shoot with ProjectileLayer.shoot at an angle offset) and `bowNumbers`.
//
//   // Greater Defense's orbiting lights live while the target's `hasAura()` says so; or end them:
//   effects.deleteJoint('spearSkill', ownerFromCharacter(target, { hasAura }), 4);
//
// Lower level, mirroring the desktop's calls (types are the names in Data/Effects/*Types.json):
//   effects.createEffect(type, position, angle, light, subType?, owner?, scale?)       CreateEffect
//   effects.createParticle(type, position, angle, light, subType?, scale?, owner?)     CreateParticle
//   effects.createSprite(type, position, scale, light, owner?, rotation?, subType?)    CreateSprite
//   effects.createJoint(type, position, targetPosition, angle, subType?, target?, scale?) CreateJoint
//   effects.schedule(task)  runs task(tick) every tick until it returns false
// Positions are MU units, angles degrees (z = facing, 0 faces -y), lights r g b (may exceed 1).
// An owner is any EffectOwner ({ position, angle, light?, isLive?, weaponPosition?, hasAura? });
// ownerFromCharacter adapts a game/characters.ts Character (same object per character).
// Only the types and SubTypes the ported skills use are implemented; the others are ignored.

export { EffectSystem } from './effectSystem.js';
export type { EffectSystemOptions, TickTask } from './effectSystem.js';
export { playSkillEffect, isPortedSkill, tripleShotAngles, createSpark, SkillEffect, ATTACK_LIMIT_TIME, SKILL_RELEASE_DELAY_SECONDS } from './skillEffects.js';
export type { SkillEffectOptions } from './skillEffects.js';
export { ownerFromCharacter, PLAYER_WEAPON_BONES } from './characterOwner.js';
export type { CharacterOwnerOptions } from './characterOwner.js';
export { TICKS_PER_SECOND, TICK_SECONDS, BITMAPS } from './core.js';
export type { BitmapName } from './core.js';
export type { EffectOwner, EffectType, ParticleType, SpriteType, JointType } from './types.js';
