// HNMO's own skills (700-727, four per class; client/web/NEW_SKILLS.md): the server defines them
// (public/data/new-skills.json is the list it was built from, checked by the server's tests); this
// is how the web shows them — how each is cast, the hero's animation, its icon (a game icon in
// another colour), its effect (the game's own effect textures and models, one recipe per skill)
// and its sound (the game's own sound files).

import * as THREE from 'three';
import { PlayerActions as A } from './combatLook.js';
import type { SkillData } from './skills.js';
import type { EffectSystem } from '../render/effects/index.js';
import type { EffectOwner, EffectType, ParticleType, SpriteType, JointType } from '../render/effects/types.js';

export const NEW_SKILL_FIRST = 700;
export const NEW_SKILL_LAST = 727;
export const isNewSkill = (skill: number): boolean => skill >= NEW_SKILL_FIRST && skill <= NEW_SKILL_LAST;

/** The entries of public/data/new-skills.json. */
export interface NewSkillEntry {
  number: number; class: string; letter: string; name: string; level: number | null; skillType: string; target: string;
  damageType: string; damage: number; manaCost: number; agCost: number; range: number; cooldownMs: number;
  element: string | null; effect: number | null; areaRange: number | null;
}

/** The web's class index (CharacterClass) of the server's class names. */
const CLASS_INDEX: Record<string, number> = { DarkWizard: 0, DarkKnight: 1, FairyElf: 2, MagicGladiator: 3, DarkLord: 4, Summoner: 5, RageFighter: 6 };

type Rgb = [number, number, number];
type Anchor = 'caster' | 'target';
/** One part of an effect: what to make, where, how many, in which colour, over how many ticks. */
interface Step {
  make: { particle: ParticleType } | { sprite: SpriteType } | { effect: EffectType; subType?: number } | { joint: JointType };
  at: Anchor;
  /** 'point': at the anchor; 'ring': around it; 'rain': falling from above it; 'line': from the caster to the target. */
  shape: 'point' | 'ring' | 'rain' | 'line';
  count: number;
  /** Radius (ring, rain) in game units (a cell is 100). */
  radius?: number;
  /** Height over the feet. */
  height?: number;
  color: Rgb;
  scale?: number;
  /** Repeated on this many ticks (25 a second), from `start`. */
  ticks?: number;
  start?: number;
}

export interface NewSkillLook {
  /** The hero's animation (the class's own skill animations). 'magic' is the class's casting pose. */
  action: number | 'magic' | 'attack';
  /** How it's sent: at a target (TargetedSkill) or an area (AreaSkill); buffs and summons on the hero. */
  packet: 'targeted' | 'area';
  /** Area skills: at the hero's cell (around the hero) or at the target / clicked cell. */
  areaAt?: 'self' | 'target';
  /** Needs a monster (or player) to aim at. */
  needsTarget: boolean;
  /** Cast on the hero (buffs, summons). */
  friendly: boolean;
  /** The game skill whose icon it wears, and the colour shift in degrees. */
  icon: { from: number; hue: number };
  sound: string;
  effect: Step[];
}

const FIRE: Rgb = [1, 0.55, 0.2], ICE: Rgb = [0.45, 0.75, 1], GOLD: Rgb = [1, 0.85, 0.35], GREEN: Rgb = [0.4, 1, 0.45];
const VIOLET: Rgb = [0.75, 0.4, 1], RED: Rgb = [1, 0.25, 0.25], WHITE: Rgb = [1, 1, 1], EARTH: Rgb = [0.75, 0.6, 0.4];
const POISON: Rgb = [0.55, 1, 0.3], DARK: Rgb = [0.45, 0.35, 0.7];

