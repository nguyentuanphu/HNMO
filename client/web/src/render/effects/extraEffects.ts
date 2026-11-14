// The registry of effect and joint types ported for the other classes' skills, so
// effectSystem.ts keeps the core and the wizard's types. Each entry follows the desktop's
// CreateEffect / CreateJoint case, its move and its draw; the entries register themselves from
// their own modules (knightEffects.ts, ...), imported by index.ts.

import type { EffectSystem } from './effectSystem.js';
import type { Effect, EffectType, Joint, JointType } from './types.js';

export interface ExtraEffect {
  /** The model drawn (directory under Data, file), if the effect has one. */
  model?: [string, string];
  /** Load the model even while HiddenMesh is -2 (the effect shows parts of it later). */
  alwaysModel?: boolean;
  /** The CreateEffect case; false when the SubType isn't ported. */
  create(o: Effect, system: EffectSystem): boolean;
  /** The move; false skips the shared tail (animation and MoveParticle), as the desktop's `return`s. */
  move(o: Effect, system: EffectSystem, luminosity: number): boolean;
  /** Sprites made every tick while it lives (UpdateDrawnEffectSprites). */
  sprites?(o: Effect, system: EffectSystem): void;
  /** Its own draw each frame (decals and quads). */
  draw?(o: Effect, system: EffectSystem): void;
}

export interface ExtraJoint {
  init(o: Joint, system: EffectSystem): boolean;
  move(o: Joint, system: EffectSystem): void;
}

export const EXTRA_EFFECTS: Partial<Record<EffectType, ExtraEffect>> = {};
export const EXTRA_JOINTS: Partial<Record<JointType, ExtraJoint>> = {};
