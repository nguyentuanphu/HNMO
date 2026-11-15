// Drawing Dark Side's dash (CMonkSystem::DarksideRendering and RageFighterEffect): each placement of the dash
// (render/effects/darkSideDash.ts) is a copy of the fighter's body in PLAYER_SKILL_DARKSIDE_ATTACK, drawn bright
// (RENDER_TEXTURE | RENDER_BRIGHT) at its own alpha; the fighter itself fades while it readies. The copies are the
// body only (DarksideRendering is called from the body parts' draw), made once per look and kept for the next cast.

import * as THREE from 'three';
import type { AnimatedModel } from '../bmd.js';
import type { Character } from './characters.js';
import { createPlayerModel } from './characters.js';
import { DARKSIDE_ATTACK, DARKSIDE_READY, darkSideReadyAlpha, type DarkSidePlacement } from '../render/effects/darkSideDash.js';

/** One dashing copy and up to five afterimages. */
const MAX_COPIES = 6;
const DEGREES = Math.PI / 180;

export class DarkSideGhosts {
  private copies: AnimatedModel[] = [];
  private lookKey = '';
  private loading: Promise<void> | null = null;
  /** The hero's materials' opacity while it fades, to put back. */
  private faded: Map<THREE.MeshBasicMaterial, { opacity: number; transparent: boolean }> = new Map();

  constructor(private readonly parent: THREE.Object3D) {}

  /** How many copies are drawn now (for tests). */
  get shown(): number { return this.copies.filter((copy) => copy.group.visible).length; }

  private ensure(hero: Character): void {
    const key = JSON.stringify([hero.classIndex, hero.equipment ?? {}]);
    if (key === this.lookKey || this.loading) return;
    this.lookKey = key;
    const bodySet = (hero.classIndex ?? 6) + 1;
    const equipment = hero.equipment ?? {};
    this.loading = Promise.all(Array.from({ length: MAX_COPIES }, () => createPlayerModel(bodySet, equipment))).then((models) => {
      for (const copy of this.copies) { copy.group.removeFromParent(); copy.release(); }
      this.copies = models.map((model) => {
        for (const part of model.parts) {
          // RENDER_BRIGHT: added to what is behind it, its light times its alpha.
          Object.assign(part.mesh.material, { blending: THREE.AdditiveBlending, transparent: true, depthWrite: false, alphaTest: 0 });
        }
        model.group.visible = false;
        this.parent.add(model.group);
        return model;
      });
    }).catch((error: unknown) => {
      console.warn('Dark Side copies not made:', error);
      this.lookKey = '';
    }).finally(() => { this.loading = null; });
  }

  /** Puts the copies where the last tick placed them, and fades the hero while it readies. */
  show(hero: Character, placements: readonly DarkSidePlacement[]): void {
    if (placements.length) this.ensure(hero);
    this.copies.forEach((copy, index) => {
      const at = placements[index];
      copy.group.visible = at !== undefined;
      if (!at) return;
      copy.group.position.copy(at.position);
      copy.group.rotation.set(0, 0, at.angle * DEGREES);
      copy.group.scale.copy(hero.group.scale);
      copy.pose(DARKSIDE_ATTACK, at.frame);
      for (const part of copy.parts) part.mesh.material.color.setScalar(at.alpha); // BodyLight (1, 1, 1) * Alpha
    });
    this.fadeHero(hero);
  }

  // RageFighterEffect: PLAYER_SKILL_DARKSIDE_READY draws the body at sin(...) * 0.7 + 1.
  private fadeHero(hero: Character): void {
    const readying = (hero.oneShot?.action ?? hero.action) === DARKSIDE_READY;
    if (!readying) { this.restoreHero(); return; }
    const keys = hero.model.skeleton.actions[DARKSIDE_READY]?.numKeys ?? 1;
    const alpha = Math.min(1, Math.max(0, darkSideReadyAlpha(hero.time, keys)));
    for (const part of hero.model.parts) {
      const material = part.mesh.material;
      if (material.blending === THREE.AdditiveBlending) continue;
      if (!this.faded.has(material)) this.faded.set(material, { opacity: material.opacity, transparent: material.transparent });
      material.transparent = true;
      material.opacity = alpha;
    }
  }

  private restoreHero(): void {
    for (const [material, was] of this.faded) { material.opacity = was.opacity; material.transparent = was.transparent; }
    this.faded.clear();
  }

  hide(): void {
    for (const copy of this.copies) copy.group.visible = false;
    this.restoreHero();
  }
}
