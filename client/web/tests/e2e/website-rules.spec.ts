import { expect, test } from '@playwright/test';

test('website and guide display updated realm rules in EN and VI', async ({ page }) => {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'HNMO' } }));
  await page.route('**/website/data', (route) => route.fulfill({ status: 503 }));

  await page.goto('/home.html');
  await page.locator('#en').click();
  await expect(page.locator('#realm-rules-box')).toContainText('Key realm rules');
  await expect(page.locator('#realm-rules')).toContainText('level 150');
  await expect(page.locator('#realm-rules')).toContainText('15 minutes');
  await expect(page.locator('#realm-rules')).toContainText('Crywolf warp requires level 300');

  await page.locator('#vi').click();
  await expect(page.locator('#realm-rules-box')).toContainText('Quy tắc máy chủ chính');
  await expect(page.locator('#realm-rules')).toContainText('cấp 150');
  await expect(page.locator('#realm-rules')).toContainText('Huyết Lâu kéo dài 15 phút');

  await page.goto('/guide.html');
  await page.locator('[data-site-language="en"]').click();
  await expect(page.locator('body')).toContainText('create a guild at the Guild Master in Devias (level 150 by default)');
  await expect(page.locator('body')).toContainText('Riding the Horn of Fenrir requires the second class or higher.');

  await page.locator('[data-site-language="vi"]').click();
  await expect(page.locator('body')).toContainText('Gia nhập bang hội từ cấp 6; lập bang hội tại Hội trưởng ở Devias (mặc định cấp 150)');
  await expect(page.locator('body')).toContainText('Cưỡi Sói Tinh (Horn of Fenrir) yêu cầu cấp chuyển chức lần 2 trở lên.');
});
