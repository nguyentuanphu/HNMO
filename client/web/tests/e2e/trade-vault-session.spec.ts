// Real-game checks of the trade and vault zen boxes being bound to their trade, offer and vault page (#410, #414):
// - an OK on the item-change warning of a trade that has ended does nothing in the next trade with the same partner;
// - a vault zen box left open while the vault page changes closes, and no zen moves on either page or in the bag.
// Two browsers (test5, test6) stand in Lorencia for the trade; test300 uses the Lorencia vault. Needs the game server
// (BASE_URL); zen and items are compared before and after on both sides.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, dismissOnboarding } from './helpers.js';

type G = any; // eslint-disable-line @typescript-eslint/no-explicit-any
const g = <T>(page: Page, f: string) => page.evaluate(`(() => { const g = window.__game; return (${f}); })()`) as Promise<T>;
const state = (page: Page) => g<{ money: number; bag: number; vault: number }>(page, '({ money: g.inventory.money, bag: g.inventory.items.size, vault: g.inventory.vaultMoney })');

test('trade: OK on the item-change warning of an ended trade does nothing in the next trade', async ({ browser }) => {
  test.setTimeout(8 * 60_000);
  const errors: string[] = [];
  const ca = await browser.newContext(), cb = await browser.newContext();
  const a = await ca.newPage(), b = await cb.newPage();
  for (const p of [a, b]) p.on('pageerror', (e) => errors.push(e.message));
  await Promise.all([enterWorld(a, 'test5'), enterWorld(b, 'test6')]);
  await Promise.all([dismissOnboarding(a), dismissOnboarding(b)]);
  const nameB = await g<string>(b, 'g.hero.name');
  const idOfB = () => a.evaluate((n) => [...(window as G).__game.online.characters.values()].find((c: G) => c.name === n)?.id ?? null, nameB);
  await expect(async () => {
    const at = await g<{ x: number; y: number }>(a, '({ ...g.online.hero.cell })');
    await b.evaluate((cell) => { const o = (window as G).__game.online; const n = o.cellNextTo(cell); if (n) o.walkTo(n); }, at);
    await expect.poll(idOfB, { timeout: 30_000 }).not.toBeNull();
  }).toPass({ timeout: 120_000 });
  const before = { a: await state(a), b: await state(b) };
  console.log('TRADE before', JSON.stringify(before));

  // Trade 1: A asks, B accepts.
  await a.evaluate((id) => (window as G).__game.trade.requestTrade(id), (await idOfB())!);
  await b.click('.confirm-dialog .confirm-ok', { timeout: 60_000 });
  await expect.poll(() => g<boolean>(a, 'g.trade.isOpen'), { timeout: 30_000 }).toBe(true);
  await expect.poll(() => g<boolean>(a, 'performance.now() >= g.itemWindows.trade.view.waitUntil'), { timeout: 30_000 }).toBe(true);

  // A presses OK while the server's item-change flag is set (set here: it needs a partner's item to change in level
  // or options): the warning box opens and stays open.
  await a.evaluate(() => { const t = (window as G).__game.trade; t.alert = true; void t.confirmButton(); });
  const warning = a.locator('.confirm-dialog').filter({ hasText: 'levels and options' });
  await expect(warning).toBeVisible({ timeout: 30_000 });

  // B ends trade 1; A asks again and B accepts: trade 2 is open on A under the old warning.
  await b.evaluate(() => (window as G).__game.trade.cancel());
  await expect.poll(() => g<boolean>(a, 'g.trade.isOpen'), { timeout: 30_000 }).toBe(false);
  await a.evaluate((id) => (window as G).__game.trade.requestTrade(id), (await idOfB())!);
  await b.click('.confirm-dialog .confirm-ok', { timeout: 60_000 });
  await expect.poll(() => g<boolean>(a, 'g.trade.isOpen'), { timeout: 30_000 }).toBe(true);
  await expect(warning).toBeVisible();

  // OK on the old warning: trade 2 must stay unconfirmed on both sides.
  await warning.locator('.confirm-ok').click({ timeout: 30_000 });
  await a.waitForTimeout(3_000);
  const confirmed = { a: await g<boolean>(a, 'g.itemWindows.trade.view.myConfirmed'), bSeesA: await g<boolean>(b, 'g.itemWindows.trade.view.partnerConfirmed') };
  console.log('TRADE after old OK', JSON.stringify(confirmed));
  expect(confirmed.a).toBe(false);
  expect(confirmed.bSeesA).toBe(false);

  await b.evaluate(() => (window as G).__game.trade.cancel());
  await expect.poll(() => g<boolean>(a, 'g.trade.isOpen'), { timeout: 30_000 }).toBe(false);
  await a.waitForTimeout(2_000);
  const after = { a: await state(a), b: await state(b) };
  console.log('TRADE after', JSON.stringify(after));
  expect(after.a.money).toBe(before.a.money);
  expect(after.b.money).toBe(before.b.money);
  expect(after.a.bag).toBe(before.a.bag);
  expect(after.b.bag).toBe(before.b.bag);
  expect(errors).toEqual([]);
  await ca.close(); await cb.close();
});

