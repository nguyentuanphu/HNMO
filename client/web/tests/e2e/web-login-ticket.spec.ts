import { test, expect } from '@playwright/test';
import { expectNoPageErrors } from './helpers.js';

test('website sign-in -> Play now opens the game with a ticket in fragment and clears it', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await page.route('**/portal/login-ticket', async (route) => {
    expect(route.request().postDataJSON()).toEqual({ loginName: 'test8', password: 'test8' });
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ loginName: 'test8', ticket: 'fake-one-time-ticket-128bits', expiresInSeconds: 60 }),
    });
  });
  await page.route('**/portal/status', route => route.fulfill({ json: { ready: true } }));
  await page.route('**/portal/account/overview', route => route.fulfill({ json: {
    loginName: 'test8', accountOnline: false, balances: { wCoinC: 0, wCoinP: 0, goblinPoints: 0 },
    vipLevel: 0, characters: [], history: [], page: 0, hasMore: false,
  } }));

  await page.goto('/account.html#play');
  await page.fill('#accountUser', 'test8');
  await page.fill('#accountSecret', 'test8');
  await page.click('#accountSignInButton');

  await expect(page.locator('#playSignedIn')).toBeVisible();
  await expect(page.locator('#playSignedName')).toHaveText('test8');

  // Clicking Play now navigates to /index.html#ticket=...&account=test8; the game clears the fragment immediately.
  await page.click('#playNowButton');
  await page.waitForURL('**/index.html**');
  await expect.poll(() => page.evaluate(() => location.hash)).toBe('');
  await expect(page.locator('#username')).toHaveValue('test8');

  await expectNoPageErrors(errors);
});

test('game login shows "Log in on the website" button (EN+VI) and hides password form when requireWebsiteLogin is on', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  // Default (requireWebsiteLogin: false): both website button and password form are visible.
  await page.route('**/website/settings', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ googleClientId: '', requireWebsiteLogin: false }),
    });
  });

  await page.goto('/index.html');
  const websiteBtn = page.locator('#websiteLoginButton');
  await expect(websiteBtn).toBeVisible();
  await expect(websiteBtn).toHaveText('Log in on the website');
  await expect(page.locator('#loginForm fieldset')).toBeVisible();

  // Switch to Vietnamese and verify button label updates.
  await page.click('#loginLanguage button[data-lang="vi"]');
  await expect(websiteBtn).toHaveText('Đăng nhập trên trang web');

  // When requireWebsiteLogin is true, the password form is hidden while the website button stays visible.
  await page.route('**/website/settings', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ googleClientId: '', requireWebsiteLogin: true }),
    });
  });
  await page.reload();
  await expect(websiteBtn).toBeVisible();
  await expect(page.locator('#loginForm fieldset')).toBeHidden();

  await expectNoPageErrors(errors);
});
