// Dark Side's dash (CMonkSystem: SetDarksideTargetIndex, SendDarksideAtt, InitDarksideTarget, StepDarkside,
// CalculateDarksideTrans, UpdateDarkside, and CDummyUnit's SetDummy / IsDummyRender / CalDummyPosition, in
// GameLogic/Social/MonkSystem.cpp). The fighter stands readying (PLAYER_SKILL_DARKSIDE_READY) while a copy of
// it dashes from it to each target the server named (RageAttackRangeResponse, C1 4B) and up to five
// afterimages close in on the current target from around it; each draws as the fighter in
// PLAYER_SKILL_DARKSIDE_ATTACK at its own place, alpha and key. The dash moves on to the next target each time
// the readying has lasted 2.5 keys (SendDarksideAtt). The desktop keeps one such state, the hero's.
//
// UpdateDarkside steps the state once for each body part it draws (six for a player) every tick, and draws
// each part at its own step's places; the web draws the whole body at the last step's places.

import * as THREE from 'three';
import type { EffectOwner } from './types.js';
import type { EffectSystem } from './effectSystem.js';
import { createAngle2D } from './core.js';

/** DARKSIDE_TARGET_MAX. */
export const DARK_SIDE_TARGETS = 5;
/** DS_TARGET_NONE (NUMOFMON): an empty slot of the target list. */
export const DARK_SIDE_NO_TARGET = 10000;
/** PLAYER_SKILL_DARKSIDE_READY / PLAYER_SKILL_DARKSIDE_ATTACK. */
export const DARKSIDE_READY = 250;
export const DARKSIDE_ATTACK = 251;
/** MAX_BODYPART: UpdateDarkside steps once per drawn body part. */
const BODY_PARTS = 6;
/** g_iLimitAttackTime: AttackTime goes back to 0 there. */
const LIMIT_ATTACK_TIME = 15;

/** One copy of the fighter to draw this tick (DarksidePlacement). */
export interface DarkSidePlacement {
  position: THREE.Vector3;
  /** Facing in degrees (o->Angle[2]). */
  angle: number;
  alpha: number;
  /** The key of PLAYER_SKILL_DARKSIDE_ATTACK it is drawn at. */
  frame: number;
}

/** CDummyUnit: an afterimage that starts beside the target and runs in through it, faster each step. */
class DummyUnit {
  readonly position: THREE.Vector3;
  readonly start: THREE.Vector3;
  private readonly direction: THREE.Vector3;
  private disFrame = 0;
  private aniFrame = 0;
  alpha = 0.2;

  constructor(position: THREE.Vector3, target: THREE.Vector3) {
    this.position = position.clone();
    this.start = position.clone();
    this.direction = target.clone().sub(this.start).normalize();
  }

  /** IsDistance: fades in up to 0.6 while within 300 of its start, out after; gone beyond 500. */
  isDistance(): boolean {
    const distance = this.start.distanceTo(this.position);
    if (distance > 300) this.alpha = this.alpha < 0 ? 0 : this.alpha - 0.07;
    else this.alpha = this.alpha > 0.6 ? 0.6 : this.alpha + 0.02;
    return distance <= 500;
  }

  /** CalDummyPosition (outside Chaos Castle and transformations): returns its key. */
  step(): number {
    this.disFrame += 0.7;
    this.aniFrame += 0.05;
    if (this.aniFrame >= 2) this.aniFrame = 1.9;
    this.direction.normalize().multiplyScalar(this.disFrame * 0.7);
    this.position.add(this.direction);
    return this.aniFrame;
  }
}

/** What the dash reads of the fighter. */
export interface DarkSideCaster extends EffectOwner {
  currentAction?(): number;
  animationFrame?(): number;
  playSpeed?(action: number): number;
}

const WHITE = new THREE.Vector3(1, 1, 1);

