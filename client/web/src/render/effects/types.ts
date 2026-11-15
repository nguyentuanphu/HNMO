// The objects of the effect system, mirroring the desktop's OBJECT (effects, sprites), PARTICLE
// and JOINT records (Engine/Object/ZzzObject.h). Units are the desktop's: x and y on the ground,
// z up, angles in degrees, lifetimes and speeds per 25 FPS tick.

import type * as THREE from 'three';
import type { AnimatedModel } from '../../bmd.js';
import type { BitmapName } from './core.js';

/**
 * What an effect can follow or aim at: a character, a monster or another object. The effect
 * system reads it every tick, so keep the vectors up to date (or return live views).
 */
export interface EffectOwner {
  /** Where it stands: x, y on the ground and z at its feet (world units). */
  readonly position: THREE.Vector3;
  /** Its angles in degrees; z is the facing (0 faces -y, as the desktop's o->Angle[2]). */
  readonly angle: THREE.Vector3;
  /** Its light (o->Light), the colour some effects take from their maker. White when left out. */
  readonly light?: THREE.Vector3;
  /** Whether it is alive and shown (o->Live && o->Visible). Effects that track it end when it isn't. */
  isLive?(): boolean;
  /**
   * The world position of the bone a weapon hangs on (c->Weapon[hand].LinkBone, 33 and 42 on the
   * player model). Lightning starts there; without it the start is 130 units over the feet.
   */
  weaponPosition?(hand: 0 | 1): THREE.Vector3 | null;
  /** The world position of one of its model's bones (b->TransformPosition(o->BoneTransform[bone])). */
  bonePosition?(bone: number): THREE.Vector3 | null;
  /** A bone's whole world matrix (o->BoneTransform[bone] placed in the world), for models held in a hand. */
  boneMatrix?(bone: number): THREE.Matrix4 | null;
  /** How many bones its model has (b->NumBones). */
  boneCount?(): number;
  /** The action its model plays (o->CurrentAction), for effects that last while an action plays. */
  currentAction?(): number;
  /** Where its action is (o->AnimationFrame, in keys), for effects that follow a swing. */
  animationFrame?(): number;
  /** An action's PlaySpeed (keys per 25 FPS tick), for effects timed to the caster's actions. */
  playSpeed?(action: number): number;
  /**
   * Whether it still has a buff that keeps aura joints (Greater Defense's orbiting lights) alive:
   * eBuff_Attack, eBuff_Defense or eBuff_HelpNpc (ShouldKeepAuraJointAlive). Alive while it lives
   * when left out.
   */
  hasAura?(): boolean;
  /** Whether it has a buff (eBuffState number), for the buff looks that last while it does. */
  hasBuff?(buff: number): boolean;
  /** Its right hand weapon's model and item group, for the effects that draw it (Twisting Slash's wheel, Rageful Blow). */
  weapon?(): { model: THREE.Object3D; group: number } | null;
  /** The weapons in its hands (not arrows, bolts or a shield): their bones, and whether each is a bow. */
  heldWeapons?(): { bone: number; bow: boolean }[];
  /** Whether its weapons hang on its back (a safe zone): the held weapons' effects don't show then. */
  weaponsOnBack?(): boolean;
}

