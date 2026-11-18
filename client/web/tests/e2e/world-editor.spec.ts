import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test.describe('Admin 3D World Editor (ROADMAP 14.24 Part A)', () => {
  test('mounts Three.js viewport, paints terrain flags, supports undo/redo, and updates spawns & gates', async ({ page }) => {
    const bundlePath = resolve(process.cwd(), '../../server/src/Web/AdminPanel/wwwroot/js/world-editor.js');
    const bundleJs = readFileSync(bundlePath, 'utf8');

    await page.route('https://hnmo-world-editor.local/**', async (route) => {
      const url = route.request().url();
      if (url.endsWith('/world-editor.js')) {
        await route.fulfill({ status: 200, contentType: 'application/javascript', body: bundleJs });
        return;
      }
      if (url.includes('/api/world-editor/data/')) {
        await route.fulfill({ status: 404, body: 'Not found' });
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'text/html',
        body: `<!doctype html>
<html>
  <body style="margin:0">
    <div id="host" style="width:960px;height:540px"></div>
    <script type="module">
      import { createWorldEditor } from '/world-editor.js';
      const host = document.getElementById('host');
      const zeroBytes = new Uint8Array(256 * 256);
      let bin = '';
      for (let i = 0; i < zeroBytes.length; i += 0x8000) bin += String.fromCharCode(...zeroBytes.subarray(i, i + 0x8000));
      const editor = createWorldEditor(host, null, { dataBaseUrl: '/api/world-editor' });
      const loaded = await editor.setMap(0, btoa(bin), [], [], []);
      window.__worldEditor = editor;
      window.__loaded = loaded;
    </script>
  </body>
</html>`,
      });
    });

    await page.goto('https://hnmo-world-editor.local/test.html');
    await page.waitForFunction(() => Boolean((window as unknown as { __worldEditor?: unknown }).__worldEditor));

    const canvasBox = await page.locator('#world-editor-canvas').boundingBox();
    expect(canvasBox).not.toBeNull();
    expect(canvasBox!.width).toBeGreaterThan(300);
    expect(canvasBox!.height).toBeGreaterThan(200);

    const summaryAfterOps = await page.evaluate(() => {
      const ed = (window as unknown as {
        __worldEditor: {
          paintAtTile: (x: number, y: number, tool: string, r: number) => number;
          undo: () => boolean;
          redo: () => boolean;
          updateEntities: (s: unknown[], eg: unknown[], xg: unknown[]) => void;
          setCameraMode: (m: 'player' | 'orbit', x: number, y: number) => void;
          getTileFlagsBase64: () => string;
          getStateSummary: () => { undoDepth: number; redoDepth: number; safezoneCount: number; blockedCount: number };
        };
      }).__worldEditor;

      ed.paintAtTile(128, 128, 'safezone', 2);
      ed.paintAtTile(64, 64, 'blocked', 1);
      const afterPaint = ed.getStateSummary();
      const undone = ed.undo();
      const afterUndo = ed.getStateSummary();
      const redone = ed.redo();
      ed.updateEntities(
        [{ id: 's1', monsterNumber: 1, monsterName: 'Spider', x1: 120, y1: 120, x2: 124, y2: 124, quantity: 5, direction: 1, spawnTrigger: 0 }],
        [{ id: 'e1', number: 1, levelRequirement: 1, x1: 50, y1: 50, x2: 52, y2: 52, targetGateId: null, targetGateLabel: 'Dungeon' }],
        [{ id: 'x1', x1: 130, y1: 130, x2: 132, y2: 132, direction: 1, isSpawnGate: true }],
      );
      ed.setCameraMode('player', 128, 128);
      const bytes = Uint8Array.from(atob(ed.getTileFlagsBase64()), (c) => c.charCodeAt(0));

      // Part B: 3D Object placement/transform/delete, light painting, and minimap capture
      const ext = ed as unknown as {
        addObject: (t: number, x: number, y: number, s: number) => number;
        updateObjectTransform: (idx: number, x: number, y: number, z: number, ax: number, ay: number, az: number, s: number) => boolean;
        removeObject: (idx: number) => boolean;
        getRawObjectsBase64: () => string | null;
        setLightBrush: (hex: string, inten: number) => void;
        paintLightAtTile: (x: number, y: number, r?: number) => number;
        getLightMapJpegBase64: (force?: boolean) => string | null;
        captureMinimapRgba: (size?: number) => { width: number; height: number; rgbaBase64: string; previewDataUrl: string };
      };
      const objIdx = ext.addObject(3, 100, 110, 1.5);
      const moved = ext.updateObjectTransform(objIdx, 10150, 11250, 50, 0, 45, 0, 2.0);
      const rawObjB64 = ext.getRawObjectsBase64();
      const rawObjBytes = rawObjB64 ? Uint8Array.from(atob(rawObjB64), (c) => c.charCodeAt(0)) : new Uint8Array(0);
      const removed = ext.removeObject(objIdx);

      ext.setLightBrush('#ffcc66', 0.8);
      const litCells = ext.paintLightAtTile(128, 128, 2);
      const lightJpg = ext.getLightMapJpegBase64();
      const minimap = ext.captureMinimapRgba(128);

      return {
        afterPaint,
        undone,
        afterUndo,
        redone,
        finalTileCenter: bytes[128 * 256 + 128],
        finalTileBlocked: bytes[64 * 256 + 64],
        objIdx,
        moved,
        rawObjByteLength: rawObjBytes.length,
        removed,
        litCells,
        hasLightJpg: Boolean(lightJpg && lightJpg.length > 20),
        minimapWidth: minimap.width,
        minimapHeight: minimap.height,
        hasMinimapData: minimap.rgbaBase64.length > 100 && minimap.previewDataUrl.startsWith('data:image/png'),
      };
    });

    expect(summaryAfterOps.afterPaint.safezoneCount).toBe(9);
    expect(summaryAfterOps.afterPaint.blockedCount).toBe(1);
    expect(summaryAfterOps.undone).toBe(true);
    expect(summaryAfterOps.afterUndo.blockedCount).toBe(0);
    expect(summaryAfterOps.redone).toBe(true);
    expect(summaryAfterOps.finalTileCenter).toBe(1);
    expect(summaryAfterOps.finalTileBlocked).toBe(4);
    expect(summaryAfterOps.objIdx).toBe(0);
    expect(summaryAfterOps.moved).toBe(true);
    expect(summaryAfterOps.rawObjByteLength).toBe(34);
    expect(summaryAfterOps.removed).toBe(true);
    expect(summaryAfterOps.litCells).toBeGreaterThanOrEqual(4);
    expect(summaryAfterOps.hasLightJpg).toBe(true);
    expect(summaryAfterOps.minimapWidth).toBe(128);
    expect(summaryAfterOps.minimapHeight).toBe(128);
    expect(summaryAfterOps.hasMinimapData).toBe(true);
  });
});