export class DarkSideDash {
  /** m_DarksideIndex: the target list's ids as the server sent them. */
  private targetIds: number[] = [];
  /** m_DarkTargetIndex: the named targets (null where the id names nobody visible). */
  private targets: (EffectOwner | null)[] = [];
  private count = 0;
  private effectTotal = 0;
  private effectAttack = 0;
  private otherAniFrame = 0.1;
  private distanceFrame = 0;
  private distanceNextFrame = 0;
  private effectOnce = false;
  private effectOnce2 = false;
  private dummies: DummyUnit[] = [];
  private dummyAniFrame = 0;
  /** c->AttackTime of the fighter: 1 when the server shows the dash (ReceiveStraightAttack), counted every tick. */
  attackTime = 0;
  /** o->m_sTargetIndex: the target the fighter attacks (no afterimages without one). */
  mainTarget: EffectOwner | null = null;
  /** The copies to draw, made by the last tick. */
  placements: DarkSidePlacement[] = [];

  constructor(private readonly system: EffectSystem, readonly caster: DarkSideCaster) {}

  /** InitDarksideTarget. */
  reset(): void {
    this.count = 0;
    this.effectTotal = 0;
    this.effectAttack = 0;
    this.otherAniFrame = 0.1;
    this.distanceFrame = 0;
    this.distanceNextFrame = 0;
    this.effectOnce = false;
    this.effectOnce2 = false;
    this.targetIds = [];
    this.targets = [];
  }

  /**
   * ReceiveDarkside -> InitDarksideTarget and SetDarksideTargetIndex. Returns false when the list names nobody: the
   * desktop then sends the skill at the hero (SendRequestMagic(skill, HeroKey)).
   */
  setTargets(ids: readonly number[], find: (id: number) => EffectOwner | null): boolean {
    this.reset();
    for (let i = 0; i < DARK_SIDE_TARGETS; i++) {
      const id = ids[i] ?? DARK_SIDE_NO_TARGET;
      this.targetIds.push(id);
      if (id === DARK_SIDE_NO_TARGET) continue;
      this.targets.push(find(id));
      this.effectTotal++;
      this.distanceFrame = 0;
      this.distanceNextFrame = 0;
      this.effectOnce = false;
      this.effectOnce2 = false;
    }
    return this.effectTotal > 0;
  }

  get targetCount(): number { return this.effectTotal; }
  get dashedTargets(): number { return this.count; }

  /** CreateEffect of the dashing copy's shock waves; their StartPosition is the fighter's own place. */
  private shockWave(position: THREE.Vector3, angle: number, light: THREE.Vector3, subType: 3 | 4, start: THREE.Vector3): void {
    const owner: EffectOwner = Object.assign(Object.create(this.caster) as EffectOwner, { startPosition: start.clone() });
    this.system.createEffect('shockWave1', position, new THREE.Vector3(90, 0, angle - 180), light, subType, owner, 0.8);
  }

  /** CreateParticleFpsChecked(BITMAP_DAMAGE2): the flash where the dash strikes. */
  private flash(position: THREE.Vector3, angle: number): void {
    this.system.createParticle('damage2', position, new THREE.Vector3(0, 0, angle), WHITE, 0, 1);
  }

  /** SendDarksideAtt: on to the next target; false when the list is done (it is then cleared) or the next is empty. */
  private nextTarget(): boolean {
    if (this.count >= DARK_SIDE_TARGETS) { this.reset(); return false; }
    if (!this.targetIds.length) return false;
    if (this.targetIds[this.count] === DARK_SIDE_NO_TARGET) return false;
    this.count++;
    return true;
  }

  /** Whether the fighter readies Dark Side now, with a target (DarksideDrawnParts names its body parts then). */
  get drawing(): boolean {
    return this.caster.currentAction?.() === DARKSIDE_READY && this.mainTarget !== null;
  }

  /** One tick: UseSkillRagePosition's dash counter, AttackTime, then UpdateDarkside. */
  tick(): void {
    const readying = this.caster.currentAction?.() === DARKSIDE_READY;
    if (readying) {
      // UseSkillRagePosition: every int(2.5 / PlaySpeed) ticks of readying the dash moves on.
      const speed = this.caster.playSpeed?.(DARKSIDE_READY) ?? 0.3;
      const attackTimeLimit = Math.trunc(2.5 / speed);
      if (this.attackTime >= attackTimeLimit && this.nextTarget()) this.attackTime = 1;
    }
    if (Math.trunc(this.attackTime) > 0) this.attackTime += 1;
    if (Math.trunc(this.attackTime) >= LIMIT_ATTACK_TIME) this.attackTime = 0;
    if (!this.drawing) { this.placements = []; return; }
    let placed: DarkSidePlacement[] = [];
    for (let part = 0; part < BODY_PARTS; part++) {
      placed = [];
      this.step(placed);
    }
    this.placements = placed;
  }