/** CreateEffect types ported (names as in Data/Effects/EffectTypes.json). */
export type EffectType =
  | 'energy' // BITMAP_ENERGY: Energy Ball's flying ball
  | 'fireModel' // MODEL_FIRE: Fire Ball's ball, Meteorite's rock, Decay's falling poison (Skill/Fire01.bmd)
  | 'magic2' // MODEL_MAGIC2: Power Wave's wave, Power Slash's blades (Skill/Magic02.bmd)
  | 'magicGround2' // BITMAP_MAGIC+1: the healing / buff circle on the ground
  | 'stone1' | 'stone2' // MODEL_STONE1/2: debris
  | 'bone1' | 'bone2' // MODEL_BONE1/2: a skeleton's bones when it dies (SetPlayerDie)
  | 'ice' // MODEL_ICE: Ice's crystal (Skill/Ice01.bmd)
  | 'iceSmall' // MODEL_ICE_SMALL: its shards (Skill/Ice02.bmd)
  | 'poison' // MODEL_POISON: Poison's cloud (Skill/Poison01.bmd)
  | 'flame' // BITMAP_FLAME: Flame's burning ground
  | 'storm' // MODEL_STORM: Twister (Skill/Storm01.bmd)
  | 'circle' // MODEL_CIRCLE: Hellfire's circle, Nova's ring (Skill/Circle01.bmd)
  | 'circleLight' // MODEL_CIRCLE_LIGHT: Hellfire's light (Skill/Circle02.bmd)
  | 'bossLaser' // BITMAP_BOSS_LASER: Aqua Beam
  | 'skillInferno' // MODEL_SKILL_INFERNO: Inferno's ring, Decay's splash (Skill/Inferno01.bmd)
  | 'blizzardModel' // MODEL_BLIZZARD: Ice Storm's ice (Skill/blizzard.bmd)
  | 'laser' // MODEL_LASER: Evil Spirit's dark heads (Skill/Laser01.bmd)
  | 'spark3' // BITMAP_SPARK+1: Teleport's column of sparks
  | 'skillWheel1' | 'skillWheel2' // MODEL_SKILL_WHEEL1/2: Twisting Slash's spinning weapon
  | 'skillFuryStrike' // MODEL_SKILL_FURY_STRIKE: Rageful Blow
  | 'earthQuake1' | 'earthQuake2' | 'earthQuake3' | 'earthQuake4' // MODEL_SKILL_FURY_STRIKE+1..8:
  | 'earthQuake5' | 'earthQuake6' | 'earthQuake7' | 'earthQuake8' // Rageful Blow's cracks (Skill/EarthQuake0N.bmd)
  | 'wave' // MODEL_WAVE: Rageful Blow's flash (Skill/flashing.bmd)
  | 'tail' // MODEL_TAIL: Rageful Blow's falling streaks (Skill/tail.bmd)
  | 'lightSpear' // MODEL_SPEAR: the anchor of Impale's and Death Stab's spiralling flares (no model)
  | 'spearSkillModel' // MODEL_SPEARSKILL as an effect: Impale's thrusting spears (Skill/RidingSpear01.bmd)
  | 'combo' // MODEL_COMBO: the knights' combo burst
  | 'gathering' // BITMAP_GATHERING: Penetration's / Fire Slash's gathering light
  | 'multiShot1' | 'multiShot2' | 'multiShot3' // MODEL_MULTI_SHOT1..3: Multi-Shot's rings (Effect/multishot0N.bmd)
  | 'infinityArrow' | 'infinityArrow1' | 'infinityArrow2' | 'infinityArrow3' | 'infinityArrow4' // Infinity Arrow (Skill/arrowsre0N.bmd)
  | 'waves' // MODEL_WAVES: Force Wave's rings (Skill/m_Waves.bmd)
  | 'piercing2' // MODEL_PIERCING2: Force Wave's spear (Skill/m_Piercing.bmd)
  | 'pierPart' // MODEL_PIER_PART: Fire Burst's flames (Skill/PierPart.bmd)
  | 'darkLordSkill' // MODEL_DARKLORD_SKILL: the lord's glowing hands (Skill/DarkLordSkill.bmd)
  | 'swordForce' // BITMAP_SWORD_FORCE: Fire Slash
  | 'flameStrike' // MODEL_EFFECT_FLAME_STRIKE (Effect/FlameStrike.bmd)
  | 'jointThunderEffect' // BITMAP_JOINT_THUNDER as an effect: Gigantic Storm's bolts
  | 'magicCircle' // BITMAP_MAGIC: a magic circle on the ground
  | 'buffLight' // BITMAP_LIGHT SubType 1: the Swell Life buff's rising lights
  | 'aliceDrainLife' // MODEL_ALICE_DRAIN_LIFE: Drain Life (no model)
  | 'chainLightning' // MODEL_CHAIN_LIGHTNING: one link of Chain Lightning (no model)
  | 'aliceBuffSkillEffect' | 'aliceBuffSkillEffect2' // Sleep's and Berserker's rings (Effect/elshildring*.bmd)
  | 'magicZin' // BITMAP_MAGIC_ZIN: Weakness's and Innovation's circles
  | 'summonerCastingEffect2' | 'summonerCastingEffect22' | 'summonerCastingEffect222' // Effect/Suhwanzin2*.bmd
  | 'shiny5' // BITMAP_SHINY+6: falling shines
  | 'pinLight' // BITMAP_PIN_LIGHT: falling pins of light
  | 'lightningShock' // MODEL_LIGHTNING_SHOCK: Lightning Shock's ball and burst (no model)
  | 'magic' // BITMAP_MAGIC: circles on the ground
  | 'damage01Mono' // BITMAP_DAMAGE_01_MONO: a burst on the ground
  | 'wolfHeadEffect' // MODEL_WOLF_HEAD_EFFECT: Killing Blow's wolf heads (Effect/wolf_head_effect.bmd)
  | 'sbumb' // BITMAP_SBUMB: Killing Blow's hits on the target
  | 'downAttackDummyR' | 'downAttackDummyL' // Beast Uppercut's fists (Effect/down_*_punch.bmd)
  | 'swordEffectMono' // BITMAP_SWORD_EFFECT_MONO: the party buffs' rising blade
  | 'shockWaveGround1' // MODEL_SHOCKWAVE_GROUND01: their rings on the ground
  | 'eventCloud' // BITMAP_EVENT_CLOUD: their clouds on the ground
  | 'windForce' // MODEL_WINDFOCE: their whirl of wind (Effect/wind_foce.bmd)
  | 'lightRed' // BITMAP_LIGHT_RED: the party buffs' glow
  | 'chromeEnergy2' // BITMAP_CHROME_ENERGY2: Gigantic Storm's flashes on the ground
  | 'flareForce' // BITMAP_FLARE_FORCE: Electric Spike's beam and bolts, the Critical Damage buff's sparks (joints only)
  | 'darkHorseSkill' // UpdateDarkHorseSkill: Earthshake's stomp (no model)
  | 'shockWave' // BITMAP_SHOCK_WAVE: a shrinking ring on the ground
  | 'groundStone' | 'groundStone2' // MODEL_GROUND_STONE(2): Earthshake's slabs (Skill/GroundStone*.bmd)
  | 'desair' // MODEL_DESAIR: Chaotic Diseier's winged shadows (Skill/desair.bmd)
  | 'feather' // MODEL_FEATHER: their feathers (Skill/darkwing_hetachi.bmd)
  | 'windForceMirror' // MODEL_WINDFOCE_MIRROR: Increase Defense's passing wind (Effect/wind_foce_mirror.bmd)
  | 'dragonKickDummy' // MODEL_DRAGON_KICK_DUMMY: Dragon Slasher's timing
  | 'bladeSkill' // MODEL_BLADE_SKILL: Multi-Shot's fan (Effect/bladetonedo.bmd)
  | 'skillBlast' // MODEL_SKILL_BLAST: Cometfall's comets (Skill/Blast01.bmd)
  | 'blowOfDestruction' // MODEL_BLOW_OF_DESTRUCTION: Strike of Destruction (no model)
  | 'nightWater1' // MODEL_NIGHTWATER_01: its water splashes (Effect/nightwater01.bmd)
  | 'knightPlancrackA' | 'knightPlancrackB' // its ground cracks (Effect/knight_plancrack_a/b.bmd)
  | 'darkScream' | 'darkScreamFire' // MODEL_DARK_SCREAM(_FIRE): Fire Scream's claws (Skill/darkfirescrem02/01.bmd)
  | 'skillJavelin' // MODEL_SKILL_JAVELIN: Lance (Skill/Javelin.bmd)
  | 'swellOfMagicPower' // MODEL_SWELL_OF_MAGICPOWER: Expansion of Wizardry (Effect/magic_powerup.bmd)
  | 'arrowsre6' // MODEL_ARROWSRE06: its hand flashes (Effect/arrowsre06.bmd)
  | 'twlight' // BITMAP_TWLIGHT: its twilight ring
  | 'swellOfMagicPowerBuffEffect' // MODEL_SWELL_OF_MAGICPOWER_BUFF_EFF: its buff's lasting lights
  | 'summon' // MODEL_SUMMON: Recovery's circle (Skill/nightmaresum.bmd)
  | 'raklionBossCrackEffect'; // MODEL_RAKLION_BOSS_CRACKEFFECT: its great crack (Effect/knight_plancrack_grand.bmd)

