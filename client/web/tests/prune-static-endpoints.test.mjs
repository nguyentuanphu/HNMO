// Unit tests of tools/prune-static-endpoints.mjs (run: node --test client/web/tests/*.test.mjs).
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { pruneServerFolder } from '../tools/prune-static-endpoints.mjs';

test('drops the routes of deleted compressed copies and keeps the plain files', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'hnmo-endpoints-'));
  try {
    const layout = path.join(root, 'wwwroot', '_content', 'Panel', 'Layout');
    fs.mkdirSync(layout, { recursive: true });
    fs.writeFileSync(path.join(layout, 'Modal.razor.js'), 'export {}');
    const asset = '_content/Panel/Layout/Modal.razor.js';
    const manifest = {
      Version: 1,
      Endpoints: [
        { Route: asset, AssetFile: asset, Selectors: [] },
        { Route: asset, AssetFile: `${asset}.gz`, Selectors: [{ Name: 'Content-Encoding', Value: 'gzip' }] },
        { Route: '_content/Panel/Layout/Modal.abc123.razor.js', AssetFile: asset, Selectors: [] },
        { Route: '_content/Panel/Layout/Modal.abc123.razor.js', AssetFile: `${asset}.br`, Selectors: [{ Name: 'Content-Encoding', Value: 'br' }] },
        { Route: `${asset}.gz`, AssetFile: `${asset}.gz`, Selectors: [] },
      ],
    };
    fs.writeFileSync(path.join(root, 'HNMO.Startup.staticwebassets.endpoints.json'), JSON.stringify(manifest));

    const lines = pruneServerFolder(root);

    assert.deepEqual(lines, ['HNMO.Startup.staticwebassets.endpoints.json: 2 routes kept, 3 without a file removed']);
    const pruned = JSON.parse(fs.readFileSync(path.join(root, 'HNMO.Startup.staticwebassets.endpoints.json'), 'utf8'));
    assert.equal(pruned.Version, 1);
    assert.deepEqual(pruned.Endpoints.map((e) => e.Route), [asset, '_content/Panel/Layout/Modal.abc123.razor.js']);
    assert.ok(pruned.Endpoints.every((e) => e.AssetFile === asset));
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
