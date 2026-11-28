// The server choice over world 74 (the desktop's login scene: CServerSelWin over the scene's world, PARITY.md section 1):
// no group open at first, a click on a group shows its servers with their load bars, a click on a server picks it, and
// the character choice goes on on the same stage, without loading world 74 again. The plain list's buttons stay as
// invisible hooks (the other specs click them).
import { test, expect } from '@playwright/test';

type Win = Window & { __game: { characterStage?: object | null; serverSelect?: object | null }; __serverStage?: object | null };
const shots = process.env.HNMO_SHOTS ?? 'test-results';

test('the server choice shows over world 74 and leads into the character stage without loading the world again', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('console', (message) => { if (/server choice|Character stage/.test(message.text())) errors.push(message.text()); });
  const worldFiles: string[] = [];
  page.on('request', (request) => { if (/\/World75\//i.test(request.url())) worldFiles.push(request.url()); });

  await page.goto('/index.html');
  await page.fill('#username', 'test9');
  await page.fill('#password', 'test9');
  await page.click('button.primary');
  await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 60_000 });
  test.skip(!(await page.isVisible('#servers .choice >> nth=0')), 'the server lists one game server: no choice to make');

  // World 74 comes up behind the choice, and the desktop's window over it: the groups, none open yet.
  await expect(page.locator('body.stage-world')).toHaveCount(1, { timeout: 120_000 });
  const groups = page.locator('#stageServers .server-group-btn');
  await expect(groups.first()).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('#stageServers #server_col')).toBeHidden();
  // The plain list is still there, as hooks.
  expect((await page.locator('#servers .choice').first().boundingBox())?.width ?? 0).toBeLessThanOrEqual(2);
  await expect(page.locator('#servers .seasons-title')).toBeHidden();
  await page.waitForTimeout(1500);
  await page.screenshot({ path: `${shots}/server-choice-groups.png` });

  // A group shows its servers, each with its ten-segment load bar, and the group's description.
  await groups.first().click();
  await expect(groups.first()).toHaveClass(/checked/);
  const rows = page.locator('#stageServers .server-row:not(.server-row-sizer)');
  await expect(rows.first()).toBeVisible();
  expect(await rows.count()).toBeGreaterThan(0);
  await expect(rows.first().locator('.server-gauge-segment')).toHaveCount(10);
  await expect(page.locator('#stageServers #desc_panel')).not.toBeEmpty();
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
