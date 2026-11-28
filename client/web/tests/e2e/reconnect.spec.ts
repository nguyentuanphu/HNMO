// Reconnect after a lost connection (PARITY.md section 1, net/reconnect.ts): the connection to the game server drops
// while in the world, the game's reconnect box shows over it ("Connection lost", the step, the gauge, Cancel), the game
// looks whether the server is back, takes a one-time ticket of the website session and comes back to the same server
// and the same character, with a line in the chat. A logout of the game's own is no reason to reconnect. When the
// tries can't work (no website session), the game's box offers Retry and Back to server choice; on a 320 px phone both
// boxes fit.
//
// The relay is the test's (routeWebSocket): it passes everything through, drops the game connection when asked, and,
// since this server has no website session to issue real tickets, answers the ticket login after the reload with the
// account's own first login of the test (the bytes the page sent then).
import { test, expect, type Page, type WebSocketRoute } from '@playwright/test';
import { enterWorld, chatText } from './helpers.js';

type Win = Window & { __game: { hero?: { name: string } | null; mapNumber: number | null; connection?: { closed: boolean; close(): void } }; __viewerReady?: boolean; __beforeDrop?: boolean };
const shots = process.env.HNMO_SHOTS ?? 'test-results';

interface Relay { games: { page: WebSocketRoute; server: WebSocketRoute }[]; drop(): void; ticketLogins: number }

/** The test's relay: everything passes; the ticket login is answered with the first password login's bytes. */
async function routeRelay(page: Page): Promise<Relay> {
  let passwordLogin: Buffer | null = null;
  const relay: Relay = {
    games: [],
    ticketLogins: 0,
    drop() {
      const last = relay.games.at(-1);
      last?.server.close();
      last?.page.close();
    },
  };
  await page.routeWebSocket(/\/relay/, (ws) => {
    const server = ws.connectToServer();
    if (/encrypted=1/.test(ws.url())) relay.games.push({ page: ws, server });
    ws.onMessage((message) => {
      if (typeof message === 'string') { server.send(message); return; }
      const bytes = Buffer.from(message);
      // LoginLongPassword (C3 F1 01): kept. HnmoTicketLogin (C1 F5 24): the kept login instead.
      if (bytes[0] === 0xc3 && bytes[2] === 0xf1 && bytes[3] === 0x01 && !passwordLogin) passwordLogin = Buffer.from(bytes);
      if (bytes[0] === 0xc1 && bytes[2] === 0xf5 && bytes[3] === 0x24 && passwordLogin) { relay.ticketLogins++; server.send(passwordLogin); return; }
      server.send(bytes);
    });
    server.onMessage((message) => ws.send(message));
    server.onClose(() => ws.close());
    ws.onClose(() => server.close());
  });
  return relay;
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

test('without a website session the game box offers Retry and Back to server choice; both boxes fit a 320 px phone', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const relay = await routeRelay(page);
  // No website session, and none can be made here: the ticket is refused (after a moment, to see the box).
  let release: () => void = () => {};
  const answered = new Promise<void>((resolve) => { release = resolve; });
  await page.route('**/portal/login-ticket', async (route) => { await answered; await route.fulfill({ status: 401, json: { error: 'login' } }); });
  await page.route('**/portal/session', (route) => route.fulfill({ status: 503, json: { error: 'setup' } }));
  await enterWorld(page, 'test2');
  await page.setViewportSize({ width: 320, height: 568 });
  await page.waitForTimeout(500);

  relay.drop();
  const box = page.locator('.reconnect-dialog');
  await expect(box.locator('#panel')).toBeVisible({ timeout: 15_000 });
  await expect(box.locator('#rc_step')).toHaveText('Connecting to the server', { timeout: 30_000 });
  for (const part of ['#panel', '#cancel']) {
    const rect = await box.locator(part).boundingBox();
    expect(rect, part).not.toBeNull();
    expect(rect!.x).toBeGreaterThanOrEqual(0);
    expect(rect!.x + rect!.width).toBeLessThanOrEqual(320);
    expect(rect!.y + rect!.height).toBeLessThanOrEqual(568);
  }
  await page.screenshot({ path: `${shots}/reconnect-box-320.png` });

  release();
  // The game's menu box: Retry, and Back to server choice.
  const failed = page.locator('.choose-dialog.reconnect-failed');
  const retry = failed.locator('.reconnect-retry');
  const back = failed.locator('.reconnect-back');
  await expect(retry).toBeVisible({ timeout: 30_000 });
  await expect(box).toHaveCount(0);
  await expect(failed).toContainText('Connection lost');
  await expect(back).toContainText('Back to server choice');
  await expect(back).toBeVisible();
  for (const button of [retry, back]) {
    const rect = await button.boundingBox();
    expect(rect).not.toBeNull();
    expect(rect!.x).toBeGreaterThanOrEqual(0);
    expect(rect!.x + rect!.width).toBeLessThanOrEqual(320);
    expect(rect!.y + rect!.height).toBeLessThanOrEqual(568);
  }
  await page.screenshot({ path: `${shots}/reconnect-failed-320.png` });

  // Back to server choice: this page logged in with a password, so its login comes back.
  await Promise.all([page.waitForEvent('load', { timeout: 30_000 }), back.click()]);
  await expect(page.locator('#loginForm fieldset')).toBeVisible({ timeout: 30_000 });
  expect(await page.evaluate(() => (window as unknown as Win).__viewerReady ?? false)).toBe(false);
});
