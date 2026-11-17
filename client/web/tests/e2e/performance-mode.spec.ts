import { expect, test } from '@playwright/test';
import { execFileSync, spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const BRIDGE_DIR = path.resolve(__dirname, '../../bridge');
const PUBLIC_DIR = path.resolve(__dirname, '../../public');
const VENDOR_DIR = path.resolve(__dirname, '../../node_modules');
const DATA_DIR = path.resolve(__dirname, '../../../src/bin/Data');

let bridgeProc: ChildProcessWithoutNullStreams | null = null;
let serverUrl = process.env.BASE_URL ?? '';

test.beforeAll(async () => {
  if (serverUrl) return;
  const dll = path.join(BRIDGE_DIR, 'bin/Release/net10.0/HNMO.dll');
  if (!fs.existsSync(dll)) {
    execFileSync('dotnet', ['build', path.join(BRIDGE_DIR, 'HNMO.Web.csproj'), '-c', 'Release'], { stdio: 'inherit' });
  }
  const port = 18086;
  serverUrl = `http://127.0.0.1:${port}`;
  bridgeProc = spawn(
    'dotnet',
    [
      dll,
      `--urls=${serverUrl}`,
      `--HNMO:WebRoot=${PUBLIC_DIR}`,
      `--HNMO:VendorPath=${VENDOR_DIR}`,
      `--HNMO:DataPath=${DATA_DIR}`,
      '--Launcher:Enabled=false',
    ],
    { cwd: BRIDGE_DIR },
  );
  for (let i = 0; i < 40; i++) {
    try {
      const res = await fetch(`${serverUrl}/health`);
      if (res.ok) break;
    } catch {
      // Waiting for bridge startup.
    }
    await new Promise((r) => setTimeout(r, 250));
  }
});

test.afterAll(() => {
  bridgeProc?.kill();
});

test.describe('Performance mode (settings, EN/VI, hotkeys, and FPS/draw-call measurement)', () => {
  test('toggles settings in EN and VI via panel and Alt+P/O/W/E hotkeys', async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', (err) => errors.push(err.message));

    await page.goto(`${serverUrl}/index.html`, { waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => Boolean((window as unknown as { __game?: { perfReport?: unknown } }).__game?.perfReport), null, { timeout: 60_000 });
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());

    // Open Settings -> Graphics tab
    await page.keyboard.press('o');
    await page.waitForSelector('.rml-option_window #panel', { timeout: 30_000 });
    await page.locator('.rml-option_window .option-tab-btn').nth(1).click();

    await expect(page.locator('#hnmo_perf_mode .option-row-label')).toHaveText('Performance mode (Alt+P)');
    await expect(page.locator('#hnmo_hide_players .option-row-label')).toHaveText('Hide other players (Alt+O)');
    await expect(page.locator('#hnmo_hide_wings_pets .option-row-label')).toHaveText('Hide wings and pets (Alt+W)');
    await expect(page.locator('#hnmo_reduced_effects .option-row-label')).toHaveText('Fewer skill and item effects (Alt+E)');

    // Verify Vietnamese translations
    await page.evaluate(async () => {
      const i18n = await import('/js/i18n.js' as string) as { setLanguage(lang: string): Promise<void> };
      await i18n.setLanguage('vi');
    });
    await expect(page.locator('#hnmo_perf_mode .option-row-label')).toHaveText('Chế độ mượt (Alt+P)');
    await expect(page.locator('#hnmo_hide_players .option-row-label')).toHaveText('Ẩn người chơi khác (Alt+O)');
    await expect(page.locator('#hnmo_hide_wings_pets .option-row-label')).toHaveText('Ẩn cánh và thú cưng (Alt+W)');
    await expect(page.locator('#hnmo_reduced_effects .option-row-label')).toHaveText('Giảm hiệu ứng kỹ năng và vật phẩm (Alt+E)');

    // Close settings and toggle via hotkeys
    await page.keyboard.press('Escape');
    await page.keyboard.press('Alt+KeyP');
    await expect(page.locator('#hnmo-perf-toast')).toContainText('BẬT');

    const stored = await page.evaluate(() => JSON.parse(localStorage.getItem('hnmo.settings') || '{}'));
    expect(stored.quality.hideOtherPlayers).toBe(true);
    expect(stored.quality.hideWingsPets).toBe(true);
    expect(stored.quality.reducedEffects).toBe(true);

    // Individual hotkey toggle off Alt+O
    await page.keyboard.press('Alt+KeyO');
    const afterO = await page.evaluate(() => JSON.parse(localStorage.getItem('hnmo.settings') || '{}'));
    expect(afterO.quality.hideOtherPlayers).toBe(false);
    expect(afterO.quality.hideWingsPets).toBe(true);

    // Individual hotkeys Alt+W and Alt+E
    await page.keyboard.press('Alt+KeyW');
    await page.keyboard.press('Alt+KeyE');
    const afterWE = await page.evaluate(() => JSON.parse(localStorage.getItem('hnmo.settings') || '{}'));
    expect(afterWE.quality.hideWingsPets).toBe(false);
    expect(afterWE.quality.reducedEffects).toBe(false);

    expect(errors, errors.join('\n')).toEqual([]);
  });

  test('improves frame times / cuts draw calls in crowded scene and runs Ctrl+Shift+P report', async ({ page }) => {
    await page.goto(`${serverUrl}/index.html`, { waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => Boolean((window as unknown as { __game?: { debug?: { renderer?: unknown } } }).__game?.debug?.renderer), null, { timeout: 60_000 });
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());

    const metrics = await page.evaluate(async () => {
      const THREE = await import('three' as string) as typeof import('three');
      const qMod = await import('/js/render/quality.js' as string) as typeof import('../../src/render/quality.js');
      const g = (window as unknown as { __game: { debug: { renderer: import('three').WebGLRenderer; scene: import('three').Scene; camera: import('three').PerspectiveCamera } } }).__game;

      // Build a crowded synthetic arena (40 player groups with body + wings + pet + heavy orb effects)
      // directly in the camera's view so all 160 meshes render without frustum culling.
      const crowdRoot = new THREE.Group();
      const playerGeom = new THREE.SphereGeometry(40, 24, 24);
      const wingGeom = new THREE.PlaneGeometry(90, 60);
      const fxGeom = new THREE.TorusKnotGeometry(35, 10, 64, 12);
      const mat = new THREE.MeshBasicMaterial({ color: 0xffaa44 });
      const viewDir = new THREE.Vector3();
      g.debug.camera.getWorldDirection(viewDir);
      const focusCenter = g.debug.camera.position.clone().addScaledVector(viewDir, 800);
      const crowdPlayers: { root: import('three').Group; body: import('three').Mesh; wing: import('three').Mesh; pet: import('three').Mesh; fx: import('three').Mesh }[] = [];
      for (let i = 0; i < 40; i++) {
        const pRoot = new THREE.Group();
        pRoot.position.set(focusCenter.x + (i % 8 - 4) * 35, focusCenter.y + Math.floor(i / 8 - 2) * 35, focusCenter.z);
        const body = new THREE.Mesh(playerGeom, mat);
        const wing = new THREE.Mesh(wingGeom, mat);
        const pet = new THREE.Mesh(playerGeom, mat);
        const fx = new THREE.Mesh(fxGeom, mat);
        body.frustumCulled = wing.frustumCulled = pet.frustumCulled = fx.frustumCulled = false;
        pRoot.add(body, wing, pet, fx);
        crowdRoot.add(pRoot);
        crowdPlayers.push({ root: pRoot, body, wing, pet, fx });
      }
      g.debug.scene.add(crowdRoot);

      const sampleWindow = (framesToDraw: number) => {
        const perf = qMod.performanceFlags();
        for (const p of crowdPlayers) {
          p.root.visible = !perf.hideOtherPlayers;
          p.wing.visible = !perf.hideWingsPets;
          p.pet.visible = !perf.hideWingsPets;
          p.fx.visible = !perf.reducedEffects;
        }
        const t0 = performance.now();
        for (let i = 0; i < framesToDraw; i++) g.debug.renderer.render(g.debug.scene, g.debug.camera);
        const calls = g.debug.renderer.info.render.calls;
        const elapsedMs = Math.max(0.1, performance.now() - t0);
        return { calls, msPerFrame: elapsedMs / framesToDraw, fps: (framesToDraw * 1000) / elapsedMs };
      };

      const baseline = sampleWindow(30);
      qMod.applyQuality({ renderer: g.debug.renderer, scene: g.debug.scene, camera: g.debug.camera }, { ...qMod.DEFAULT_QUALITY, hideWingsPets: true, reducedEffects: true });
      const hideExtrasOnly = sampleWindow(30);
      qMod.applyQuality({ renderer: g.debug.renderer, scene: g.debug.scene, camera: g.debug.camera }, { ...qMod.DEFAULT_QUALITY, ...qMod.PERFORMANCE_MODE });
      const perfOn = sampleWindow(30);
      g.debug.scene.remove(crowdRoot);
      return { baseline, hideExtrasOnly, perfOn };
    });

    console.log('CROWD PERF COMPARISON:', JSON.stringify(metrics));
    expect(metrics.baseline.calls).toBeGreaterThanOrEqual(160);
    expect(metrics.hideExtrasOnly.calls).toBeLessThan(metrics.baseline.calls);
    expect(metrics.perfOn.calls).toBeLessThan(metrics.hideExtrasOnly.calls);

    // Trigger Ctrl+Shift+P to verify the performance report UI opens cleanly.
    await page.keyboard.press('Control+Shift+KeyP');
    await expect(page.locator('.hnmo-perf-report')).toBeVisible({ timeout: 30_000 });
  });
});
