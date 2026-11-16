// HNMO's gift codes in the web game and on the website. The gift code window (the community window's button, the touch
// "more" menu, /code without a code) sends the code (HnmoGiftCodesAction) and shows the state the server sends
// (HnmoGiftCodesState, C2 F5 1B): the result of the code, the rewards waiting for room in the bag with Take, and the
// account's last codes; results are told in the chat. The states are fed through the game's connection (the test server
// may run without the gift codes), so what's checked is the window, the chat and the packets; the server's rules are
// covered by its unit tests (GiftCodeEngineTest, WebsiteGiftCodesControllerTests). The website's home page section reads
// /website/gift-codes and the account page calls /portal/gift-codes/* (both answered here by the test).
import { connect } from 'node:net';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
    giftCodes?: { isOpen: boolean; handle(bytes: Uint8Array): boolean };
    chat?: { entries: { text: string }[]; input?: HTMLInputElement };
  };
  __sent: Sent[];
};

const HOUR = 3_600_000;

const STATE = (event?: Record<string, unknown>, owed = false) => ({
  v: 1, on: true, website: true,
  owed: owed ? [{ code: 'XMAS2026', zen: 50000, items: [{ group: 14, number: 13, level: 0, count: 2, name: 'Jewel of Bless' }] }] : [],
  recent: [{ code: 'WELCOME', at: Date.now() - HOUR, character: 'test7', source: 'game' }, { code: 'TETAB12CD', at: Date.now() - 30 * HOUR, character: 'test7', source: 'website' }],
  event,
});

/** The HnmoGiftCodesState packet (C2 F5 1B) of a state; the server escapes non-ASCII text, so the JSON is plain ASCII. */
function packet(value: unknown): number[] {
  const text = JSON.stringify(value).replace(/[\u0080-￿]/g, (c) => `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`);
  const json = Buffer.from(text, 'ascii');
  const length = 5 + json.length + 1;
  return [0xc2, length >> 8, length & 0xff, 0xf5, 0x1b, ...json, 0];
}

async function feed(page: Page, value: unknown): Promise<void> {
  await page.evaluate((bytes) => {
    const w = window as unknown as Win & { __feeding?: boolean };
    w.__feeding = true;
    try { w.__game.connection.dispatch(new Uint8Array(bytes)); } finally { w.__feeding = false; }
  }, packet(value));
}

/** From now on the window takes only the states this test feeds (a server with the gift codes also sends its own). */
async function onlyFedStates(page: Page): Promise<void> {
  await page.evaluate(() => {
    const w = window as unknown as Win & { __feeding?: boolean };
    const panel = w.__game.giftCodes;
    if (!panel) return;
    const handle = panel.handle.bind(panel);
    panel.handle = (bytes: Uint8Array) => {
      const state = bytes[0] === 0xc2 && bytes[3] === 0xf5 && bytes[4] === 0x1b;
      if (state && !w.__feeding) return true;
      return handle(bytes);
    };
  });
}

const sent = (page: Page) => page.evaluate(() => (window as unknown as Win).__sent.filter((s) => s.name.startsWith('HnmoGiftCode')));
const chat = (page: Page) => page.evaluate(() => ((window as unknown as Win).__game.chat?.entries ?? []).map((e) => e.text));

function gameServerUp(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host: '127.0.0.1', port: 44406 }, () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
    socket.setTimeout(2000, () => { socket.destroy(); resolve(false); });
  });
}

