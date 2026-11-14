// Text fit at a screen size (VIEWPORT=WxH, default 1280x720): opens the main windows and lists every
// text of the interface documents that runs out of its box (sideways by more than a pixel and a half,
// or below it by more than three), and screenshots each window into SHOTS. The desktop shrinks such
// texts to fit or cuts them with "..": anything listed here is a text to look at.
// STRICT_FIT=1 fails the test when a text overflows. Needs a running server (ACCOUNT, default test8).
import { test, expect, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { enterWorld, expectNoPageErrors } from './helpers.js';

const [WIDTH, HEIGHT] = (process.env.VIEWPORT ?? '1280x720').split('x').map(Number);
const SHOTS = process.env.SHOTS ?? 'test-results/text-fit';
const OPEN = { timeout: 90_000 };
const POLL = { timeout: 30_000 };
mkdirSync(SHOTS, { recursive: true });
test.use({ viewport: { width: WIDTH, height: HEIGHT }, deviceScaleFactor: 1 });

interface Overflow { doc: string; box: string; text: string; right: number; below: number }
interface Npc { id: number; monsterNumber: number | null; cell: { x: number; y: number } }
interface Win {
  __game: {
    online: { characters: Map<number, Npc>; hero: { cell: { x: number; y: number } }; talkTo(npc: unknown): void; say(text: string): void; walkTo(cell: { x: number; y: number }): boolean };
    inventory: { items: Map<number, unknown> };
  };
}

/** The texts of the open interface documents that run out of their boxes. */
function overflows(page: Page): Promise<Overflow[]> {
  return page.evaluate(() => {
    const found: Overflow[] = [];
    for (const host of document.querySelectorAll<HTMLElement>('.rml-document')) {
      const doc = [...host.classList].find((name) => name.startsWith('rml-') && name !== 'rml-document') ?? '?';
      const root = host.shadowRoot;
      if (!root) continue;
      const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
      for (let node = walker.nextNode() as Text | null; node; node = walker.nextNode() as Text | null) {
        const text = node.data.trim();
        const parent = node.parentElement;
        if (!text || !parent || parent.closest('style')) continue;
        if (parent.getClientRects().length === 0 || getComputedStyle(parent).visibility === 'hidden') continue;
        let faded = false; // a faded-out text (the map name after its banner) isn't drawn
        for (let e: HTMLElement | null = parent; e && !faded; e = e.parentElement) faded = getComputedStyle(e).opacity === '0';
        if (faded) continue;
        const range = document.createRange();
        range.selectNodeContents(node);
        const textBox = range.getBoundingClientRect();
        if (textBox.width === 0 || textBox.height === 0) continue;
        // The box the text is drawn in: its nearest block (an inline span's box is its text).
        let box: HTMLElement | null = parent;
        while (box && getComputedStyle(box).display === 'inline') box = box.parentElement;
        // A sharp-text layer as wide as its text (auto width), or centred and wider than anything on
        // purpose, is drawn in its parent's box; one with a width of its own is its own box.
        if (box?.classList.contains('sharp-text')) {
          const own = box.getBoundingClientRect();
          if (box.classList.contains('sharp-centre') || Math.abs(own.width - textBox.width) < 1) box = box.parentElement;
        }
        if (!box) continue;
        const boxRect = box.getBoundingClientRect();
        if (boxRect.width <= 2) continue;
        const anchor = box.offsetHeight <= 2; // a line's anchor (in its own pixels): its text hangs below it by design
        // A scrolling list's rows run below it by design.
        if (box.closest('.scroll-pane') && textBox.top >= boxRect.bottom) continue;
        const right = Math.max(textBox.right - boxRect.right, boxRect.left - textBox.left);
        const below = anchor ? 0 : textBox.bottom - boxRect.bottom;
        if (right > 1.5 || below > 3) {
          found.push({ doc, box: `${box.tagName.toLowerCase()}${box.id ? `#${box.id}` : ''}${box.className ? `.${String(box.className).trim().split(/\s+/).join('.')}` : ''}`,
            text: text.slice(0, 70), right: Math.round(right), below: Math.round(below) });
        }
      }
    }
    return found;
  });
}

const all: Overflow[] = [];
async function check(page: Page, name: string): Promise<void> {
  await page.waitForTimeout(2_500);
  await page.screenshot({ path: `${SHOTS}/${WIDTH}x${HEIGHT}-${name}.png` });
  const list = await overflows(page);
  for (const o of list) console.log(`[${WIDTH}x${HEIGHT}] ${name}: ${o.doc} ${o.box} "${o.text}" right ${o.right} below ${o.below}`);
  all.push(...list.filter((o) => !all.some((a) => a.doc === o.doc && a.box === o.box && a.text === o.text)));
}

async function talkToNearest(page: Page, monster: number, cell?: { x: number; y: number }): Promise<void> {
  if (cell) await page.evaluate((c) => (window as unknown as Win).__game.online.walkTo(c), cell);
  const nearest = () => page.evaluate((number) => {
    const { online } = (window as unknown as Win).__game;
    const { x, y } = online.hero.cell;
    const npcs = [...online.characters.values()].filter((c) => c.monsterNumber === number);
    npcs.sort((a, b) => Math.hypot(a.cell.x - x, a.cell.y - y) - Math.hypot(b.cell.x - x, b.cell.y - y));
    return npcs[0]?.id ?? null;
  }, monster);
  await expect.poll(nearest, POLL).not.toBeNull();
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(id)); }, (await nearest())!);
}

