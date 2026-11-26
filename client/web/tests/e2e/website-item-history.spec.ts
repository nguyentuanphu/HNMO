import { mkdir } from 'node:fs/promises';
import { expect, test } from '@playwright/test';

const serial = '9876543210abcdef9876543210abcdef';
const snapshot = { group: 3, number: 14, level: 8, quantity: 1, sockets: 0, durability: 10, hasSkill: false };

for (const [language, viewport] of [['en', { width: 1440, height: 900 }], ['vi', { width: 390, height: 844 }]] as const) {
  test(`finds recorded item changes by exact serial in ${language}`, async ({ page }) => {
    const vi = language === 'vi';
    await page.setViewportSize(viewport);
    await page.addInitScript(lang => localStorage.setItem('hnmo.language', lang), language);
    await page.route('**/website/tracker/item?**', route => {
      const actual = new URL(route.request().url()).searchParams.get('serial');
      return route.fulfill({ json: {
        enabled: true, retentionDays: 30, events: actual === serial ? [{
          kind: 'upgrade', time: '2026-11-24T08:00:00+11:00', character: null, partner: null,
          before: snapshot, after: { ...snapshot, level: 9 },
        }] : [],
      } });
    });
    await page.goto('/tracker.html');
    const main = page.locator('#item-history');
    const search = main.getByRole('button', { name: vi ? 'Tìm kiếm' : 'Search', exact: true });
    const lookup = main.getByLabel(vi ? 'Số sê-ri vật phẩm' : 'Item serial');
    await lookup.fill('irrelevant');
    await search.click();
    await expect(main.getByRole('status')).toContainText(vi ? 'Nhập số sê-ri vật phẩm hợp lệ.' : 'Enter a valid item serial.');
    await lookup.fill(serial);
    await search.click();
    await expect(main.getByRole('heading', { name: vi ? 'Nâng cấp' : 'Upgrade', exact:true })).toBeVisible();
    await expect(main.getByText('+8', { exact:false })).toBeVisible();
    await expect(main.getByText('+9', { exact:false })).toBeVisible();
    await expect(main.getByText('Sydney', { exact:false })).toBeVisible();
    await expect(main.getByText(vi ? '30 ngày' : '30 days', { exact:false })).toBeVisible();
    expect(await main.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await mkdir('docs/screenshots/item-history', { recursive: true });
    await main.screenshot({ path: `docs/screenshots/item-history/tracker-${language}-${viewport.width}.png` });
    await lookup.fill('11111111111111111111111111111111');
    await search.click();
    await expect(main.getByRole('status')).toContainText(vi ? 'Chưa có bản ghi công khai' : 'No public records');
    await expect(main.locator('#item-history-results li')).toHaveCount(0);
  });
}

test('reports that item history is disabled when the host turns off the tracker', async ({ page }) => {
  await page.route('**/website/tracker/item?**', route => route.fulfill({json:{enabled:false}}));
  await page.goto('/tracker.html?serial=' + serial);
  await expect(page.locator('#item-history-status')).toHaveText('The host has disabled item history.');
});

for (const [language, width, title] of [['en', 1440, 'Item history'], ['vi', 390, 'Lịch sử vật phẩm']] as const) {
  test(`home links to public item history in ${language} only while available`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.addInitScript(lang => {
      localStorage.setItem('website-language', lang);
      localStorage.setItem('hnmo.language', lang);
    }, language);
    let enabled = false;
    await page.route('**/website/tracker/status', route => route.fulfill({ json: { enabled } }));
    const status = () => page.waitForResponse('**/website/tracker/status');
    let loaded = status();
    await page.goto('/home.html');
    await loaded;
    const entry = page.locator('#home-item-history');
    await expect(entry).toBeHidden();

    enabled = true;
    loaded = status();
    await page.locator('#refresh').click();
    await loaded;
    await expect(entry).toBeVisible();
    await expect(entry).toHaveText(title);
    const right = await entry.evaluate(link => link.getBoundingClientRect().right);
    expect(right).toBeLessThanOrEqual(width);
    await entry.click();
    await expect(page).toHaveURL(/\/tracker\.html$/);
    await expect(page.locator('#item-history-heading')).toHaveText(title);
  });
}

test('home keeps item history hidden when availability cannot be read', async ({ page }) => {
  await page.route('**/website/tracker/status', route => route.fulfill({ status: 503 }));
  const status = page.waitForResponse('**/website/tracker/status');
  await page.goto('/home.html');
  await status;
  await expect(page.locator('#home-item-history')).toBeHidden();
});
