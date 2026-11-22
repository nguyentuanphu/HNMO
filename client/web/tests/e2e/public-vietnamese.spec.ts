import { expect, test } from '@playwright/test';

for (const width of [375, 820, 1280]) {
  test(`account language switches every section at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Close' } }));
    await page.route('**/portal/**', (route) => route.fulfill({ status: 503, json: { error: 'Server unavailable' } }));
    await page.route('**/portal/session', route => route.fulfill({ json: { authenticated: false } }));
    await page.goto('/account.html');
    await page.locator('[data-site-language="vi"]').click();
    await expect(page.locator('html')).toHaveAttribute('lang', 'vi');
    await expect(page.locator('#accountRegisterTitle')).toHaveText('Tạo tài khoản');
    await expect(page.locator('#registerForm')).toContainText('Tên tài khoản');
    await expect(page.locator('#registerForm')).toContainText('Mật khẩu');
    await expect(page.locator('#resetsTitle')).toHaveText('Reset và điểm');
    await expect(page.locator('[data-game-name]')).toHaveText('Close');
    await page.locator('#accountCreateButton').click();
    await page.locator('#loginName').fill('Password');
    await expect(page.locator('#loginName')).toHaveValue('Password');
    await expect(page.locator('[data-gift-lang="vi"]')).toHaveAttribute('aria-pressed', 'true');
    await expect(page.locator('[data-reset-lang="vi"]')).toHaveAttribute('aria-pressed', 'true');
    await expect(page.locator('[data-safety-lang="vi"]')).toHaveAttribute('aria-pressed', 'true');
    await page.locator('[data-site-language="en"]').click();
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.locator('#accountRegisterTitle')).toHaveText('Create an account');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });

  test(`guide translates and switches back at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Close' } }));
    await page.goto('/guide.html');
    await page.locator('[data-site-language="vi"]').click();
    await expect(page.locator('html')).toHaveAttribute('lang', 'vi');
    await expect(page.locator('#phone')).toContainText(/điện thoại/i);
    await expect(page.locator('kbd').filter({ hasText: /^Shift$/ }).first()).toHaveText('Shift');
    await expect(page.locator('[data-game-name]')).toHaveText('Close');
    await page.screenshot({ path: `test-results/vietnamese/guide-${width}.png`, fullPage: true });
    await page.locator('[data-site-language="en"]').click();
    await expect(page.locator('#phone')).toHaveText('Play on your phone');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
