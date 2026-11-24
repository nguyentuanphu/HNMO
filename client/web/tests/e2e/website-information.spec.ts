import { mkdir } from 'node:fs/promises';
import { expect, test } from '@playwright/test';

const content = {
  roadmap: { en: 'Sample roadmap for the coming season.\n\nThe next milestone is under review.', vi: 'Lộ trình mẫu cho mùa sắp tới.\n\nCột mốc tiếp theo đang được xem xét.' },
  about: { en: 'Sample host introduction.', vi: 'Lời giới thiệu mẫu của chủ máy chủ.' },
  terms: { en: 'Sample terms of use. <b>Plain text only</b>', vi: 'Điều khoản sử dụng mẫu. <b>Chỉ là văn bản</b>' },
  privacy: { en: 'Sample privacy text.', vi: '' },
};

for (const [language, viewport] of [['en', { width: 1440, height: 900 }], ['vi', { width: 390, height: 844 }]] as const) {
  test(`navigates editable information and existing store links in ${language}`, async ({ page }) => {
    const vi = language === 'vi';
    await page.setViewportSize(viewport);
    await page.addInitScript(lang => localStorage.setItem('hnmo.language', lang), language);
    await page.route('**/website/content', route => route.fulfill({ json: content }));
    await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'Fictional realm name' } }));
    await page.goto('/home.html');
    const homeLinks = page.locator('footer [data-information-footer]');
    await homeLinks.getByRole('link', { name: vi ? 'Lộ trình' : 'Roadmap' }).click();
    const info = page.locator('#website-information');
    await expect(info.getByRole('heading', { name: vi ? 'Lộ trình' : 'Roadmap', exact: true })).toBeVisible();
    await expect(info.getByRole('link', { name: 'Fictional realm name' })).toBeVisible();
    await expect(info.locator('#information-body p')).toHaveCount(2);
    await expect(info.locator('#information-body')).toContainText(vi ? 'Lộ trình mẫu' : 'Sample roadmap');
    await expect(info.getByRole('link', { name: vi ? 'Danh mục cửa hàng' : 'Shop catalogue' })).toHaveAttribute('href', '/home.html#server-guide');
    expect(await info.evaluate(node => node.scrollWidth <= node.clientWidth)).toBe(true);
    await mkdir('docs/screenshots/website-information', { recursive: true });
    await info.screenshot({ path: `docs/screenshots/website-information/roadmap-${language}-${viewport.width}.png` });
    await info.getByRole('link', { name: vi ? 'Điều khoản sử dụng' : 'Terms of use' }).click();
    await expect(info.locator('#information-body')).toContainText(vi ? '<b>Chỉ là văn bản</b>' : '<b>Plain text only</b>');
    await expect(info.locator('#information-body b')).toHaveCount(0);
    await expect(info.getByRole('link', { name: vi ? 'Điều khoản sử dụng' : 'Terms of use' })).toHaveAttribute('aria-current', 'page');
    await info.getByRole('link', { name: vi ? 'Chính sách quyền riêng tư' : 'Privacy policy' }).click();
    if (vi) {
      await expect(info.getByRole('status')).toHaveText('Chủ máy chủ chưa công bố nội dung bằng ngôn ngữ này.');
      await info.getByRole('button', { name: 'English', exact: true }).click();
    }
    await expect(info.locator('#information-body')).toHaveText('Sample privacy text.');
    await info.getByRole('link', { name: 'Top up coins', exact: true }).click();
    await expect(page).toHaveURL(/\/topup\.html$/);
    await expect(page.locator('[data-information-footer]').getByRole('link', { name: 'Terms of use' })).toHaveAttribute('href', '/information.html?page=terms');
  });
}

test('reports a failed content read and keeps the navigation available', async ({ page }) => {
  await page.route('**/website/content', route => route.fulfill({ status: 503 }));
  await page.goto('/information.html?page=about');
  await expect(page.getByRole('heading', { name: 'About' })).toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: 'Content is temporarily unavailable.' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'News', exact: true })).toHaveAttribute('href', '/home.html#news');
});
