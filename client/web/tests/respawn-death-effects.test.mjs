// A player character that dies and respawns (dead = false, e.g. after 5 s in the Arena) must be
// restored to full visibility, opacity and ground height by DeathEffects, and able to die again.
import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { DeathEffects } from '../public/js/game/combatEffects.js';

test('a respawned hero is restored to visible, full opacity and ground height after DeathEffects fade and vanish', () => {
  const root = new THREE.Group();
  const group = new THREE.Group();
  const material = new THREE.MeshBasicMaterial({ opacity: 1, transparent: false });
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), material);
  group.add(mesh);
  group.position.set(6300, 2700, 130);
  root.add(group);

  const hero = {
    dead: true,
    group,
    oneShot: null,
    time: 0,
    model: { skeleton: { actions: [{ numKeys: 1 }] }, matrices: [] },
  };

  const deaths = new DeathEffects(root);
  deaths.died(hero, null);

  // 4.5 seconds of death (inside the Arena's 5 s respawn wait): the body has faded, sunk and vanished.
  for (let i = 0; i < 45; i++) deaths.update(0.1);
  assert.equal(hero.group.visible, false);
  assert.ok(material.opacity < 0.15);

  // Respawn packet (C1 F3 04) arrives: hero.dead becomes false and DeathEffects ticks (or revived is called).
  hero.dead = false;
  deaths.update(1 / 60);

  assert.equal(hero.group.visible, true);
  assert.equal(material.opacity, 1);
  assert.equal(material.transparent, false);
  assert.equal(hero.group.position.z, 130);
});
