import { expect, test, type Page } from '@playwright/test';

async function sessionFixture(page: Page, signedIn: boolean): Promise<{ calls: string[] }> {
  const calls: string[] = [];
  await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'HNMO' } }));
  await page.route('**/website/**', route => route.fulfill({ status: 503 }));
  await page.route('**/website/settings', route => route.fulfill({ json: {} }));
  await page.route('**/portal/status', route => route.fulfill({ json: { ready: true } }));
  await page.route('**/portal/session', async route => {
    const method = route.request().method(); calls.push(method);
    if (method === 'POST') { expect(route.request().postDataJSON()).toEqual({ loginName: 'ann', password: 'secret' }); signedIn = true; }
    if (method === 'DELETE') signedIn = false;
    await route.fulfill({ json: { authenticated: signedIn } });
  });
  await page.route('**/portal/account/**', async route => {
    expect(route.request().headers()['x-hnmo-session']).toBe('1');
    expect(route.request().postDataJSON()).not.toHaveProperty('password');
    expect(route.request().postDataJSON()).not.toHaveProperty('loginName');
    if (!signedIn) return route.fulfill({ status: 401, json: { error: 'login' } });
    if (route.request().url().endsWith('/overview')) return route.fulfill({ json: { loginName: 'ann', accountOnline: false, balances: { wCoinC: 250, wCoinP: 0, goblinPoints: 20 }, vipLevel: 0, characters: [], history: [], page: 0, hasMore: false } });
    return route.fulfill({ json: { enabled: false } });
  });
  await page.route('**/portal/login-ticket', async route => {
    expect(route.request().headers()['x-hnmo-session']).toBe('1');
    expect(route.request().postDataJSON()).not.toHaveProperty('password');
    expect(signedIn).toBe(true); calls.push('ticket');
    return route.fulfill({ json: { loginName: 'ann', ticket: 'single-use-fixture' } });
  });
  await page.route(url => url.pathname === '/index.html', route => route.fulfill({ contentType: 'text/html', body: '<main id="game-arrival">Game ticket arrival</main>' }));
  return { calls };
}

for (const width of [390, 1440]) for (const language of ['en', 'vi'] as const) {
  test(`one account entry, private profile and session reuse (${width}, ${language})`, async ({ page }) => {
    await page.setViewportSize({ width, height: 950 });
    await page.addInitScript(lang => localStorage.setItem('hnmo.language', lang), language);
    const fixture = await sessionFixture(page, false);
    await page.goto('/account.html');
    await expect(page.locator('#accountLoginPanel')).toBeVisible();
    for (const id of ['accountDashboard', 'accountPanels', 'accountRegistration', 'gift', 'resets', 'safety', 'play']) await expect(page.locator('#' + id)).toBeHidden();
    await expect(page.getByRole('button', { name: /Apple/i })).toHaveCount(0);
    await page.locator('#accountCreateButton').click(); await expect(page.locator('#registerForm')).toBeVisible();
    await expect(page.locator('#accountPanels')).toBeHidden();
    await page.locator('#accountReturnSignIn').click();
    await page.locator('#accountUser').fill('ann'); await page.locator('#accountSecret').fill('secret');
    await page.locator('#accountSignInButton').click(); await expect(page.locator('#account-overview')).toBeVisible();
    await expect(page.locator('#playLogin')).toBeHidden(); await expect(page.locator('#playNowButton')).toBeVisible();
    expect(fixture.calls.filter(x => x === 'POST')).toHaveLength(1);
    await page.goto('/home.html');
    await expect(page.locator('#register')).toHaveCount(0); await expect(page.locator('#account')).toHaveCount(0);
    await page.goto('/account.html'); await expect(page.locator('#account-overview')).toBeVisible();
    expect(fixture.calls.filter(x => x === 'POST')).toHaveLength(1);
    const storage = await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }));
    expect(storage).not.toContain('secret'); expect(storage).not.toContain('single-use-fixture');
    await page.locator('#playNowButton').click(); await expect(page.locator('#game-arrival')).toBeVisible();
    expect(fixture.calls.filter(x => x === 'ticket')).toHaveLength(1);
    await expect(page).toHaveURL(/ticket=single-use-fixture&account=ann/);
    await page.goto('/account.html'); await expect(page.locator('#account-overview')).toBeVisible();
    await page.locator('#accountLogout').click(); await expect(page.locator('#accountLoginPanel')).toBeVisible();
    await expect(page.locator('#accountDashboard')).toBeHidden();
    await expect(page.locator('.website-back-home')).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test('Enter game reuses a session and signed-out visitors see only the single sign-in screen', async ({ page }) => {
  const fixture = await sessionFixture(page, true);
  await page.goto('/home.html'); await page.locator('.hero a[data-enter-game]').click();
  await expect(page.locator('#game-arrival')).toBeVisible(); expect(fixture.calls).toContain('ticket');
  expect(fixture.calls).not.toContain('POST');
});

test('Google creation and existing Google password recovery both lead to the same profile session', async ({ page }) => {
  await sessionFixture(page, false);
  await page.addInitScript(() => localStorage.setItem('hnmo.language', 'en'));
  await page.route('**/website/settings', route => route.fulfill({ json: { googleClientId: 'fixture-client' } }));
  await page.route('https://accounts.google.com/gsi/client', route => route.fulfill({ contentType: 'text/javascript', body: `window.google={accounts:{id:{initialize(o){window.cb=o.callback},renderButton(el){const b=document.createElement('button');b.id='google-fixture';b.textContent='Google';b.onclick=()=>window.cb({credential:'verified-fixture'});el.append(b)}}}}` }));
  let created = false; let recovered = false;
  await page.route('**/portal/google', async route => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    expect(body.credential).toBe('verified-fixture');
    if (body.loginName) { expect(body.password).toBe('secret'); created = true; return route.fulfill({ json: { created: true } }); }
    if (body.newPassword) { expect(body.newPassword).toBe('secret'); recovered = true; return route.fulfill({ json: { loginName: 'ann', passwordChanged: true } }); }
    return route.fulfill({ json: created ? { loginName: 'ann' } : { needsAccount: true } });
  });
  await page.goto('/account.html'); await page.locator('#google-fixture').click();
  await expect(page.locator('#registerForm')).toBeVisible();
  await page.locator('#loginName').fill('ann'); await page.locator('#password').fill('secret'); await page.locator('#registerForm button').click();
  await expect(page.locator('#account-overview')).toBeVisible(); expect(created).toBe(true);
  await page.locator('#accountLogout').click(); await expect(page.locator('#accountLoginPanel')).toBeVisible();
  await page.locator('#google-fixture').click(); await expect(page.locator('#google-linked')).toBeVisible();
  await page.locator('#google-linked input').fill('secret'); await page.locator('#google-linked button').click();
  await expect(page.locator('#account-overview')).toBeVisible(); expect(recovered).toBe(true);
});
