// Server chat (HNMO): a whisper to a character who isn't in the game is kept and arrives, marked
// "(offline)", when that character enters the game; the sender is told. test0 writes, test1 reads.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, chatText } from './helpers.js';

type Win = Window & { __game: { hero: { name: string }; chat: { whisper: ((name: string, text: string) => void) | null } } };

test('a whisper to an offline character arrives when it logs in', async ({ browser }) => {
  test.setTimeout(8 * 60_000);
  const a = await (await browser.newContext()).newPage();
  await enterWorld(a, 'test0');
  await a.waitForFunction(() => !!(window as unknown as Win).__game.chat?.whisper, null, { timeout: 120_000 });
  const stamp = `hello offline ${Date.now() % 100000}`;
  await a.evaluate((text) => (window as unknown as Win).__game.chat.whisper!('test1Dk', text), stamp);
  await expect.poll(() => chatText(a), { timeout: 30_000 }).toContain('test1Dk is offline');
  const b: Page = await (await browser.newContext()).newPage();
  await enterWorld(b, 'test1');
  await expect.poll(() => chatText(b), { timeout: 60_000 }).toContain(stamp);
  console.log((await chatText(b)).split('\n').filter((line) => line.includes(stamp)).join(' | '));
});
