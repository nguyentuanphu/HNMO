// Verifies Chain Lightning draws its bolts and thunder joints even when the first hit killed the initial
// target right before ChainLightningHitInfo arrived (as happens on one-shot kills in HNMO).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as THREE from 'three';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const effectsBase = path.resolve(__dirname, '../public/js/render/effects');

const { playChainLightning } = await import(pathToFileURL(path.join(effectsBase, 'skillEffects.js')).href);
const { EXTRA_EFFECTS } = await import(pathToFileURL(path.join(effectsBase, 'extraEffects.js')).href);
await import(pathToFileURL(path.join(effectsBase, 'summonerEffects.js')).href);

function owner(x, y, { live = true, present = true } = {}) {
  return {
    position: new THREE.Vector3(x, y, 0),
    angle: new THREE.Vector3(0, 0, 0),
    light: new THREE.Vector3(1, 1, 1),
    isLive: () => live,
    isPresent: () => present,
    boneCount: () => 2,
    bonePosition: () => new THREE.Vector3(x, y, 60),
  };
}

function runChain(caster, targets) {
  const createdEffects = [];
  const createdJoints = [];
  const createdParticles = [];
  const mockSystem = {
    createEffect(type, position, angle, light, subType, effectOwner, _arg, extras = {}) {
      const obj = { type, position, angle, light, subType, owner: effectOwner, target: extras.target ?? null, lifeTime: 20, live: true };
      createdEffects.push(obj);
      return obj;
    },
    createJoint(...args) { createdJoints.push(args); },
    createParticle(...args) { createdParticles.push(args); },
  };

  playChainLightning(mockSystem, caster, targets);
  const handler = EXTRA_EFFECTS.chainLightning;
  assert.ok(handler, 'chainLightning handler registered');

  for (const effect of createdEffects) {
    assert.ok(handler.create(effect, mockSystem));
    effect.lifeTime = 15;
    handler.move(effect, mockSystem);
  }
  return { createdEffects, createdJoints, createdParticles };
}

test('Chain Lightning draws bolts and joints across both hops when target 1 died on the initial hit', () => {
  const caster = owner(1000, 1000);
  const target1Killed = owner(1200, 1000, { live: false, present: true });
  const target2Alive = owner(1300, 1000, { live: true, present: true });

  const { createdEffects, createdJoints, createdParticles } = runChain(caster, [target1Killed, target2Alive, target2Alive]);

  assert.equal(createdEffects.length, 2, 'creates links caster->T1 and T1->T2');
  assert.equal(createdEffects.filter((e) => e.live).length, 2, 'both links stay alive while killed T1 is still in scene');
  assert.ok(createdJoints.length >= 10, `expected thunder joints across both hops, got ${createdJoints.length}`);
  assert.ok(createdParticles.length > 0, 'creates impact lights at lifeTime 15');
});

test('Chain Lightning draws the initial bolt when the sole target died on the initial hit', () => {
  const caster = owner(1000, 1000);
  const soleTargetKilled = owner(1200, 1000, { live: false, present: true });

  const { createdEffects, createdJoints } = runChain(caster, [soleTargetKilled, soleTargetKilled, soleTargetKilled]);

  assert.equal(createdEffects.length, 1, 'creates caster->T1 link');
  assert.ok(createdJoints.length >= 8, `expected 8 thunder joints on hop 0, got ${createdJoints.length}`);
});

test('Chain Lightning aborts links whose target has already left the scene', () => {
  const caster = owner(1000, 1000);
  const despawned = owner(1200, 1000, { live: false, present: false });

  const { createdEffects, createdJoints } = runChain(caster, [despawned, despawned, despawned]);

  assert.equal(createdEffects.length, 0);
  assert.equal(createdJoints.length, 0);
});
