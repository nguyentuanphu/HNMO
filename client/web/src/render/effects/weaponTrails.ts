// Weapon trails: the streak a sword (axe, mace, spear) leaves while a player swings it, as the
// desktop's CreateWeaponBlur (Engine/Object/ZzzCharacter.cpp) and its Blur records (CreateBlur,
// AddBlurSample, MoveBlurs, RenderBlurs in Render/Effects/ZzzEffectBlurSpark.cpp).
//
// Every drawn frame of an attack (from its third key on) the weapon's line from 20 to 120 units
// along the hand bone is sampled at ten poses over the last tick's slice of the animation; a new
// frame's samples replace the previous frame's until a tick ages the trail (MoveBlurs): then the
// samples stay and the oldest fall off. A trail lives 30 ticks after its first sample.

import * as THREE from 'three';
import { computeBoneMatrices } from '../../bmd.js';
import type { Character } from '../../game/characters.js';
import type { BitmapName } from './core.js';
import type { EffectLayer } from './effectSystem.js';
import type { QuadBatches } from './quadBatch.js';

/** MAX_BLUR_TAILS and MAX_BLUR_LIFETIME. */
const MAX_TAILS = 30;
const LIFETIME = 30;
/** The poses sampled per frame (CreateWeaponBlur's `inter`). */
const SAMPLES = 10;
const TICKS_PER_SECOND = 25;
/** The hand bones (c->Weapon[0/1].LinkBone of the player model). */
const HAND_BONES: readonly [number, number] = [33, 42];
/** The textures of the blur mappings (BITMAP_BLUR + Type). */
const MAPPING_TEXTURES: Record<number, BitmapName> = { 0: 'blur1', 1: 'blur2', 2: 'blur3' };

// The player actions (PlayerAction in Engine/Object/ZzzCharacter.h) the trail looks at.
const A = {
  SWORD_RIGHT1: 39, SWORD_LEFT1: 41, SWORD_LEFT2: 42, TWO_HAND_SWORD3: 45, SPEAR1: 46, SCYTHE3: 49,
  SKILL_SWORD1: 60, SKILL_SWORD4: 63, SKILL_SWORD5: 64, STRIKE: 80, ONE_FLASH: 136, RUSH: 137,
  TWO_HAND_SWORD_TWO: 145, BLOW_OF_DESTRUCTION: 176,
} as const;

// Item groups (the weapon's kind).
const SWORD = 0, AXE = 1, MACE = 2, SPEAR = 3;

interface Blur {
  owner: Character;
  p1: THREE.Vector3[];
  p2: THREE.Vector3[];
  number: number;
  frameSamples: number;
  frame: number;
  lifeTime: number;
  light: THREE.Vector3;
  mapping: number;
}

/** What CreateWeaponBlur decides for the action and the weapon: the blur mapping (texture), or null for no trail. */
function blurMapping(action: number, group: number | null, frame: number): number | null {
  if (frame < 3 && action !== A.TWO_HAND_SWORD_TWO) return null;
  if (action === A.ONE_FLASH || action === A.RUSH || action === A.STRIKE) return 2;
  if (action >= A.SKILL_SWORD1 && action <= A.SKILL_SWORD4) return 2;
  if (action === A.BLOW_OF_DESTRUCTION && frame >= 2 && frame <= 8) return 2;
  if (action === A.SKILL_SWORD5) return 2;
  if (group === SWORD) {
    if ((action >= A.SWORD_RIGHT1 && action <= A.TWO_HAND_SWORD3) || action === A.TWO_HAND_SWORD_TWO) {
      return action === A.TWO_HAND_SWORD3 || action === A.TWO_HAND_SWORD_TWO ? 1 : 0;
    }
    return null;
  }
  if (group === AXE || group === MACE) return action >= A.SKILL_SWORD1 && action <= A.SKILL_SWORD5 ? 2 : null;
  if (group === SPEAR && action >= A.SPEAR1 && action <= A.SCYTHE3) return action === A.SCYTHE3 ? 1 : 0;
  return null;
}

/** The trail's colour: white for the skills' mappings; by the weapon's level for mapping 0. */
function blurLight(mapping: number, level: number): THREE.Vector3 {
  if (mapping !== 0) return new THREE.Vector3(1, 1, 1);
  if (level >= 7) return new THREE.Vector3(1, 0.6, 0.2);
  if (level >= 5) return new THREE.Vector3(0.2, 0.4, 1);
  if (level >= 3) return new THREE.Vector3(1, 0.2, 0.2);
  return new THREE.Vector3(0.8, 0.8, 0.8);
}

export class WeaponTrails implements EffectLayer {
  private readonly blurs: Blur[] = [];
  private frameCount = 0;
  private readonly matrices = new Map<Character, THREE.Matrix4[]>();
  private readonly p1Offset = new THREE.Vector3(0, -20, 0);
  private readonly p2Offset = new THREE.Vector3(0, -120, 0);

  constructor(private readonly characters: () => Iterable<Character>) {}

  private readonly scratchP1 = new THREE.Vector3();
  private readonly scratchP2 = new THREE.Vector3();
  private readonly scratchLight = new THREE.Vector3();
  private readonly scratchCorners = [new THREE.Vector3(), new THREE.Vector3(), new THREE.Vector3(), new THREE.Vector3()];
  private readonly scratchUs = [0, 0, 0, 0];