test('in the game: the gift code window, /code, the results in the chat, waiting rewards and Take', async ({ page }) => {
  test.skip(!(await gameServerUp()), 'No game server runs on 44406.');
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.addInitScript(() => {
    const w = window as unknown as Win;
    w.__sent = [];
    const timer = setInterval(() => {
      const connection = w.__game?.connection;
      if (!connection || (connection as unknown as { __wrapped?: boolean }).__wrapped) return;
      const send = connection.send.bind(connection);
      // The gift code actions are only recorded: the shared test server must not get test codes. Later requests too:
      // a server with the gift codes would answer them with its own states.
      connection.send = (group, name, fields) => {
        const recordOnly = (name === 'HnmoGiftCodesAction' || name === 'HnmoGiftCodeRedeemExtended') || (name === 'HnmoGiftCodesRequest' && w.__sent.some((s) => s.name === name));
        w.__sent.push({ name, fields: { ...(fields ?? {}) } });
        if (!recordOnly) send(group, name, fields);
      };
      (connection as unknown as { __wrapped?: boolean }).__wrapped = true;
      clearInterval(timer);
    }, 5);
  });
  await enterWorld(page, 'test7');
  await expect.poll(() => sent(page).then((list) => list.some((s) => s.name === 'HnmoGiftCodesRequest')), { timeout: 60_000 }).toBe(true);
  await page.waitForFunction(() => !!(window as unknown as Win).__game.chat?.input && !!(window as unknown as Win).__game.giftCodes, null, { timeout: 120_000 });
  await onlyFedStates(page);

  // /code without a code opens the window (it isn't sent to the server).
  await page.mouse.move(640, 300);
  await page.keyboard.press('Enter');
  await expect(page.locator('.rml-chat_input #chat_field')).toBeFocused();
  await page.keyboard.type('/code');
  await page.keyboard.press('Enter');
  const panel = page.locator('.rml-hnmo_gift_codes');
  await expect(panel).toHaveCount(1, { timeout: 90_000 });
  await feed(page, STATE());
  await expect(panel.locator('.gc-recent').first()).toContainText('WELCOME');
  await expect(panel.locator('.gc-recent').nth(1)).toContainText('website: test7');
  await expect(panel.locator('.gc-claim')).toBeHidden();
  await expect(panel).toContainText('You can also type /code CODE in the chat, or enter codes on the website\'s account page.');

  // A code typed into the field goes to the server with Enter.
  const input = panel.locator('.gc-code-input');
  await input.click();
  await input.fill('xmas2026');
  await input.press('Enter');
  await expect.poll(() => sent(page).then((list) => list.find((s) => s.name === 'HnmoGiftCodeRedeemExtended')?.fields)).toEqual({ GiftCode: 'xmas2026', DeviceId: expect.any(String) });

  // The answer: accepted with a full bag; the window and the chat tell it, the waiting rewards show with Take.
  await feed(page, STATE({ type: 'redeem', result: 'Ok', code: 'XMAS2026', reward: { coins: 250, coinType: 'GoblinPoints', zen: 50000, items: [{ group: 14, number: 13, level: 0, count: 2, name: 'Jewel of Bless' }] }, waiting: true, triesLeft: 5 }, true));
  await expect(panel.locator('.gc-result')).toHaveText('Gift code XMAS2026 accepted: 250 Goblin Points + 50,000 zen + Jewel of Bless x2. Your bag is full: the items wait. Make room, then take them here.');
  await expect.poll(() => chat(page)).toContain('[Gift code] Gift code XMAS2026 accepted: 250 Goblin Points + 50,000 zen + Jewel of Bless x2. Your bag is full: the items wait. Make room, then take them here.');
  await expect(panel.locator('.gc-owed')).toHaveText('XMAS2026 · 50,000 zen + Jewel of Bless x2');
  await page.screenshot({ path: 'test-results/gift-codes-window.png' });
  await panel.locator('.gc-claim').click();
  await expect.poll(() => sent(page).then((list) => list.filter((s) => s.name === 'HnmoGiftCodesAction').at(-1)?.fields)).toEqual({ Action: 2, Argument: '' });
  await feed(page, STATE({ type: 'claim', result: 'Ok' }));
  await expect.poll(() => chat(page)).toContain('The waiting gift code rewards are in your bag.');
  await expect(panel.locator('.gc-claim')).toBeHidden();

  // A wrong code; then too many; with /code CODE the server answers in the chat, so only the window tells it.
  await feed(page, STATE({ type: 'redeem', result: 'Invalid', triesLeft: 3 }));
  await expect(panel.locator('.gc-result')).toHaveText('That gift code doesn\'t exist (3 tries left).');
  const before = (await chat(page)).length;
  await feed(page, STATE({ type: 'command', result: 'TooManyTries', retryAt: Date.UTC(2026, 10, 20, 7, 30) }));
  await expect(panel.locator('.gc-result')).toContainText('Too many wrong codes. Try again after Fri, 20 Nov, 18:30 (Sydney time');
  expect((await chat(page)).slice(before).filter((line) => line.includes('Too many'))).toEqual([]);
  await feed(page, STATE({ type: 'redeem', result: 'LevelTooLow', details: { limit: 'OncePerAccount', minLevel: 50, maxLevel: 0, minResets: 0, maxResets: -1, classes: [] } }));
  await expect(panel.locator('.gc-result')).toHaveText('This gift code needs level 50.');

  // Vietnamese.
  await page.locator('.language-picker button[data-lang="vi"]:visible').first().click();
  await expect(panel.locator('.gc-send')).toHaveText('Nhập mã');
  await feed(page, STATE({ type: 'redeem', result: 'AlreadyUsedAccount' }));
  await expect(panel.locator('.gc-result')).toHaveText('Tài khoản của bạn đã dùng gift code này rồi.');
  await page.locator('.language-picker button[data-lang="en"]:visible').first().click();

  // Escape closes it; the community window's button and the touch menu's event open it again.
  await page.keyboard.press('Escape');
  await expect(panel).toHaveCount(0);
  await page.evaluate(() => window.dispatchEvent(new Event('hnmo-gift-codes-open')));
  await expect(panel).toHaveCount(1, { timeout: 90_000 });
  await panel.locator('.gc-close').click();
  await expect(panel).toHaveCount(0);
  await page.mouse.move(640, 300);
  await page.keyboard.press('k');
  const community = page.locator('.rml-hnmo_community');
  await expect(community).toHaveCount(1, { timeout: 90_000 });
  await community.locator('.cm-gift').click();
  await expect(panel).toHaveCount(1, { timeout: 90_000 });
  expect((await sent(page)).filter((s) => s.name === 'HnmoGiftCodesAction')).toHaveLength(2);
  await page.goto('about:blank');
  await expectNoPageErrors(errors);
});

