// The chat as the desktop runs it (game/chat.ts): Enter opens the input box (chat_input.rml), a line is
// sent and comes back in the chat log (chat_log.rml) as "name : text"; the channel buttons put the
// party / guild / Gens sign in front; the line history (Up); Escape closes; F2 shows only whispers; F5
// the frame, the size button 3 more lines; the game's own messages go to the system log (system_log.rml);
// a right click on a said line puts its name in the whisper box.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface ChatGame {
  hero: { name: string };
  online: { connection: { send(group: string, name: string, fields: Record<string, unknown>): void } };
  chat: {
    input: unknown; isOpen: boolean; whisperTarget: string;
    entries: { kind: string; text: string }[];
    shownLines: { chat: string[]; system: string[] };
    add(text: string, kind?: string, sender?: string): void;
    setWhisperTarget(name: string): void;
    setTab(index: number): void;
    currentTab: number;
  };
}
type Win = Window & { __game: ChatGame; __sent: { name: string; fields: Record<string, unknown> }[] };
const chat = <T>(page: Page, f: (c: ChatGame['chat']) => T) => page.evaluate(`(${f.toString()})(window.__game.chat)`) as Promise<T>;

test('the chat: input box, log, channels, history, frame, system log, whisper target', async ({ page }) => {
  test.setTimeout(9 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test6');
  await page.waitForFunction(() => !!(window as unknown as Win).__game.chat?.input, null, { timeout: 120_000 });
  const heroName = await page.evaluate(() => (window as unknown as Win).__game.hero.name);
  // What goes to the server.
  await page.evaluate(() => {
    const w = window as unknown as Win;
    w.__sent = [];
    const connection = w.__game.online.connection;
    const send = connection.send.bind(connection);
    connection.send = (group, name, fields) => { w.__sent.push({ name, fields }); return send(group, name, fields); };
  });
  const lastSent = () => page.evaluate(() => { const w = window as unknown as Win; return w.__sent.filter((p) => p.name === 'PublicChatMessage' || p.name === 'ChatMessage').map((p) => String(p.fields.Message)).pop() ?? ''; });
  const input = page.locator('.rml-chat_input #panel');
  const field = page.locator('.rml-chat_input #chat_field');

  // The game's own messages are in the system log, not the chat log.
  expect((await chat(page, (c) => c.shownLines)).system.join('\n')).toContain('Welcome to');
  await expect(input).toBeHidden();

  // Enter opens the box; typing stays in it (no window opens on "i"); Enter sends and closes it.
  await page.keyboard.press('Enter');
  await expect(input).toBeVisible();
  await expect(field).toBeFocused();
  await page.keyboard.type('hi from the chat test');
  await expect(page.locator('.rml-inventory')).toHaveCount(0);
  await page.keyboard.press('Enter');
  await expect(input).toBeHidden();
  await expect.poll(lastSent).toBe('hi from the chat test');
  await expect.poll(() => chat(page, (c) => c.shownLines.chat.join('\n')), { timeout: 30_000 }).toContain(`${heroName} : hi from the chat test`);
  await expect(page.locator('.rml-chat_log .chat-line--chat').last()).toHaveText(`${heroName} : hi from the chat test`);

  // The party button: the line goes out with "~"; Up brings the last line back; Escape closes.
  await page.waitForTimeout(1100); // one line a second
  await page.keyboard.press('Enter');
  await page.locator('.rml-chat_input #btn_type_1').click();
  await expect(page.locator('.rml-chat_input #btn_type_1')).toHaveClass(/\bon\b/);
  await field.focus();
  await page.keyboard.press('ArrowUp');
  await expect(field).toHaveValue('hi from the chat test');
  await page.keyboard.press('Escape');
  await expect(input).toBeHidden();
  await page.keyboard.press('Enter');
  await page.keyboard.type('party line');
  await page.keyboard.press('Enter');
  await expect.poll(lastSent).toBe('~party line');
  // Back to normal chat.
  await page.keyboard.press('Enter');
  await page.locator('.rml-chat_input #btn_type_0').click();
  await page.keyboard.press('Escape');

  // F5 the frame (backdrop and resize handle), the size button 3 more lines (6 -> 9: 15 units each).
  await page.keyboard.press('Enter');
  await page.keyboard.press('F5');
  await expect(page.locator('.rml-chat_log #panel')).toHaveClass(/\bframed\b/);
  await expect(page.locator('.rml-chat_log #resize_handle')).toBeVisible();
  const height = () => page.locator('.rml-chat_log #lines').evaluate((e) => (e as HTMLElement).offsetHeight);
  expect(await height()).toBe(90);
  await page.locator('.rml-chat_input #btn_size').click();
  await expect.poll(height).toBe(135);
  await page.screenshot({ path: 'test-results/chat-frame.png' });
  // Closing the box takes the frame away (ClosingProcess).
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-chat_log #panel')).not.toHaveClass(/\bframed\b/);

  // F2: only whispers; again: everything.
  await chat(page, (c) => c.add('a whispered line', 'whisper', 'Someone'));
  await page.keyboard.press('F2');
  await expect.poll(() => chat(page, (c) => c.shownLines.chat)).toEqual(['Someone : a whispered line']);
  await page.keyboard.press('F2');
  await expect.poll(() => chat(page, (c) => c.shownLines.chat.length)).toBeGreaterThan(1);

  // A right click on a said line puts its name in the whisper box.
  const line = page.locator('.rml-chat_log .chat-line--whisper').last();
  const box = (await line.boundingBox())!;
  await page.mouse.move(box.x + 10, box.y + box.height / 2);
  await expect(line).toHaveClass(/chat-line--pointed/);
  await page.mouse.down({ button: 'right' });
  await page.mouse.up({ button: 'right' });
  await expect.poll(() => chat(page, (c) => c.whisperTarget)).toBe('Someone');
  await chat(page, (c) => c.setWhisperTarget(''));

  await page.screenshot({ path: 'test-results/chat.png' });

  // HNMO: the same line again within 30 s is counted on the last line; rude words of others are hidden.
  await chat(page, (c) => { c.add('buy my sword', 'chat', 'Seller'); c.add('buy my sword', 'chat', 'Seller'); c.add('what the fuck', 'chat', 'Rude'); c.add('đm thằng này', 'chat', 'Rude'); });
  await expect.poll(() => chat(page, (c) => c.shownLines.chat.slice(-3))).toEqual(['Seller : buy my sword  (x2)', 'Rude : what the ****', 'Rude : *** thằng này']);
  // Blocked players' lines don't show; /unblock lets them through again.
  await page.keyboard.press('Enter');
  await page.keyboard.type('/block Rude');
  await page.keyboard.press('Enter');
  await chat(page, (c) => c.add('still here', 'chat', 'Rude'));
  await page.waitForTimeout(300);
  expect((await chat(page, (c) => c.shownLines.chat)).some((line) => line.includes('still here'))).toBe(false);
  await page.waitForTimeout(1100);
  await page.keyboard.press('Enter');
  await page.keyboard.type('/unblock Rude');
  await page.keyboard.press('Enter');
  await chat(page, (c) => c.add('back again', 'chat', 'Rude'));
  await expect.poll(() => chat(page, (c) => c.shownLines.chat.at(-1))).toBe('Rude : back again');

  // Tabs: the Party tab counts a party line while another tab shows, and shows only party lines.
  await page.keyboard.press('Enter');
  await expect(page.locator('.rml-chat_log #hnmo_tabs')).toBeVisible();
  await chat(page, (c) => c.add('party up', 'party', 'Friend'));
  await expect(page.locator('.rml-chat_log #hnmo_tabs button', { hasText: 'Party' })).toContainText('1');
  await page.locator('.rml-chat_log #hnmo_tabs button', { hasText: 'Party' }).click();
  await expect.poll(() => chat(page, (c) => c.shownLines.chat)).toEqual(['Friend : party up']);
  await page.locator('.rml-chat_log #hnmo_tabs button', { hasText: 'All' }).click();
  await page.keyboard.press('Escape');

  // A click on a name opens its menu.
  const named = page.locator('.rml-chat_log .chat-line', { hasText: 'Friend : party up' });
  const at = (await named.boundingBox())!;
  await page.mouse.move(at.x + 8, at.y + at.height / 2);
  await page.mouse.down();
  await page.mouse.up();
  await expect(page.locator('.hnmo-chat-menu')).toContainText('Friend');
  await expect(page.locator('.hnmo-chat-menu button')).toContainText(['Whisper', 'Invite to party', 'Trade', 'Add friend', 'Block', 'Copy name']);
  await page.screenshot({ path: 'test-results/chat-menu.png' });
  await page.mouse.click(5, 300);
  await expect(page.locator('.hnmo-chat-menu')).toHaveCount(0);

  // Lines fade 10 s after they came while the chat is closed.
  await expect.poll(() => page.locator('.rml-chat_log .chat-line.faded').count(), { timeout: 30_000 }).toBeGreaterThan(0);

  // After a reload the character's lines are there again.
  await page.waitForTimeout(2500); // the history is saved a moment after a line
  await enterWorldAgain(page); // a fresh page load
  await expect.poll(() => chat(page, (c) => c.shownLines.chat.join('\n')), { timeout: 60_000 }).toContain('Friend : party up');
  await expectNoPageErrors(errors);
});

async function enterWorldAgain(page: Page): Promise<void> {
  // Leave, so the server ends the session, then log in again (it refuses an account that is still in).
  await page.goto('about:blank');
  await expect(async () => {
    await page.waitForTimeout(5_000);
    await enterWorld(page, 'test6');
  }).toPass({ timeout: 180_000 });
  await page.waitForFunction(() => !!(window as unknown as Win).__game.chat?.input, null, { timeout: 120_000 });
}
