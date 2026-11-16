// Per-frame movement recorder (behind window.__game.debug, off by default): logs for the hero and
// the 5 nearest monsters their time, position, angle, action, animation time and path length, and
// counts angle snaps (> 20 degrees in one frame), brief stop<->walk flips and position jumps.

import type { Character } from './characters.js';
import { MonsterAction } from './monsterData.js';
import { PlayerActions } from './combatLook.js';

const RAD_TO_DEG = 180 / Math.PI;
const TWO_PI = Math.PI * 2;
const MAX_NEAREST_MONSTERS = 5;
const BRIEF_FLIP_MAX_FRAMES = 15;

export interface CharacterMovementSample {
  id: number;
  name: string;
  kind: string;
  x: number;
  y: number;
  angle: number;
  action: number;
  animTime: number;
  pathLength: number;
}

export interface MovementFrameLog {
  time: number;
  dt: number;
  hero: CharacterMovementSample | null;
  monsters: CharacterMovementSample[];
}

export interface AngleSnapEvent {
  time: number;
  id: number;
  name: string;
  deltaDeg: number;
  pathLength: number;
}

export interface StopWalkFlipEvent {
  time: number;
  id: number;
  name: string;
  frames: number;
  fromAction: number;
  briefAction: number;
  toAction: number;
}

export interface PositionJumpEvent {
  time: number;
  id: number;
  name: string;
  distance: number;
  reversal: boolean;
}

export interface MovementSummary {
  frames: number;
  durationSeconds: number;
  angleSnapsOver20Deg: number;
  stopWalkFlips: number;
  positionJumps: number;
  perMinute: {
    angleSnapsOver20Deg: number;
    stopWalkFlips: number;
    positionJumps: number;
  };
  events: {
    angleSnaps: AngleSnapEvent[];
    stopWalkFlips: StopWalkFlipEvent[];
    positionJumps: PositionJumpEvent[];
  };
}

function wrapSignedRadians(delta: number): number {
  let d = delta % TWO_PI;
  if (d > Math.PI) d -= TWO_PI;
  else if (d < -Math.PI) d += TWO_PI;
  return d;
}

function isWalkOrRunAction(kind: string, action: number): boolean {
  if (kind !== 'player') return action === MonsterAction.WALK || action === 10;
  const A = PlayerActions;
  return (
    (action >= A.WALK_MALE && action <= A.RUN_RIDE_WEAPON) ||
    action === A.FLY_RIDE || action === A.FLY_RIDE_WEAPON ||
    action === A.DARKLORD_WALK || action === A.RUN_RIDE_HORSE ||
    (action >= A.FENRIR_RUN && action <= A.FENRIR_RUN_ONE_LEFT_ELF) ||
    (action >= A.FENRIR_WALK && action <= A.FENRIR_WALK_ONE_LEFT) ||
    action === A.WALK_TWO_HAND_SWORD_TWO || action === A.RUN_TWO_HAND_SWORD_TWO ||
    action === A.RAGE_UNI_RUN || action === A.RAGE_UNI_RUN_ONE_RIGHT ||
    (action >= A.RAGE_FENRIR_WALK && action <= A.RAGE_FENRIR_RUN_ONE_LEFT)
  );
}

function isStopAction(kind: string, action: number): boolean {
  if (kind !== 'player') return action === MonsterAction.STOP || action === 1;
  const A = PlayerActions;
  return (
    (action >= A.STOP_MALE && action <= A.STOP_RIDE_WEAPON) ||
    action === A.DARKLORD_STAND || action === A.STOP_RIDE_HORSE ||
    (action >= A.FENRIR_STAND && action <= A.FENRIR_STAND_ONE_LEFT) ||
    action === A.STOP_TWO_HAND_SWORD_TWO || action === A.RAGE_UNI_STOP_ONE_RIGHT ||
    (action >= A.RAGE_FENRIR_STAND && action <= A.RAGE_FENRIR_STAND_ONE_LEFT) ||
    action === A.STOP_RAGEFIGHTER
  );
}

function sampleCharacter(c: Character): CharacterMovementSample {
  return {
    id: c.id,
    name: c.name,
    kind: c.kind,
    x: Math.round(c.position.x * 100) / 100,
    y: Math.round(c.position.y * 100) / 100,
    angle: Math.round(c.angle * 10000) / 10000,
    action: c.action,
    animTime: Math.round(c.time * 1000) / 1000,
    pathLength: c.path.length,
  };
}

export class MovementRecorder {
  enabled = false;
  readonly logs: MovementFrameLog[] = [];
  private elapsed = 0;

  start(): void {
    this.logs.length = 0;
    this.elapsed = 0;
    this.enabled = true;
  }

  stop(): MovementSummary {
    this.enabled = false;
    return this.summarize();
  }

  clear(): void {
    this.logs.length = 0;
    this.elapsed = 0;
  }

