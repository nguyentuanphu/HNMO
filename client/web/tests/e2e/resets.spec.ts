import { websiteAccountFixture, signInWebsiteFixture } from './fixtures/websiteAccount.js';
// HNMO's reset options in the web game and on the website. In the game the server answers /reset, /relife and /autoreset
// in the chat; the reset state it sends (HnmoResetsState, C2 F5 1E) keeps the character window's reset count right and
// adds a notice with today's count after a reset, a relife or the delegated resets done while offline leveling. The
// states are fed through the game's connection (the test server may run without the reset options, and no real test
// character is reset), so what's checked is the chat, the character window and the packets; the server's rules are
// covered by its unit tests (ResetEngineTest, WebsiteResetsControllerTests). The home page's reset section reads
// /website/resets and the account page calls /portal/resets/* (both answered here by the test).
import { connect } from 'node:net';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
    resets?: { handle(bytes: Uint8Array): boolean; act(action: number): void; state: { character: { resets: number } } | null };
    stats?: { resets: number };
    chat?: { entries: { text: string }[]; input?: HTMLInputElement };
  };
  __sent: Sent[];
};

const PLAN = (result = 'Ok', extra: Record<string, unknown> = {}) => ({
  result, level: 350, levelAfter: 1, resetsAfter: 13, relifesAfter: 0, zen: 5_000_000, coins: [], items: [], mode: 'Total', points: 6500,
  clearStats: true, vip: false, maximum: 0, ...extra,
});

const CHARACTER = (resets: number, relifes = 0, today = 3, extra: Record<string, unknown> = {}) => ({
  name: 'test8Dw', level: 1, resets, relifes, vip: false, today, dailyLimit: 20, todayRelifes: 0, relifeDailyLimit: 1, month: 7,
  delegated: { allowed: true, on: false, today: 0, dailyLimit: 0 },
  next: PLAN(), relife: PLAN('ResetsTooLow', { resetsNeeded: 50, level: 400, zen: 50_000_000 }), ...extra,
});

const STATE = (character: unknown, event?: Record<string, unknown>) => ({ v: 1, on: true, character, event });

/** The HnmoResetsState packet (C2 F5 1E) of a state; the server escapes non-ASCII text, so the JSON is plain ASCII. */
function packet(value: unknown): number[] {
  const text = JSON.stringify(value).replace(/[\u0080-￿]/g, (c) => `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`);
  const json = Buffer.from(text, 'ascii');
  const length = 5 + json.length + 1;
  return [0xc2, length >> 8, length & 0xff, 0xf5, 0x1e, ...json, 0];
}

async function feed(page: Page, value: unknown): Promise<void> {
  await page.evaluate((bytes) => {
    const w = window as unknown as Win & { __feeding?: boolean };
    w.__feeding = true;
    try { w.__game.connection.dispatch(new Uint8Array(bytes)); } finally { w.__feeding = false; }
  }, packet(value));
}

/** From now on the game takes only the reset states this test feeds (a server with the reset options sends its own). */
async function onlyFedStates(page: Page): Promise<void> {
  await page.evaluate(() => {
    const w = window as unknown as Win & { __feeding?: boolean };
    const resets = w.__game.resets;
    if (!resets) return;
    const handle = resets.handle.bind(resets);
    resets.handle = (bytes: Uint8Array) => {
      const state = bytes[0] === 0xc2 && bytes[3] === 0xf5 && bytes[4] === 0x1e;
      if (state && !w.__feeding) return true;
      return handle(bytes);
    };
  });
}

const sent = (page: Page) => page.evaluate(() => (window as unknown as Win).__sent.filter((s) => s.name.startsWith('HnmoResets')));
const chat = (page: Page) => page.evaluate(() => ((window as unknown as Win).__game.chat?.entries ?? []).map((e) => e.text));

function gameServerUp(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host: '127.0.0.1', port: 44406 }, () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
    socket.setTimeout(2000, () => { socket.destroy(); resolve(false); });
  });
}

