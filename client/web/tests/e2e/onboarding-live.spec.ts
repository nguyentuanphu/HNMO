// The new-player guide end to end on a running server: a brand-new character gets the starter kit
// (told in the chat, its items in the bag) and the guide panel opens by itself with the first step.
// Creates a character on ONBOARDING_ACCOUNT (default test3) when it has a free slot; skips without a
// game server on 44406.
import { test, expect } from '@playwright/test';
import { expectNoPageErrors } from './helpers.js';
import net from 'node:net';

interface LiveGame {
  chat: { entries: { text: string }[] };
  inventory: { items: Map<number, unknown> };
  onboarding?: { isOpen: boolean };
}
type Win = Window & { __game: LiveGame; __viewerReady?: boolean };

function portOpen(port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = net.connect(port, '127.0.0.1');
    socket.once('connect', () => { socket.destroy(); resolve(true); });
    socket.once('error', () => resolve(false));
    socket.setTimeout(2_000, () => { socket.destroy(); resolve(false); });
  });
}

test('a new character gets the starter kit and the guide opens by itself', async ({ page }) => {
  test.skip(!(await portOpen(44406)), 'No game server runs on 44406.');
  test.setTimeout(10 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  const account = process.env.ONBOARDING_ACCOUNT ?? 'test3';
  await page.goto('/index.html?devlogin=1');
  await page.fill('#username', account);
  await page.fill('#password', account);
  await page.click('button.primary');
  await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 60_000 });
  if (await page.isVisible('#servers .choice')) await page.click('#servers .choice >> nth=0');
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
  const create = page.locator('#characters .choice', { hasText: 'Create a character' });
  test.skip(!(await create.count()), `${account} has no free character slot.`);
  await create.click();
  const name = `ob${Date.now() % 100_000_000}`;
  await page.fill('#newName', name);
  await page.click('#createIt');
  await page.locator('#characters .choice', { hasText: name }).click({ timeout: 60_000 });
  await page.waitForFunction(() => (window as unknown as Win).__viewerReady, null, { timeout: 180_000 });
  const chat = () => page.evaluate(() => (window as unknown as Win).__game.chat.entries.map((e) => e.text).join('\n'));
  // The starter kit and the first step are told in the chat; the panel opens by itself.
  await expect.poll(chat, { timeout: 90_000 }).toMatch(/starter kit|quà khởi đầu|bộ khởi đầu/i);
  await expect(page.locator('.rml-hnmo_onboarding')).toHaveCount(1, { timeout: 60_000 });
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.inventory.items.size), { timeout: 60_000 }).toBeGreaterThan(0);
  console.log((await chat()).split('\n').slice(-8).join('\n'));
  await page.screenshot({ path: 'test-results/onboarding-live.png' });
  await expectNoPageErrors(errors);
});
