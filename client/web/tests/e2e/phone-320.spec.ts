// A phone under 360 px wide (320 x 568): the server choice window, the character stage's labels and buttons, and the
// create and delete dialogs fit the screen with no horizontal scroll, and their buttons can be reached and pressed.
// (The reconnect boxes at 320 px: reconnect.spec.ts.)
import { test, expect, type Locator, type Page } from '@playwright/test';

type Win = Window & { __game: { characterStage?: { slots: number[]; selectedSlot: number | null } | null } };
const shots = process.env.HNMO_SHOTS ?? 'test-results';
const WIDTH = 320, HEIGHT = 568;

test.use({ viewport: { width: WIDTH, height: HEIGHT } });

async function noSideScroll(page: Page): Promise<void> {
  expect(await page.evaluate(() => Math.max(document.documentElement.scrollWidth, document.body.scrollWidth))).toBeLessThanOrEqual(WIDTH);
}

/** On the screen, whole, and big enough to press with a finger. */
async function reachable(target: Locator, name: string, minimum = 24): Promise<void> {
  const rect = await target.boundingBox();
  expect(rect, name).not.toBeNull();
  expect(rect!.x, `${name} left`).toBeGreaterThanOrEqual(0);
  expect(rect!.y, `${name} top`).toBeGreaterThanOrEqual(0);
  expect(rect!.x + rect!.width, `${name} right`).toBeLessThanOrEqual(WIDTH + 0.5);
  expect(rect!.y + rect!.height, `${name} bottom`).toBeLessThanOrEqual(HEIGHT + 0.5);
  expect(Math.min(rect!.width, rect!.height), `${name} size`).toBeGreaterThanOrEqual(minimum);
}

test('320 px: the server choice, the character stage and its create and delete dialogs fit and can be used', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/index.html');
  await page.fill('#username', 'test3');
  await page.fill('#password', 'test3');
  await page.click('button.primary');
  await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 60_000 });

  if (await page.isVisible('#servers .choice >> nth=0')) {
    await expect(page.locator('body.stage-world')).toHaveCount(1, { timeout: 120_000 });
    const rows = page.locator('#stageServers .server-row:not(.server-row-sizer)');
    await expect(rows.first()).toBeVisible({ timeout: 30_000 });
    await page.waitForTimeout(800);
    const panel = await page.locator('#stageServers #panel').boundingBox();
    expect(panel).not.toBeNull();
    expect(panel!.x).toBeGreaterThanOrEqual(0);
    expect(panel!.x + panel!.width).toBeLessThanOrEqual(WIDTH);
    for (let i = 0; i < await rows.count(); i++) await reachable(rows.nth(i), `server row ${i}`);
    const groups = page.locator('#stageServers .server-group-btn');
    for (let i = 0; i < await groups.count(); i++) await reachable(groups.nth(i), `group ${i}`);
    await noSideScroll(page);
    await page.screenshot({ path: `${shots}/phone-320-server-choice.png` });
    await rows.first().click();
  }

  // The character stage: every character in view with its label on the screen, the four plates reachable.
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
  await expect(page.locator('body.character-stage')).toHaveCount(1, { timeout: 120_000 });
  await expect(page.locator('#btn_create')).toBeVisible({ timeout: 30_000 });
  await page.waitForTimeout(1500);
  const slots = await page.evaluate(() => (window as unknown as Win).__game.characterStage?.slots ?? []);
  expect(slots.length).toBeGreaterThan(0);
  const labels = page.locator('.stage-label');
  await expect(labels).toHaveCount(slots.length);
  for (let i = 0; i < slots.length; i++) {
    await expect(labels.nth(i)).toBeVisible();
    await reachable(labels.nth(i), `label ${i}`, 10);
  }
  for (const id of ['#btn_create', '#btn_menu', '#btn_connect', '#btn_delete']) await reachable(page.locator(id), id, 30);
  await noSideScroll(page);
  await page.screenshot({ path: `${shots}/phone-320-character-stage.png` });

  // Create: the form, its fields and buttons on the screen.
  if (await page.locator('#characters .choice[data-create]').count()) {
    await page.locator('#btn_create').click();
    await expect(page.locator('#newName')).toBeVisible();
    await page.waitForTimeout(800);
    for (const id of ['#newName', '#randomName', '#newClass', '#createIt', '#cancelIt']) await reachable(page.locator(id), id);
    await noSideScroll(page);
    await page.screenshot({ path: `${shots}/phone-320-create.png` });
    await page.click('#cancelIt');
    await expect(page.locator('#btn_create')).toBeVisible({ timeout: 30_000 });
  }

  // Delete: the question, then the code, and their buttons on the screen; then cancelled.
  const picked = await page.evaluate(() => (window as unknown as Win).__game.characterStage?.selectedSlot ?? null);
  expect(picked).not.toBeNull();
  await page.locator('#btn_delete').click();
  await expect(page.locator('#deleteQuestion')).toBeVisible();
  for (const id of ['#deleteYes', '#deleteNo']) await reachable(page.locator(id), id);
  await noSideScroll(page);
  await page.click('#deleteYes');
  await expect(page.locator('#deleteCode')).toBeVisible();
  for (const id of ['#deleteCode', '#deleteOk', '#deleteCancel']) await reachable(page.locator(id), id);
  await noSideScroll(page);
  await page.screenshot({ path: `${shots}/phone-320-delete.png` });
  await page.click('#deleteCancel');
  await expect(page.locator('#btn_delete')).toBeVisible({ timeout: 30_000 });
  expect(errors).toEqual([]);
});