const burst = (particle: ParticleType, color: Rgb, count: number, at: Anchor = 'target'): Step => ({ make: { particle }, at, shape: 'point', count, height: 80, color });
const ring = (particle: ParticleType, color: Rgb, radius: number, count: number, at: Anchor = 'caster', ticks = 1): Step => ({ make: { particle }, at, shape: 'ring', radius, count, height: 20, color, ticks });
const glow = (sprite: SpriteType, color: Rgb, scale: number, at: Anchor, ticks = 10): Step => ({ make: { sprite }, at, shape: 'point', count: 1, height: 90, color, scale, ticks });
const circle = (color: Rgb, at: Anchor = 'caster'): Step => ({ make: { effect: 'magicGround2' }, at, shape: 'point', count: 1, color });
const bolt = (color: Rgb, count = 2): Step => ({ make: { joint: 'jointThunder' }, at: 'caster', shape: 'line', count, height: 120, color, ticks: 6 });
const beam = (color: Rgb): Step => ({ make: { joint: 'jointHealing' }, at: 'caster', shape: 'line', count: 1, height: 110, color, ticks: 8 });
const rain = (particle: ParticleType, color: Rgb, radius: number, count: number, ticks: number): Step => ({ make: { particle }, at: 'target', shape: 'rain', radius, count, color, ticks });
const stones = (at: Anchor, count: number): Step => ({ make: { effect: 'stone1' }, at, shape: 'ring', radius: 120, count, color: EARTH });
const fireBall = (color: Rgb): Step => ({ make: { effect: 'fireModel' }, at: 'caster', shape: 'line', count: 1, height: 100, color });
const wave = (color: Rgb): Step => ({ make: { effect: 'magic2', subType: 2 }, at: 'caster', shape: 'point', count: 1, color });

