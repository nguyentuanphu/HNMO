// Trading between two players, like on the desktop (CTrade): player A asks player B to trade, B
// accepts, both see the trade window; A puts a bag item in (a right click on it), both press OK,
// and the item arrives in B's inventory. Two browsers, two test accounts standing in Lorencia.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface TradeItem { group: number; number: number; width: number; height: number }
interface TradeGame {
  hero: { name: string };
  online: { playerId: number; characters: Map<number, { id: number; name: string; kind: string }>; say(text: string): void };
  inventory: { items: Map<number, TradeItem>; trade: Map<number, TradeItem> };
  trade: {
    isOpen: boolean;
    requestTrade(playerId: number): void;
    view: { partner: Map<number, TradeItem>; myConfirmed: boolean; partnerConfirmed: boolean; partnerName: string };
  };
}
type Win = Window & { __game: TradeGame };

// Reads the game's state in the page: the reader runs there with window.__game.
const game = <T>(page: Page, read: (g: TradeGame) => T): Promise<T> => page.evaluate(`(${read.toString()})(window.__game)`);

const tradeOpen = (page: Page) => game(page, (g) => g.trade.isOpen);
const bagItems = (page: Page) => game(page, (g) => [...g.inventory.items.entries()].filter(([slot]) => slot >= 12)
  .map(([slot, item]) => ({ slot, group: item.group, number: item.number, width: item.width, height: item.height })));

// The screen point of a cell of a window's grid (#item_grid / #partner_grid), at the item's centre.
async function gridPoint(page: Page, documentName: string, gridId: string, index: number, size: { width: number; height: number }) {
  return page.evaluate(({ documentName, gridId, index, size }) => {
    const panel = document.querySelector(`.rml-${documentName}`)!.shadowRoot!.querySelector<HTMLElement>('#panel')!;
    const grid = panel.querySelector<HTMLElement>(`#${gridId}`)!;
    const rect = panel.getBoundingClientRect();
    const scale = rect.width / panel.offsetWidth;
    const x = grid.offsetLeft + ((index % 8) + size.width / 2) * 20, y = grid.offsetTop + (Math.floor(index / 8) + size.height / 2) * 20;
    return { x: rect.left + x * scale, y: rect.top + y * scale };
  }, { documentName, gridId, index, size });
}

async function clickElement(page: Page, documentName: string, id: string): Promise<void> {
  const box = await page.evaluate(({ documentName, id }) => {
    const r = document.querySelector(`.rml-${documentName}`)!.shadowRoot!.querySelector<HTMLElement>(`#${id}`)!.getBoundingClientRect();
    return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
  }, { documentName, id });
  await page.mouse.move(box.x, box.y, { steps: 4 });
  await page.mouse.click(box.x, box.y);
}

test('two players trade an item', async ({ browser }) => {
  test.setTimeout(8 * 60_000);
  const errors: string[] = [];
  const contextA = await browser.newContext(), contextB = await browser.newContext();
  const a = await contextA.newPage(), b = await contextB.newPage();
  for (const page of [a, b]) page.on('pageerror', (error) => errors.push(error.message));
  // Other questions (the zen prompt) answered yes.
  b.on('dialog', (dialog) => { void dialog.accept(); });
  a.on('dialog', (dialog) => { void dialog.accept(); });
  await Promise.all([enterWorld(a, 'test5'), enterWorld(b, 'test6')]);

  // A sees B (both start in Lorencia's town).
  const nameB = await game(b, (g) => g.hero.name);
  const idOfB = () => a.evaluate((name) => [...(window as unknown as Win).__game.online.characters.values()].find((c) => c.name === name)?.id ?? null, nameB);
  await expect.poll(idOfB, { timeout: 90_000 }).not.toBeNull();

  // A asks, B accepts: both trade windows open (with the inventory), each naming the other.
  await a.evaluate((id) => (window as unknown as Win).__game.trade.requestTrade(id), (await idOfB())!);
  await b.click('.confirm-dialog .confirm-ok', { timeout: 60_000 }); // the in-game question
  await expect.poll(() => tradeOpen(a), { timeout: 30_000 }).toBe(true);
  await expect.poll(() => tradeOpen(b), { timeout: 30_000 }).toBe(true);
  await expect(a.locator('.rml-trade #panel')).toBeVisible({ timeout: 90_000 });
  await expect(b.locator('.rml-trade #panel')).toBeVisible({ timeout: 90_000 });
  await expect(a.locator('.rml-my_inventory #panel')).toBeVisible();
  expect(await game(b, (g) => g.trade.view.partnerName)).toBe(await game(a, (g) => g.hero.name));

  // A right-clicks a 1 x 1 bag item: it goes into A's trade grid and shows on B's partner grid.
  const itemsA = await bagItems(a);
  const offered = itemsA.find((item) => item.width === 1 && item.height === 1) ?? itemsA[0];
  expect(offered, 'test5 needs an item in the bag').toBeTruthy();
  const bagPoint = await gridPoint(a, 'my_inventory', 'item_grid', offered.slot - 12, offered);
  await a.mouse.move(bagPoint.x, bagPoint.y, { steps: 4 });
  await a.mouse.click(bagPoint.x, bagPoint.y, { button: 'right' });
  await expect.poll(() => game(a, (g) => g.inventory.trade.size), { timeout: 30_000 }).toBe(1);
  await expect.poll(() => game(b, (g) => g.trade.view.partner.size), { timeout: 30_000 }).toBe(1);
  const shown = await game(b, (g) => [...g.trade.view.partner.values()].map((item) => ({ group: item.group, number: item.number }))[0]);
  expect(shown).toEqual({ group: offered.group, number: offered.number });
  await a.waitForTimeout(2_000);
  await b.screenshot({ path: 'test-results/trade-partner.png' });

  // Both press OK: B first, A sees B's mark; then A, and the trade completes.
  const before = (await bagItems(b)).filter((item) => item.group === offered.group && item.number === offered.number).length;
  await clickElement(b, 'trade', 'my_confirm');
  await expect.poll(() => game(a, (g) => g.trade.view.partnerConfirmed), { timeout: 30_000 }).toBe(true);
  await a.screenshot({ path: 'test-results/trade-ok.png' });
  await clickElement(a, 'trade', 'my_confirm');

  // The windows close and the item is in B's bag, not in A's any more.
  await expect.poll(() => tradeOpen(a), { timeout: 30_000 }).toBe(false);
  await expect.poll(() => tradeOpen(b), { timeout: 30_000 }).toBe(false);
  await expect.poll(async () => (await bagItems(b)).filter((item) => item.group === offered.group && item.number === offered.number).length, { timeout: 30_000 })
    .toBe(before + 1);
  await expect.poll(async () => (await bagItems(a)).some((item) => item.slot === offered.slot && item.group === offered.group && item.number === offered.number))
    .toBe(false);
  await expectNoPageErrors(errors);
  await contextA.close();
  await contextB.close();
});