async function openVault(page: Page): Promise<void> {
  await page.evaluate(() => (window as G).__game.online.say('/move lorencia'));
  await expect.poll(() => g<number>(page, 'g.mapNumber'), { timeout: 60_000 }).toBe(0);
  await expect.poll(() => g<boolean>(page, '!g.online.changingMap'), { timeout: 60_000 }).toBe(true);
  await expect.poll(() => g<boolean>(page, 'g.online.walkTo({ x: 144, y: 113 })'), { timeout: 60_000 }).toBe(true);
  await expect.poll(() => page.evaluate(() => {
    const game = (window as G).__game;
    const keeper = [...game.online.characters.values()].find((c: G) => /Safety Guardian|Vault Keeper|Storage/i.test(c.name));
    if (keeper && !game.online.hero.isWalking) { game.online.talkTo(keeper); return 'talked'; }
    return [...game.online.characters.values()].filter((c: G) => c.kind === 'npc').map((c: G) => c.name).join(',');
  }), { timeout: 120_000 }).toBe('talked');
  await expect(page.locator('.hnmo-vault-pages')).toBeVisible({ timeout: 120_000 });
}

async function deposit(page: Page, amount: number): Promise<void> {
  const vault = await g<number>(page, 'g.inventory.vaultMoney');
  await page.evaluate(() => { void (window as G).__game.itemWindows.askMoney(true); });
  const box = page.locator('.vault-zen-dialog');
  await expect(box).toBeVisible({ timeout: 30_000 });
  await box.locator('#gcd_input').fill(String(amount));
  await box.locator('.confirm-ok').click();
  await expect.poll(() => g<number>(page, 'g.inventory.vaultMoney'), { timeout: 30_000 }).toBe(vault + amount);
}

async function toPage(page: Page, index: number): Promise<void> {
  const button = page.locator(`.hnmo-vault-pages .hnmo-vault-page[data-page="${index}"]`);
  if (await button.getAttribute('aria-pressed') === 'true') return; // the shown page's button is disabled
  await button.click();
  await expect(button).toHaveAttribute('aria-pressed', 'true', { timeout: 30_000 });
  await page.waitForTimeout(1_500);
}

test('vault: a zen box left open while the page changes closes, and no zen moves', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await enterWorld(page, 'test300');
  await dismissOnboarding(page);
  await openVault(page);
  await toPage(page, 0);
  await deposit(page, 1_000);
  await toPage(page, 1);
  await deposit(page, 500);
  const pages = { p0: 0, p1: await g<number>(page, 'g.inventory.vaultMoney') };
  await toPage(page, 0);
  pages.p0 = await g<number>(page, 'g.inventory.vaultMoney');
  const bag = await g<number>(page, 'g.inventory.money');
  console.log('VAULT pages', JSON.stringify(pages), 'bag', bag);
  expect(pages.p0).not.toBe(pages.p1);

  // A deposit box on page 1 (index 0), the amount typed but not answered; then page 2 (index 1).
  await page.evaluate(() => { void (window as G).__game.itemWindows.askMoney(true); });
  const box = page.locator('.vault-zen-dialog');
  await expect(box).toBeVisible({ timeout: 30_000 });
  await box.locator('#gcd_input').fill('300');
  await toPage(page, 1);
  await expect(box, 'the box closes with the page change').toHaveCount(0, { timeout: 15_000 });
  await page.waitForTimeout(2_000);
  const onP1 = await g<number>(page, 'g.inventory.vaultMoney');
  await toPage(page, 0);
  const onP0 = await g<number>(page, 'g.inventory.vaultMoney');
  const bagAfter = await g<number>(page, 'g.inventory.money');
  console.log('VAULT after', JSON.stringify({ p0: onP0, p1: onP1, bag: bagAfter }));
  expect(onP0).toBe(pages.p0);
  expect(onP1).toBe(pages.p1);
  expect(bagAfter).toBe(bag);
  expect(errors).toEqual([]);
});
