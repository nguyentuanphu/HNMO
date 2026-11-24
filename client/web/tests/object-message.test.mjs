import test from 'node:test';
import assert from 'node:assert/strict';
import { OnlineWorld } from '../public/js/game/online.js';
import { write } from '../public/js/net/packets.js';

test('OnlineWorld decodes ObjectMessage (C1 01) for NPCs and hero', () => {
  let listener = null;
  const connection = {
    onPacket(fn) { listener = fn; return () => {}; },
    send() {},
  };
  const fakeHero = { id: 100, name: 'Hero', kind: 'player', cell: { x: 10, y: 10 }, position: { x: 1050, y: 1050 }, update() {}, release() {} };
  const fakeNpc = { id: 371, name: 'Leo the Helper', kind: 'npc', cell: { x: 12, y: 10 }, position: { x: 1250, y: 1050 }, update() {}, release() {} };
  const world = new OnlineWorld({
    connection,
    root: { add() {}, remove() {} },
    world: { heights: new Float32Array(256 * 256), light: new Float32Array(256 * 256 * 3), flags: new Uint8Array(256 * 256), mapNumber: 0 },
    hero: fakeHero,
    playerId: 100,
  });
  world.characters.set(371, fakeNpc);

  const seen = [];
  world.onObjectMessage = (speaker, id, message) => seen.push({ speaker, id, message });

  const npcPacket = write('ServerToClient', 'ObjectMessage', { ObjectId: 0x8000 | 371, Message: 'Good luck on your adventure!' });
  listener(npcPacket);
  const heroPacket = write('ServerToClient', 'ObjectMessage', { ObjectId: 100, Message: 'Hello!' });
  listener(heroPacket);

  assert.equal(seen.length, 2);
  assert.equal(seen[0].speaker, fakeNpc);
  assert.equal(seen[0].id, 371);
  assert.equal(seen[0].message, 'Good luck on your adventure!');
  assert.equal(seen[1].speaker, fakeHero);
  assert.equal(seen[1].id, 100);
  assert.equal(seen[1].message, 'Hello!');
  clearInterval(world.pingTimer);
});
