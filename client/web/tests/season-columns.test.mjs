// Unit tests of the start screen's season columns (src/ui/seasonColumns.ts): Season 6.3 and Season 9 servers side
// by side. Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const { groupBySeason, seasonColumnTexts } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/ui/seasonColumns.js')).href);

test('servers go to their world\'s season, in the order they came', () => {
  const seasons = new Map([[0, 's6'], [1, 's9'], [2, 's6'], [3, 's9']]);
  const columns = groupBySeason([3, 0, 1, 2], (id) => seasons.get(id));
  assert.deepEqual(columns, { s6: [0, 2], s9: [3, 1] });
});

test('a server without a known season is Season 6.3\'s', () => {
  assert.deepEqual(groupBySeason([7, 8], () => undefined), { s6: [7, 8], s9: [] });
});

test('the texts exist in English and Vietnamese', () => {
  for (const vi of [false, true]) {
    const texts = seasonColumnTexts(vi);
    for (const value of [texts.title, texts.empty, texts.heading.s6, texts.heading.s9, texts.hint.s6, texts.hint.s9]) {
      assert.ok(typeof value === 'string' && value.length > 0);
    }
  }
  assert.notEqual(seasonColumnTexts(true).empty, seasonColumnTexts(false).empty);
});