/** The 28 skills' looks, by skill number (700 + 4 × class + letter; the server's class order). */
export const NEW_SKILL_LOOKS: Record<number, NewSkillLook> = {
  // Dark Knight
  700: { action: A.ATTACK_SKILL_SWORD4, packet: 'area', areaAt: 'self', needsTarget: false, friendly: false, icon: { from: 42, hue: 30 }, sound: 'SOUND_SKILL_SWORD4',
    effect: [stones('caster', 10), ring('smoke', EARTH, 220, 16), ring('spark', ICE, 180, 10)] },
  701: { action: A.ATTACK_SKILL_SWORD5, packet: 'targeted', needsTarget: true, friendly: false, icon: { from: 23, hue: -30 }, sound: 'SOUND_SKILL_SWORD2',
    effect: [{ ...burst('blood2', RED, 12), ticks: 3, start: 2 }, { ...burst('spark', RED, 8), ticks: 3, start: 2 }] },
  702: { action: A.SKILL_VITALITY, packet: 'targeted', needsTarget: false, friendly: true, icon: { from: 27, hue: 40 }, sound: 'SOUND_SOULBARRIER',
    effect: [circle(GOLD), glow('shiny2', GOLD, 2.5, 'caster', 20), ring('spark', GOLD, 90, 12)] },
  703: { action: A.ATTACK_SKILL_SWORD2, packet: 'targeted', needsTarget: true, friendly: false, icon: { from: 20, hue: 200 }, sound: 'SOUND_RIDINGSPEAR',
    effect: [{ make: { particle: 'smoke' }, at: 'caster', shape: 'line', count: 8, height: 20, color: EARTH }, burst('spark', WHITE, 16), glow('light', WHITE, 2, 'target', 6)] },
  // Dark Wizard
  704: { action: 'magic', packet: 'area', areaAt: 'target', needsTarget: false, friendly: false, icon: { from: 39, hue: 20 }, sound: 'SOUND_ICE',
    effect: [{ make: { sprite: 'flareBlue' }, at: 'caster', shape: 'line', count: 10, height: 110, color: ICE, scale: 1.6, ticks: 6 }, { make: { particle: 'energy' }, at: 'caster', shape: 'line', count: 12, height: 100, color: ICE }] },
  705: { action: 'magic', packet: 'area', areaAt: 'target', needsTarget: false, friendly: false, icon: { from: 13, hue: -20 }, sound: 'SOUND_METEORITE01',
    effect: [rain('fire', FIRE, 150, 2, 60), { ...stones('target', 3), ticks: 1, start: 15 }, { ...stones('target', 3), ticks: 1, start: 45 }, ring('smoke', FIRE, 150, 10, 'target', 20)] },
  706: { action: 'magic', packet: 'targeted', needsTarget: true, friendly: false, icon: { from: 3, hue: 60 }, sound: 'SOUND_THUNDER01',
    effect: [bolt(VIOLET), { ...ring('energy', VIOLET, 80, 6, 'target', 30) }, glow('energy', VIOLET, 1.8, 'target', 25)] },
  707: { action: 'magic', packet: 'targeted', needsTarget: false, friendly: true, icon: { from: 16, hue: 60 }, sound: 'SOUND_SOULBARRIER',
    effect: [circle(ICE), glow('flareBlue', ICE, 3, 'caster', 25), ring('energy', ICE, 80, 10)] },
  // Fairy Elf
  708: { action: 'attack', packet: 'area', areaAt: 'target', needsTarget: false, friendly: false, icon: { from: 52, hue: -40 }, sound: 'SOUND_FLAME',
    effect: [fireBall(FIRE), { make: { particle: 'fire' }, at: 'caster', shape: 'line', count: 14, height: 110, color: FIRE, ticks: 4 }] },
  709: { action: 'attack', packet: 'area', areaAt: 'target', needsTarget: false, friendly: false, icon: { from: 51, hue: 30 }, sound: 'SOUND_ICE',
    effect: [rain('energy', ICE, 200, 3, 25), ring('spark', ICE, 200, 12, 'target', 4)] },
  710: { action: 'magic', packet: 'targeted', needsTarget: false, friendly: true, icon: { from: 26, hue: 90 }, sound: 'SOUND_SKILL_RECOVER',
    effect: [circle(GREEN), ring('energy', GREEN, 120, 6, 'caster', 40), glow('shiny2', GREEN, 2.2, 'caster', 30)] },
  711: { action: 'magic', packet: 'targeted', needsTarget: false, friendly: true, icon: { from: 30, hue: 180 }, sound: 'SOUND_MAGIC',
    effect: [circle(WHITE), ring('smoke', WHITE, 100, 14), glow('light', ICE, 2.5, 'caster', 12)] },
  // Magic Gladiator
  712: { action: A.ATTACK_SKILL_SWORD3, packet: 'area', areaAt: 'target', needsTarget: true, friendly: false, icon: { from: 56, hue: 180 }, sound: 'SOUND_SKILL_CHAIN_LIGHTNING',
    effect: [bolt(ICE, 3), burst('spark', ICE, 14), ring('energy', ICE, 300, 6, 'target')] },
  713: { action: A.ATTACK_SKILL_WHEEL, packet: 'area', areaAt: 'self', needsTarget: false, friendly: false, icon: { from: 55, hue: -20 }, sound: 'SOUND_ATTACK_FIRE_BUST',
    effect: [ring('fire', FIRE, 200, 8, 'caster', 12), wave(FIRE)] },
  714: { action: 'magic', packet: 'targeted', needsTarget: false, friendly: true, icon: { from: 28, hue: 120 }, sound: 'SOUND_SWELLLIFE',
    effect: [circle(VIOLET), glow('shiny2', VIOLET, 2.4, 'caster', 20), ring('spark', FIRE, 90, 8), ring('spark', ICE, 90, 8)] },
  715: { action: 'magic', packet: 'area', areaAt: 'target', needsTarget: false, friendly: false, icon: { from: 57, hue: 160 }, sound: 'SOUND_FLASH',
    effect: [rain('energy', GOLD, 180, 2, 30), glow('light', GOLD, 3, 'target', 20), { ...ring('spark', GOLD, 180, 14, 'target'), start: 20 }] },
  // Dark Lord
  716: { action: 'magic', packet: 'area', areaAt: 'target', needsTarget: true, friendly: false, icon: { from: 78, hue: 220 }, sound: 'SOUND_FIRE_SCREAM',
    effect: [{ make: { particle: 'smoke' }, at: 'caster', shape: 'line', count: 10, height: 140, color: DARK, ticks: 5 }, burst('smoke', DARK, 10), ring('spark', DARK, 250, 10, 'target')] },
  717: { action: A.SKILL_VITALITY, packet: 'targeted', needsTarget: false, friendly: true, icon: { from: 64, hue: 100 }, sound: 'SOUND_SWELLLIFE',
    effect: [circle(RED), ring('spark', RED, 150, 16), glow('shiny2', RED, 2.6, 'caster', 20)] },
  718: { action: A.SKILL_STAMP, packet: 'area', areaAt: 'self', needsTarget: false, friendly: false, icon: { from: 62, hue: 30 }, sound: 'SOUND_SKILL_SWORD4',
    effect: [stones('caster', 12), ring('smoke', EARTH, 250, 20), wave(EARTH)] },
  719: { action: A.SKILL_FLASH, packet: 'area', areaAt: 'target', needsTarget: false, friendly: false, icon: { from: 65, hue: 40 }, sound: 'SOUND_FLASH',
    effect: [beam(GOLD), { make: { sprite: 'light' }, at: 'caster', shape: 'line', count: 8, height: 110, color: GOLD, scale: 1.8, ticks: 8 }] },
  // Summoner
  720: { action: 'magic', packet: 'area', areaAt: 'target', needsTarget: false, friendly: false, icon: { from: 225, hue: 80 }, sound: 'SOUND_EVIL',
    effect: [ring('smoke', POISON, 180, 8, 'target', 40), glow('energy', POISON, 2.5, 'target', 40)] },
  721: { action: 'magic', packet: 'targeted', needsTarget: false, friendly: true, icon: { from: 223, hue: 200 }, sound: 'SOUND_HELLFIRE',
    effect: [circle(RED), ring('fire', RED, 100, 10), glow('light', RED, 2.5, 'caster', 12)] },
  722: { action: 'magic', packet: 'targeted', needsTarget: true, friendly: false, icon: { from: 221, hue: 140 }, sound: 'SOUND_EVIL',
    effect: [beam(DARK), ring('smoke', DARK, 70, 8, 'target', 20)] },
  723: { action: 'magic', packet: 'targeted', needsTarget: true, friendly: false, icon: { from: 214, hue: 180 }, sound: 'SOUND_SKILL_DRAIN_LIFE',
    effect: [beam(ICE), { make: { particle: 'energy' }, at: 'target', shape: 'line', count: 8, height: 110, color: ICE, ticks: 10 }] },
  // Rage Fighter
  724: { action: A.SKILL_DRAGONLORE, packet: 'area', areaAt: 'target', needsTarget: false, friendly: false, icon: { from: 264, hue: 40 }, sound: 'SOUND_CRITICAL',
    effect: [{ make: { sprite: 'light' }, at: 'caster', shape: 'line', count: 10, height: 100, color: GOLD, scale: 2, ticks: 6 }, fireBall(GOLD)] },
  725: { action: A.SKILL_DRAGONKICK, packet: 'area', areaAt: 'self', needsTarget: false, friendly: false, icon: { from: 262, hue: 160 }, sound: 'SOUND_SKILL_SWORD3',
    effect: [ring('spark', WHITE, 150, 10, 'caster', 4), ring('smoke', WHITE, 150, 8)] },
  726: { action: A.SKILL_ATT_UP_OURFORCES, packet: 'targeted', needsTarget: false, friendly: true, icon: { from: 268, hue: 40 }, sound: 'SOUND_SOULBARRIER',
    effect: [circle(GOLD), glow('shiny2', EARTH, 2.4, 'caster', 20), ring('spark', GOLD, 90, 10)] },
  727: { action: A.SKILL_GIANTSWING, packet: 'area', areaAt: 'self', needsTarget: false, friendly: false, icon: { from: 265, hue: 200 }, sound: 'SOUND_SKILL_SWORD4',
    effect: [stones('caster', 10), wave(ICE), ring('energy', ICE, 250, 12)] },
};