test('the texts fit their boxes', async ({ page }) => {
  test.setTimeout(25 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, process.env.ACCOUNT ?? 'test8');
  await page.waitForSelector('.rml-main_frame', { state: 'attached', ...OPEN });
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(8_000);
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  await check(page, 'hud');

  // The cash shop, each category (the package names are long).
  await page.keyboard.press('x');
  if (await page.locator('.rml-in_game_shop #panel').waitFor({ state: 'visible', timeout: 90_000 }).then(() => true, () => false)) {
    await page.waitForTimeout(4_000);
    for (let category = 0; category < 7; category++) {
      const point = await page.evaluate((i) => {
        const e = document.querySelector('.rml-in_game_shop')?.shadowRoot?.querySelectorAll<HTMLElement>('.igs-category')[i];
        const r = e?.getBoundingClientRect();
        return r ? { x: r.left + r.width / 2, y: r.top + r.height / 2 } : null;
      }, category);
      if (!point) break;
      await page.mouse.click(point.x, point.y);
      const pages = () => page.evaluate(() => { const shop = (window as unknown as { __game: { cashShop: { page: number; totalPages: number } } }).__game.cashShop; return [shop.page, shop.totalPages]; });
      for (let i = 0; i < 4; i++) {
        await check(page, `in_game_shop-${category}-${i + 1}`);
        const [current, total] = await pages();
        if (current >= total) break;
        await page.evaluate(() => document.querySelector('.rml-in_game_shop')?.shadowRoot?.querySelector<HTMLElement>('#igs_page_next')?.dispatchEvent(new MouseEvent('click', { bubbles: true })));
        await expect.poll(async () => (await pages())[0], POLL).toBe(current + 1);
      }
    }
    await page.keyboard.press('Escape');
    await page.waitForTimeout(1_000);
  }

  // The windows a key opens.
  const windows: [string, string][] = [
    ['i', 'my_inventory'], ['c', 'character_info'], ['a', 'master_level'], ['t', 'my_quest_info'], ['o', 'option_window'],
    ['m', 'move_command'], ['p', 'party_info'], ['g', 'guild_info'], ['f', 'friend_shell'], ['z', 'mu_helper_config'], ['Tab', 'mini_map'],
  ];
  for (const [key, doc] of windows) {
    await page.keyboard.press(key);
    const opened = await page.locator(`.rml-${doc}`).first().waitFor({ state: 'attached', timeout: 20_000 }).then(() => true, () => false);
    if (opened) await check(page, doc);
    await page.keyboard.press(key);
    await page.waitForTimeout(1_000);
  }
  // The auto play window's other tabs.
  await page.keyboard.press('z');
  if (await page.locator('.rml-mu_helper_config').first().waitFor({ state: 'attached', timeout: 20_000 }).then(() => true, () => false)) {
    for (const tab of [1, 2]) {
      await page.locator(`.rml-mu_helper_config #tab_${tab}`).click({ force: true });
      await check(page, `mu_helper_config-tab${tab}`);
    }
  }
  await page.keyboard.press('z');

  // An item's tooltip in the bag.
  await page.keyboard.press('i');
  await page.locator('.rml-my_inventory').first().waitFor({ state: 'attached', ...OPEN });
  await page.waitForTimeout(2_000);
  const cell = await page.evaluate(() => {
    const host = document.querySelector('.rml-my_inventory');
    const grid = host?.shadowRoot?.querySelector('#item_grid');
    const r = grid?.getBoundingClientRect();
    return r ? { x: r.left + 10, y: r.top + 10 } : null;
  });
  if (cell) {
    await page.mouse.move(cell.x, cell.y, { steps: 4 });
    await check(page, 'tooltip');
  }
  await page.keyboard.press('i');

  // The NPC dialogue (Elf Soldier), the shop (Hanzo the Blacksmith).
  await page.waitForTimeout(1_000);
  await talkToNearest(page, 257).catch(() => console.log('no Elf Soldier in view'));
  if (await page.locator('.rml-npc_dialogue').first().waitFor({ state: 'attached', timeout: 20_000 }).then(() => true, () => false)) await check(page, 'npc_dialogue');
  await page.keyboard.press('Escape');
  await page.waitForTimeout(1_000);
  await talkToNearest(page, 251, { x: 118, y: 140 }).catch(() => console.log('no Hanzo in view'));
  if (await page.locator('.rml-npc_shop').first().waitFor({ state: 'attached', timeout: 20_000 }).then(() => true, () => false)) await check(page, 'npc_shop');
  await page.keyboard.press('Escape');

  console.log(`[${WIDTH}x${HEIGHT}] texts out of their boxes: ${all.length}`);
  await expectNoPageErrors(errors);
  if (process.env.STRICT_FIT) expect(all).toEqual([]);
});