  /** MoveBlurs: once per tick the samples age. */
  tick(): void {
    for (const blur of this.blurs) {
      blur.frameSamples = 0;
      blur.lifeTime--;
      blur.number = Math.max(blur.number - 1, 0);
      for (let i = blur.number - 1; i >= 0; i--) {
        (blur.p1[i + 1] ??= new THREE.Vector3()).copy(blur.p1[i]);
        (blur.p2[i + 1] ??= new THREE.Vector3()).copy(blur.p2[i]);
      }
      if (blur.lifeTime <= 0) blur.number = Math.max(blur.number - 1, 0);
    }
    for (let i = this.blurs.length - 1; i >= 0; i--) if (this.blurs[i].lifeTime <= 0 && this.blurs[i].number <= 0) this.blurs.splice(i, 1);
  }

  /** CreateWeaponBlur for every swinging player, each drawn frame. */
  frame(): void {
    this.frameCount++;
    for (const character of this.characters()) {
      if (character.kind !== 'player' || character.dead || !character.oneShot) continue;
      const action = character.oneShot.action;
      const left = action === A.SWORD_LEFT1 || action === A.SWORD_LEFT2;
      const weapon = left ? character.equipment?.leftHand : character.equipment?.rightHand;
      const held = weapon ?? character.equipment?.rightHand ?? character.equipment?.leftHand ?? null;
      if (!held) continue;
      const mapping = blurMapping(action, held.group, character.time);
      if (mapping === null) continue;
      const light = blurLight(mapping, held.glowLevel ?? 0);
      const bone = HAND_BONES[left ? 1 : 0];
      const skeleton = character.model.skeleton;
      const slice = character.oneShot.keysPerSecond / TICKS_PER_SECOND;
      const matrices = this.matrices.get(character) ?? skeleton.bones.map(() => new THREE.Matrix4());
      this.matrices.set(character, matrices);
      character.group.updateMatrix();
      let frame = character.time - slice;
      for (let i = 0; i < SAMPLES; i++) {
        computeBoneMatrices(skeleton, action, Math.max(0, frame), matrices);
        const m = matrices[bone];
        if (m) {
          this.scratchP1.copy(this.p1Offset).applyMatrix4(m).applyMatrix4(character.group.matrix);
          this.scratchP2.copy(this.p2Offset).applyMatrix4(m).applyMatrix4(character.group.matrix);
          this.add(character, this.scratchP1, this.scratchP2, light, mapping);
        }
        frame += slice / SAMPLES;
      }
    }
  }

  // CreateBlur / AddBlurSample.
  private add(owner: Character, p1: THREE.Vector3, p2: THREE.Vector3, light: THREE.Vector3, mapping: number): void {
    let blur = this.blurs.find((b) => b.owner === owner);
    if (!blur) {
      blur = { owner, p1: [], p2: [], number: 0, frameSamples: 0, frame: this.frameCount, lifeTime: LIFETIME, light, mapping };
      this.blurs.push(blur);
    }
    blur.mapping = mapping;
    blur.light = light;
    if (blur.frame !== this.frameCount) {
      const replaced = blur.frameSamples;
      for (let i = 0; i + replaced < blur.number; i++) {
        (blur.p1[i] ??= new THREE.Vector3()).copy(blur.p1[i + replaced]);
        (blur.p2[i] ??= new THREE.Vector3()).copy(blur.p2[i + replaced]);
      }
      blur.number -= replaced;
      blur.frame = this.frameCount;
      blur.frameSamples = 0;
    }
    for (let i = blur.number - 1; i >= 0; i--) {
      (blur.p1[i + 1] ??= new THREE.Vector3()).copy(blur.p1[i]);
      (blur.p2[i + 1] ??= new THREE.Vector3()).copy(blur.p2[i]);
    }
    (blur.p1[0] ??= new THREE.Vector3()).copy(p1);
    (blur.p2[0] ??= new THREE.Vector3()).copy(p2);
    blur.number = Math.min(blur.number + 1, MAX_TAILS - 1);
    blur.frameSamples = Math.min(blur.frameSamples + 1, blur.number);
  }

  /** RenderBlurs: glowing quads between the samples, fading along the trail. */
  draw(quads: QuadBatches): void {
    for (const blur of this.blurs) {
      if (blur.number < 2) continue;
      const texture = MAPPING_TEXTURES[blur.mapping] ?? 'blur1';
      for (let j = 0; j < blur.number - 1; j++) {
        const l1 = (blur.number - j) / blur.number, l2 = (blur.number - (j + 1)) / blur.number;
        const u1 = j / blur.number, u2 = (j + 1) / blur.number;
        // The two samples' lights differ; the quad takes their mean (the batches colour per quad).
        this.scratchLight.copy(blur.light).multiplyScalar((l1 + l2) / 2);
        this.scratchCorners[0].copy(blur.p1[j]);
        this.scratchCorners[1].copy(blur.p2[j]);
        this.scratchCorners[2].copy(blur.p2[j + 1]);
        this.scratchCorners[3].copy(blur.p1[j + 1]);
        this.scratchUs[0] = u1; this.scratchUs[1] = u1; this.scratchUs[2] = u2; this.scratchUs[3] = u2;
        quads.quad(texture, 'glow', this.scratchCorners, this.scratchUs, [1, 0, 0, 1], this.scratchLight);
      }
    }
  }

  /** How many trails are drawn (for tests). */
  get count(): number {
    return this.blurs.length;
  }
}
