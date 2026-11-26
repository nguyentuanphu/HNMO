// Confirm boxes asked while one is open wait in a queue (gameDialogs.ts, as on the desktop). The next box reuses the
// open document: opening another one left a second box in the page for good, on top of the one in use, where it caught
// the clicks meant for its OK button (the trade question and the vault zen box of trade.spec.ts and
// small-ui-feedback.spec.ts waited behind such a pair).
import { test, expect } from '@playwright/test';

type Dialogs = { showConfirm(config: { lines: string[]; tag?: string; showCancel?: boolean }): Promise<{ button: string }> };

test('queued confirm boxes share one document and each OK button takes its click', async ({ page }) => {
  test.setTimeout(2 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/index.html');

  await page.evaluate(async () => {
    const dialogs = await import('/js/ui/gameDialogs.js' as string) as Dialogs;
    const w = window as unknown as { __answers: string[] };
    w.__answers = [];
    for (const tag of ['first-box', 'second-box', 'third-box']) {
      void dialogs.showConfirm({ lines: [tag], tag, showCancel: true }).then((a) => w.__answers.push(`${tag}:${a.button}`));
    }
  });

  const boxes = page.locator('.rml-generic_confirm_dialog');
  await expect(page.locator('.first-box')).toBeVisible({ timeout: 30_000 });
  await page.locator('.first-box .confirm-ok').click();
  await expect(page.locator('.second-box')).toBeVisible({ timeout: 30_000 });
  expect(await boxes.count(), 'one box document while the queue goes on').toBe(1);
  await page.locator('.second-box .confirm-ok').click({ timeout: 10_000 });
  await expect(page.locator('.third-box')).toBeVisible({ timeout: 30_000 });
  expect(await boxes.count()).toBe(1);
  await page.locator('.third-box .confirm-cancel').click({ timeout: 10_000 });
  await expect(boxes).toHaveCount(0);

  expect(await page.evaluate(() => (window as unknown as { __answers: string[] }).__answers))
    .toEqual(['first-box:primary', 'second-box:primary', 'third-box:cancel']);
  expect(errors).toEqual([]);
});
