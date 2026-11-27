// Unit tests of the socket bytes as the desktop client reads them (CItemMng::CreateItem): 0xFF is no socket and ends
// the item's sockets, 0xFE is an empty socket ("No item application"), any other byte is a seed sphere. Before, five
// 0xFF bytes read as five seeds 5 (255 % 50) and the tooltip showed five "Fire(AG cost decrease +0%)" lines.
// Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const load = (file) => import(pathToFileURL(path.resolve(__dirname, '../public/js/game', file)).href);
const { NO_SOCKET, SOCKET_EMPTY, socketBytes } = await load('socketBytes.js');
const { decodeItem } = await load('inventory.js');

test('0xFF is no socket: the sockets end at the first one', () => {
  assert.equal(NO_SOCKET, 0xff);
  assert.deepEqual(socketBytes([0xff, 0xff, 0xff, 0xff, 0xff]), []);
  assert.deepEqual(socketBytes([0xfe, 0xfe, 0xff, 0xff, 0xff]), [0xfe, 0xfe]);
});

test('0xFE is an empty socket and stays one', () => {
  assert.equal(SOCKET_EMPTY, 0xfe);
  assert.deepEqual(socketBytes([0xfe, 0xfe, 0xfe]), [0xfe, 0xfe, 0xfe]);
});

test('a seed sphere byte is kept: 0x33 is seed 1 at sphere level 2', () => {
  const sockets = socketBytes([0x33, 0xfe, 0xff]);
  assert.deepEqual(sockets, [0x33, 0xfe]);
  assert.equal(sockets[0] % 50, 1);
  assert.equal(Math.floor(sockets[0] / 50) + 1, 2);
});

// An extended item (ItemSerializerExtended) with the sockets flag (0x80): Flamberge-like 0/26, +0, durability 40,
// then the socket byte (bonus 0xF high, count 5 low) and five socket bytes.
const socketed = (sockets) => new Uint8Array([0x00, 26, 0, 40, 0x80, 0xf0 | sockets.length, ...sockets]);

test('decodeItem keeps the item length but drops the 0xFF sockets', () => {
  const none = decodeItem(socketed([0xff, 0xff, 0xff, 0xff, 0xff]));
  assert.deepEqual(none.sockets, []);
  assert.equal(none.length, 11);
  const mixed = decodeItem(socketed([0x33, 0xfe, 0xfe, 0xff, 0xff]));
  assert.deepEqual(mixed.sockets, [0x33, 0xfe, 0xfe]);
  assert.equal(mixed.socketBonus, 0xf);
  assert.equal(mixed.length, 11);
});
