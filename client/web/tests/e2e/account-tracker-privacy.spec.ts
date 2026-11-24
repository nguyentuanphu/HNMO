import { expect, test } from '@playwright/test';
import { signInWebsiteFixture, websiteAccountFixture } from './fixtures/websiteAccount.js';

for (const [width, language] of [[390, 'vi'], [1440, 'en']] as const) {
  test(`tracker opt-out uses the account session and survives reopen at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.addInitScript(language => localStorage.setItem('hnmo.language', language), language);
    await websiteAccountFixture(page, 'tester', 'fixture-secret');
    let optedOut = false;
    await page.route('**/portal/account/tracker/privacy', route => {
      expect(route.request().headers()['x-hnmo-session']).toBe('1');
      const body = route.request().postDataJSON() as Record<string, unknown>;
      expect(body).not.toHaveProperty('password');
      expect(body).not.toHaveProperty('loginName');
      if (typeof body.optedOut === 'boolean') optedOut = body.optedOut;
      return route.fulfill({ json: { optedOut } });
    });
    await page.goto('/account.html#security');
    const region = page.locator('#accountTrackerPrivacy');
    await expect(region).toBeHidden();
    await signInWebsiteFixture(page, 'tester', 'fixture-secret');
    const toggle = region.getByRole('checkbox');
    await expect(toggle).toBeEnabled();
    await toggle.check();
    await expect(region.getByRole('status')).toHaveText(language === 'vi' ? 'Đã lưu quyền riêng tư.' : 'Privacy preference saved.');
    await page.reload();
    await expect(toggle).toBeChecked();
    await region.screenshot({ path: `docs/screenshots/tracker/privacy-${language}-${width}.png` });
    await page.locator('#accountLogout').click();
    await expect(region).toBeHidden();
    await expect(page.locator('#accountLoginPanel')).toBeVisible();
    expect(await page.evaluate(() => JSON.stringify(localStorage))).not.toContain('fixture-secret');
  });
}