/** CreateParticle types ported (names as in Data/Effects/ParticleTypes.json, or the texture's file). */
export type ParticleType = 'energy' | 'spark' | 'spark3' | 'fire' | 'fire3' | 'smoke' | 'blood2' | 'explosion' | 'flame' | 'light' | 'flareBlue' | 'flare'
  | 'light3' // BITMAP_LIGHT+2: Drain Life's red lights (SubType 7)
  | 'lightningMega' // BITMAP_LIGHTNING_MEGA1..3: Lightning Shock's sparks
  | 'magicParticle' // BITMAP_MAGIC as a particle: Lightning Shock's shrinking circle
  | 'pinLightParticle' // BITMAP_PIN_LIGHT as a particle: Weakness's and Innovation's falling pins
  | 'shiny5Particle' // BITMAP_SHINY+6 as a particle
  | 'swordEffectMonoParticle' // BITMAP_SWORD_EFFECT_MONO as a particle
  | 'sahamuttExplosion' // BITMAP_SUMMON_SAHAMUTT_EXPLOSION: CreateBomb3's explosion
  | 'sbumb' // BITMAP_SBUMB: Killing Blow's hits
  | 'fire2' // BITMAP_FIRE+1: Fire Burst's trails' fire
  | 'smokeLine1' // BITMAP_SMOKELINE1: the smoke lines rising from Increase Attack's fists
  | 'shiny4' // BITMAP_SHINY+4 (ring.jpg): Cometfall's ring where a comet lands
  | 'poundingBall' // BITMAP_POUNDING_BALL: Lance's trail
  | 'twintailWater' // BITMAP_TWINTAIL_WATER: the Sleep debuff's bubbles
  | 'waterfall5' | 'waterfall3'; // BITMAP_WATERFALL_5 / _3: Strike of Destruction's spray

