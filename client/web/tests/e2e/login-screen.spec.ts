// Players log in on the website (owner 2026-11-28): the game page has no login box of its own. A visitor without a
// ticket sees the website's login button and the language picker, not the account, password, server details, Connect
// or saved accounts. The form stays for developers and the tests behind ?devlogin=1 (kept for the tab).
import { test, expect } from '@playwright/test';

test('without the developer switch the page shows only the website login button, no login form', async ({ page }) => {
  await page.route('**/website/settings', (route) => route.fulfill({ json: { requireWebsiteLogin: false } }));
  await page.goto('/index.html');
  await expect(page.locator('#websiteLoginButton')).toBeVisible();
  await expect(page.locator('#loginLanguage')).toBeVisible();
  for (const hidden of ['#username', '#password', '#host', '#loginForm button.primary', '#savedAccounts']) {
    await expect(page.locator(hidden), hidden).toBeHidden();
  }
  await page.screenshot({ path: `${process.env.HNMO_SHOTS ?? 'test-results'}/login-screen-player.png` });
});

test('with ?devlogin=1 the form is there for developers and tests, also after a reload', async ({ page }) => {
  await page.route('**/website/settings', (route) => route.fulfill({ json: { requireWebsiteLogin: false } }));
  await page.goto('/index.html?devlogin=1');
  await expect(page.locator('#username')).toBeVisible();
  await expect(page.locator('#password')).toBeVisible();
  await expect(page.locator('#loginForm button.primary')).toBeVisible();
  await page.goto('/index.html');
  await expect(page.locator('#username')).toBeVisible();
});