let entries: NewSkillEntry[] | null = null;

/** Loads the list and returns each skill as the game's skill data (name, level, costs, range, cooldown, class). */
export async function loadNewSkills(): Promise<Map<number, SkillData>> {
  if (!entries) {
    const response = await fetch('/data/new-skills.json');
    entries = response.ok ? await response.json() as NewSkillEntry[] : [];
  }
  const result = new Map<number, SkillData>();
  for (const e of entries) {
    const classes = [0, 0, 0, 0, 0, 0, 0];
    const index = CLASS_INDEX[e.class];
    if (index !== undefined) classes[index] = 1;
    result.set(e.number, {
      name: e.name, level: e.level ?? 0, damage: e.damage, mana: e.manaCost, ability: e.agCost, distance: e.range, delay: e.cooldownMs,
      energy: 0, leadership: 0, masteryType: 0, useType: 0, brand: 0, killCount: 0, classes, icon: 0, strength: 0, dexterity: 0,
    });
  }
  return result;
}

/** The list entry of a skill (for the tooltip's lines), once loaded. */
export function newSkillEntry(skill: number): NewSkillEntry | null {
  return entries?.find((e) => e.number === skill) ?? null;
}

const point = new THREE.Vector3();
const UP = new THREE.Vector3(0, 0, 0);

