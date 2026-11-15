// The missions end to end on a running server: the admin panel's "Missions and rewards" page lowers
// the daily kill mission to one monster (the running game uses it at once), the hero kills a monster,
// the missions window shows the mission done, the claim pays its zen and the chat says so. The page
// is put back afterwards. Needs the game server (44406) and an admin panel login
// (ADMIN_USER / ADMIN_PASSWORD, for a panel with admin users); skips without them.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';
import net from 'node:net';

const adminUrl = (process.env.ADMIN_URL ?? 'http://127.0.0.1:8095').replace(/\/$/, '');
const adminUser = process.env.ADMIN_USER ?? '';
const adminPassword = process.env.ADMIN_PASSWORD ?? '';

interface Fighter { id: number; kind: string; dead: boolean; cell: { x: number; y: number } }
interface LiveGame {
  online: { characters: Map<number, Fighter>; hero: { cell: { x: number; y: number }; isWalking: boolean; money?: number }; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  combat: { attack(target: unknown): void };
  missions: { isOpen: boolean; state: { missions: { id: string; value: number; target: number; claimed: boolean }[] } | null };
  chat: { entries: { text: string }[] };
  inventory: { money: number };
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

/** Signs in to the admin panel and sets the daily kill mission's target. */
async function setKillTarget(page: Page, target: number): Promise<void> {
  await page.goto(`${adminUrl}/missions-rewards`, { waitUntil: 'networkidle' });
  // The sign-in form is interactive only once its circuit is up: type again until it goes through.
  await expect(async () => {
    if (await page.locator('table input').count()) return;
    if (!await page.locator('#login-name').count()) await page.goto(`${adminUrl}/missions-rewards`, { waitUntil: 'networkidle' });
    if (await page.locator('#login-name').count()) {
      await page.waitForTimeout(1_500);
      await page.locator('#login-name').fill(adminUser);
      await page.locator('#login-password').fill(adminPassword);
      await page.locator('button[type=submit]').first().click();
      await page.waitForTimeout(3_000);
      await page.goto(`${adminUrl}/missions-rewards`, { waitUntil: 'networkidle' });
    }
    await expect(page.locator('table input').first()).toBeVisible({ timeout: 15_000 });
  }).toPass({ timeout: 120_000 });
  const idInputs = page.locator('table input');
  await expect(idInputs.first()).toBeVisible({ timeout: 60_000 });
  // The row whose id input says daily-kill.
  const rows = page.locator('table tbody tr');
  const count = await rows.count();
  let found = false;
  for (let i = 0; i < count; i++) {
    const r = rows.nth(i);
    const inputs = r.locator('input');
    if (await inputs.count() < 3) continue;
    if (await inputs.nth(1).inputValue() !== 'daily-kill') continue;
    const targetInput = r.locator('input[type=number]').first();
    await targetInput.fill(String(target));
    await targetInput.dispatchEvent('change');
    found = true;
    break;
  }
  expect(found, 'the daily-kill row').toBe(true);
  await page.click('#missions-save');
  await page.waitForTimeout(2_000);
  await expect(page.locator('#blazor-error-ui')).toBeHidden();
}

test('a mission set in the admin panel is done in the game and its claim pays', async ({ browser }) => {
  test.skip(!(await portOpen(44406)), 'No game server runs on 44406.');
  test.skip(!adminUser || !adminPassword, 'ADMIN_USER / ADMIN_PASSWORD not set.');
  test.setTimeout(12 * 60_000);
  const admin = await browser.newPage();
  await setKillTarget(admin, 1);
  const page = await browser.newPage();
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  try {
    await enterWorld(page, process.env.MISSIONS_ACCOUNT ?? 'test5');
    // The board of the day (J), before the kill.
    await page.keyboard.press('j');
    await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.missions.state?.missions.find((m) => m.id === 'daily-kill')?.target ?? 0), { timeout: 60_000 }).toBe(1);
    const before = await page.evaluate(() => (window as unknown as Win).__game.missions.state!.missions.find((m) => m.id === 'daily-kill')!);
    if (!before.claimed) {
      await page.keyboard.press('j');
      // Out to the spiders north of Lorencia and kill one.
      await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
      await page.waitForTimeout(8_000);
      const nearest = () => page.evaluate(() => {
        const g = (window as unknown as Win).__game;
        const hero = g.online.hero.cell;
        let best: Fighter | null = null, bestDistance = Infinity;
        for (const c of g.online.characters.values()) {
          if (c.kind !== 'monster' || c.dead) continue;
          const d = Math.hypot(c.cell.x - hero.x, c.cell.y - hero.y);
          if (d < bestDistance) { best = c; bestDistance = d; }
        }
        return best ? best.id : null;
      });
      for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 85 }, { x: 160, y: 70 }, { x: 170, y: 58 }, { x: 185, y: 45 }, { x: 200, y: 35 }]) {
        await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
        await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
        if (goal.y <= 70 && await nearest()) break;
      }
      await expect.poll(nearest, { timeout: 60_000 }).not.toBeNull();
      await expect(async () => {
        const id = await nearest();
        if (id !== null) await page.evaluate((target) => { const g = (window as unknown as Win).__game; g.combat.attack(g.online.characters.get(target)); }, id);
        await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.chat.entries.map((e) => e.text).join('\n')), { timeout: 60_000 })
          .toMatch(/Mission complete|Nhiệm vụ/i);
      }).toPass({ timeout: 5 * 60_000 });
      // The window shows it done; claim it.
      await page.keyboard.press('j');
      const claim = page.locator('.rml-hnmo_missions .mi-btn.claim').first();
      await expect(claim).toBeVisible({ timeout: 60_000 });
      await page.screenshot({ path: 'test-results/missions-live-done.png' });
      const zenBefore = await page.evaluate(() => (window as unknown as Win).__game.inventory.money);
      await claim.click();
      await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.missions.state?.missions.find((m) => m.id === 'daily-kill')?.claimed ?? false), { timeout: 60_000 }).toBe(true);
      const chat = await page.evaluate(() => (window as unknown as Win).__game.chat.entries.map((e) => e.text).join('\n'));
      console.log(chat.split('\n').slice(-6).join('\n'));
      expect(chat).toMatch(/Reward of .* received|Đã nhận phần thưởng/);
      // The mission's zen (500,000 by default) is in the bag.
      await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.inventory.money), { timeout: 30_000 }).toBeGreaterThanOrEqual(zenBefore + 500_000);
      await page.screenshot({ path: 'test-results/missions-live-claimed.png' });
    }
    await expectNoPageErrors(errors);
  } finally {
    await setKillTarget(admin, 300).catch((error: unknown) => console.warn('Put back:', error));
  }
});