  recordFrame(dt: number, hero: Character | null, others: Iterable<Character> | undefined): void {
    if (!this.enabled) return;
    this.elapsed += dt;
    const heroSample = hero ? sampleCharacter(hero) : null;
    const monsters: { character: Character; distSq: number }[] = [];
    if (others && hero) {
      const hx = hero.position.x, hy = hero.position.y;
      for (const c of others) {
        if (c.kind !== 'monster' || c.dead) continue;
        const dx = c.position.x - hx, dy = c.position.y - hy;
        monsters.push({ character: c, distSq: dx * dx + dy * dy });
      }
      monsters.sort((a, b) => a.distSq - b.distSq);
    }
    this.logs.push({
      time: Math.round(this.elapsed * 1000) / 1000,
      dt: Math.round(dt * 10000) / 10000,
      hero: heroSample,
      monsters: monsters.slice(0, MAX_NEAREST_MONSTERS).map((m) => sampleCharacter(m.character)),
    });
  }

  summarize(): MovementSummary {
    const byId = new Map<number, { time: number; dt: number; sample: CharacterMovementSample }[]>();
    for (const frame of this.logs) {
      if (frame.hero) {
        let list = byId.get(frame.hero.id);
        if (!list) { list = []; byId.set(frame.hero.id, list); }
        list.push({ time: frame.time, dt: frame.dt, sample: frame.hero });
      }
      for (const m of frame.monsters) {
        let list = byId.get(m.id);
        if (!list) { list = []; byId.set(m.id, list); }
        list.push({ time: frame.time, dt: frame.dt, sample: m });
      }
    }

    const angleSnaps: AngleSnapEvent[] = [];
    const stopWalkFlips: StopWalkFlipEvent[] = [];
    const positionJumps: PositionJumpEvent[] = [];

    for (const [id, entries] of byId) {
      let prevVx = 0, prevVy = 0, prevStep = 0;
      let runAction = entries[0]?.sample.action ?? -1;
      let prevRunAction = -1;
      let runFrames = 0;

      for (let i = 0; i < entries.length; i++) {
        const { time, dt, sample } = entries[i];
        if (sample.action === runAction) {
          runFrames++;
        } else {
          // Check if the completed run was a brief stop<->walk flip between walk and stop.
          const prevIsWalk = isWalkOrRunAction(sample.kind, prevRunAction);
          const prevIsStop = isStopAction(sample.kind, prevRunAction);
          const briefIsWalk = isWalkOrRunAction(sample.kind, runAction);
          const briefIsStop = isStopAction(sample.kind, runAction);
          const nextIsWalk = isWalkOrRunAction(sample.kind, sample.action);
          const nextIsStop = isStopAction(sample.kind, sample.action);
          if (
            prevRunAction >= 0 &&
            runFrames > 0 &&
            runFrames <= BRIEF_FLIP_MAX_FRAMES &&
            ((prevIsWalk && briefIsStop && nextIsWalk) || (prevIsStop && briefIsWalk && nextIsStop))
          ) {
            stopWalkFlips.push({
              time, id, name: sample.name, frames: runFrames,
              fromAction: prevRunAction, briefAction: runAction, toAction: sample.action,
            });
          }
          prevRunAction = runAction;
          runAction = sample.action;
          runFrames = 1;
        }

        if (i === 0) continue;
        const prev = entries[i - 1];
        // Skip gaps where a monster left and re-entered the top-5 list seconds later.
        if (time - prev.time > Math.max(0.25, dt * 4)) {
          prevVx = 0; prevVy = 0; prevStep = 0;
          continue;
        }

        const deltaDeg = Math.abs(wrapSignedRadians(sample.angle - prev.sample.angle)) * RAD_TO_DEG;
        if (deltaDeg > 20) {
          angleSnaps.push({
            time, id, name: sample.name,
            deltaDeg: Math.round(deltaDeg * 10) / 10,
            pathLength: sample.pathLength,
          });
        }

        const dx = sample.x - prev.sample.x;
        const dy = sample.y - prev.sample.y;
        const step = Math.hypot(dx, dy);
        const maxNormalStep = Math.max(18, 450 * Math.max(dt, 1 / 60));
        const reversal =
          prevStep > 0.5 && step > 0.5 &&
          prev.sample.pathLength > 0 && sample.pathLength > 0 &&
          dx * prevVx + dy * prevVy < -0.5 * step * prevStep;
        if (step > maxNormalStep || reversal) {
          positionJumps.push({
            time, id, name: sample.name,
            distance: Math.round(step * 100) / 100,
            reversal,
          });
        }
        prevVx = dx; prevVy = dy; prevStep = step;
      }
    }

    const durationSeconds = this.logs.length > 1 ? this.logs[this.logs.length - 1].time - this.logs[0].time : 0;
    const perMin = (count: number) => (durationSeconds > 0 ? Math.round((count * 60 / durationSeconds) * 10) / 10 : 0);
    return {
      frames: this.logs.length,
      durationSeconds: Math.round(durationSeconds * 100) / 100,
      angleSnapsOver20Deg: angleSnaps.length,
      stopWalkFlips: stopWalkFlips.length,
      positionJumps: positionJumps.length,
      perMinute: {
        angleSnapsOver20Deg: perMin(angleSnaps.length),
        stopWalkFlips: perMin(stopWalkFlips.length),
        positionJumps: perMin(positionJumps.length),
      },
      events: { angleSnaps, stopWalkFlips, positionJumps },
    };
  }
}
