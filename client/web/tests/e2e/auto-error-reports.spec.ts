// Uses the isolated demo server and bridge with developer forwarding disabled.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld } from './helpers.js';

const ADMIN = process.env.ADMIN_URL ?? 'http://127.0.0.1:8095';

async function autoCount(admin: Page, fingerprint: string): Promise<number> {
  await admin.goto(`${ADMIN}/bug-reports`);
  await expect(async () => {
    await admin.locator('#bug-tab-auto').click();
    await expect(admin.locator('#auto-settings')).toBeVisible({ timeout: 1_000 });
  }).toPass({ timeout: 30_000 });
  await admin.locator('#auto-filter').selectOption('all');
  const cells = admin.locator('#auto-table tr.auto-group')
    .filter({ has: admin.getByText(fingerprint, { exact: true }).and(admin.locator('.auto-fingerprint')) })
    .locator('.auto-count');
  const counts = await cells.allTextContents();
  return counts.reduce((total, count) => total + Number(count), 0);
}

function watchPackets(page: Page): Record<string, unknown>[] {
  const reports: Record<string, unknown>[] = [];
  page.on('websocket', ws => ws.on('framesent', ({ payload }) => {
    if (typeof payload === 'string' || payload[0] !== 0xc2 || payload[3] !== 0xf5 || payload[4] !== 0x60) return;
    reports.push(JSON.parse(payload.subarray(5).toString('utf8')));
  }));
  return reports;
}

test('groups one coarse diagnostic across page loads, even when the raw error changes', async ({ page, browser }) => {
  test.setTimeout(5 * 60_000);
  const reports = watchPackets(page);
  const admin = await browser.newPage();
  const signature = 'error|main.js|45991';
  const before = await autoCount(admin, signature);
  await enterWorld(page, 'test2');
  const raise = (message: string) => page.evaluate(text => {
    window.dispatchEvent(new ErrorEvent('error', { message: text, filename: '/js/main.js', lineno: 45991 }));
  }, message);

  await raise('person@example.invalid');
  await raise('synthetic API token');
  await expect.poll(() => reports.filter(r => r.fingerprint === signature).length).toBe(1);
  expect(JSON.stringify(reports)).not.toContain('example.invalid');
  await enterWorld(page, 'test2');
  await raise('different fixture message');
  await expect.poll(() => autoCount(admin, signature), { timeout: 30_000 }).toBe(before + 2);
  await admin.close();
});

test('reports loss of a real WebGL context to the host', async ({ page, browser }) => {
  test.setTimeout(5 * 60_000);
  const admin = await browser.newPage();
  const before = await autoCount(admin, 'webgl');
  await enterWorld(page, 'test2');
  await page.evaluate(async () => {
    const canvas = document.createElement('canvas');
    document.body.append(canvas);
    const extension = canvas.getContext('webgl2')?.getExtension('WEBGL_lose_context');
    if (!extension) throw new Error('Chromium needs WEBGL_lose_context for this test.');
    try {
      const lost = new Promise<void>(resolve => canvas.addEventListener('webglcontextlost', () => resolve(), { once: true }));
      extension.loseContext();
      await lost;
    } finally { canvas.remove(); }
  });

  await expect.poll(() => autoCount(admin, 'webgl'), { timeout: 30_000 }).toBe(before + 1);
  await admin.close();
});

test('queues an unexpected game WebSocket close and flushes it after reauthentication', async ({ page, browser }) => {
  test.setTimeout(5 * 60_000);
  const reports = watchPackets(page);
  const admin = await browser.newPage();
  const before = await autoCount(admin, 'disconnect');
  await enterWorld(page, 'test2');
  await page.evaluate(async () => {
    const { socket } = (window as unknown as { __game: { connection: { socket: WebSocket } } }).__game.connection;
    const closed = new Promise<void>(resolve => socket.addEventListener('close', () => resolve(), { once: true }));
    socket.close(4001, 'isolated fixture');
    await closed;
  });

  expect(reports.filter(r => r.kind === 'disconnect')).toHaveLength(0);
  expect(await autoCount(admin, 'disconnect')).toBe(before);
  await enterWorld(page, 'test2');
  await expect.poll(() => autoCount(admin, 'disconnect'), { timeout: 30_000 }).toBe(before + 1);
  expect(reports.filter(r => r.kind === 'disconnect')).toHaveLength(1);
  await admin.close();
});
