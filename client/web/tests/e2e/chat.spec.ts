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
  };
}
type Win = Window & { __game: ChatGame; __sent: { name: string; fields: Record<string, unknown> }[] };
const chat = <T>(page: Page, f: (c: ChatGame['chat']) => T) => page.evaluate(`(${f.toString()})(window.__game.chat)`) as Promise<T>;

test('the chat: input box, log, channels, history, frame, system log, whisper target', async ({ page }) => {
  test.setTimeout(5 * 60_000);
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
  await expectNoPageErrors(errors);
});