/** Plays a new skill's effect at its cast (the caster's and the target's positions are read live). */
export function playNewSkillEffect(system: EffectSystem, skill: number, caster: EffectOwner, target: EffectOwner | null): boolean {
  const look = NEW_SKILL_LOOKS[skill];
  if (!look) return false;
  const last = Math.max(...look.effect.map((s) => (s.start ?? 0) + (s.ticks ?? 1)));
  let tick = 0;
  system.schedule(() => {
    for (const step of look.effect) {
      const from = step.start ?? 0;
      if (tick >= from && tick < from + (step.ticks ?? 1)) makeStep(system, step, caster, target ?? caster, tick - from);
    }
    tick++;
    return tick < last;
  });
  return true;
}

function makeStep(system: EffectSystem, step: Step, caster: EffectOwner, target: EffectOwner, tick: number): void {
  const anchor = step.at === 'caster' ? caster : target;
  const light = new THREE.Vector3(...step.color);
  const height = step.height ?? 0;
  for (let i = 0; i < step.count; i++) {
    const at = anchor.position;
    switch (step.shape) {
      case 'point':
        point.set(at.x + (Math.random() - 0.5) * 40, at.y + (Math.random() - 0.5) * 40, at.z + height);
        break;
      case 'ring': {
        const angle = (i / step.count) * Math.PI * 2 + tick * 0.4;
        const r = step.radius ?? 100;
        point.set(at.x + Math.cos(angle) * r, at.y + Math.sin(angle) * r, at.z + height);
        break;
      }
      case 'rain': {
        const r = (step.radius ?? 100) * Math.sqrt(Math.random());
        const angle = Math.random() * Math.PI * 2;
        point.set(at.x + Math.cos(angle) * r, at.y + Math.sin(angle) * r, at.z + 250 + Math.random() * 150);
        break;
      }
      case 'line': {
        const t = step.count > 1 ? i / (step.count - 1) : 1;
        point.lerpVectors(caster.position, target.position, t);
        point.z += height;
        break;
      }
    }
    make(system, step, point, caster, target, light);
  }
}

function make(system: EffectSystem, step: Step, at: THREE.Vector3, caster: EffectOwner, target: EffectOwner, light: THREE.Vector3): void {
  const kind = step.make;
  if ('particle' in kind) system.createParticle(kind.particle, at, caster.angle, light, 0, step.scale ?? 1);
  else if ('sprite' in kind) system.createSprite(kind.sprite, at, step.scale ?? 1, light);
  else if ('effect' in kind) system.createEffect(kind.effect, at, caster.angle, light, kind.subType ?? 0, kind.effect === 'magicGround2' ? caster : null);
  else {
    const from = caster.position.clone();
    from.z += step.height ?? 100;
    system.createJoint(kind.joint, from, target.position, UP.set(-60, 0, caster.angle.z), 0, target, 30);
  }
}

/** The buffs and debuffs of HNMO's skills (the server's magic effects 180-186), for the buff strip: a name, what it does, and the game buff whose icon it shows. */
export const NEW_SKILL_BUFFS: { number: number; name: string; kind: 'buff' | 'debuff'; text: string; iconFrom: number }[] = [
  { number: 180, name: 'Iron Guard', kind: 'buff', text: 'Damage taken decreases by 20%.', iconFrom: 2 },
  { number: 181, name: 'Mana Shield', kind: 'buff', text: 'Part of the damage taken is paid with mana.', iconFrom: 4 },
  { number: 182, name: 'Forest Blessing', kind: 'buff', text: 'Life recovers over time.', iconFrom: 8 },
  { number: 183, name: 'Twin Power', kind: 'buff', text: 'All damage increases by 10%.', iconFrom: 1 },
  { number: 184, name: 'War Command', kind: 'buff', text: 'Attack and magic speed increase.', iconFrom: 5 },
  { number: 185, name: 'Iron Body', kind: 'buff', text: 'Defence increases and part of the damage is reflected.', iconFrom: 2 },
  { number: 186, name: 'Weakening Curse', kind: 'debuff', text: 'Defence decreases by 20%.', iconFrom: 58 },
];
