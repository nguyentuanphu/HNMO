// The plain screens (game options, Display: "Plain server and character screens"; ui/settingsWindow.ts): for weak
// devices the server and character choice are the plain lists, and World 74 isn't loaded at all. Saved in the browser
// (localStorage hnmo.settings), off by default; switched on while the 3D scene is up, the lists come at once.
import { test, expect, type Page } from '@playwright/test';

type Win = Window & { __game: { characterStage?: object | null; mapNumber: number | null }; __viewerReady?: boolean };
const shots = process.env.HNMO_SHOTS ?? 'test-results';

async function logIn(page: Page, account: string): Promise<void> {
  await page.goto('/index.html');
  await page.fill('#username', account);
  await page.fill('#password', account);
  await page.click('button.primary');
  await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 60_000 });
}

test('with the plain screens on, the server and character choice are plain lists and World 74 never loads', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  await page.addInitScript(() => localStorage.setItem('hnmo.settings', JSON.stringify({ plainScreens: true })));
  const worldFiles: string[] = [];
  page.on('request', (request) => { if (/\/(World75|Object75)\//i.test(request.url())) worldFiles.push(request.url()); });
  await logIn(page, 'test5');
  if (await page.isVisible('#servers .choice >> nth=0')) {
    // The plain list, as a list: real buttons, no scene and no server_select window over it.
    await page.waitForTimeout(1500);
    const button = await page.locator('#servers .choice').first().boundingBox();
    expect(button?.width ?? 0).toBeGreaterThan(100);
    await expect(page.locator('#stageServers')).toHaveCount(0);
    await expect(page.locator('body.stage-world')).toHaveCount(0);
    await page.screenshot({ path: `${shots}/plain-screens-servers.png` });
    await page.click('#servers .choice >> nth=0');
  }
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
  await page.waitForTimeout(1500);
  expect((await page.locator('#characters .choice').first().boundingBox())?.width ?? 0).toBeGreaterThan(100);
  await expect(page.locator('body.character-stage')).toHaveCount(0);
  await expect(page.locator('#stagePlates')).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as Win).__game.characterStage ?? null)).toBeNull();
  await page.screenshot({ path: `${shots}/plain-screens-characters.png` });
  expect(worldFiles).toEqual([]);
  await page.click('#characters .choice >> nth=0');
  await page.waitForFunction(() => (window as unknown as Win).__viewerReady, null, { timeout: 180_000 });
  expect(await page.evaluate(() => (window as unknown as Win).__game.mapNumber)).not.toBeNull();
  expect(worldFiles).toEqual([]);
});

test('the setting is off by default, sits in the game options, is saved, and takes the scene away at once', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  await logIn(page, 'test5');
  if (await page.isVisible('#servers .choice >> nth=0')) await page.click('#servers .choice >> nth=0');
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
  await expect(page.locator('body.character-stage')).toHaveCount(1, { timeout: 120_000 });
  await expect(page.locator('#btn_menu')).toBeVisible({ timeout: 30_000 });
  // The plates' Menu opens the game options; the Display tab has the row, unticked.
  await page.locator('#btn_menu').click();
  const row = page.locator('#hnmo_plain_screens');
  await expect(row).toBeVisible({ timeout: 30_000 });
  await expect(row).toContainText('Plain server and character screens');
  await expect(row.locator('.checkbox-box')).not.toHaveClass(/checked/);
  expect(await page.evaluate(() => JSON.parse(localStorage.getItem('hnmo.settings') ?? '{}').plainScreens ?? false)).toBe(false);
  await page.screenshot({ path: `${shots}/plain-screens-setting.png` });

  await row.click();
  await expect(row.locator('.checkbox-box')).toHaveClass(/checked/);
  expect(await page.evaluate(() => JSON.parse(localStorage.getItem('hnmo.settings') ?? '{}').plainScreens)).toBe(true);
  // The scene goes and the plain list stays, usable.
  await expect(page.locator('body.stage-world')).toHaveCount(0, { timeout: 30_000 });
  await expect(page.locator('#stagePlates')).toHaveCount(0);
  await page.keyboard.press('Escape');
  await page.waitForTimeout(800);
  expect((await page.locator('#characters .choice').first().boundingBox())?.width ?? 0).toBeGreaterThan(100);
  await page.screenshot({ path: `${shots}/plain-screens-switched-on.png` });
  await page.click('#characters .choice >> nth=0');
  await page.waitForFunction(() => (window as unknown as Win).__viewerReady, null, { timeout: 180_000 });
});
