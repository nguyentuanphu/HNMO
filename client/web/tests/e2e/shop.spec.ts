// NPC shops like on the desktop: talking to a merchant opens its window with its items next to
// the inventory; a click buys an item into the bag, a right click on a bag item sells it, and a
// repair shop shows its repair buttons.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Npc { id: number; kind: string; monsterNumber: number | null; cell: { x: number; y: number } }
interface ShopGame {
  online: { characters: Map<number, Npc>; hero: { isWalking: boolean }; walkTo(cell: { x: number; y: number }): boolean; talkTo(npc: unknown): void; say(text: string): void };
  inventory: { items: Map<number, { group: number; number: number }>; shop: Map<number, { group: number; number: number; width: number; height: number }>; money: number; openWindow: number | null };
}
type Win = Window & { __game: ShopGame };

const HANZO = 251; // Hanzo the Blacksmith, Lorencia (116, 141): sells weapons and repairs

const bagCount = (page: Page) => page.evaluate(() => [...(window as unknown as Win).__game.inventory.items.keys()].filter((slot) => slot >= 12).length);
const money = (page: Page) => page.evaluate(() => (window as unknown as Win).__game.inventory.money);

test('buys from and sells to a merchant', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test1'); // ten million zen
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(6_000);
  await page.evaluate(() => (window as unknown as Win).__game.online.walkTo({ x: 118, y: 140 }));
  const hanzo = () => page.evaluate((number) => [...(window as unknown as Win).__game.online.characters.values()].find((c) => c.monsterNumber === number)?.id ?? null, HANZO);
  await expect.poll(hanzo, { timeout: 90_000 }).not.toBeNull();
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(id)); }, (await hanzo())!);
  // The merchant's window opens next to the inventory, with its items and the repair buttons.
  const shop = page.locator('.rml-npc_shop #panel');
  await expect(shop).toBeVisible({ timeout: 60_000 });
  await expect(page.locator('.rml-my_inventory #panel')).toBeVisible();
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.inventory.shop.size)).toBeGreaterThan(0);
  await expect(page.locator('.rml-npc_shop #btn_repair')).toBeVisible();
  // Its items are drawn (their 3D models in the cells).
  await expect.poll(() => page.evaluate(() => document.querySelector('.rml-npc_shop')!.shadowRoot!.querySelectorAll('#panel canvas').length), { timeout: 30_000 }).toBeGreaterThan(0);
  await page.waitForTimeout(3_000);
  await page.screenshot({ path: 'test-results/shop.png' });

  // A click on the first item buys it: one more item in the bag, less money.
  const slots = () => page.evaluate(() => [...(window as unknown as Win).__game.inventory.items.keys()].filter((slot) => slot >= 12));
  const slotsBefore = await slots();
  const before = { count: slotsBefore.length, money: await money(page) };
  const first = await page.evaluate(() => {
    const shop = (window as unknown as Win).__game.inventory.shop;
    const slot = Math.min(...shop.keys());
    const item = shop.get(slot)!;
    return { slot, width: item.width, height: item.height };
  });
  const point = await page.evaluate(({ slot, width, height }) => {
    const panel = document.querySelector('.rml-npc_shop')!.shadowRoot!.querySelector<HTMLElement>('#panel')!;
    const grid = panel.querySelector<HTMLElement>('#item_grid')!;
    const rect = panel.getBoundingClientRect();
    const scale = rect.width / panel.offsetWidth;
    const x = grid.offsetLeft + ((slot % 8) + width / 2) * 20, y = grid.offsetTop + (Math.floor(slot / 8) + height / 2) * 20;
    return { x: rect.left + x * scale, y: rect.top + y * scale };
  }, first);
  await page.mouse.move(point.x, point.y, { steps: 4 });
  await page.mouse.click(point.x, point.y);
  await expect.poll(() => bagCount(page), { timeout: 30_000 }).toBe(before.count + 1);
  await expect.poll(() => money(page)).toBeLessThan(before.money);

  // A right click on the bought item sells it back: the bag holds one less, the money goes up.
  const bought = (await slots()).find((slot) => !slotsBefore.includes(slot))!;
  const afterBuy = await money(page);
  const sold = await page.evaluate((slot) => {
    const panel = document.querySelector('.rml-my_inventory')!.shadowRoot!.querySelector<HTMLElement>('#panel')!;
    const grid = panel.querySelector<HTMLElement>('#item_grid')!;
    const rect = panel.getBoundingClientRect();
    const scale = rect.width / panel.offsetWidth;
    const index = slot - 12;
    return { x: rect.left + (grid.offsetLeft + (index % 8) * 20 + 10) * scale, y: rect.top + (grid.offsetTop + Math.floor(index / 8) * 20 + 10) * scale };
  }, bought);
  await page.mouse.move(sold.x, sold.y, { steps: 4 });
  await page.mouse.click(sold.x, sold.y, { button: 'right' });
  await expect.poll(() => bagCount(page), { timeout: 30_000 }).toBe(before.count);
  await expect.poll(() => money(page)).toBeGreaterThan(afterBuy);

  // Escape closes the shop (CloseNpcRequest).
  await page.keyboard.press('Escape');
  await expect(shop).toBeHidden();
  await expectNoPageErrors(errors);
});
