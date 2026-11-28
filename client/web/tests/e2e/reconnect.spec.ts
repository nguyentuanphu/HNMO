// Reconnect after a lost connection (PARITY.md section 1, net/reconnect.ts): the connection to the game server drops
// while in the world, the game's reconnect box shows over it ("Connection lost", the step, the gauge, Cancel), the game
// looks whether the server is back, takes a one-time ticket of the website session and comes back to the same server
// and the same character, with a line in the chat. A logout of the game's own is no reason to reconnect. While the
// server stays away the tries go on, the waits growing to 16 seconds, until the player presses Cancel. Cancel checks
// the website session: valid, the server choice with a new ticket; ended, only the website's login button and a line;
// the website unreachable, a box saying so with Retry. On a 320 px phone the box and its Cancel fit.
//
// The relay is the test's (routeWebSocket): it passes everything through, drops the game connection when asked, and can
// keep the game server away. This server has no website session to issue real tickets: the tickets come from
// page.route, and the relay answers a ticket login with the account's own login (test accounts: password = name).
import { test, expect, type Page, type WebSocketRoute } from '@playwright/test';
import { enterWorld, chatText } from './helpers.js';

type Win = Window & { __game: { hero?: { name: string } | null; mapNumber: number | null; connection?: { closed: boolean; close(): void } }; __viewerReady?: boolean; __beforeDrop?: boolean };
const shots = process.env.HNMO_SHOTS ?? 'test-results';

interface Relay { games: { page: WebSocketRoute; server: WebSocketRoute }[]; drop(): void; ticketLogins: number; gameAway: boolean; refused: number }

/** LoginLongPassword (C3 3C F1 01) for a test account: its name and password Xor3'd, the web client's version and serial. */
function passwordLogin(account: string): Buffer {
  const bytes = Buffer.alloc(60);
  bytes.set([0xc3, 60, 0xf1, 0x01]);
  const keys = [0xfc, 0xcf, 0xab];
  const field = (text: string, at: number, length: number) => {
    const raw = Buffer.alloc(length);
    raw.write(text, 'latin1');
    for (let i = 0; i < length; i++) bytes[at + i] = raw[i] ^ keys[i % 3];
  };
  field(account, 4, 10);
  field(account, 14, 20);
  bytes.write('20404', 38, 'latin1');
  bytes.write('k1Pk2jcET48mxL3b', 43, 'latin1');
  return bytes;
}

/** The test's relay: everything passes; a ticket login (C1 F5 24) is answered with the account's password login. */
async function routeRelay(page: Page): Promise<Relay> {
  const relay: Relay = {
    games: [],
    ticketLogins: 0,
    gameAway: false,
    refused: 0,
    drop() {
      const last = relay.games.at(-1);
      last?.server.close();
      last?.page.close();
    },
  };
  await page.routeWebSocket(/\/relay/, (ws) => {
    // The game server away: its connections close at once (the connect server still answers).
    if (relay.gameAway && /encrypted=1/.test(ws.url())) { relay.refused++; ws.close(); return; }
    const server = ws.connectToServer();
    if (/encrypted=1/.test(ws.url())) relay.games.push({ page: ws, server });
    ws.onMessage((message) => {
      if (typeof message === 'string') { server.send(message); return; }
      const bytes = Buffer.from(message);
      if (bytes[0] === 0xc1 && bytes[2] === 0xf5 && bytes[3] === 0x24) {
        relay.ticketLogins++;
        server.send(passwordLogin(bytes.subarray(4, 14).toString('latin1').replace(/\0+$/, '')));
        return;
      }
      server.send(bytes);
    });
    server.onMessage((message) => ws.send(message));
    server.onClose(() => ws.close());
    ws.onClose(() => server.close());
  });
  return relay;
}