/** CreateSprite types: the bitmap drawn. */
export type SpriteType = BitmapName;

/** CreateJoint types ported (names as in Data/Effects/JointTypes.json). */
export type JointType = 'jointThunder' | 'jointHealing' | 'spearSkill' | 'jointSpirit' | 'smokeTrail' | 'flare' | 'jointSpark' | 'lightJoint' | 'piercing' | 'flareForceJoint' | 'jointForce' | 'blur'
  | 'drainLifeGhost' | 'jointEnergy' | 'pinLightJoint' | 'forcePillar' | 'swordEff'
  | 'twoLineGhost' // BITMAP_2LINE_GHOST: Chaotic Diseier's ghost trails
  | 'flareBlueJoint' // BITMAP_FLARE_BLUE as a joint: Mana Rays' flares
  | 'flash'; // BITMAP_FLASH: Stun's bolts, the cancels' falling flashes

/** The desktop's model render types the ported effects use: normal, or RENDER_DARK (subtracted). */
export type RenderType = 'normal' | 'dark';

export interface Effect {
  type: EffectType;
  subType: number;
  position: THREE.Vector3;
  startPosition: THREE.Vector3;
  angle: THREE.Vector3;
  headAngle: THREE.Vector3;
  direction: THREE.Vector3;
  light: THREE.Vector3;
  scale: number;
  lifeTime: number;
  gravity: number;
  /** o->Velocity: animation keys per tick. */
  velocity: number;
  blendMesh: number;
  blendMeshLight: number;
  blendMeshTexCoordU: number;
  blendMeshTexCoordV: number;
  hiddenMesh: number;
  alpha: number;
  renderType: RenderType;
  owner: EffectOwner | null;
  /** o->Owner->Owner or o->m_sTargetIndex: the character a two-ended effect reaches (Drain Life, Chain Lightning). */
  target: EffectOwner | null;
  /** CreateEffect's Scale argument as given (0 when left out); o->Scale is 0.9 then. */
  callScale: number;
  animationFrame: number;
  /** o->Weapon, o->Kind, o->Skill, o->PKKey, o->Distance: counters some moves keep. */
  counter: number;
  kind: number;
  skill: number;
  pkKey: number;
  distance: number;
  /** The drawn model, once loaded (model effects only). */
  model: AnimatedModel | null;
  /** A model the effect draws besides its own (the caster's weapon for Twisting Slash). */
  extra: THREE.Object3D | null;
  /** The joint it rides (m_sTargetIndex as a joint index: MODEL_DESAIR on Chaotic Diseier's ghosts). */
  joint: Joint | null;
  /** This effect as an owner other effects can follow. */
  self: EffectOwner;
  live: boolean;
}

