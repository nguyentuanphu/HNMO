import { expect, test } from '@playwright/test';

test('website omits unavailable realm values and the guide remains bilingual', async ({ page }) => {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'HNMO' } }));
  await page.route('**/website/data', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/server-guide', (route) => route.fulfill({ status: 503 }));

  await page.goto('/home.html');
  await page.locator('#en').click();
  await expect(page.locator('#realm-rules-box')).toContainText('Key realm rules');
  await expect(page.locator('#realm-rules')).toContainText('Mini games refuse entry');
  await expect(page.locator('#realm-rules')).not.toContainText(/\d/);

  await page.locator('#vi').click();
  await expect(page.locator('#realm-rules-box')).toContainText('Quy tắc máy chủ chính');
  await expect(page.locator('#realm-rules')).toContainText('Không thể vào phó bản');
  await expect(page.locator('#realm-rules')).not.toContainText(/\d/);

  await page.goto('/guide.html');
  await page.locator('[data-site-language="en"]').click();
  await expect(page.getByRole('link', { name: 'See the server guide for the guild creation requirement.' }))
    .toHaveAttribute('href', '/home.html#realm-rules');
  await expect(page.locator('body')).not.toContainText('level 150 by default');
  await expect(page.locator('body')).toContainText('Riding the Horn of Fenrir requires the second class or higher.');

  await page.locator('[data-site-language="vi"]').click();
  await expect(page.getByRole('link', { name: 'Xem hướng dẫn máy chủ để biết yêu cầu lập bang hội.' }))
    .toHaveAttribute('href', '/home.html#realm-rules');
  await expect(page.locator('body')).not.toContainText('mặc định cấp 150');
  await expect(page.locator('body')).toContainText('Cưỡi Sói Tinh (Horn of Fenrir) yêu cầu cấp chuyển chức lần 2 trở lên.');
});
