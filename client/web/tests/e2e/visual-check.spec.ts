// A visual check of the interface: opens the main windows and screenshots each, and draws every item of the
// catalogue as the inventory draws it, one sheet per group, flagging items with a black square behind them
// (most of the item's box opaque black). Screenshots go to SHOTS (default test-results/visual); look at them.
// Needs a running server for the windows (test400: a master character); the item sheets need only the web client.
import { test, expect, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { enterWorld, expectNoPageErrors } from './helpers.js';

const SHOTS = process.env.SHOTS ?? 'test-results/visual';
const TAG = process.env.TAG ?? 'shot';
const OPEN = { timeout: 90_000 };
mkdirSync(SHOTS, { recursive: true });

// The key that opens a window and its document.
const WINDOWS: [string, string][] = [
  ['i', 'my_inventory'], ['c', 'character_info'], ['a', 'master_level'], ['t', 'my_quest_info'],
  ['o', 'option_window'], ['m', 'move_command'], ['p', 'party_info'], ['g', 'guild_info'],
  ['f', 'friend_shell'], ['z', 'mu_helper_config'], ['Tab', 'mini_map'], ['x', 'in_game_shop'],
];

interface Npc { id: number; monsterNumber: number | null; cell: { x: number; y: number } }
interface Win { __game: { online: { characters: Map<number, Npc>; hero: { cell: { x: number; y: number } }; talkTo(npc: unknown): void; say(text: string): void } } }
const ELF_SOLDIER = 257;

async function shoot(page: Page, name: string): Promise<void> {
  await page.screenshot({ path: `${SHOTS}/${TAG}-${name}.png` });
}

test('the windows', async ({ page }) => {
  test.setTimeout(20 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, process.env.ACCOUNT ?? 'test400');
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(6_000);
  for (const [key, doc] of WINDOWS) {
    await page.keyboard.press(key);
    const window = page.locator(`.rml-${doc}`);
    await expect(window).toHaveCount(1, OPEN);
    await page.waitForTimeout(4_000);
    await shoot(page, doc);
    await page.keyboard.press(key === 'x' ? 'Escape' : key);
    await page.waitForTimeout(1_500);
  }
  // The auto-play window's other tabs, its detail page.
  await page.keyboard.press('z');
  await expect(page.locator('.rml-mu_helper_config')).toHaveCount(1, OPEN);
  for (const tab of [1, 2]) {
    await page.locator(`.rml-mu_helper_config #tab_${tab}`).click({ force: true });
    await page.waitForTimeout(2_000);
    await shoot(page, `mu_helper_config-tab${tab}`);
  }
  await page.keyboard.press('z');

  // An NPC's dialogue: Lorencia's Elf Soldier.
  const nearest = () => page.evaluate((number) => {
    const { online } = (window as unknown as Win).__game;
    const { x, y } = online.hero.cell;
    const npcs = [...online.characters.values()].filter((c) => c.monsterNumber === number);
    npcs.sort((a, b) => Math.hypot(a.cell.x - x, a.cell.y - y) - Math.hypot(b.cell.x - x, b.cell.y - y));
    return npcs[0]?.id ?? null;
  }, ELF_SOLDIER);
  await expect.poll(nearest, OPEN).not.toBeNull();
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(id)); }, (await nearest())!);
  await expect(page.locator('.rml-npc_dialogue')).toHaveCount(1, OPEN);
  await page.waitForTimeout(4_000);
  await shoot(page, 'npc_dialogue');
  await page.keyboard.press('Escape');
  await expectNoPageErrors(errors);
});

interface Sheet { flagged: string[]; drawn: number }

test('the item icons', async ({ page }) => {
  test.setTimeout(30 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/');
  const groups = (process.env.GROUPS ?? '0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15').split(',').map(Number);
  const flaggedAll: string[] = [];
  for (const group of groups) {
    const sheet = await page.evaluate(async (g): Promise<Sheet> => {
      // The web client's own modules, as the page serves them.
      const load = <T>(path: string): Promise<T> => import(path) as Promise<T>;
      const items = await load<typeof import('../../src/game/items.js')>('/js/game/items.js');
      const glow = await load<typeof import('../../src/game/itemGlow.js')>('/js/game/itemGlow.js');
      const icons = await load<typeof import('../../src/ui/itemIcons.js')>('/js/ui/itemIcons.js');
      await Promise.all([items.loadItemCatalog(), glow.loadGlowColors()]);
      document.body.innerHTML = '';
      document.body.style.cssText = 'margin:0;background:#3a4a5a;';
      const grid = document.createElement('div');
      grid.style.cssText = 'display:flex;flex-wrap:wrap;gap:2px;padding:2px;align-items:flex-start;';
      document.body.append(grid);
      const SCALE = 2;
      const flagged: string[] = [];
      let drawn = 0;
      for (const { number } of items.itemsWithModels(g)) {
        const definition = items.itemDefinition(g, number);
        const width = Number(definition?.width ?? 1), height = Number(definition?.height ?? 1);
        const cell = document.createElement('div');
        cell.style.cssText = `position:relative;width:${width * 20 * SCALE}px;height:${height * 20 * SCALE}px;background:#1d2b2b;outline:1px solid #556;`;
        const canvas = document.createElement('canvas');
        canvas.width = width * 20 * SCALE; canvas.height = height * 20 * SCALE;
        cell.append(canvas);
        const label = document.createElement('span');
        label.textContent = String(number);
        label.style.cssText = 'position:absolute;left:1px;top:0;font:9px sans-serif;color:#ff0;';
        cell.append(label);
        grid.append(cell);
        const item = { group: g, number, level: 0, excellent: 0, ancientSet: 0, width, height };
        // drawItem takes an inventory item; the icon needs only these fields.
        await icons.drawItem(canvas, item as unknown as Parameters<typeof icons.drawItem>[1], { pixelsPerReference: SCALE });
        drawn++;
        // Opaque near-black pixels over a large share of the box: a black square behind the item.
        const data = canvas.getContext('2d')!.getImageData(0, 0, canvas.width, canvas.height).data;
        let black = 0, opaque = 0;
        for (let i = 0; i < data.length; i += 4) {
          if (data[i + 3] <= 200) continue;
          opaque++;
          if (data[i] + data[i + 1] + data[i + 2] < 24) black++;
        }
        if (black > canvas.width * canvas.height * 0.03 && black > opaque * 0.3) { flagged.push(`${g}:${number} ${items.itemName(g, number)}`); cell.style.outline = '2px solid red'; }
      }
      return { flagged, drawn };
    }, group);
    console.log(`group ${group}: ${sheet.drawn} items, black: ${sheet.flagged.join('; ') || 'none'}`);
    flaggedAll.push(...sheet.flagged);
    await page.screenshot({ path: `${SHOTS}/${TAG}-items-${String(group).padStart(2, '0')}.png`, fullPage: true });
  }
  console.log(`items with a black square: ${flaggedAll.length}`);
  await expectNoPageErrors(errors);
  if (process.env.STRICT_BLACK) expect(flaggedAll).toEqual([]);
});
