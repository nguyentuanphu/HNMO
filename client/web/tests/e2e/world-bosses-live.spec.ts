// World bosses end to end on a running server: the admin panel's "World bosses and rankings" page adds a
// test boss (a goblin, which doesn't live in Lorencia, north of its town), saves it and presses "Spawn now"; the hero sees the spawn in the
// chat and on the screen line, kills it, and gets the kill notice, boss-hunt points and a reward. The
// test boss is removed again. Needs the game server (44406) and ADMIN_USER / ADMIN_PASSWORD; skips without.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';
import net from 'node:net';

const adminUrl = (process.env.ADMIN_URL ?? 'http://127.0.0.1:8095').replace(/\/$/, '');
const adminUser = process.env.ADMIN_USER ?? '';
const adminPassword = process.env.ADMIN_PASSWORD ?? '';
const BOSS_ID = 'e2e-test-boss';
const SPOT = { x: 165, y: 60 };


interface Fighter { id: number; kind: string; dead: boolean; name: string; cell: { x: number; y: number } }
interface LiveGame {
  online: { characters: Map<number, Fighter>; hero: { cell: { x: number; y: number }; isWalking: boolean }; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  combat: { attack(target: unknown): void };
  worldBosses: { state: { bosses: { id: string; alive?: { myDamage: number; myRank: number } }[]; huntPoints: number } | null };
  chat: { entries: { text: string }[] };
}
type Win = Window & { __game: LiveGame };

function portOpen(port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = net.connect(port, '127.0.0.1');
    socket.once('connect', () => { socket.destroy(); resolve(true); });
    socket.once('error', () => resolve(false));
    socket.setTimeout(2_000, () => { socket.destroy(); resolve(false); });
  });
}

/** Opens the admin page, signing in when asked (the form takes input once its circuit is up). */
async function openAdmin(page: Page): Promise<void> {
  const url = `${adminUrl}/world-bosses-rankings`;
  await page.goto(url, { waitUntil: 'networkidle' });
  await expect(async () => {
    if (await page.locator('#wb-save').count()) return;
    if (!await page.locator('#login-name').count()) await page.goto(url, { waitUntil: 'networkidle' });
    if (await page.locator('#login-name').count()) {
      await page.waitForTimeout(1_500);
      await page.locator('#login-name').fill(adminUser);
      await page.locator('#login-password').fill(adminPassword);
      await page.locator('button[type=submit]').first().click();
      await page.waitForTimeout(3_000);
      await page.goto(url, { waitUntil: 'networkidle' });
    }
    await expect(page.locator('#wb-save')).toBeVisible({ timeout: 15_000 });
  }).toPass({ timeout: 120_000 });
}

async function save(page: Page): Promise<void> {
  await page.click('#wb-save');
  await page.waitForTimeout(2_500);
  await expect(page.locator('#blazor-error-ui')).toBeHidden();
  await expect(page.locator('#wb-errors')).toHaveCount(0);
}

/** Adds the test boss (or finds it), saves, and spawns it now. */
async function addAndSpawn(page: Page): Promise<void> {
  await removeBosses(page);
  await openAdmin(page);
  await expect(page.locator('section.wb-boss').first()).toBeVisible({ timeout: 60_000 });
  {
    const before = await page.locator('section.wb-boss').count();
    await expect(async () => {
      if (await page.locator('section.wb-boss').count() === before) await page.click('#wb-add');
      await expect(page.locator('section.wb-boss')).toHaveCount(before + 1, { timeout: 3_000 });
    }).toPass({ timeout: 30_000 });
    const last = page.locator('section.wb-boss').last();
    let card = last;
    const fill = async (suffix: string, value: string) => {
      const input = card.locator(`[id$="-${suffix}"]`).first();
      await input.fill(value);
      await input.dispatchEvent('change');
    };
    // Right after "Add" the new card can still be redrawn: set the id until the card shows it.
    await expect(async () => {
      await page.waitForTimeout(1_000);
      await fill('id', BOSS_ID);
      await expect(page.locator(`section.wb-boss[data-boss="${BOSS_ID}"]`)).toHaveCount(1, { timeout: 3_000 });
    }).toPass({ timeout: 30_000 });
    card = page.locator(`section.wb-boss[data-boss="${BOSS_ID}"]`);
    await fill('en', 'Test Goblin Boss');
    await fill('vi', 'Yêu tinh thử');
    await fill('monster', '26');
    await fill('map', '0');
    await fill('spots', `${SPOT.x},${SPOT.y}`);
    await fill('despawn', '20');
    await save(page);
  }
  // Saved and in use: the public list has it.
  await expect.poll(async () => {
    const board = await (await page.request.get(`${adminUrl}/api/website/world-bosses`)).json() as { bosses: { id: string }[] };
    return board.bosses.some((b) => b.id === BOSS_ID);
  }, { timeout: 60_000 }).toBe(true);
  await openAdmin(page);
  const card = page.locator(`section.wb-boss[data-boss="${BOSS_ID}"]`);
  await expect(card).toHaveCount(1);
  // The button works once the page's circuit is up: press until the public list shows the boss alive.
  await expect(async () => {
    await page.waitForTimeout(2_000);
    await card.locator('.wb-spawn').click();
    await expect.poll(async () => {
      const board = await (await page.request.get(`${adminUrl}/api/website/world-bosses`)).json() as { bosses: { id: string; alive?: unknown }[] };
      return !!board.bosses.find((b) => b.id === BOSS_ID)?.alive;
    }, { timeout: 10_000 }).toBe(true);
  }).toPass({ timeout: 90_000 });
}

