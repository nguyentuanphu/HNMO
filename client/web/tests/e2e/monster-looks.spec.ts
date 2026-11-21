// How the web game draws monsters and NPCs (game/monsterData.ts, game/characters.ts), checked without a game server:
// the dev monster viewer (src/dev/monsterViewer.ts) builds them with the game's own code from Monsters.json and
// Models.json. The Skeleton is the skeleton skin with its sword and shield, the Skeleton Archer holds its bow, a
// guard wears its plate set and crossbow; every number of the list (each look) is built without an error, and only
// the entries the data leaves without a model get the unknown monsters' look. A monster that leaves view and comes
// back makes no new buffers, textures or shader programs (WebGL calls counted). Pictures go to SHOTS_DIR
// (default test-results/monster-looks).
import { test, expect, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';

interface AuditRow {
  number: number; name: string; kind: string; model: string; variant: number; unknown: string | null; error: string | null;
  warnings: string[]; meshes: number; visibleMeshes: number; untextured: number; attachments: string[]; body: string[];
  scale: number; playerShaped: boolean; ignored: string[];
}

const SHOTS_DIR = process.env.SHOTS_DIR ?? 'test-results/monster-looks';
/** The entries the data gives no model (docs/monster-data.md, Unknown monsters) and Molt, whose file is missing. */
const UNKNOWN_LOOKS: Record<number, string> = { 76: 'noModel', 252: 'noModel', 296: 'noModel', 585: 'noModel', 68: 'missingFile' };
/** Models whose data names textures their folders lack (the same files on the desktop): Life Stone, Selupan, Jeweler Raul. */
const KNOWN_UNTEXTURED = [278, 459, 546];
/** Player-shaped transformation looks: their skin is drawn for a (transformed) player only, so as monsters they show no body. */
const PLAYER_ONLY_LOOKS = [372, 373, 374, 378, 404, 405, 503, 548];
/**
 * Bodies the desktop does not draw either: the Crywolf altars (an effect of the map), Maya (the map's own code), and
 * the Laser Trap in Kanturu, whose map object (Object39/Object52.bmd) the data lacks; the Imperial Guardian's trap
 * (model "none"); and Trap 104, a map object of no listed map (drawn on the map the server places it on).
 */
const NO_BODY = [104, 106, 205, 206, 207, 208, 209, 364, 523];

const VIEWER_PAGE = '/monster-viewer.html';

async function openViewer(page: Page): Promise<void> {
  // A page of the web game's origin with three.js mapped, as the game's own page has it.
  await page.route(`**${VIEWER_PAGE}`, (route) => route.fulfill({
    contentType: 'text/html',
    body: '<!doctype html><html><head><script type="importmap">{"imports":{"three":"/vendor/three/build/three.module.js","three/":"/vendor/three/"}}</script></head>'
      + '<body style="margin:0;background:#000"><canvas id="c" width="1280" height="720"></canvas></body></html>',
  }));
  await page.addInitScript(() => {
    // WebGL calls that upload or compile, counted for the reuse check.
    const counts: Record<string, number> = {};
    (window as unknown as { __gl: Record<string, number> }).__gl = counts;
    const proto = WebGL2RenderingContext.prototype as unknown as Record<string, (...args: unknown[]) => unknown>;
    for (const name of ['bufferData', 'createBuffer', 'texImage2D', 'texStorage2D', 'createTexture', 'compileShader', 'linkProgram']) {
      const original = proto[name];
      proto[name] = function (this: unknown, ...args: unknown[]) { counts[name] = (counts[name] ?? 0) + 1; return original.apply(this, args); };
    }
  });
  await page.goto(VIEWER_PAGE);
}

const audit = (page: Page, numbers?: number[]): Promise<AuditRow[]> => page.evaluate(`(async () => {
  const viewer = await import('/js/dev/monsterViewer.js');
  if (!${JSON.stringify(numbers ?? null)}) return viewer.auditAll();
  await viewer.ready();
  const rows = [];
  for (const number of ${JSON.stringify(numbers ?? [])}) { const { row, character } = await viewer.auditOne(number); character?.release(); rows.push(row); }
  return rows;
})()`);

/** The numbers of the web's Season 9 list (public/data/Monsters/Season9.json). */
const season9Numbers = (page: Page): Promise<number[]> => page.evaluate(() => fetch('/data/Monsters/Season9.json').then((r) => r.json())
  .then((file: { monsters: { numbers: { number: number }[] }[] }) => file.monsters.flatMap((monster) => monster.numbers.map((own) => own.number))));

test.describe('monster looks', () => {
  test.beforeEach(async ({ page }) => {
    page.on('pageerror', (error) => { throw error; });
    await openViewer(page);
  });

  test('the skeletons, the archer and the guards wear what the desktop gives them', async ({ page }) => {
    const rows = await audit(page, [14, 15, 16, 247, 249, 34, 236]);
    const by = (number: number) => rows.find((row) => row.number === number)!;
    // Skeleton 14: the player model drawn as Skeleton01 (MODEL_SKELETON1), a Short Sword and a Small Shield in its hands.
    expect(by(14)).toMatchObject({ playerShaped: true, body: ['skin Skill/Skeleton01.bmd'], error: null, unknown: null });
    expect(by(14).attachments).toEqual(['rightHand 0,6 bone 33', 'leftHand 6,4 bone 42']);
    expect(by(14).scale).toBeCloseTo(0.95);
    expect(by(14).visibleMeshes).toBeGreaterThan(0);
    // Skeleton Archer 15: Skeleton02, nothing in its right hand, a bow in its left.
    expect(by(15)).toMatchObject({ body: ['skin Skill/Skeleton02.bmd'], attachments: ['leftHand 4,2 bone 42'] });
    expect(by(16)).toMatchObject({ body: ['skin Skill/Skeleton03.bmd'], attachments: ['rightHand 1,3 bone 33', 'leftHand 6,6 bone 42'] });
    // Guard 247: the plate set and a crossbow with bolts; 249 has its own equipment (a spear).
    expect(by(247).playerShaped).toBe(true);
    expect(by(247).attachments).toEqual(['rightHand 4,11 bone 33', 'leftHand 4,7 bone 42']);
    expect(by(247).meshes).toBeGreaterThanOrEqual(5);
    expect(by(247).scale).toBeCloseTo(0.9); // "character": a Dark Wizard's size
    expect(by(249).attachments).toEqual(['rightHand 3,7 bone 33']);
    // The Cursed Wizard: the Legendary set +9, its staff and shield.
    expect(by(34).attachments).toEqual(['rightHand 5,5 bone 33', 'leftHand 6,14 bone 42']);
    for (const row of rows) expect(row.warnings, `${row.number} ${row.name}`).toEqual([]);
  });

  test('every monster and NPC of the list is drawn, none falls back without a reason', async ({ page }) => {
    test.setTimeout(10 * 60_000);
    // The Season 9 list's numbers have their own test below (their files are in the Season 9 pack only).
    const season9 = new Set(await season9Numbers(page));
    const rows = (await audit(page)).filter((row) => !season9.has(row.number));
    expect(rows.length).toBeGreaterThan(500);
    const errors = rows.filter((row) => row.error).map((row) => `${row.number} ${row.name}: ${row.error}`);
    expect(errors).toEqual([]);
    const unknown = Object.fromEntries(rows.filter((row) => row.unknown).map((row) => [row.number, row.unknown]));
    expect(unknown).toEqual(UNKNOWN_LOOKS);
    const untextured = rows.filter((row) => row.untextured > 0).map((row) => row.number);
    expect(untextured.sort((a, b) => a - b)).toEqual(KNOWN_UNTEXTURED);
    const noBody = rows.filter((row) => row.visibleMeshes === 0 && !row.unknown).map((row) => row.number);
    expect(noBody.sort((a, b) => a - b)).toEqual([...NO_BODY, ...PLAYER_ONLY_LOOKS].sort((a, b) => a - b));
    // What the web leaves out, for the report (test output).
    const ignored = new Map<string, number[]>();
    for (const row of rows) for (const field of row.ignored) ignored.set(field, [...(ignored.get(field) ?? []), row.number]);
    console.log(`monster looks: ${rows.length} looks, ${new Set(rows.map((row) => row.number)).size} numbers, `
      + `${rows.filter((row) => row.playerShaped).length} player-shaped`);
    for (const [field, numbers] of [...ignored].sort()) console.log(`  not drawn by the web: ${field} (${numbers.length}: ${numbers.slice(0, 12).join(', ')}${numbers.length > 12 ? ', …' : ''})`);
  });

  // The map check of 2026-11-20: the Season 9 monsters and NPCs (Nars, Ferea, Uruk Mountain, Loren Market, ...)
  // had no entry, so every one was the unknown monsters' ghost. They are drawn with their Season 9 models now.
  test('the Season 9 monsters and NPCs are drawn with their Season 9 models, not the ghost', async ({ page }) => {
    const seasons = await page.evaluate(() => fetch('/data/seasons.json').then((r) => r.json())) as { s9?: boolean };
    const numbers = await season9Numbers(page);
    expect(numbers.length).toBeGreaterThanOrEqual(30);
    for (const season of ['s9', 's6']) {
      const rows = await page.evaluate(`(async () => {
        (await import('/js/dataRoot.js')).setSeason(${JSON.stringify(season)});
        const viewer = await import('/js/dev/monsterViewer.js');
        await viewer.ready();
        const rows = [];
        for (const number of ${JSON.stringify(numbers)}) { const { row, character } = await viewer.auditOne(number); character?.release(); rows.push(row); }
        return rows;
      })()`) as AuditRow[];
      for (const row of rows) {
        // Every number has an entry and a model (no "noEntry" or "noModel" ghost in either season).
        expect(row.unknown === null || row.unknown === 'missingFile', `${season} ${row.number} ${row.name}: ${row.unknown}`).toBe(true);
        expect(row.model, `${row.number}`).toMatch(/^(MONSTER_)?MODEL_S9_/);
        expect(row.error, `${row.number}`).toBeNull();
        if (!seasons.s9) continue; // without the Season 9 pack the files are missing: the ghost, for a reason
        // With the pack (both seasons: the files are Season 9's own, whichever season plays): its body, textured.
        expect(row.unknown, `${season} ${row.number} ${row.name}`).toBeNull();
        expect(row.playerShaped, `${row.number}`).toBe(false);
        expect(row.visibleMeshes, `${season} ${row.number} ${row.name}`).toBeGreaterThan(0);
        expect(row.untextured, `${season} ${row.number} ${row.name}: ${row.warnings.join('; ')}`).toBe(0);
      }
    }
    test.info().annotations.push({ type: 'season 9 pack', description: seasons.s9 ? 'served: bodies checked' : 'not served: entries only' });
  });

  test('a monster that comes back into view reuses its buffers, textures and programs', async ({ page }) => {
    const counts = await page.evaluate(`(async () => {
      const THREE = await import('three');
      const viewer = await import('/js/dev/monsterViewer.js');
      const { createMonsterCharacter } = await import('/js/game/characters.js');
      const { warmObject } = await import('/js/game/prewarm.js');
      await viewer.ready();
      const renderer = new THREE.WebGLRenderer({ canvas: document.getElementById('c') });
      renderer.outputColorSpace = THREE.LinearSRGBColorSpace;
      const scene = new THREE.Scene(), camera = new THREE.PerspectiveCamera(35, 16 / 9, 10, 20000);
      camera.position.set(0, 300, 1500);
      const size = 256, world = { heights: new Float32Array(size * size), light: new Float32Array(size * size * 3).fill(1), flags: new Uint8Array(size * size), mapNumber: 0 };
      const rounds = [];
      for (let round = 0; round < 3; round++) {
        const before = { ...window.__gl };
        const shown = [];
        for (const type of [14, 15, 247, 34, 3, 7]) {
          const character = await createMonsterCharacter({ id: type, type, map: 0 });
          character.update(0, world);
          await warmObject({ renderer, camera, scene }, character.group);
          scene.add(character.group);
          shown.push(character);
          renderer.render(scene, camera);
        }
        for (const character of shown) { scene.remove(character.group); character.release(); }
        rounds.push(Object.fromEntries(Object.keys(window.__gl).map((key) => [key, window.__gl[key] - (before[key] ?? 0)])));
      }
      return rounds;
    })()`) as Record<string, number>[];
    console.log('GL calls per round of appearances:', JSON.stringify(counts));
    expect(counts[0].bufferData ?? 0).toBeGreaterThan(0);
    for (const later of counts.slice(1)) {
      for (const call of ['bufferData', 'createBuffer', 'texImage2D', 'texStorage2D', 'createTexture', 'compileShader', 'linkProgram']) {
        expect(later[call] ?? 0, call).toBe(0);
      }
    }
  });

  test('pictures of a sample', async ({ page }) => {
    mkdirSync(SHOTS_DIR, { recursive: true });
    const groups: [string, number[], number | null][] = [
      ['skeletons', [14, 15, 16], 1], ['guards-and-wizard', [247, 249, 34, 229, 297], 0], ['chaos-castle', [162, 163, 164], 18],
      ['regular', [0, 1, 7, 10, 26, 85], null], ['npcs', [230, 253, 255, 236, 536, 538], 0],
    ];
    for (const [name, numbers, map] of groups) {
      const drawn = await page.evaluate(`(async () => {
        const viewer = await import('/js/dev/monsterViewer.js');
        const old = document.getElementById('c');
        const canvas = document.createElement('canvas');
        canvas.width = 1280; canvas.height = 720; canvas.id = 'c';
        old.replaceWith(canvas);
        window.__viewer = await viewer.showRow(canvas, ${JSON.stringify(numbers)}, ${JSON.stringify(map)});
        window.__viewer.render(0.5);
        return window.__viewer.characters.length;
      })()`);
      expect(drawn).toBe(numbers.length);
      await page.waitForTimeout(300);
      await page.evaluate('window.__viewer.render(0.6)');
      await page.locator('#c').screenshot({ path: `${SHOTS_DIR}/${name}.png` });
    }
  });
});

// In the game against a running server (IN_GAME=1, account test300: the Dungeon needs level 30): the dungeon's skeletons come with the server's
// monster packets and are drawn as above. Only looks are read; the hero moves to the dungeon and back to Lorencia.
test('in the game: the dungeon skeletons are skeletons with their weapons', async ({ page }) => {
  test.skip(!process.env.IN_GAME, 'needs a game server (IN_GAME=1)');
  test.setTimeout(6 * 60_000);
  const { enterWorld } = await import('./helpers.js');
  // LOCAL_JS: this checkout's built client in place of the server's (e.g. against an HNMO folder of an older build).
  const localJs = process.env.LOCAL_JS;
  if (localJs) {
    const { readFileSync, existsSync } = await import('node:fs');
    await page.route('**/js/**', (route) => {
      const file = `${localJs}/${new URL(route.request().url()).pathname.replace(/^\/js\//, '')}`;
      return existsSync(file) ? route.fulfill({ contentType: 'text/javascript', body: readFileSync(file) }) : route.continue();
    });
  }
  await enterWorld(page, process.env.IN_GAME_ACCOUNT ?? 'test300');
  await page.evaluate('window.__game.online.say("/move dungeon")');
  const skeleton = () => page.evaluate(`(() => {
    for (const c of window.__game.online.characters.values()) {
      if (c.monsterNumber >= 14 && c.monsterNumber <= 16 && !c.dead) {
        return { number: c.monsterNumber, skin: c.look?.player?.skin?.fileName ?? null, shaped: c.playerShaped,
          attachments: c.attachments.map((a) => a.slot + ' ' + a.item.group + ',' + a.item.number), visible: c.group.visible && !!c.group.parent };
      }
    }
    return null;
  })()`) as Promise<{ number: number; skin: string | null; shaped: boolean; attachments: string[]; visible: boolean } | null>;
  await expect.poll(skeleton, { timeout: 120_000, intervals: [2_000] }).not.toBeNull();
  const found = (await skeleton())!;
  expect(found.shaped).toBe(true);
  expect(found.skin).toBe(`Skeleton0${found.number - 13}.bmd`);
  const expected: Record<number, string[]> = { 14: ['rightHand 0,6', 'leftHand 6,4'], 15: ['leftHand 4,2'], 16: ['rightHand 1,3', 'leftHand 6,6'] };
  expect(found.attachments).toEqual(expected[found.number]);
  mkdirSync(SHOTS_DIR, { recursive: true });
  await page.screenshot({ path: `${SHOTS_DIR}/in-game-dungeon.png` });
  await page.evaluate('window.__game.online.say("/move lorencia")');
  await page.waitForTimeout(3_000);
});
