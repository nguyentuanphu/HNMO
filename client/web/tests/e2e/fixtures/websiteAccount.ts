import { expect, type Page } from '@playwright/test';

/** A cookie-authenticated account boundary for existing feature fixtures; never supplies passwords to feature routes. */
export async function websiteAccountFixture(page: Page, loginName: string, password: string): Promise<void> {
  let authenticated = false;
  await page.route('**/portal/status', route => route.fulfill({ json: { ready: true } }));
  await page.route('**/portal/session', route => {
    const method = route.request().method();
    if (method === 'POST') {
      const body = route.request().postDataJSON() as Record<string, unknown>;
      authenticated = body.loginName === loginName && body.password === password;
      return route.fulfill(authenticated ? { json: { authenticated: true } } : { status: 401, json: { error: 'login' } });
    }
    if (method === 'DELETE') authenticated = false;
    return route.fulfill({ json: { authenticated } });
  });
  await page.route('**/portal/account/overview', route => {
    expect(route.request().headers()['x-hnmo-session']).toBe('1'); expect(route.request().postDataJSON()).not.toHaveProperty('password');
    return route.fulfill({ json: { loginName, accountOnline: false, balances: { wCoinC: 0, wCoinP: 0, goblinPoints: 0 }, vipLevel: 0, characters: [], history: [], page: 0, hasMore: false } });
  });
}

export async function signInWebsiteFixture(page: Page, loginName: string, password: string): Promise<void> {
  await page.locator('#accountUser').fill(loginName); await page.locator('#accountSecret').fill(password);
  await page.locator('#accountSignInButton').click(); await expect(page.locator('#accountDashboard')).toBeVisible();
}