export interface Particle {
  type: ParticleType;
  subType: number;
  texture: BitmapName;
  position: THREE.Vector3;
  startPosition: THREE.Vector3;
  velocity: THREE.Vector3;
  angle: THREE.Vector3;
  light: THREE.Vector3;
  turningForce: THREE.Vector3;
  scale: number;
  gravity: number;
  lifeTime: number;
  frame: number;
  rotation: number;
  target: EffectOwner | null;
  /** o->Alpha. */
  alpha: number;
  live: boolean;
}

export interface Sprite {
  type: SpriteType;
  /** 0 glow (additive); 1 subtracted (the dark sprites). */
  subType: number;
  position: THREE.Vector3;
  scale: number;
  light: THREE.Vector3;
  rotation: number;
}

/** One cross-section of a trail: four corners (two along x, two along z of its matrix). */
export type Tail = [THREE.Vector3, THREE.Vector3, THREE.Vector3, THREE.Vector3];

/** RENDER_TYPE_ALPHA_BLEND (glow), _ALPHA_BLEND_MINUS (subtract), _ALPHA_BLEND_OTHER (blend2). */
export type JointBlend = 'glow' | 'subtract' | 'blend2';

export interface Joint {
  type: JointType;
  subType: number;
  texture: BitmapName;
  position: THREE.Vector3;
  targetPosition: THREE.Vector3;
  startPosition: THREE.Vector3;
  angle: THREE.Vector3;
  direction: THREE.Vector3;
  light: THREE.Vector3;
  scale: number;
  velocity: number;
  lifeTime: number;
  maxTails: number;
  numTails: number;
  tails: (Tail | null)[];
  target: EffectOwner | null;
  /** The joint's slot number on the desktop; orbits use it to differ. */
  index: number;
  blend: JointBlend;
  /** RENDER_FACE_ONE (1, the z corners) and RENDER_FACE_TWO (2, the x corners). */
  faces: number;
  createTails: boolean;
  collision: boolean;
  /** o->Weapon, o->Skill, o->PKKey: counters some moves keep. */
  counter: number;
  skill: number;
  pkKey: number;
  live: boolean;
}