test('in the game: reset and relife notices, the character window\'s reset count, the delegated resets while away', async ({ page }) => {
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
      // The reset actions are only recorded: no real test character is reset on the shared test server.
      connection.send = (group, name, fields) => {
        const recordOnly = name === 'HnmoResetsAction' || (name === 'HnmoResetsRequest' && w.__sent.some((s) => s.name === name));
        w.__sent.push({ name, fields: { ...(fields ?? {}) } });
        if (!recordOnly) send(group, name, fields);
      };
      (connection as unknown as { __wrapped?: boolean }).__wrapped = true;
      clearInterval(timer);
    }, 5);
  });
  await page.evaluate(() => { try { localStorage.setItem('hnmo.language', 'en'); } catch { /* none */ } }).catch(() => {});
  await enterWorld(page, 'test8');
  await expect.poll(() => sent(page).then((list) => list.some((s) => s.name === 'HnmoResetsRequest')), { timeout: 60_000 }).toBe(true);
  await page.waitForFunction(() => !!(window as unknown as Win).__game.chat?.input && !!(window as unknown as Win).__game.resets, null, { timeout: 120_000 });
  await onlyFedStates(page);

  // A reset (the reset NPC or /reset; the server says the rest in the chat): the notice with today's count, the count.
  await feed(page, STATE(CHARACTER(12), { type: 'reset', result: 'Ok', count: 0 }));
  await expect.poll(() => chat(page)).toContain('Reset 12 done. Today: 3/20 resets.');
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.stats?.resets)).toBe(12);

  // The character window shows it (Level: … | Resets: …, as on the desktop).
  await page.mouse.move(640, 300);
  await page.keyboard.press('c');
  const charWindow = page.locator(".rml-character_info");
  await expect(charWindow.locator(".level-text")).toContainText('Resets: 12', { timeout: 90_000 });

  // A refused reset: no notice from the web game (the server's line tells why).
  const before = (await chat(page)).length;
  await feed(page, STATE(CHARACTER(12, 0, 3, { next: PLAN('LevelTooLow') }), { type: 'reset', result: 'LevelTooLow', count: 0 }));
  await page.waitForTimeout(500);
  expect((await chat(page)).slice(before).filter((line) => line.includes('Reset'))).toEqual([]);

  // A relife: the resets go back, the window follows.
  await feed(page, STATE(CHARACTER(0, 1, 3), { type: 'relife', result: 'Ok', count: 0 }));
  await expect.poll(() => chat(page)).toContain('Relife 1 done. Resets now: 0.');
  await expect(charWindow.locator(".level-text")).toContainText('Resets: 0');

  // Delegated resets while offline leveling, told when the character comes back.
  await feed(page, STATE(CHARACTER(3, 1, 3), { type: 'away', result: 'Ok', count: 3 }));
  await expect.poll(() => chat(page)).toContain('Delegated reset: 3 resets while you were offline leveling. Resets now: 3, today: 3/20.');
  await expect(charWindow.locator(".level-text")).toContainText('Resets: 3');
  await page.screenshot({ path: 'test-results/resets-game.png' });

  // The actions go to the server as HnmoResetsAction (here only recorded).
  await page.evaluate(() => (window as unknown as Win).__game.resets?.act(5));
  await expect.poll(() => sent(page).then((list) => list.find((s) => s.name === 'HnmoResetsAction')?.fields)).toEqual({ Action: 5 });

  // Vietnamese.
  if (await page.locator('#hudToolsToggle').isVisible()) await page.locator('#hudToolsToggle').click();
  await page.locator('.language-picker button[data-lang="vi"]:visible').first().click();
  // The Vietnamese texts load after the click: feed again until the notice is in Vietnamese.
  await expect(async () => {
    await feed(page, STATE(CHARACTER(4, 1, 4, { vip: true }), { type: 'reset', result: 'Ok', count: 0 }));
    expect(await chat(page)).toContain('Đã reset lần 4 (VIP). Hôm nay: 4/20 lần reset.');
  }).toPass({ timeout: 60_000 });
  await page.locator('.language-picker button[data-lang="en"]:visible').first().click();
  await page.goto('about:blank');
  await expectNoPageErrors(errors);
});