/** Enters as a player does: with the website's ticket (no login form), the first server and character. */
async function enterWithTicket(page: Page, account: string): Promise<void> {
  await page.goto(`/index.html#ticket=entry-fixture&account=${account}`);
  await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 120_000 });
  if (await page.isVisible('#servers .choice >> nth=0')) await page.click('#servers .choice >> nth=0');
  await page.waitForSelector('#characters .choice', { timeout: 120_000 });
  await page.click('#characters .choice >> nth=0');
  await page.waitForFunction(() => (window as unknown as Win).__viewerReady, null, { timeout: 180_000 });
}

/** Drops the game connection with the game server kept away, and waits for the box to have tried three times. */
async function dropAndWait(page: Page, relay: Relay): Promise<void> {
  relay.gameAway = true;
  relay.drop();
  await expect(page.locator('.reconnect-dialog #panel')).toBeVisible({ timeout: 15_000 });
  await expect.poll(() => relay.refused, { timeout: 60_000 }).toBeGreaterThanOrEqual(3);
  await expect(page.locator('.reconnect-dialog #rc_countdown')).toHaveText(/Retrying in \d+ seconds\.\.\./, { timeout: 20_000 });
}
test('a dropped connection shows the reconnect box and comes back to the same server and character', async ({ page }) => {
  test.setTimeout(8 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  const relay = await routeRelay(page);
  let tickets = 0;
  await page.route('**/portal/login-ticket', (route) => {
    tickets++;
    expect(route.request().headers()['x-hnmo-session']).toBe('1');
    return route.fulfill({ json: { loginName: 'test2', ticket: 'reconnect-fixture-ticket', expiresInSeconds: 60 } });
  });
  await enterWorld(page, 'test2');
  const hero = await page.evaluate(() => (window as unknown as Win).__game.hero?.name ?? '');
  expect(hero).not.toBe('');

  // The network drops the game connection (the relay closes both ends).
  await page.evaluate(() => { (window as unknown as Win).__beforeDrop = true; });
  relay.drop();
  const box = page.locator('.reconnect-dialog');
  await expect(box.locator('#panel')).toBeVisible({ timeout: 15_000 });
  await expect(box.locator('#rc_title')).toHaveText('Connection lost');
  await page.screenshot({ path: `${shots}/reconnect-box.png` });

  // Back in the world after the reload: the same character, the line in the chat, no box.
  await page.waitForFunction((name) => {
    const w = window as unknown as Win;
    return !w.__beforeDrop && w.__viewerReady === true && w.__game.hero?.name === name;
  }, hero, { timeout: 4 * 60_000 });
  expect(tickets).toBeGreaterThan(0);
  expect(relay.ticketLogins).toBeGreaterThan(0);
  await expect(page.locator('.reconnect-dialog')).toHaveCount(0);
  await expect.poll(() => chatText(page), { timeout: 30_000 }).toContain('Reconnected: the connection to the server was lost.');
  // The ticket and the plan are gone from the address and the tab's storage.
  expect(await page.evaluate(() => location.hash)).toBe('');
  expect(await page.evaluate(() => sessionStorage.getItem('hnmo.reconnect'))).toBeNull();
  await page.screenshot({ path: `${shots}/reconnect-back-in-world.png` });
  expect(errors).toEqual([]);
});

test('the game closing its own connection does not reconnect', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  await routeRelay(page);
  await enterWorld(page, 'test2');
  await page.evaluate(() => (window as unknown as Win).__game.connection?.close());
  await page.waitForTimeout(3000);
  await expect(page.locator('.reconnect-dialog')).toHaveCount(0);
});

