// Friends across seasons: test6Dk (Season 6.3) and test7Dk are friends (made in the test save). test7 plays in the Arena;
// test6's friend list shows test7Dk as "Arena" instead of offline, and whispers go both ways between the programs
// ("(Season 6.3) …" in the Arena, "(Arena) …" in Season 6.3). Needs the Arena program next to the web client and
// the friendship in the save: set ARENA_BATTLES=1.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, chatText } from './helpers.js';

interface FriendGame { social?: { messenger: { friends: { name: string; server: number }[] } }; chat: { whisper: ((name: string, text: string) => void) | null } }
type Win = Window & { __game: FriendGame };

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

test('a friend in the Arena shows in the friend list, and whispers cross', async ({ browser }) => {
  test.skip(!process.env.ARENA_BATTLES, 'Needs the Arena program and the test friendship: set ARENA_BATTLES=1.');
  test.setTimeout(10 * 60_000);
  const arena = await (await browser.newContext()).newPage();
  const home = await (await browser.newContext()).newPage();
  await enterArena(arena, 'test7');
  await enterWorld(home, 'test6');

  // test7Dk plays in the Arena: the friend list says so (0xE2).
  const serverOf = () => home.evaluate(() => (window as unknown as Win).__game.social?.messenger.friends.find((f) => f.name === 'test7Dk')?.server ?? -1);
  await expect.poll(serverOf, { timeout: 120_000 }).toBe(0xe2);

  // A whisper from Season 6.3 reaches the Arena, and the answer comes back.
  await home.evaluate(() => (window as unknown as Win).__game.chat.whisper?.('test7Dk', 'hello from home'));
  await expect.poll(() => chatText(arena), { timeout: 60_000 }).toContain('(Season 6.3) hello from home');
  await arena.evaluate(() => (window as unknown as Win).__game.chat.whisper?.('test6Dk', 'hello from the arena'));
  await expect.poll(() => chatText(home), { timeout: 60_000 }).toContain('(Arena) hello from the arena');
  await home.screenshot({ path: 'test-results/friends-across-seasons.png' });
});
