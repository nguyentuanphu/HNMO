// The personal store between two players, like on the desktop (CMyShopInventory, CPurchaseShopInventory):
// player A opens the inventory, S opens the store next to it, a bag item put on the store asks its zen price,
// the store gets a name and opens (the question first); B sees the store's title over A's head, opens it with
// /purchase <name>, clicks the item and buys it after the question: the item moves to B's bag and the zen to
// A. A's store closes by itself once it's empty. Two browsers, test5 and test6 in Lorencia (as trade.spec.ts).
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface ShopItem { group: number; number: number; width: number; height: number }
interface ShopGame {
  hero: { name: string };
  mapNumber: number;
  stats: { level: number };
  online: { playerId: number; characters: Map<number, { id: number; name: string; kind: string }>; say(text: string): void };
  inventory: { items: Map<number, ShopItem>; money: number; pendingMove: unknown; move(fs: number, f: number, ts: number, t: number): void; firstEmptySlot(s: Map<number, unknown>, g: unknown, i: unknown): number | null };
  personalShop: {
    isOpen: boolean; title: string; seller: { id: number; name: string } | null;
    purchase: { stock: Map<number, ShopItem> };
    prices: Map<number, { amount: number }>;
    hasStore(id: number): boolean;
    command(text: string, facing: () => null): boolean;
    closeStore(): void;
  };
}

const game = <T>(page: Page, read: (g: ShopGame) => T): Promise<T> => page.evaluate(`(${read.toString()})(window.__game)`);
const bagItems = (page: Page) => game(page, (g) => [...g.inventory.items.entries()].filter(([slot]) => slot >= 12 && slot < 76)
  .map(([slot, item]) => ({ slot, group: item.group, number: item.number, width: item.width, height: item.height })));
const storeSlots = (page: Page) => game(page, (g) => [...g.inventory.items.keys()].filter((slot) => slot >= 204 && slot < 236));

// The screen point of a grid cell of a window (#item_grid), at an item's centre.
async function gridPoint(page: Page, documentName: string, index: number, size: { width: number; height: number }) {
  return page.evaluate(({ documentName, index, size }) => {
    const panel = document.querySelector(`.rml-${documentName}`)!.shadowRoot!.querySelector<HTMLElement>('#panel')!;
    const grid = panel.querySelector<HTMLElement>('#item_grid')!;
    const rect = panel.getBoundingClientRect();
    const scale = rect.width / panel.offsetWidth;
    const x = grid.offsetLeft + ((index % 8) + size.width / 2) * 20, y = grid.offsetTop + (Math.floor(index / 8) + size.height / 2) * 20;
    return { x: rect.left + x * scale, y: rect.top + y * scale };
  }, { documentName, index, size });
}

async function clickPoint(page: Page, point: { x: number; y: number }, button: 'left' | 'right' = 'left') {
  await page.mouse.move(point.x, point.y, { steps: 4 });
  await page.mouse.click(point.x, point.y, { button });
}

async function clickElement(page: Page, documentName: string, id: string): Promise<void> {
  const box = await page.evaluate(({ documentName, id }) => {
    const r = document.querySelector(`.rml-${documentName}`)!.shadowRoot!.querySelector<HTMLElement>(`#${id}`)!.getBoundingClientRect();
    return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
  }, { documentName, id });
  await clickPoint(page, box);
}

// Answers the in-game questions that come (the price under the item's value, …) with OK.
async function answerYes(page: Page, times = 1): Promise<void> {
  for (let i = 0; i < times; i++) {
    await expect(page.locator('.confirm-dialog:not(.ask-dialog)')).toBeVisible({ timeout: 30_000 });
    await page.click('.confirm-dialog:not(.ask-dialog) .confirm-ok');
  }
}

