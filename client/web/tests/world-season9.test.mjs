import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const { filesFolder, mapName } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/world.js')).href);

test('Season 9 maps resolve to their data folders and display names', () => {
  assert.equal(filesFolder(91), 92);
  assert.equal(filesFolder(92), 92);
  assert.equal(filesFolder(95), 96);
  assert.equal(filesFolder(96), 96);
  assert.equal(filesFolder(97), 19);
  assert.equal(filesFolder(98), 99);
  assert.equal(filesFolder(99), 99);
  assert.equal(filesFolder(100), 101);
  assert.equal(filesFolder(101), 101);
  assert.equal(filesFolder(102), 103);

  assert.equal(mapName(91), 'Acheron');
  assert.equal(mapName(92), 'Acheron');
  assert.equal(mapName(95), 'Debenter');
  assert.equal(mapName(96), 'Debenter');
  assert.equal(mapName(97), 'Chaos Castle Survival');
  assert.equal(mapName(98), 'Illusion Temple League');
  assert.equal(mapName(99), 'Illusion Temple League');
  assert.equal(mapName(100), 'Uruk Mountain');
  assert.equal(mapName(101), 'Uruk Mountain');
  assert.equal(mapName(102), 'Tormented Square');
});