  /** StepDarkside. */
  private step(placed: DarkSidePlacement[]): void {
    const origin = this.caster.position.clone();
    const frame = this.caster.animationFrame?.() ?? 0;
    let vPos = origin.clone();
    if (this.effectTotal > this.effectAttack) {
      const target = this.targets[this.effectAttack] ?? null;
      this.otherAniFrame = (this.attackTime + 1) * 0.1 * 2;
      this.distanceFrame += 1;
      this.distanceNextFrame = this.distanceFrame + 1;
      if (this.otherAniFrame >= 2) this.otherAniFrame = 1.9;
      if (target) vPos = target.position.clone();
      // CalculateDarksideTrans: the copy runs d * d * 0.05 from the fighter towards the target, and past it.
      const direction = vPos.clone().sub(origin).normalize();
      const position = origin.clone().addScaledVector(direction, this.distanceFrame * this.distanceFrame * 0.05);
      const angle = createAngle2D(origin, vPos);
      const next = origin.clone().addScaledVector(direction, this.distanceNextFrame * this.distanceNextFrame * 0.05);
      const targetLength = origin.distanceTo(vPos);
      const objectLength = origin.distanceTo(position);
      if (targetLength > objectLength && targetLength < origin.distanceTo(next)) this.effectOnce = true;
      if (origin.distanceTo(position) > 30 && !this.effectOnce2) {
        this.shockWave(position, angle, WHITE, 3, origin);
        this.effectOnce2 = true;
      }
      if (this.effectOnce) {
        this.flash(vPos, angle);
        this.effectOnce = false;
      }
      if (this.count > this.effectAttack) {
        this.effectAttack++;
        this.otherAniFrame = 0.1;
        this.distanceFrame = 0;
        this.distanceNextFrame = 0;
        this.effectOnce = false;
        this.effectOnce2 = false;
      }
      placed.push({ position, angle, alpha: 1, frame: this.otherAniFrame });
    }
    if (this.count <= 0) return;
    if (frame > this.dummies.length * 2 && this.dummies.length < 5) {
      if (this.mainTarget) vPos = this.mainTarget.position.clone();
      const current = this.targets[this.effectAttack];
      if (this.effectAttack < this.targets.length && current) vPos = current.position.clone();
      // SetDummy: 150, 150 from the target, turned 60 degrees more for each afterimage, at the target's height.
      const turn = (60 * this.dummies.length * Math.PI) / 180;
      const start = new THREE.Vector3(vPos.x + 150 * Math.cos(turn) - 150 * Math.sin(turn), vPos.y + 150 * Math.sin(turn) + 150 * Math.cos(turn), vPos.z);
      this.dummies.push(new DummyUnit(start, vPos));
      this.dummyAniFrame = 0;
    }
    for (const dummy of this.dummies) {
      if (!dummy.isDistance()) continue;
      this.dummyAniFrame = dummy.step();
      const angle = createAngle2D(dummy.start, dummy.position);
      const key = Math.trunc(this.dummyAniFrame * 10);
      if (key === 15) this.shockWave(dummy.position, angle, new THREE.Vector3(0.2, 0.2, 0.2), 4, dummy.start);
      if (key === 10 && this.effectTotal > this.effectAttack) this.flash(vPos, angle);
      placed.push({ position: dummy.position.clone(), angle, alpha: dummy.alpha, frame: this.dummyAniFrame });
    }
  }

  /** RageCreateEffect(AT_SKILL_DARKSIDE)'s InitDummyCal: the afterimages start again with each cast. */
  clearDummies(): void {
    this.dummies = [];
    this.dummyAniFrame = 0;
  }
}

/**
 * The hero's body while it readies Dark Side (CMonkSystem::RageFighterEffect): its alpha dips from 1 to 0.3 and
 * back over the action (sin(180 / keys * frame + 180) * 0.7 + 1).
 */
export function darkSideReadyAlpha(frame: number, keys: number): number {
  const degrees = (180 / Math.max(1, keys)) * frame + 180;
  return Math.sin((degrees * Math.PI) / 180) * 0.7 + 1;
}
