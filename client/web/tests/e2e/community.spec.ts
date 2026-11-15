// HNMO's community in the web game and on the website. In the game: the community window (K) shows the state the server
// sends (HnmoCommunityState, C2 F5 15): the Discord coins with the link code (DiscordLinkCode, C1 F5 05), the Zalo
// claim and its code, the community events with the code field, the waiting rewards; its buttons send
// HnmoCommunityAction (F5 16). The states are fed through the game's connection and the community actions and link
// code requests are only recorded, not sent (the test server is shared, and this test changes nothing there); the
// server's rules are covered by its unit tests (CommunityEngineTest). Print Screen saves a JPG named like the desktop's
// screenshots, and the share panel (the touch "more" menu, the window's button) downloads one. The website's
// Community section reads /website/community (answered here by the test).
import { connect } from 'node:net';
import { readFile } from 'node:fs/promises';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
    community?: { isOpen: boolean; handle(bytes: Uint8Array): boolean };
    screenshots?: { isOpen: boolean; lastName: string };
    chat?: { entries: { text: string }[] };
  };
  __sent: Sent[];
  __feeding?: boolean;
};

const STATE = (patch: Record<string, unknown> = {}) => ({
  v: 1, now: Date.now(),
  discord: { on: true, coins: 50, coinType: 'GoblinPoints', linkAvailable: true, linked: false, paid: false },
  zalo: { on: true, coins: 40, coinType: 'GoblinPoints', status: 'None' },
  events: [{
    id: 'summer', titleEn: 'Summer party', titleVi: 'Tiệc mùa hè', descriptionEn: 'Say the word in Lorencia.', descriptionVi: 'Nói mật mã ở Lorencia.',
    start: Date.now() - 3_600_000, end: Date.now() + 86_400_000, phase: 'Open', hasCode: true, entered: false, winner: false,
    reward: { coins: 15, coinType: 'GoblinPoints', zen: 1000, items: [{ group: 14, number: 13, level: 0, count: 1, name: 'Jewel of Bless' }] },
  }],
  owed: [],
  ...patch,
});

/** The HnmoCommunityState packet (C2 F5 15); the server escapes non-ASCII text, so the JSON is plain ASCII. */
function statePacket(value: unknown): number[] {
  const text = JSON.stringify(value).replace(/[\u0080-￿]/g, (c) => `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`);
  const json = Buffer.from(text, 'ascii');
  const length = 5 + json.length + 1;
  return [0xc2, length >> 8, length & 0xff, 0xf5, 0x15, ...json, 0];
}

/** The DiscordLinkCode packet (C1 F5 05): result, the code (10 bytes), the minutes. */
function linkCodePacket(code: string, minutes: number): number[] {
  const bytes = [...Buffer.from(code.padEnd(10, '\0'), 'ascii')].slice(0, 10);
  return [0xc1, 16, 0xf5, 0x05, 0, ...bytes, minutes];
}

async function feed(page: Page, bytes: number[]): Promise<void> {
  await page.evaluate((data) => {
    const w = window as unknown as Win;
    w.__feeding = true;
    try { w.__game.connection.dispatch(new Uint8Array(data)); } finally { w.__feeding = false; }
  }, bytes);
}

/** From now on the window takes only the states this test feeds (a server with the community also sends its own). */
async function onlyFedStates(page: Page): Promise<void> {
  await page.evaluate(() => {
    const w = window as unknown as Win;
    const window_ = w.__game.community;
    if (!window_) return;
    const handle = window_.handle.bind(window_);
    window_.handle = (bytes: Uint8Array) => {
      const ours = (bytes[0] === 0xc2 && bytes[3] === 0xf5 && bytes[4] === 0x15) || (bytes[0] === 0xc1 && bytes[2] === 0xf5 && bytes[3] === 0x05);
      if (ours && !w.__feeding) return true;
      return handle(bytes);
    };
  });
}

const sent = (page: Page) => page.evaluate(() => (window as unknown as Win).__sent.filter((s) => s.name.startsWith('HnmoCommunity') || s.name === 'DiscordLinkCodeRequest'));
const chat = (page: Page) => page.evaluate(() => ((window as unknown as Win).__game.chat?.entries ?? []).map((e) => e.text));

