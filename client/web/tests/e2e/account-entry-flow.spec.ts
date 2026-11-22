import { expect, test, type Page } from '@playwright/test';

async function sessionFixture(page: Page, signedIn: boolean): Promise<{ calls: string[] }> {
  const calls: string[] = [];
  await page.context().route('**/portal/**', route => route.fulfill({ status: 503, json: { error: 'fixture-unavailable' } }));
  await page.context().route('**/game-name.json', route => route.fulfill({ json: { name: 'HNMO' } }));
  await page.context().route('**/website/**', route => route.fulfill({ status: 503 }));
  await page.context().route('**/website/settings', route => route.fulfill({ json: {} }));
  await page.context().route('**/portal/status', route => route.fulfill({ json: { ready: true } }));
  await page.context().route('**/portal/session', async route => {
    const method = route.request().method(); calls.push(method);
    if (method === 'POST') { expect(route.request().postDataJSON()).toEqual({ loginName: 'ann', password: 'secret' }); signedIn = true; }
    if (method === 'DELETE') signedIn = false;
    await route.fulfill({ json: { authenticated: signedIn } });
  });
  await page.context().route('**/portal/account/**', async route => {
    expect(route.request().headers()['x-hnmo-session']).toBe('1');
    expect(route.request().postDataJSON()).not.toHaveProperty('password');
    expect(route.request().postDataJSON()).not.toHaveProperty('loginName');
    if (!signedIn) return route.fulfill({ status: 401, json: { error: 'login' } });
    if (route.request().url().endsWith('/overview')) return route.fulfill({ json: { loginName: 'ann', accountOnline: false, balances: { wCoinC: 250, wCoinP: 0, goblinPoints: 20 }, vipLevel: 0, characters: [], history: [], page: 0, hasMore: false } });
    return route.fulfill({ json: { enabled: false } });
  });
  await page.context().route('**/portal/login-ticket', async route => {
    expect(route.request().headers()['x-hnmo-session']).toBe('1');
    expect(route.request().postDataJSON()).not.toHaveProperty('password');
    expect(signedIn).toBe(true); calls.push('ticket');
    return route.fulfill({ json: { loginName: 'ann', ticket: 'single-use-fixture' } });
  });
  await page.context().route(url => url.pathname === '/index.html', route => route.fulfill({ contentType: 'text/html', body: '<main id="game-arrival">Game ticket arrival</main>' }));
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
    await page.screenshot({ path: `docs/screenshots/account-flow/sign-in-${language}-${width}.png` });
    await page.locator('#accountCreateButton').click(); await expect(page.locator('#registerForm')).toBeVisible();
    await page.screenshot({ path: `docs/screenshots/account-flow/register-${language}-${width}.png` });
    await expect(page.locator('#accountPanels')).toBeHidden();
    await page.locator('#accountReturnSignIn').click();
    await page.locator('#accountUser').fill('ann'); await page.locator('#accountSecret').fill('secret');
    await page.locator('#accountSignInButton').click(); await expect(page.locator('#account-overview')).toBeVisible();
    await page.screenshot({ path: `docs/screenshots/account-flow/profile-${language}-${width}.png` });
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


test('signed-out Enter game asks once then consumes a server ticket without a second login', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const fixture = await sessionFixture(page, false);
  await page.goto('/home.html'); await page.locator('.hero a[data-enter-game]').click();
  await expect(page).toHaveURL(/account\.html\?play=1/);
  await expect(page.locator('#accountDashboard')).toBeHidden();
  await page.locator('#accountUser').fill('ann'); await page.locator('#accountSecret').fill('secret');
  await page.locator('#accountSignInButton').click(); await expect(page.locator('#game-arrival')).toBeVisible();
  expect(fixture.calls.filter(value => value === 'POST')).toHaveLength(1);
  expect(fixture.calls.filter(value => value === 'ticket')).toHaveLength(1);
});

test('an unavailable session check asks to retry rather than forcing another login', async ({ page }) => {
  const fixture = await sessionFixture(page, true); let unavailable = true;
  await page.route('**/portal/session', route => unavailable && route.request().method() === 'GET' ? route.fulfill({ status: 503 }) : route.fallback());
  await page.goto('/home.html'); await page.locator('.hero a[data-enter-game]').click();
  await expect(page.locator('.website-entry-status')).toContainText('Try again');
  await expect(page).toHaveURL(/home\.html$/);
  unavailable = false; await page.locator('.hero a[data-enter-game]').click();
  await expect(page.locator('#game-arrival')).toBeVisible(); expect(fixture.calls).not.toContain('POST'); expect(fixture.calls).not.toContain('DELETE');
});

test('each existing public page has a floating route to the home top', async ({ page }) => {
  await sessionFixture(page, false);
  await page.route('**/js/main.js', route => route.fulfill({ body: '', contentType: 'text/javascript' }));
  for (const route of ['/home.html', '/account.html', '/topup.html', '/guide.html', '/calculator.html']) {
    await page.goto(route); const home = page.locator('.website-back-home');
    await expect(home).toBeVisible(); await expect(home).toHaveAttribute('href', '/home.html');
    await home.click(); await expect(page).toHaveURL(/home\.html$/);
  }
});


