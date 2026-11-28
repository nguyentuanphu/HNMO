// Season 9 pentagram: the tooltip lines of pentagrams and errtels, the option texts, the element colours, the result
// texts, and finding an item's slot for its tooltip.
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const pentagram = await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/pentagram.js')).href);
const window = await import(pathToFileURL(path.resolve(__dirname, '../public/js/ui/pentagramWindow.js')).href);

const errtel = { number: 221, element: 1, options: [{ index: 0, level: 3, value: 51, name: 'Elemental Dmg +%d' }] };

test('an option text puts the value in and shows the level', () => {
  assert.equal(pentagram.optionText({ index: 10, level: 2, value: 7, name: '+%d%% Attack Against Fire Element' }), '+7% Attack Against Fire Element (+2)');
  assert.equal(pentagram.optionText(errtel.options[0]), 'Elemental Dmg +51 (+3)');
});

test('pentagrams and errtels are recognised, other items not', () => {
  assert.equal(pentagram.isPentagramItem(12, 200), true);
  assert.equal(pentagram.isPentagramItem(12, 214), true);
  assert.equal(pentagram.isPentagramItem(12, 231), true);
  assert.equal(pentagram.isPentagramItem(12, 215), false);
  assert.equal(pentagram.isPentagramItem(14, 221), false);
});

test('a pentagram tooltip shows its element and each usable slot', () => {
  const lines = pentagram.pentagramTooltipTexts({ slot: 20, number: 210, element: 2, usable: 3, errtels: [errtel, null, null, null, null] });
  assert.deepEqual(lines.map((l) => l.text), ['Element: Water', 'Slot 1: Elemental Dmg +51 (+3)', 'Slot 2: empty', 'Slot 3: empty']);
});

test('an errtel tooltip shows its element and its options by rank', () => {
  const lines = pentagram.pentagramTooltipTexts({ slot: 30, number: 221, element: 1, errtel });
  assert.deepEqual(lines.map((l) => l.text), ['Element: Fire', 'Rank 1: Elemental Dmg +51 (+3)']);
});

test('an item finds its tooltip lines through its inventory slot, only once the state is on', () => {
  const item = { group: 12, number: 221 };
  pentagram.bindPentagramInventory(new Map([[30, item]]));
  assert.deepEqual(pentagram.pentagramTooltipFor(item), []);
  pentagram.setPentagramState({ v: 1, enabled: true, worn: null, items: [{ slot: 30, number: 221, element: 1, errtel }] });
  assert.equal(pentagram.pentagramTooltipFor(item).length, 2);
  assert.deepEqual(pentagram.pentagramTooltipFor({ group: 12, number: 221 }), [], 'another object of the same kind is not in the bag');
});

test('each element has its own damage colour, none is white', () => {
  const colors = [1, 2, 3, 4, 5].map((e) => pentagram.elementColor(e).join(','));
  assert.equal(new Set(colors).size, 5);
  assert.deepEqual(pentagram.elementColor(0), [1, 1, 1]);
});

test('every server result has a text, an unknown one none', () => {
  for (const result of ['Ok', 'Disabled', 'InvalidItem', 'Occupied', 'Empty', 'ElementMismatch', 'BagFull', 'SaveFailed', 'WrongPlace', 'NotEnoughZen', 'MaximumReached', 'Failed', 'Broken']) {
    assert.notEqual(window.pentagramResultText(result), '', result);
  }

  assert.equal(window.pentagramResultText('Whatever'), '');
  assert.equal(window.pentagramResultText(null), '');
});
