import { expect, test } from '@playwright/test';

test.use({ baseURL: process.env.ADMIN_URL ?? 'http://127.0.0.1:8095' });

for (const [locale, width] of [['en', 1440], ['vi', 390]] as const) {
  test.describe(locale, () => {
    test.use({ locale });
    test(`economy admin can find the snapshot controls (${locale}, ${width}px)`, async ({ page }) => {
      const vietnamese = locale === 'vi';
      await page.setViewportSize({ width, height: 900 });
      await page.goto(`/economy-dashboard?culture=${locale}&ui-culture=${locale}`);
      await expect(page.getByRole('heading', {
        name: vietnamese ? 'Bảng điều khiển Kinh tế' : 'Economy dashboard',
      })).toBeVisible();
      const capture = page.getByRole('button', {
        name: vietnamese ? 'Chụp thống kê hôm nay' : "Capture today's snapshot",
      });
      await expect(capture).toBeEnabled();
      await page.locator('#ed-help-btn').click();
      await expect(page.locator('#economy-dashboard-help'))
        .toContainText(vietnamese ? 'ngân hàng ngọc' : 'Jewel Bank');
      await expect(page.locator('#blazor-error-ui')).toBeHidden();
    });
  });
}