test('the website: the public codes on the home page and a code entered on the account page, in both languages', async ({ page }) => {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test realm', links: {} } }));
  for (const path of ['data', 'details', 'missions', 'world-bosses', 'onboarding', 'guilds', 'community', 'top-races']) await page.route(`**/website/${path}`, (route) => route.fulfill({ status: 503 }));
  const PUBLIC = {
    v: 1, enabled: true, website: true, now: Date.now(), timeZone: 'Australia/Sydney',
    codes: [{
      code: 'WELCOME2026', note: 'Welcome to the server', usesLeft: 98,
      reward: { coins: 100, coinType: 'GoblinPoints', zen: 20000, items: [{ group: 14, number: 13, level: 0, count: 1, name: 'Jewel of Bless' }] },
      rules: { limit: 'OncePerAccount', end: Date.UTC(2026, 11, 31, 13, 0), minLevel: 10, maxLevel: 0, minResets: 0, maxResets: -1, classes: [] },
    }],
  };
  await page.route('**/website/gift-codes', (route) => route.fulfill({ json: PUBLIC }));
  await page.goto('/home.html');
  await page.locator('#en').click();
  const section = page.locator('#gifts');
  await expect(page.locator('nav a[href="#gifts"]')).toHaveText('Gift codes');
  await expect(section.locator('h2')).toHaveText('Gift codes');
  await expect(section).toContainText('/code CODE');
  const card = section.locator('.gift-card[data-code="WELCOME2026"]');
  await expect(card.locator('h4')).toHaveText('WELCOME2026');
  await expect(card).toContainText('Welcome to the server');
  await expect(card.locator('.gift-reward')).toHaveText('20,000 Zen · 100 Goblin Points · Jewel of Bless x1');
  await expect(card.locator('.gift-rules')).toContainText('once per account · level 10-∞ · 98 uses left · until');
  await expect(card.locator('.gift-rules')).toContainText('Sydney time');
  await expect(section.locator('#gift-account a')).toHaveAttribute('href', '/account.html#gift');
  await section.scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'test-results/gift-codes-website.png' });
  await page.locator('#vi').click();
  await expect(section.locator('h2')).toHaveText('Gift code');
  await expect(card.locator('.gift-rules')).toContainText('một lần mỗi tài khoản');
  await expect(card.locator('.gift-rules')).toContainText('giờ Sydney');

  // The account page: a wrong password, then the characters, then a code.
  const calls: { url: string; body: Record<string, string> }[] = [];
  await page.route('**/portal/gift-codes/characters', async (route) => {
    const body = route.request().postDataJSON() as Record<string, string>;
    calls.push({ url: 'characters', body });
    if (body.password !== 'test7') return route.fulfill({ status: 401, json: { error: 'login' } });
    return route.fulfill({ json: { loginName: 'test7', characters: [{ name: 'test7Dk', level: 60, resets: 1, cls: 4, className: 'Dark Knight' }, { name: 'test7Elf', level: 12, resets: 0, cls: 8, className: 'Fairy Elf' }] } });
  });
  await page.route('**/portal/gift-codes/redeem', async (route) => {
    const body = route.request().postDataJSON() as Record<string, string>;
    calls.push({ url: 'redeem', body });
    if (body.code.toUpperCase() === 'WELCOME2026') return route.fulfill({ json: { v: 1, result: 'Ok', code: 'WELCOME2026', character: body.character, reward: PUBLIC.codes[0]!.reward, triesLeft: 5 } });
    return route.fulfill({ json: { v: 1, result: 'Invalid', character: body.character, triesLeft: 4 } });
  });
  await page.evaluate(() => { try { localStorage.setItem('hnmo.language', 'en'); } catch { /* none */ } });
  await page.goto('/account.html#gift');
  const gift = page.locator('#gift');
  await expect(gift.locator('h2')).toHaveText('Gift code');
  await page.fill('#giftAccount', 'test7');
  await page.fill('#giftPassword', 'wrong');
  await gift.locator('#giftLogin button').click();
  await expect(page.locator('#giftMessage')).toHaveText('Wrong account name or password.');
  await expect(page.locator('#giftForm')).toBeHidden();
  await page.fill('#giftPassword', 'test7');
  await gift.locator('#giftLogin button').click();
  await expect(page.locator('#giftForm')).toBeVisible();
  await expect(page.locator('#giftCharacter option')).toHaveText(['test7Dk (level 60, resets 1)', 'test7Elf (level 12, resets 0)']);
  await page.selectOption('#giftCharacter', 'test7Elf');
  await page.fill('#giftCode', 'nope');
  await gift.locator('#giftForm button').click();
  await expect(page.locator('#giftMessage')).toHaveText('That gift code doesn\'t exist (4 tries left).');
  await page.fill('#giftCode', 'welcome2026');
  await gift.locator('#giftForm button').click();
  await expect(page.locator('#giftMessage')).toHaveText('Gift code WELCOME2026 accepted for test7Elf: 100 Goblin Points + 20,000 zen + Jewel of Bless x1.');
  expect(calls.at(-1)).toEqual({ url: 'redeem', body: { loginName: 'test7', password: 'test7', character: 'test7Elf', code: 'welcome2026', deviceId: expect.any(String) } });
  await gift.scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'test-results/gift-codes-account.png' });

  await gift.locator('[data-gift-lang="vi"]').click();
  await expect(page.locator('#giftMessage')).toHaveText('Đã nhận gift code WELCOME2026 cho test7Elf: 100 Goblin Points + 20.000 zen + Jewel of Bless x1.');
  await expect(gift.locator('#giftForm button')).toHaveText('Dùng mã');
  await expect(page.locator('#giftCharacter option').first()).toHaveText('test7Dk (cấp 60, reset 1)');

  // The server doesn't let the website enter codes: the form is hidden with a note.
  await page.route('**/website/gift-codes', (route) => route.fulfill({ json: { ...PUBLIC, website: false } }));
  await page.reload();
  await expect(page.locator('#giftOff')).toBeVisible();
  await expect(page.locator('#giftLogin')).toBeHidden();
});
