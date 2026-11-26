// The Season 9 realm's client settings (F5 58 / F5 59): the web game asks only in a Season 9 realm, and the admin
// panel's "Season 9 minimap" setting shows or hides the Season 9 minimap markers.
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const js = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../public/js');
const load = (file) => import(pathToFileURL(path.join(js, file)).href);
const { write } = await load('net/packets.js');
const client = await load('game/seasonNineClient.js');
const miniMap = await load('game/miniMapData.js');
const dataRoot = await load('dataRoot.js');

test('the state packet sets the Season 9 minimap markers on and off', () => {
  const off = write('ServerToClient', 'HnmoSeasonNineClientState', { Json: '{"v":1,"minimap":false}' });
  assert.deepEqual(client.parseSeasonNineClientState(off), { v: 1, minimap: false });
  assert.equal(client.handleSeasonNineClientState(off), true);
  assert.equal(miniMap.seasonNineMinimapMarkers(), false);

  const on = write('ServerToClient', 'HnmoSeasonNineClientState', { Json: '{"v":1,"minimap":true}' });
  assert.equal(client.handleSeasonNineClientState(on), true);
  assert.equal(miniMap.seasonNineMinimapMarkers(), true);
});

test('another packet is not taken for the state', () => {
  const other = write('ServerToClient', 'HnmoCatchupCapState', { Json: '{"v":1}' });
  assert.equal(client.parseSeasonNineClientState(other), null);
  assert.equal(client.handleSeasonNineClientState(other), false);
});

test('the settings are asked for in a Season 9 realm only', () => {
  const sent = [];
  dataRoot.setSeason('s6');
  client.requestSeasonNineClientState((name) => sent.push(name));
  assert.deepEqual(sent, []);
  dataRoot.setSeason('s9');
  client.requestSeasonNineClientState((name) => sent.push(name));
  assert.deepEqual(sent, ['HnmoSeasonNineClientRequest']);
});

test('a season change shows the markers again until the new realm says otherwise', () => {
  dataRoot.setSeason('s9');
  client.handleSeasonNineClientState(write('ServerToClient', 'HnmoSeasonNineClientState', { Json: '{"v":1,"minimap":false}' }));
  assert.equal(miniMap.seasonNineMinimapMarkers(), false);
  dataRoot.setSeason('s6');
  assert.equal(miniMap.seasonNineMinimapMarkers(), true);
});