test('signing in and out updates other open account tabs without another password', async ({ page, context }) => {
  const fixture = await sessionFixture(page, false);
  await page.goto('/account.html');
  const other = await context.newPage();
  const ready = other.waitForResponse(response => response.url().endsWith('/portal/session') && response.request().method() === 'GET');
  await other.goto('/account.html'); await ready;
  await page.locator('#accountUser').fill('ann'); await page.locator('#accountSecret').fill('secret'); await page.locator('#accountSignInButton').click();
  await expect(page.locator('#account-overview')).toBeVisible(); await expect(other.locator('#account-overview')).toBeVisible();
  expect(fixture.calls.filter(call => call === 'POST')).toHaveLength(1);
  await page.locator('#accountLogout').click(); await expect(other.locator('#accountLoginPanel')).toBeVisible();
  await expect(other.locator('#accountDashboard')).toBeHidden(); await expect(other.locator('#accountPanels')).not.toContainText('250');
});


test('private feature forms never flash before the account module is ready', async ({ page }) => {
  await sessionFixture(page, false);
  let release: () => void = () => {}; let arrived: () => void = () => {};
  const pending = new Promise<void>(resolve => { release = resolve; }); const requested = new Promise<void>(resolve => { arrived = resolve; });
  await page.route('**/js/accountPage.js', async route => { arrived(); await pending; await route.continue(); });
  await page.goto('/account.html', { waitUntil: 'commit' }); await requested;
  for (const id of ['play', 'gift', 'resets', 'safety']) { await expect(page.locator('#'+id)).toHaveCount(1); await expect(page.locator('#'+id)).toBeHidden(); }
  await expect(page.locator('#accountLoginPanel')).toBeHidden(); release();
  await page.waitForLoadState('domcontentloaded'); await expect(page.locator('#accountDashboard')).toBeHidden();
});

test('failed logout hides private data and offers a real retry without claiming success', async ({ page }) => {
  await sessionFixture(page, true); let fail = true;
  await page.route('**/portal/session', route => route.request().method() === 'DELETE' && fail ? route.fulfill({ status: 503 }) : route.fallback());
  await page.goto('/account.html'); await expect(page.locator('#account-overview')).toBeVisible();
  await page.locator('#accountLogout').click(); await expect(page.locator('#accountMessage')).toContainText('not confirmed');
  await expect(page.locator('#accountDashboard')).toBeHidden(); await expect(page.locator('#accountLoginPanel')).toBeHidden();
  await page.locator('header [data-account-language=vi]').click();
  await expect(page.locator('#accountMessage')).toContainText('Chưa xác nhận');
  fail = false; await page.locator('#accountRetryLogout').click(); await expect(page.locator('#accountLoginPanel')).toBeVisible();
  await expect(page.locator('#accountDashboard')).toBeHidden();
});


test('registration network errors remain Vietnamese and keep private panels closed', async ({ page }) => {
  await sessionFixture(page, false); await page.addInitScript(() => localStorage.setItem('hnmo.language', 'vi'));
  await page.route('**/portal/register', route => route.abort());
  await page.goto('/account.html'); await page.locator('#accountCreateButton').click();
  await page.locator('#loginName').fill('ann'); await page.locator('#password').fill('secret'); await page.locator('#registerForm button').click();
  await expect(page.locator('#registerMessage')).toContainText('Hiện chưa dùng được');
  await expect(page.locator('#accountDashboard')).toBeHidden();
});


test('a delayed session restore never asks an already signed-in player for a password', async ({ page }) => {
  const fixture = await sessionFixture(page, true); let release: () => void = () => {}; let arrived: () => void = () => {};
  const pending = new Promise<void>(resolve => { release = resolve; }); const requested = new Promise<void>(resolve => { arrived = resolve; });
  await page.route('**/portal/session', async route => { if (route.request().method() === 'GET') { arrived(); await pending; } await route.fallback(); });
  await page.goto('/account.html'); await requested;
  await expect(page.locator('#accountLoginPanel')).toBeHidden(); await expect(page.locator('#accountDashboard')).toBeHidden();
  release(); await expect(page.locator('#account-overview')).toBeVisible(); expect(fixture.calls).not.toContain('POST');
});

test('profile restore retries a network failure using its existing session', async ({ page }) => {
  const fixture = await sessionFixture(page, true); let fail = true;
  await page.route('**/portal/session', route => fail && route.request().method() === 'GET' ? route.fulfill({ status: 503 }) : route.fallback());
  await page.goto('/account.html'); await expect(page.locator('#accountRetrySession')).toBeVisible(); await expect(page.locator('#accountLoginPanel')).toBeHidden();
  fail = false; await page.locator('#accountRetrySession').click(); await expect(page.locator('#account-overview')).toBeVisible(); expect(fixture.calls).not.toContain('POST');
});

for (const path of ['/account.html', '/topup.html']) {
  test(`the ${path} header enters the game with its existing session`, async ({ page }) => {
    const fixture = await sessionFixture(page, true);
    await page.goto(path);
    await page.locator('header a[data-enter-game]').click();
    await expect(page.locator('#game-arrival')).toBeVisible();
    expect(fixture.calls.filter(call => call === 'ticket')).toHaveLength(1);
    expect(fixture.calls).not.toContain('POST');
  });
}