test('the tries go on until Cancel; with a valid website session Cancel lands on the server choice; the box fits 320 px', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const relay = await routeRelay(page);
  let tickets = 0;
  await page.route('**/portal/login-ticket', (route) => { tickets++; return route.fulfill({ json: { loginName: 'test2', ticket: 'server-choice-fixture', expiresInSeconds: 60 } }); });
  await enterWithTicket(page, 'test2');
  tickets = 0;
  await page.setViewportSize({ width: 320, height: 568 });
  await page.waitForTimeout(500);

  await dropAndWait(page, relay);
  const box = page.locator('.reconnect-dialog');
  await expect(box.locator('#rc_title')).toHaveText('Connection lost');
  expect(tickets).toBe(0); // no ticket while the server is away
  for (const part of ['#panel', '#cancel']) {
    const rect = await box.locator(part).boundingBox();
    expect(rect, part).not.toBeNull();
    expect(rect!.x).toBeGreaterThanOrEqual(0);
    expect(rect!.x + rect!.width).toBeLessThanOrEqual(320);
    expect(rect!.y + rect!.height).toBeLessThanOrEqual(568);
  }
  await page.screenshot({ path: `${shots}/reconnect-box-320.png` });

  await Promise.all([page.waitForEvent('load', { timeout: 30_000 }), box.locator('#cancel').click()]);
  expect(tickets).toBe(1);
  await page.waitForSelector('#servers .choice', { state: 'attached', timeout: 60_000 });
  await expect(page.locator('.reconnect-dialog')).toHaveCount(0);
  await expect(page.locator('#username')).toBeHidden();
  expect(await page.evaluate(() => (window as unknown as Win).__viewerReady ?? false)).toBe(false);
  expect(await page.evaluate(() => sessionStorage.getItem('hnmo.reconnect'))).toBeNull();
  await page.setViewportSize({ width: 1280, height: 720 });
  await expect(page.locator('body.stage-world')).toHaveCount(1, { timeout: 120_000 });
  await expect(page.locator('#stageServers .server-row:not(.server-row-sizer)').first()).toBeVisible({ timeout: 30_000 });
  await page.waitForTimeout(1500);
  await page.screenshot({ path: `${shots}/reconnect-cancel-server-choice.png` });
});

test('Cancel with the website session ended shows only the website login button and a line', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const relay = await routeRelay(page);
  let ended = false;
  await page.route('**/portal/login-ticket', (route) => (ended
    ? route.fulfill({ status: 401, json: { error: 'login' } })
    : route.fulfill({ json: { loginName: 'test2', ticket: 'entry-fixture', expiresInSeconds: 60 } })));
  await enterWithTicket(page, 'test2');
  ended = true;
  await dropAndWait(page, relay);
  await Promise.all([page.waitForEvent('load', { timeout: 30_000 }), page.locator('.reconnect-dialog #cancel').click()]);
  await expect(page.locator('#websiteLoginButton')).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('#loginError')).toContainText('Your website session has ended');
  for (const hidden of ['#username', '#password', '#loginForm button.primary', '#savedAccounts']) await expect(page.locator(hidden), hidden).toBeHidden();
  expect(await page.evaluate(() => location.search)).toBe('');
  await page.screenshot({ path: `${shots}/reconnect-cancel-session-ended.png` });
});

test('Cancel with the website unreachable says so and Retry asks again', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const relay = await routeRelay(page);
  let reachable = true;
  await page.route('**/portal/login-ticket', (route) => (reachable
    ? route.fulfill({ json: { loginName: 'test2', ticket: 'entry-fixture', expiresInSeconds: 60 } })
    : route.abort('failed')));
  await enterWithTicket(page, 'test2');
  reachable = false;
  await dropAndWait(page, relay);
  await page.locator('.reconnect-dialog #cancel').click();
  const unreachable = page.locator('.confirm-dialog.reconnect-unreachable');
  await expect(unreachable.locator('#gcd_btn_primary')).toBeVisible({ timeout: 30_000 });
  await expect(unreachable).toContainText("Can't reach the server.");
  await expect(unreachable).not.toContainText('session');
  await page.screenshot({ path: `${shots}/reconnect-cancel-unreachable.png` });
  // Still unreachable: the box comes back. Then reachable: the server choice.
  await unreachable.locator('#gcd_btn_primary').click();
  await expect(unreachable.locator('#gcd_btn_primary')).toBeVisible({ timeout: 30_000 });
  reachable = true;
  await Promise.all([page.waitForEvent('load', { timeout: 30_000 }), unreachable.locator('#gcd_btn_primary').click()]);
  await page.waitForSelector('#servers .choice', { state: 'attached', timeout: 60_000 });
  expect(await page.evaluate(() => (window as unknown as Win).__viewerReady ?? false)).toBe(false);
});