function gameServerUp(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host: '127.0.0.1', port: 44406 }, () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
    socket.setTimeout(2000, () => { socket.destroy(); resolve(false); });
  });
}

test('in the game: the community window with K, the Discord and Zalo coins, an event code, Print Screen and the share panel', async ({ page }) => {
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
      // Only the first community request reaches the server; the actions and link codes are only recorded.
      connection.send = (group, name, fields) => {
        const recordOnly = name === 'HnmoCommunityAction' || name === 'DiscordLinkCodeRequest'
          || (name === 'HnmoCommunityRequest' && w.__sent.some((s) => s.name === name));
        w.__sent.push({ name, fields: { ...(fields ?? {}) } });
        if (!recordOnly) send(group, name, fields);
      };
      (connection as unknown as { __wrapped?: boolean }).__wrapped = true;
      clearInterval(timer);
    }, 5);
  });
  await enterWorld(page, 'test7');
  await expect.poll(() => sent(page).then((list) => list.some((s) => s.name === 'HnmoCommunityRequest')), { timeout: 60_000 }).toBe(true);
  await page.waitForFunction(() => !!(window as unknown as Win).__game.chat, null, { timeout: 120_000 });
  await onlyFedStates(page);

  // K opens the window.
  await page.mouse.move(640, 300);
  await page.keyboard.press('k');
  const panel = page.locator('.rml-hnmo_community');
  await expect(panel).toHaveCount(1, { timeout: 90_000 });
  await feed(page, statePacket(STATE()));
  await expect(panel.locator('.cm-discord-text')).toContainText('Join our Discord and link your account: 50 Goblin Points once.');
  await expect(panel.locator('.cm-zalo-text')).toContainText('Join our Zalo group, then claim 40 Goblin Points once');
  await expect(panel).toContainText('Summer party');
  await expect(panel).toContainText('Reward: 15 Goblin Points + 1,000 zen + Jewel of Bless x1');
  await expect(panel).toContainText('Enter the event code below.');

  // Discord: the link code button asks for a code; the code comes and is shown and told.
  await panel.locator('.cm-discord-link').click();
  await expect.poll(() => sent(page).then((list) => list.some((s) => s.name === 'DiscordLinkCodeRequest'))).toBe(true);
  await feed(page, linkCodePacket('ABCD-EFGH', 10));
  await expect(panel.locator('.cm-discord-code')).toHaveText('ABCD-EFGH');
  await expect.poll(() => chat(page)).toContain('In our Discord server, type /link ABCD-EFGH (valid for 10 minutes).');
  // Linked meanwhile: the window takes the coins by itself, then the paid state is shown.
  await feed(page, statePacket(STATE({ discord: { on: true, coins: 50, coinType: 'GoblinPoints', linkAvailable: true, linked: true, paid: false } })));
  await expect.poll(() => sent(page).then((list) => list.filter((s) => s.name === 'HnmoCommunityAction').map((s) => s.fields))).toContainEqual({ Action: 1, Argument: '' });
  await feed(page, statePacket(STATE({ discord: { on: true, coins: 50, coinType: 'GoblinPoints', linkAvailable: true, linked: true, paid: true }, event: { type: 'discord', result: 'Ok', coins: 50 } })));
  await expect.poll(() => chat(page)).toContain('Thanks for joining our Discord: 50 Goblin Points received.');
  await expect(panel.locator('.cm-discord-text')).toHaveText('Discord linked: you got 50 Goblin Points.');

  // An event code typed in the field.
  const code = panel.locator('.cm-code-input');
  await code.fill('sun shine');
  await code.press('Enter');
  await expect.poll(() => sent(page).then((list) => list.filter((s) => s.name === 'HnmoCommunityAction').map((s) => s.fields))).toContainEqual({ Action: 3, Argument: 'sun shine' });
  const entered = STATE().events.map((e) => ({ ...e, entered: true }));
  const paid = { on: true, coins: 50, coinType: 'GoblinPoints', linkAvailable: true, linked: true, paid: true };
  await feed(page, statePacket(STATE({ discord: paid, events: entered, event: { type: 'code', result: 'Ok', coins: 15, eventId: 'summer' } })));
  await expect.poll(() => chat(page)).toContain('Code accepted: Summer party. Reward: 15 Goblin Points + 1,000 zen + Jewel of Bless x1.');
  await expect(panel).toContainText('Code entered.');

  // Zalo: the claim with the name in Zalo, then the code to post.
  await panel.locator('.cm-zalo-input').fill('Ann Zalo');
  await panel.locator('.cm-zalo-send').click();
  await expect.poll(() => sent(page).then((list) => list.filter((s) => s.name === 'HnmoCommunityAction').map((s) => s.fields))).toContainEqual({ Action: 2, Argument: 'Ann Zalo' });
  await feed(page, statePacket(STATE({ discord: paid, events: entered, zalo: { on: true, coins: 40, coinType: 'GoblinPoints', status: 'Pending', code: 'Z-K7M2QX' }, event: { type: 'zalo', result: 'Ok' } })));
  await expect(panel.locator('.cm-zalo-code')).toHaveText('Z-K7M2QX');
  await expect.poll(() => chat(page)).toContain('Post the code Z-K7M2QX in our Zalo group (or send it to the admin). The coins come when the admin approves it.');
  await page.screenshot({ path: 'test-results/community-window.png' });

  // Vietnamese.
  await page.locator('.language-picker button[data-lang="vi"]:visible').first().click();
  await expect(panel).toContainText('Tiệc mùa hè');
  await expect(panel.locator('.cm-discord-text')).toHaveText('Đã liên kết Discord: bạn đã nhận 50 Goblin Points.');
  await page.locator('.language-picker button[data-lang="en"]:visible').first().click();

  // Print Screen: a JPG named like the desktop's, and the chat line.
  await page.mouse.move(640, 300);
  const download = page.waitForEvent('download');
  await page.keyboard.press('PrintScreen');
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/^Screen\(\d\d_\d\d-\d\d_\d\d\)-\d{4}\.jpg$/);
  const bytes = await readFile(await file.path());
  expect(bytes.length).toBeGreaterThan(5000);
  expect([bytes[0], bytes[1]]).toEqual([0xff, 0xd8]);
  await expect.poll(() => chat(page).then((lines) => lines.some((l) => l.startsWith(`Screenshot saved: ${file.suggestedFilename()} [`)))).toBe(true);

  // The share panel from the window's button: a preview, the caption switch and Download.
  await panel.locator('.cm-share').click();
  const share = page.locator('#hnmoShare');
  await expect(share).toHaveClass(/open/);
  await expect(share.locator('img')).toHaveAttribute('src', /^blob:/);
  await expect(share.locator('label')).toHaveText('Caption with the game\'s name');
  const second = page.waitForEvent('download');
  await share.locator('button.download').click();
  expect((await second).suggestedFilename()).toMatch(/^Screen\(.*\)-\d{4}\.jpg$/);
  await page.screenshot({ path: 'test-results/community-share.png' });
  await share.locator('button.close').click();
  await expect(share).not.toHaveClass(/open/);

  // The touch "more" menu has the community and the camera.
  await expect(page.locator('#touchMore button[title="Community"]')).toHaveCount(1);
  await expect(page.locator('#touchMore button[title="Screenshot / share"]')).toHaveCount(1);
  await page.evaluate(() => window.dispatchEvent(new Event('hnmo-share-open')));
  await expect(share).toHaveClass(/open/);
  await page.keyboard.press('Escape');
  await expect(share).not.toHaveClass(/open/);

  // Escape closed the window too; K opens and closes it.
  await expect(panel).toHaveCount(0);
  await page.mouse.move(640, 300);
  await page.keyboard.press('k');
  await expect(panel).toHaveCount(1, { timeout: 90_000 });
  await page.keyboard.press('k');
  await expect(panel).toHaveCount(0);
  await page.goto('about:blank');
  await expectNoPageErrors(errors);
});

