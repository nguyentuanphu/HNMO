// The loading bar (ui/loadingProgress.ts, loadProgress.ts): while World 74 loads behind the server choice, and then
// while the game world loads behind the loading picture, the game's gauge shows how far the real loads are (data files
// done of those asked for, by stage) in percent; it only goes forward and goes away when the scene is there. The data
// files come a little slower here, so the bar can be seen moving.
import { test, expect, type Page } from '@playwright/test';

type Win = Window & { __viewerReady?: boolean };
const shots = process.env.HNMO_SHOTS ?? 'test-results';

interface Reading { fraction: number; done: number; total: number; label: string }

/** The bar's numbers now (null while there is none). */
function reading(page: Page, extra = ''): Promise<Reading | null> {
  return page.evaluate((selector) => {
    const host = document.querySelector<HTMLElement>(`.loading-progress${selector}`);
    if (!host?.dataset.fraction) return null;
    return {
      fraction: Number(host.dataset.fraction), done: Number(host.dataset.done), total: Number(host.dataset.total),
      label: host.shadowRoot?.querySelector('#rc_step')?.textContent ?? '',
    };
  }, extra);
}

/** Reads the bar until `until` holds, keeping each reading; a screenshot once it is between a fifth and four fifths. */
async function watch(page: Page, until: () => Promise<boolean>, shot: string): Promise<Reading[]> {
  const seen: Reading[] = [];
  let shotTaken = false;
  for (let i = 0; i < 2400 && !(await until()); i++) {
    const now = await reading(page);
    if (now) {
      seen.push(now);
      if (!shotTaken && now.fraction > 0.05 && now.fraction < 0.95) { shotTaken = true; await page.screenshot({ path: shot }); }
    }
    await page.waitForTimeout(100);
  }
  return seen;
}

function expectForward(seen: Reading[]): void {
  expect(seen.length).toBeGreaterThan(2);
  for (let i = 1; i < seen.length; i++) expect(seen[i].fraction).toBeGreaterThanOrEqual(seen[i - 1].fraction);
  expect(seen.some((r) => r.fraction > 0 && r.fraction < 1)).toBe(true);
  expect(Math.max(...seen.map((r) => r.total))).toBeGreaterThan(0);
}

test('the loading bar follows the real loads of World 74, then of the game world', async ({ page }) => {
  test.setTimeout(8 * 60_000);
  // The world's data files come one after another, 30 ms apart: the bar has time to move on a fast machine too.
  let queue = Promise.resolve();
  await page.route(/\/data\/(World\d+|Object\d+)\//i, async (route) => {
    const turn = queue.then(() => new Promise<void>((r) => setTimeout(r, 30)));
    queue = turn;
    await turn;
    await route.continue();
  });
  await page.goto('/index.html?devlogin=1');
  await page.fill('#username', 'test0');
  await page.fill('#password', 'test0');
  await page.click('button.primary');
  await page.waitForSelector('#servers .choice, #characters .choice', { state: 'attached', timeout: 60_000 });

  // While World 74 comes, no plain list and no login form: the background and the bar only (owner 2026-11-28).
  await expect(page.locator('.loading-progress #progress_back')).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('#servers')).toBeHidden();
  await expect(page.locator('#username')).toBeHidden();

  // World 74 behind the choice: the bar, its percent and files, until the scene is drawn; then it goes.
  const stage = await watch(page, async () => (await page.locator('body.stage-world').count()) > 0, `${shots}/loading-progress-world74.png`);
  expectForward(stage);
  expect(stage.at(-1)!.label).toMatch(/Loading|Đang tải/);
  await expect(page.locator('.loading-progress')).toHaveCount(0, { timeout: 10_000 });

  if (await page.isVisible('#stageServers .server-row:not(.server-row-sizer) >> nth=0')) await page.locator('#stageServers .server-row:not(.server-row-sizer)').first().click();
  else if (await page.isVisible('#servers .choice >> nth=0')) await page.click('#servers .choice >> nth=0');
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
  await page.click('#characters .choice >> nth=0');

  // The game world behind the loading picture: the bar names the map, and goes with the picture.
  await expect(page.locator('.loading-screen')).toHaveCount(1, { timeout: 60_000 });
  const world = await watch(page, () => page.evaluate(() => (window as unknown as Win).__viewerReady === true), `${shots}/loading-progress-game-world.png`);
  expectForward(world);
  expect(world.some((r) => /^(Loading|Đang tải) \S/.test(r.label))).toBe(true);
  await expect(page.locator('.loading-progress')).toHaveCount(0);
  await expect(page.locator('.loading-screen')).toHaveCount(0);
});