test('the website: the reset ranking on the home page and resets, stats and the delegated switch on the account page', async ({ page }) => {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test realm', links: {} } }));
  for (const path of ['data', 'details', 'missions', 'world-bosses', 'onboarding', 'guilds', 'community', 'top-races', 'gift-codes']) await page.route(`**/website/${path}`, (route) => route.fulfill({ status: 503 }));
  const PUBLIC = {
    v: 1, enabled: true, month: '2026-11', previousMonth: '2026-10',
    monthly: [{ rank: 1, name: 'test8Dw', cls: 0, value: 7, resets: 12, relifes: 1 }, { rank: 2, name: 'test9Elf', cls: 8, value: 2, resets: 2, relifes: 0 }],
    previous: [], relifes: [{ rank: 1, name: 'test8Dw', cls: 0, value: 1, resets: 12, relifes: 1 }],
    rules: {
      maximumResets: 0,
      tiers: [{ from: 0, to: 49, level: 350, levelAfter: 1, zen: 5_000_000, zenTimesReset: false, coins: 0, coinType: 'GoblinPoints', points: 500, mode: 'Total', clearStats: true, dailyLimit: 20, items: 0 },
        { from: 50, to: -1, level: 400, levelAfter: 1, zen: 20_000_000, zenTimesReset: false, coins: 10, coinType: 'GoblinPoints', points: 700, mode: 'Total', clearStats: true, dailyLimit: 5, items: 0 }],
      vip: { levelReduction: 20, zenDiscount: 20, coinsDiscount: 20, extraDaily: 2, extraPoints: 0 },
      relife: { resetsNeeded: 50, level: 400, zen: 50_000_000, coins: 0, coinType: 'GoblinPoints', keepPercent: 0, pointsPerRelife: 2000, maximum: 10, dailyLimit: 1 },
      delegated: true,
    },
  };
  await page.route('**/website/resets', (route) => route.fulfill({ json: PUBLIC }));
  await page.goto('/home.html');
  await page.locator('#en').click();
  const section = page.locator('#resets');
  await expect(page.locator('nav a[href="#resets"]')).toHaveText('Resets');
  await expect(section.locator('h2')).toHaveText('Reset ranking and relife');
  await expect(section.locator('#resets-month-title')).toHaveText('This month (2026-11)');
  await expect(section.locator('#resets-monthly tr').nth(1)).toContainText('test8Dw');
  await expect(section.locator('#resets-monthly tr').nth(1)).toContainText('7');
  await expect(section.locator('#resets-relifes tr').nth(1)).toContainText('test8Dw');
  await expect(section.locator('#resets-previous')).toContainText('No resets yet.');
  await expect(section.locator('#resets-rules')).toContainText('Resets 1-50: level 350 · 5,000,000 zen · 500 points (total) · 20 a day');
  await expect(section.locator('#resets-rules')).toContainText('Resets 51+: level 400 · 20,000,000 zen + 10 Goblin Points');
  await expect(section.locator('#resets-rules')).toContainText('VIP: 20 levels lower, 20% off zen');
  await expect(section.locator('#resets-account a')).toHaveAttribute('href', '/account.html#resets');
  await section.scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'test-results/resets-website.png' });
  await page.locator('#vi').click();
  await expect(section.locator('h2')).toHaveText('Xếp hạng reset và relife');
  await expect(section.locator('#resets-month-title')).toHaveText('Tháng này (2026-11)');

  // The account page.
  const calls: { url: string; body: Record<string, unknown> }[] = [];
  const options = { allowReset: true, allowRelife: true, allowAddStats: true, allowClearStats: true, clearStatsZen: 1_000_000, clearStatsCoins: 0, clearStatsCoinType: 'GoblinPoints', allowDelegated: true };
  let character: Record<string, unknown> = { cls: 0, className: 'Dark Wizard', freePoints: 40, zen: 9_000_000, stats: { str: 18, agi: 18, vit: 15, ene: 330 }, state: { ...CHARACTER(12, 1, 3), level: 350 } };
  const busy: string | undefined = undefined;
  await page.route('**/portal/resets/account', async (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    calls.push({ url: 'account', body });
    expect(route.request().headers()['x-hnmo-session']).toBe('1'); expect(body).not.toHaveProperty('password');
    return route.fulfill({ json: { v: 1, loginName: 'test8', busy, options, characters: [{ ...character, busy }] } });
  });
  await page.route('**/portal/resets/action', async (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    calls.push({ url: 'action', body });
    if (body.action === 'reset') {
      character = { ...character, freePoints: 6500, stats: { str: 18, agi: 18, vit: 15, ene: 30 }, state: { ...CHARACTER(13, 1, 4), level: 1, next: PLAN('LevelTooLow') } };
      return route.fulfill({ json: { v: 1, result: 'Ok', plan: PLAN(), character } });
    }
    if (body.action === 'add-stats') {
      if (Number(body.ene) > 6500) return route.fulfill({ json: { v: 1, result: 'NotEnoughPoints', character } });
      character = { ...character, freePoints: 6500 - Number(body.ene ?? 0) - Number(body.str ?? 0), stats: { str: 18 + Number(body.str ?? 0), agi: 18, vit: 15, ene: 30 + Number(body.ene ?? 0) } };
      return route.fulfill({ json: { v: 1, result: 'Ok', character } });
    }
    if (body.action === 'delegated-on') {
      const state = character.state as Record<string, unknown>;
      character = { ...character, state: { ...state, delegated: { allowed: true, on: true, today: 0, dailyLimit: 0 } } };
      return route.fulfill({ json: { v: 1, result: 'Ok', character } });
    }
    return route.fulfill({ json: { v: 1, result: 'Online', character: { ...character, busy: 'online' } } });
  });
  page.on('dialog', (dialog) => void dialog.accept());
  await page.evaluate(() => { try { localStorage.setItem('hnmo.language', 'en'); } catch { /* none */ } });
  await websiteAccountFixture(page, 'test8', 'test8');
  await page.goto('/account.html#resets');
  const panel = page.locator('#resets');
  await expect(panel.locator('h2')).toHaveText('Resets and stats');
  await page.fill('#accountUser', 'test8'); await page.fill('#accountSecret', 'wrong');
  await page.locator('#accountSignInButton').click();
  await expect(page.locator('#accountMessage')).toContainText('incorrect');
  await signInWebsiteFixture(page, 'test8', 'test8');
  const card = panel.locator('.reset-card[data-character="test8Dw"]');
  await expect(card.locator('.facts')).toContainText('Resets: 12');
  await expect(card.locator('.facts')).toContainText('Relifes: 1');
  await expect(card.locator('.facts')).toContainText('Today: 3/20');
  await expect(card.locator('.facts')).toContainText('This month: 7');
  await expect(card.locator('.needs')).toContainText('Next reset: level 350, 5,000,000 zen. Relife: 50 resets, level 400, 50,000,000 zen.');
  await expect(card.getByRole('button', { name: 'Relife' })).toBeDisabled();

  // A reset (confirmed): the card shows the new state.
  await card.getByRole('button', { name: 'Reset', exact: true }).click();
  await expect(page.locator('#resetsMessage')).toHaveText('test8Dw was reset: reset 13.');
  await expect(card.locator('.facts')).toContainText('Resets: 13');
  await expect(card.locator('.facts')).toContainText('Free points: 6,500');
  await expect(card.getByRole('button', { name: 'Reset', exact: true })).toBeDisabled();
  expect(calls.at(-1)).toEqual({ url: 'action', body: { character: 'test8Dw', action: 'reset' } });

  // Free points to energy.
  await card.locator('input[data-stat="ene"]').fill('6000');
  await card.locator('button[data-action="add-stats"]').click();
  await expect(page.locator('#resetsMessage')).toHaveText('The points were added to test8Dw.');
  await expect(card.locator('.facts')).toContainText('Free points: 500');
  expect(calls.at(-1)?.body).toMatchObject({ action: 'add-stats', ene: 6000, str: 0 });

  // The delegated reset switch.
  await card.locator('input[data-action="delegated"]').check();
  await expect(page.locator('#resetsMessage')).toHaveText('The delegated reset of test8Dw is on.');
  await expect(card.locator('input[data-action="delegated"]')).toBeChecked();
  await panel.scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'test-results/resets-account.png' });

  // Clearing the stats while the account is in the game: refused, the card says so and the buttons are off.
  await card.getByRole('button', { name: /Clear stats/ }).click();
  await expect(page.locator('#resetsMessage')).toHaveText('The account is in the game or offline leveling: leave the game first.');
  await expect(card.locator('.busy')).toHaveText('This account is in the game: leave the game to make changes here.');
  await expect(card.getByRole('button', { name: /Clear stats/ })).toBeDisabled();

  // Vietnamese.
  await page.locator('header [data-account-language="vi"]').click();
  await expect(panel.locator('h2')).toHaveText('Reset và điểm');
  await expect(page.locator('#resetsMessage')).toHaveText('Tài khoản đang trong game hoặc treo máy: hãy thoát game trước.');
  await expect(card.locator('.facts')).toContainText('Reset: 13');
  await expect(card.locator('.busy')).toHaveText('Tài khoản này đang trong game: hãy thoát game để thay đổi ở đây.');

  // The reset system is off on the server: the login is hidden with a note.
  await page.route('**/website/resets', (route) => route.fulfill({ json: { v: 1, enabled: false } }));
  await page.reload();
  await expect(page.locator('#resetsOff')).toBeVisible();
  await expect(page.locator('#resetsLogin')).toBeHidden();
});