const PUBLIC = () => ({
  v: 1, enabled: true, now: Date.now(), timeZone: 'Australia/Sydney',
  discord: { on: true, coins: 50, coinType: 'GoblinPoints' },
  zalo: { on: true, coins: 40, coinType: 'WCoinP' },
  events: [{
    id: 'summer', titleEn: 'Summer party', titleVi: 'Tiệc mùa hè', descriptionEn: 'Say the word in Lorencia.', descriptionVi: 'Nói mật mã ở Lorencia.',
    start: Date.now() - 3_600_000, end: Date.UTC(2026, 11, 2, 7, 0), phase: 'Open', hasCode: true, winners: [],
    reward: { coins: 15, coinType: 'GoblinPoints', zen: 1000, items: [{ group: 14, number: 13, level: 0, count: 1, name: 'Jewel of Bless' }] },
  }, {
    id: 'screens', titleEn: 'Best screenshot', titleVi: 'Ảnh đẹp nhất', descriptionEn: '', descriptionVi: '',
    start: Date.now() - 86_400_000, end: null, phase: 'Open', hasCode: false, winners: ['Ann', 'Bob'],
    reward: { coins: 100, coinType: 'GoblinPoints', zen: 0, items: [] },
  }],
  highlights: [
    { at: Date.UTC(2026, 10, 15, 9, 30), kind: 'RareDrop', textEn: 'Ann found Excellent Sword (Goblin, Lorencia)!', textVi: 'Ann đã nhặt được Sword (hoàn hảo) (Goblin, Lorencia)!' },
    { at: Date.UTC(2026, 10, 15, 8, 0), kind: 'GuildLevelUp', textEn: 'The guild Dragons reached level 3!', textVi: 'Bang hội Dragons đã lên cấp 3!' },
  ],
});

