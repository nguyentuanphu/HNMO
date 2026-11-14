// The cash shop like on the desktop (CInGameShop): X in Lorencia's town asks the server and opens the shop with
// the sales zones, the categories and the packages of the script (Data/InGameShopScript) and the account's coins.
// When the coins allow, the cheapest package of the first zone that ends up in the inventory is bought (its
// dialog, Buy, the confirmation), it shows in the storage list, Use moves it to the inventory (the desktop's
// place for everything but seals and scrolls). test2 in Lorencia.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Package { seq: number; name: string; price: number; priceCount: number; salesFlag: number; itemCode: number; productSeqs: number[] }
interface CashGame {
  mapNumber: number;
  online: { say(text: string): void };
  inventory: { items: Map<number, { group: number; number: number }> };
  cashShop: {
    isOpen: boolean;
    coins: { wCoinC: number; wCoinP: number; goblinPoints: number };
    zoneIndex: number; categoryIndex: number; page: number;
    zones: { name: string }[];
    categories: { name: string }[];
    displayPackages: Package[];
    script: { categoriesOf(zone: unknown): { name: string }[]; packagesOf(category: unknown): Package[]; zones(): unknown[] } | null;
    storageItems: { name: string; itemCode: number }[];
    selectedRow: number;
  };
}

const game = <T>(page: Page, read: (g: CashGame) => T): Promise<T> => page.evaluate(`(${read.toString()})(window.__game)`);
const shopText = (page: Page, selector: string) => page.evaluate((s) => [...document.querySelector('.rml-in_game_shop')?.shadowRoot?.querySelectorAll(s) ?? []]
  .map((e) => e.textContent?.trim() ?? '').filter(Boolean), selector);

// Clicks an element of a document (the shop's buttons are theme sprites).
async function clickIn(page: Page, documentName: string, selector: string, index = 0): Promise<void> {
  const box = await page.evaluate(({ documentName, selector, index }) => {
    const e = document.querySelector(`.rml-${documentName}`)!.shadowRoot!.querySelectorAll<HTMLElement>(selector)[index];
    const r = e.getBoundingClientRect();
    return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
  }, { documentName, selector, index });
  await page.mouse.move(box.x, box.y, { steps: 4 });
  await page.mouse.click(box.x, box.y);
}

async function okMessage(page: Page, title: string): Promise<void> {
  await expect(page.locator('.message-dialog')).toBeVisible({ timeout: 60_000 });
  expect(await page.locator('.message-dialog .message-title').textContent()).toBe(title);
  await page.click('.message-dialog .confirm-ok');
}