test('a player opens a store and another player buys from it', async ({ browser }) => {
  test.setTimeout(10 * 60_000);
  const errors: string[] = [];
  const contextA = await browser.newContext(), contextB = await browser.newContext();
  const a = await contextA.newPage(), b = await contextB.newPage();
  for (const page of [a, b]) page.on('pageerror', (error) => errors.push(error.message));
  await Promise.all([enterWorld(a, 'test5'), enterWorld(b, 'test6')]);
  for (const page of [a, b]) {
    if (await game(page, (g) => g.mapNumber) !== 0) {
      await game(page, (g) => g.online.say('/move lorencia'));
      await expect.poll(() => game(page, (g) => g.mapNumber), { timeout: 90_000 }).toBe(0);
    }
  }
  expect(await game(a, (g) => g.stats.level), 'test5 needs level 6 for a store').toBeGreaterThanOrEqual(6);

  // A known start: nothing left in A's store slots from an earlier run.
  await a.waitForTimeout(5_000); // a store left open is opened again on entering (its list comes after)
  if (await game(a, (g) => g.personalShop.isOpen)) {
    await game(a, (g) => g.personalShop.closeStore());
    await expect.poll(() => game(a, (g) => g.personalShop.isOpen), { timeout: 30_000 }).toBe(false);
  }
  for (const slot of await storeSlots(a)) {
    await a.evaluate((s) => {
      const g = (window as unknown as { __game: ShopGame }).__game;
      const target = g.inventory.firstEmptySlot(g.inventory.items, { firstSlot: 12, columns: 8, rows: 8 }, g.inventory.items.get(s));
      if (target !== null) g.inventory.move(4, s, 0, target);
    }, slot);
    await expect.poll(() => storeSlots(a), { timeout: 30_000 }).not.toContain(slot);
  }

  // A: the inventory (I), then S opens the store next to it.
  await a.keyboard.press('i');
  await expect(a.locator('.rml-my_inventory #panel')).toBeVisible({ timeout: 90_000 });
  await a.keyboard.press('s');
  await expect(a.locator('.rml-my_shop #panel')).toBeVisible({ timeout: 90_000 });

  // A 1 x 1 bag item onto the store's first cell: the price dialog, 100 zen (and the question when that's under its value).
  const offered = (await bagItems(a)).find((item) => item.width === 1 && item.height === 1);
  expect(offered, 'test5 needs a 1 x 1 item in the bag').toBeTruthy();
  await clickPoint(a, await gridPoint(a, 'my_inventory', offered!.slot - 12, offered!));
  await clickPoint(a, await gridPoint(a, 'my_shop', 0, offered!));
  await expect(a.locator('.ask-dialog')).toBeVisible({ timeout: 30_000 });
  expect(await a.locator('.ask-dialog').textContent()).toContain('Enter selling price.');
  const price = 100;
  await a.fill('.ask-dialog .ask-input', String(price));
  await a.click('.ask-dialog .confirm-ok');
  if (await a.locator('.confirm-dialog:not(.ask-dialog)').isVisible({ timeout: 5_000 }).catch(() => false)) await answerYes(a);
  await expect.poll(() => storeSlots(a), { timeout: 60_000 }).toEqual([204]);
  expect(await game(a, (g) => g.personalShop.prices.get(204)?.amount)).toBe(price);

  // The name, then Open: the question, the store opens and the windows close.
  await a.locator('.rml-my_shop #shop_title').fill('HNMO test store');
  await a.screenshot({ path: 'test-results/personal-shop-own.png' });
  await clickElement(a, 'my_shop', 'btn_open');
  await answerYes(a);
  await expect.poll(() => game(a, (g) => g.personalShop.isOpen), { timeout: 60_000 }).toBe(true);
  await expect(a.locator('.rml-my_shop')).toHaveCount(0);

  // B sees the store's title over A's head, then opens the store with /purchase <name>.
  const nameA = await game(a, (g) => g.hero.name);
  // B walks over to A (they may stand at different places of the town).
  const cellA = await game(a, (g) => ({ ...(g.hero as unknown as { cell: { x: number; y: number } }).cell }));
  await b.evaluate((cell) => (window as unknown as { __game: { online: { walkTo(c: { x: number; y: number }): boolean } } }).__game.online.walkTo({ x: cell.x + 2, y: cell.y }), cellA);
  const idOfA = () => b.evaluate((name) => [...(window as unknown as { __game: ShopGame }).__game.online.characters.values()].find((c) => c.name === name)?.id ?? null, nameA);
  await expect.poll(idOfA, { timeout: 90_000 }).not.toBeNull();
  const idA = (await idOfA())!;
  await expect.poll(() => b.evaluate((id) => (window as unknown as { __game: ShopGame }).__game.personalShop.hasStore(id), idA), { timeout: 60_000 }).toBe(true);
  await expect(b.locator(`.shop-title[data-id="${idA}"]`)).toBeVisible({ timeout: 60_000 });
  expect(await b.locator(`.shop-title[data-id="${idA}"]`).getAttribute('title')).toBe('HNMO test store');
  await b.evaluate((name) => (window as unknown as { __game: ShopGame }).__game.personalShop.command(`/purchase ${name}`, () => null), nameA);
  await expect(b.locator('.rml-purchase_shop #panel')).toBeVisible({ timeout: 90_000 });
  await expect(b.locator('.rml-my_inventory #panel')).toBeVisible();
  await expect.poll(() => game(b, (g) => g.personalShop.purchase.stock.size), { timeout: 30_000 }).toBe(1);
  await b.waitForTimeout(2_000);
  await b.screenshot({ path: 'test-results/personal-shop-purchase.png' });

  // B clicks the item: the question, then the item comes to B's bag; the zen goes from B to A.
  const moneyA = await game(a, (g) => g.inventory.money), moneyB = await game(b, (g) => g.inventory.money);
  const countB = (await bagItems(b)).filter((i) => i.group === offered!.group && i.number === offered!.number).length;
  await clickPoint(b, await gridPoint(b, 'purchase_shop', 0, offered!));
  await answerYes(b);
  await expect.poll(async () => (await bagItems(b)).filter((i) => i.group === offered!.group && i.number === offered!.number).length, { timeout: 60_000 }).toBe(countB + 1);
  await expect.poll(() => game(b, (g) => g.inventory.money), { timeout: 30_000 }).toBe(moneyB - price);
  await expect.poll(() => game(a, (g) => g.inventory.money), { timeout: 30_000 }).toBeGreaterThan(moneyA);
  await expect.poll(() => storeSlots(a), { timeout: 30_000 }).toEqual([]);
  // The store was empty: it closed (A's title is gone, B's window too).
  await expect.poll(() => game(a, (g) => g.personalShop.isOpen), { timeout: 30_000 }).toBe(false);
  await expect.poll(() => b.evaluate((id) => (window as unknown as { __game: ShopGame }).__game.personalShop.hasStore(id), idA), { timeout: 30_000 }).toBe(false);
  await b.keyboard.press('Escape');
  await expect(b.locator('.rml-purchase_shop')).toHaveCount(0, { timeout: 30_000 });
  await expectNoPageErrors(errors);
  await contextA.close();
  await contextB.close();
});