/** Removes the test boss (only the card with its id), reloading the page after each removal. */
async function removeBosses(page: Page): Promise<void> {
  for (let guard = 0; guard < 5; guard++) {
    await openAdmin(page);
    await expect(page.locator('section.wb-boss').first()).toBeVisible({ timeout: 60_000 });
    const card = page.locator(`section.wb-boss[data-boss="${BOSS_ID}"]`);
    if (!await card.count()) return;
    const before = await page.locator('section.wb-boss').count();
    await card.first().locator('button.btn-outline-danger').first().click();
    await expect(page.locator('section.wb-boss')).toHaveCount(before - 1);
    await save(page);
  }
}

test('a world boss set in the admin panel spawns, is killed in the game and pays', async ({ browser }) => {
  test.skip(!(await portOpen(44406)), 'No game server runs on 44406.');
  test.skip(!adminUser || !adminPassword, 'ADMIN_USER / ADMIN_PASSWORD not set.');
  test.setTimeout(15 * 60_000);
  const admin = await browser.newPage();
  const page = await browser.newPage();
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  try {
    await enterWorld(page, process.env.BOSS_ACCOUNT ?? 'test4');
    await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
    await page.waitForTimeout(8_000);
    for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 85 }, { x: 160, y: 70 }, { x: SPOT.x, y: SPOT.y + 3 }]) {
      await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
      await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
    }
    await addAndSpawn(admin);
    const chat = () => page.evaluate(() => (window as unknown as Win).__game.chat.entries.map((e) => e.text).join('\n'));
    // The spawn is told in the chat, and the board has it alive.
    await expect.poll(chat, { timeout: 60_000 }).toMatch(/Test Goblin Boss/);
    await expect.poll(() => page.evaluate((id) => !!(window as unknown as Win).__game.worldBosses.state?.bosses.find((b) => b.id === id)?.alive, BOSS_ID), { timeout: 60_000 }).toBe(true);
    await page.screenshot({ path: 'test-results/world-boss-live-spawn.png' });
    // The boss is the only goblin here.
    const bossId = () => page.evaluate(() => {
      const g = (window as unknown as Win).__game;
      for (const c of g.online.characters.values()) if (c.kind === 'monster' && !c.dead && /goblin|yêu tinh/i.test(c.name)) return c.id;
      return null;
    });
    await expect.poll(bossId, { timeout: 60_000 }).not.toBeNull();
    await expect(async () => {
      const id = await bossId();
      if (id !== null) await page.evaluate((target) => { const g = (window as unknown as Win).__game; g.combat.attack(g.online.characters.get(target)); }, id);
      await expect.poll(() => page.evaluate((boss) => !(window as unknown as Win).__game.worldBosses.state?.bosses.find((b) => b.id === boss)?.alive, BOSS_ID), { timeout: 60_000 }).toBe(true);
    }).toPass({ timeout: 8 * 60_000 });
    // Killed: the notice, the hero's boss-hunt points, a reward line.
    const text = await chat();
    console.log(text.split('\n').slice(-8).join('\n'));
    expect(text).toMatch(/Test Goblin Boss/);
    // Each announcement once (the golden notice; no second line of the web game's own).
    expect(text.match(/Test Goblin Boss was defeated/g) ?? []).toHaveLength(1);
    await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.worldBosses.state?.huntPoints ?? 0), { timeout: 60_000 }).toBeGreaterThan(0);
    await page.keyboard.press('n');
    await page.waitForTimeout(3_000);
    await page.screenshot({ path: 'test-results/world-boss-live-killed.png' });
    await expectNoPageErrors(errors);
  } finally {
    await removeBosses(admin).catch((error: unknown) => console.warn('Remove:', error));
  }
});
