// The server choice over world 74 (the desktop's login scene: CServerSelWin over the scene's world, PARITY.md section 1):
// the only group open at once, several closed until one is clicked (as the desktop); a group shows its servers with their
// load bars and the host's text for it (admin panel, Worlds) or the game's own; a click on a server picks it, a full one
// says so in the game's OK box; the character choice goes on on the same stage, without loading world 74 again. The plain
// list's buttons stay as invisible hooks (the other specs click them).
import { test, expect, type Page } from '@playwright/test';

type Win = Window & { __game: { characterStage?: object | null; serverSelect?: object | null }; __serverStage?: object | null };
const shots = process.env.HNMO_SHOTS ?? 'test-results';

async function logIn(page: Page, account: string): Promise<void> {
  await page.goto('/index.html');
  await page.fill('#username', account);
  await page.fill('#password', account);
  await page.click('button.primary');
  await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 60_000 });
  test.skip(!(await page.isVisible('#servers .choice >> nth=0')), 'the server lists one game server: no choice to make');
}

test('the server choice shows over world 74 and leads into the character stage without loading the world again', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  // No website worlds: one group, the season's column.
  await page.route('**/website/data**', (route) => route.fulfill({ status: 503 }));
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('console', (message) => { if (/server choice|Character stage/.test(message.text())) errors.push(message.text()); });
  const worldFiles: string[] = [];
  page.on('request', (request) => { if (/\/World75\//i.test(request.url())) worldFiles.push(request.url()); });

  await logIn(page, 'test9');

  // World 74 comes up behind the choice, and the desktop's window over it. Without the website's worlds there is one
  // group (the season's column), and the only group is open at once (owner 2026-11-28).
  await expect(page.locator('body.stage-world')).toHaveCount(1, { timeout: 120_000 });
  const groups = page.locator('#stageServers .server-group-btn');
  await expect(groups.first()).toBeVisible({ timeout: 30_000 });
  await expect(groups).toHaveCount(1);
  await expect(groups.first()).toHaveClass(/checked/);
  await expect(page.locator('#stageServers #server_col')).toBeVisible();
  // The plain list is still there, as hooks.
  expect((await page.locator('#servers .choice').first().boundingBox())?.width ?? 0).toBeLessThanOrEqual(2);
  await expect(page.locator('#servers .seasons-title')).toBeHidden();

  // The group's servers, each with its ten-segment load bar, and the game's own text (no host text): season and hint.
  const rows = page.locator('#stageServers .server-row:not(.server-row-sizer)');
  await expect(rows.first()).toBeVisible();
  expect(await rows.count()).toBeGreaterThan(0);
  await expect(rows.first().locator('.server-gauge-segment')).toHaveCount(10);
  await expect(page.locator('#stageServers #desc_panel')).toContainText('Season 6.3: The classic worlds');
  await page.screenshot({ path: `${shots}/server-choice-servers.png` });
  // On a phone the window fits the screen.
  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(800);
  const panel = await page.locator('#stageServers #panel').boundingBox();
  expect(panel).not.toBeNull();
  expect(panel!.x).toBeGreaterThanOrEqual(0);
  expect(panel!.x + panel!.width).toBeLessThanOrEqual(390);
  await page.screenshot({ path: `${shots}/server-choice-phone.png` });
  await page.setViewportSize({ width: 1280, height: 720 });

  // A server is picked: the window goes, and the characters come onto the same stage.
  const filesBefore = worldFiles.length;
  expect(filesBefore).toBeGreaterThan(0);
  await page.evaluate(() => { const w = window as unknown as Win; w.__serverStage = w.__game.characterStage ?? null; });
  expect(await page.evaluate(() => !!(window as unknown as Win).__serverStage)).toBe(true);
  await rows.first().click();
  await expect(page.locator('#stageServers')).toHaveCount(0, { timeout: 30_000 });
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
  await expect(page.locator('body.character-stage')).toHaveCount(1, { timeout: 120_000 });
  expect(await page.evaluate(() => { const w = window as unknown as Win; return w.__game.characterStage === w.__serverStage; })).toBe(true);
  expect(worldFiles.length).toBe(filesBefore);
  await expect(page.locator('#btn_create')).toBeVisible({ timeout: 30_000 });
  await page.waitForTimeout(1500);
  await page.screenshot({ path: `${shots}/server-choice-then-characters.png` });
  expect(errors).toEqual([]);
});

test('several worlds stay closed until clicked, each shows the host text or the game text, and a full server says so in the game OK box',async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  // Two worlds of the website's data: the first with the host's text (admin panel, Worlds: "Server choice text"), the
  // second without (the game's own text).
  await page.route('**/website/data**', (route) => route.fulfill({ json: { worlds: [
    { worldId: 'origin', season: 's6', nameEn: 'Origin', nameVi: 'Khởi Nguyên', isOpen: true, servers: [{ id: 0 }, { id: 1 }],
      serverChoiceTextEn: 'Fresh start, friendly rates. Weekly Castle Siege.', serverChoiceTextVi: 'Khởi đầu mới, tỉ lệ thân thiện.' },
    { worldId: 'legend', season: 's6', nameEn: 'Legend', nameVi: 'Huyền Thoại', isOpen: true, servers: [{ id: 2 }], serverChoiceTextEn: '', serverChoiceTextVi: '' },
  ] } }));
  // The connect server reports the second server full (ServerListResponse C2 F4 06: count, then id LE, load per 4 bytes).
  await page.routeWebSocket(/\/relay/, (ws) => {
    const server = ws.connectToServer();
    ws.onMessage((message) => server.send(message));
    server.onMessage((message) => {
      if (typeof message === 'string') { ws.send(message); return; }
      const bytes = new Uint8Array(message);
      if (bytes[0] === 0xc2 && bytes[3] === 0xf4 && bytes[4] === 0x06) {
        const count = (bytes[5] << 8) | bytes[6];
        for (let i = 0; i < count; i++) {
          const at = 7 + i * 4;
          if ((bytes[at] | (bytes[at + 1] << 8)) === 1) bytes[at + 2] = 100;
        }
      }
      ws.send(Buffer.from(bytes));
    });
  });

  await logIn(page, 'test1');
  await expect(page.locator('body.stage-world')).toHaveCount(1, { timeout: 120_000 });
  const groups = page.locator('#stageServers .server-group-btn');
  await expect(groups).toHaveCount(2, { timeout: 30_000 });
  // As the desktop: with several groups none is open yet.
  await expect(page.locator('#stageServers #server_col')).toBeHidden();
  await page.waitForTimeout(1500);
  await page.screenshot({ path: `${shots}/server-choice-groups.png` });

  // The second world has no host text: the game's own.
  await groups.nth(1).click();
  await expect(page.locator('#stageServers #desc_panel')).toContainText('Season 6.3: The classic worlds');
  // The first: the host's text, under the worlds.
  await groups.nth(0).click();
  await expect(groups.nth(0)).toHaveClass(/checked/);
  await expect(page.locator('#stageServers #desc_panel')).toHaveText('Fresh start, friendly rates. Weekly Castle Siege.');
  const rows = page.locator('#stageServers .server-row:not(.server-row-sizer)');
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(1)).toContainText('(Full)');
  await page.waitForTimeout(500);
  await page.screenshot({ path: `${shots}/server-choice-season-text.png` });

  // The full server: the game's OK box, and the choice stays.
  await rows.nth(1).click();
  const box = page.locator('.confirm-dialog.server-busy');
  await expect(box).toBeVisible({ timeout: 30_000 });
  await expect(box).toContainText('The server is full.');
  await page.waitForTimeout(500);
  await page.screenshot({ path: `${shots}/server-choice-full-popup.png` });
  await box.locator('#gcd_btn_primary').click();
  await expect(box).toBeHidden();
  await expect(page.locator('#stageServers')).toHaveCount(1);
  await expect(page.locator('#characters .choice')).toHaveCount(0);

  // An open server is picked: on to the characters.
  await rows.nth(0).click();
  await expect(page.locator('#stageServers')).toHaveCount(0, { timeout: 30_000 });
  await expect(page.locator('body.character-stage')).toHaveCount(1, { timeout: 120_000 });
  expect(errors).toEqual([]);
});