test('opens the cash shop, buys the cheapest item and uses it', async ({ page }) => {
  test.setTimeout(10 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test2');
  if (await game(page, (g) => g.mapNumber) !== 0) {
    await game(page, (g) => g.online.say('/move lorencia'));
    await expect.poll(() => game(page, (g) => g.mapNumber), { timeout: 90_000 }).toBe(0);
    await page.waitForTimeout(3_000);
  }

  // X: the server allows it (a safe zone), the window opens with the zones, categories, packages and coins.
  await page.keyboard.press('x');
  await expect(page.locator('.rml-in_game_shop #panel')).toBeVisible({ timeout: 90_000 });
  await expect.poll(() => shopText(page, '.igs-zone-label'), { timeout: 30_000 }).toEqual(['W Coin (C)', 'W Coin (P)', 'Goblin Point']);
  expect(await shopText(page, '.igs-category-label')).toEqual(['Special', 'Accessory', 'Buff', 'Pet', 'Equipment', 'Ticket', 'Potion']);
  expect((await shopText(page, '.igs-package-name')).length).toBeGreaterThan(0);
  await expect.poll(() => shopText(page, '.igs-wallet-label')).toEqual(['My W Coin :', 'My W Coin(P) :', 'Goblin Points :']);
  const coins = await game(page, (g) => g.cashShop.coins.wCoinC);
  expect(await shopText(page, '.igs-wallet-value')).toContain(coins.toLocaleString('en-US'));
  // Another category shows its own packages.
  await clickIn(page, 'in_game_shop', '.igs-category', 6);
  await expect.poll(() => game(page, (g) => g.cashShop.categoryIndex)).toBe(6);
  await page.waitForTimeout(2_000);
  await page.screenshot({ path: 'test-results/cash-shop.png' });

  // The cheapest package of the first zone that goes to the inventory (one item, no seal, scroll, aura or charm).
  const cheapest = await game(page, (g) => {
    const shop = g.cashShop, script = shop.script!;
    const zone = script.zones()[0];
    let best: { category: number; index: number; pack: Package } | null = null;
    script.categoriesOf(zone).forEach((category, c) => {
      script.packagesOf(category).forEach((pack, index) => {
        if (pack.price <= 0 || pack.salesFlag !== 182 || pack.priceCount !== 1 || pack.productSeqs.length !== 1) return;
        if (!(pack.itemCode >> 9 === 13 || pack.itemCode >> 9 === 14) || /seal|scroll|aura|charm|horseshoe|statue|card|ticket|back|certificate|fruit/i.test(pack.name)) return;
        if (!best || pack.price < best.pack.price) best = { category: c, index, pack };
      });
    });
    return best as { category: number; index: number; pack: Package } | null;
  });
  expect(cheapest, 'the script has a package for the inventory').not.toBeNull();
  const affordable = coins >= cheapest!.pack.price;

  await clickIn(page, 'in_game_shop', '.igs-category', cheapest!.category);
  for (let i = 0; i < Math.floor(cheapest!.index / 9); i++) await clickIn(page, 'in_game_shop', '#igs_page_next');
  await expect.poll(() => game(page, (g) => g.cashShop.displayPackages.map((p) => p.seq))).toContain(cheapest!.pack.seq);
  const code = cheapest!.pack.itemCode;
  const countOf = () => page.evaluate((c) => [...(window as unknown as { __game: CashGame }).__game.inventory.items.entries()]
    .filter(([slot, i]) => slot >= 12 && i.group * 512 + i.number === c).length, code);
  const itemsBefore = await countOf();
  await clickIn(page, 'in_game_shop', '.igs-buy', cheapest!.index % 9);

  // The package's dialog, Buy, the confirmation, the server's answer.
  await expect(page.locator('.rml-igs_buy_package #panel')).toBeVisible({ timeout: 60_000 });
  await page.waitForTimeout(1_000);
  await page.screenshot({ path: 'test-results/cash-shop-buy.png' });
  await clickIn(page, 'igs_buy_package', '.igs-btn', 0);
  await expect(page.locator('.igs-confirm')).toBeVisible({ timeout: 30_000 });
  expect(await page.locator('.igs-confirm').textContent()).toContain(cheapest!.pack.name.trim());
  await page.click('.igs-confirm .confirm-ok');
  if (!affordable) {
    // Without the coins the server refuses it (CashShopItemBuyResult: NotEnoughCoins) and the coins stay.
    await okMessage(page, 'Purchase Failed');
    expect(await game(page, (g) => g.cashShop.coins.wCoinC)).toBe(coins);
    test.info().annotations.push({ type: 'note', description: `test2 has ${coins} W Coin (C); the cheapest item costs ${cheapest!.pack.price}: buying was refused, using not tried` });
    await page.keyboard.press('Escape');
    await expect(page.locator('.rml-in_game_shop')).toHaveCount(0, { timeout: 30_000 });
    await expectNoPageErrors(errors);
    return;
  }
  await okMessage(page, 'Purchase Completed');
  await expect.poll(() => game(page, (g) => g.cashShop.coins.wCoinC), { timeout: 30_000 }).toBe(coins - cheapest!.pack.price);

  // The storage list holds it; Use: its window, the question, and it's in the inventory.
  await expect.poll(() => game(page, (g) => g.cashShop.storageItems.some((i) => i.itemCode === code)), { timeout: 30_000 }).toBe(true);
  const index = await page.evaluate((c) => (window as unknown as { __game: CashGame }).__game.cashShop.storageItems.findIndex((i) => i.itemCode === c), code);
  await clickIn(page, 'in_game_shop', '.igs-row', index);
  await expect.poll(() => game(page, (g) => g.cashShop.selectedRow)).toBe(index);
  await clickIn(page, 'in_game_shop', '#igs_use');
  await expect(page.locator('.igs-confirm')).toBeVisible({ timeout: 30_000 });
  await page.click('.igs-confirm .confirm-ok');
  await expect(page.locator('.igs-confirm')).toBeVisible({ timeout: 30_000 });
  await page.click('.igs-confirm .confirm-ok');
  await okMessage(page, 'Item Used');
  await expect.poll(countOf, { timeout: 60_000 }).toBe(itemsBefore + 1);

  // Escape closes the shop.
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-in_game_shop')).toHaveCount(0, { timeout: 30_000 });
  await expectNoPageErrors(errors);
});
