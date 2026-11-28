// The Season 9 Muun window reads the server's F5 2C state and sends F5 2B commands (ui/muunWindow.ts).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const packets = await import(pathToFileURL(path.resolve(__dirname, '../public/js/net/packets.js')).href);
const muun = await import(pathToFileURL(path.resolve(__dirname, '../public/js/ui/muunWindow.js')).href);

const active = {
  slot: 12, group: 13, number: 212, name: 'Luki', level: 1, maxLevel: 4, evolved: false,
  bonusEn: 'Increase Defense +44', bonusVi: 'Tăng phòng thủ +44', bonus: 44, applies: false,
  conditionEn: 'From 18:00 to 24:59 (Sydney time)', conditionVi: 'Từ 18:00 đến 24:59 (giờ Sydney)',
  canEvolve: false, levelUpCost: 10,
};

test('the F5 2C state is read from a real packet', () => {
  const json = JSON.stringify({ v: 1, enabled: true, active, bag: [], energy: 3 });
  const bytes = packets.write('ServerToClient', 'HnmoMuunState', { Json: json });
  assert.equal(bytes[3], 0xf5);
  assert.equal(bytes[4], muun.MUUN_SUBCODE_STATE);
  const state = muun.parseMuunState(bytes);
  assert.equal(state.active.name, 'Luki');
  assert.equal(state.energy, 3);
});

test('other packets and broken JSON are not a Muun state', () => {
  const other = packets.write('ServerToClient', 'HnmoJewelBankState', { Json: '{}' });
  assert.equal(muun.parseMuunState(other), null);
  const broken = packets.write('ServerToClient', 'HnmoMuunState', { Json: '{not json' });
  assert.equal(muun.parseMuunState(broken), null);
});

test('the F5 2B command carries the action and the slot', () => {
  const bytes = packets.write('ClientToServer', 'HnmoMuunCommand', { Action: muun.MUUN_COMMAND.Equip, Slot: 14 });
  assert.equal(bytes.length, 6);
  const read = packets.read('ClientToServer', 'HnmoMuunCommand', bytes);
  assert.equal(read.Action, 2);
  assert.equal(read.Slot, 14);
});

test('the active line says whether the bonus applies now', () => {
  assert.match(muun.describeActive(active), /Defense \+44 .* \(waiting\)$/);
  assert.match(muun.describeActive({ ...active, applies: true }), /\(active now\)$/);
});