test('the website shows the community links, how to get the coins, the events and the recent highlights, in both languages', async ({ page }) => {
  let available = true;
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test realm', links: { discord: 'https://discord.gg/example', zalo: 'https://zalo.me/g/example' } } }));
  for (const path of ['data', 'details', 'missions', 'world-bosses', 'onboarding', 'guilds']) await page.route(`**/website/${path}`, (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/community', (route) => (available ? route.fulfill({ json: PUBLIC() }) : route.fulfill({ status: 503 })));
  await page.goto('/home.html');
  await page.locator('#en').click();
  const section = page.locator('#social');
  await expect(page.locator('nav a[href="#social"]')).toHaveText('Community');
  await expect(section.locator('h2')).toHaveText('Join the community');
  await expect(section.locator('#social-links a.community-discord')).toHaveAttribute('href', 'https://discord.gg/example');
  await expect(section.locator('#social-links a.community-zalo')).toHaveAttribute('href', 'https://zalo.me/g/example');
  await expect(section.locator('#social-links span')).toHaveText('Join us:');
  await expect(section.locator('#social-coins')).toContainText('You get 50 Goblin Points once.');
  await expect(section.locator('#social-coins')).toContainText('you get 40 W Coin (P) once.');
  await expect(section.locator('#social-coins')).toContainText('Enter the event\'s code in the community window (K), once per account.');
  await expect(section.locator('#social-events')).toContainText('Summer party');
  await expect(section.locator('#social-events')).toContainText('Reward: 1,000 Zen · 15 Goblin Points · Jewel of Bless x1');
  await expect(section.locator('#social-events')).toContainText('Winners: Ann, Bob');
  await expect(section.locator('#social-events')).toContainText('Running until');
  await expect(section.locator('#social-highlights li').first()).toContainText('Ann found Excellent Sword (Goblin, Lorencia)!');
  await expect(section.locator('#social-highlights li')).toHaveCount(2);
  await section.scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'test-results/community-website.png', fullPage: false });

  await page.locator('#vi').click();
  await expect(page.locator('nav a[href="#social"]')).toHaveText('Cộng đồng');
  await expect(section.locator('h2')).toHaveText('Tham gia cộng đồng');
  await expect(section.locator('#social-links span')).toHaveText('Tham gia:');
  await expect(section.locator('#social-events')).toContainText('Tiệc mùa hè');
  await expect(section.locator('#social-events')).toContainText('Người thắng: Ann, Bob');
  await expect(section.locator('#social-highlights li').nth(1)).toContainText('Bang hội Dragons đã lên cấp 3!');
  await expect(section.locator('#social-coins')).toContainText('Bạn nhận 50 Goblin Points một lần.');

  // The server doesn't answer: a notice instead of old data.
  available = false;
  await page.locator('#en').click();
  await page.locator('#refresh').click();
  await expect(section.locator('#social-notice')).toHaveText('The community section is unavailable: the server didn\'t answer.');
  await expect(section.locator('#social-events')).not.toContainText('Summer party');
});
