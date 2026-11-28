// The Arena's battles announced on the home servers: a game master in the Arena opens a battle royale due in ten
// minutes (/startroyale 600); the Arena publishes it in the shared database, and a player on Season 6.3 gets the notice
// in the chat ("[Arena] Battle royale at …"). Needs the Arena program next to the web client: set ARENA_BATTLES=1.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, chatText } from './helpers.js';

type Win = Window & { __game: { online: { say(text: string): void } } };

async function enterArena(page: Page, account: string): Promise<void> {
  await expect(async () => {
    await page.goto('/index.html?devlogin=1');
    await page.fill('#username', account);
    await page.fill('#password', account);
    await page.click('button.primary');
    await page.locator('.season-arena .choice').first().click({ timeout: 60_000 });
  }).toPass({ timeout: 300_000 });
  await page.waitForSelector('#characters .choice', { timeout: 90_000 });
  await page.click('#characters .choice >> nth=0');
  await page.waitForFunction(() => (window as unknown as { __viewerReady?: boolean }).__viewerReady, null, { timeout: 240_000 });
}

test('a battle royale in the Arena is announced on the home server', async ({ browser }) => {
  test.skip(!process.env.ARENA_BATTLES, 'Needs the Arena program next to the web client: set ARENA_BATTLES=1.');
  test.setTimeout(12 * 60_000);
  const home = await (await browser.newContext()).newPage();
  const gm = await (await browser.newContext()).newPage();
  await enterWorld(home, 'test9');
  await enterArena(gm, process.env.GM_ACCOUNT ?? 'testgm');
  await gm.evaluate(() => (window as unknown as Win).__game.online.say('/startroyale 600'));
  await expect.poll(() => chatText(home), { timeout: 4 * 60_000 }).toContain('[Arena] Battle royale at');
  await home.screenshot({ path: 'test-results/arena-notice-home.png' });
});
