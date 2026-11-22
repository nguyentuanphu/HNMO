import { expect, test, type Page } from '@playwright/test';

const themes = ['classic', 'ember', 'azure', 'jade', 'violet', 'parchment'] as const;
const publicSections = ['realm', 'news', 'events', 'schedule', 'newplayers', 'missions', 'bosses', 'lucky-number', 'marriage', 'guilds', 'social', 'races', 'gifts', 'resets', 'market', 'arena', 'jewel-bank', 'catchup', 'season-plan', 'leftovers', 'mounts', 'party-spots', 'rankings', 'drops', 'world', 'account', 'phone', 'start', 'topup-milestones-section'];
const accountSections = ['overview', 'coins', 'vip', 'rewards', 'resets', 'security', 'community'];

async function fixture(page: Page, theme: string): Promise<void> {
  await page.route('**/website/**', route => route.fulfill({ status: 503 }));
  await page.route('**/website/settings', route => route.fulfill({ json: { theme, googleClientId: '' } }));
  await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'HNMO host skin fixture' } }));
  await page.route('**/portal/status', route => route.fulfill({ json: { ready: true } }));
  await page.route('**/topup/info', route => route.fulfill({ json: { enabled: false } }));
  await page.route('**/portal/account/**', route => route.fulfill({ status: 503 }));
  await page.route('**/portal/account/overview', route => route.fulfill({ json: {
    loginName: 'Explorer', accountOnline: false, balances: { wCoinC: 2400, wCoinP: 120, goblinPoints: 650 }, vipLevel: 0,
    characters: [{ name: 'ExplorerKnight', className: 'Knight', classNameVi: 'Chiến binh', level: 300, resets: 12, map: 0, online: false }],
    history: [{ id: 'receipt', coinType: 'WCoinC', amount: 100, reason: 'Recorded reward', createdAt: '2026-11-21T09:00:00Z', pending: false }], page: 0, hasMore: false,
  } }));
  // Login markup/style check only. Existing live gameplay suites are unchanged.
  await page.route('**/js/main.js', route => route.fulfill({ contentType: 'text/javascript', body: '' }));
}

for (const theme of themes) {
  test(`${theme}: one host theme across every page, preserving public and account features`, async ({ page }) => {
    await fixture(page, theme);
    await page.addInitScript(() => {
      localStorage.setItem('hnmo.language', 'en');
      localStorage.setItem('hnmo.preview.theme', 'parchment');
      localStorage.setItem('hnmo.preview.hostTheme', 'violet');
    });
    for (const width of [375, 1280]) {
      await page.setViewportSize({ width, height: 950 });
      await page.goto('/home.html?theme=violet&server=999');
      await expect(page.locator('html')).toHaveAttribute('data-website-theme', theme);
      for (const section of publicSections) await expect(page.locator(`section#${section}`)).toHaveCount(1);
      if (width === 375) await page.locator('.website-navigation > summary').click();
      await expect(page.locator('nav a[href="#missions"]')).toBeVisible();
      await page.locator('nav .nav-more > summary').click();
      for (const section of ['market', 'jewel-bank', 'catchup', 'season-plan', 'leftovers', 'mounts', 'party-spots']) {
        await expect(page.locator(`nav a[href="#${section}"]`)).toBeVisible();
      }
      const before = await page.locator('main section[id]').evaluateAll(nodes => nodes.map(node => node.id));
      await page.locator('#vi').click();
      await page.locator('#en').click();
      expect(await page.locator('main section[id]').evaluateAll(nodes => nodes.map(node => node.id))).toEqual(before);
      await page.goto('/account.html?theme=parchment');
      await expect(page.locator('html')).toHaveAttribute('data-website-theme', theme);
      await page.locator('#accountUser').fill('Explorer');
      await page.locator('#accountSecret').fill('fixture-secret');
      await page.locator('#accountSignInButton').click();
      await expect(page.locator('#accountLoginPanel')).toBeHidden();
      await expect(page.locator('[data-account-language]:visible')).toHaveCount(2);
      await page.locator('header [data-account-language=vi]').click();
      await expect(page.locator('#accountSectionSelect')).toContainText('Tổng quan');
      await page.locator('header [data-account-language=en]').click();
      for (const section of accountSections) {
        await expect(page.locator(`#account-${section}`)).toHaveCount(1);
        if (width <= 600) await page.locator('#accountSectionSelect').selectOption(section);
        else await page.locator(`#accountNav button`).filter({ hasText: new RegExp(`^${section === 'overview' ? 'Overview' : section === 'coins' ? 'Coins' : section === 'vip' ? 'VIP' : section[0].toUpperCase() + section.slice(1)}$`) }).click();
        await expect(page.locator(`#account-${section}`)).toBeVisible();
      }
      for (const selector of ['#play', '#playNowButton', '#topup-milestones-section', '#accountVip', '#accountPasswordChange', '#gift', '#resets', '#safety']) await expect(page.locator(selector)).toHaveCount(1);
      await expect(page.locator('#registerForm')).toHaveCount(1);
      await page.goto('/topup.html?theme=azure');
      await expect(page.locator('html')).toHaveAttribute('data-website-theme', theme);
      for (const id of ['account', 'amount', 'presets', 'banks', 'qr', 'milestones']) await expect(page.locator(`#${id}`)).toHaveCount(1);
      await page.goto('/index.html?theme=jade');
      await expect(page.locator('html')).toHaveAttribute('data-website-theme', theme);
      await expect(page.locator('#loginGuide')).toHaveCount(1);
      await expect(page.locator('#login form')).toHaveCount(1);
      await expect(page.locator('.hud')).toHaveCSS('--gold', '#c9a24a');
      await page.goto('/guide.html');
      await expect(page.locator('html')).toHaveAttribute('data-website-theme', theme);
      expect(await page.locator('table').count()).toBeGreaterThan(0);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
      await page.goto('/calculator.html');
      await expect(page.locator('html')).toHaveAttribute('data-website-theme', theme);
      for (const control of ['calc-class', 'calc-level', 'calc-run', 'calc-share']) await expect(page.locator(`#${control}`)).toBeVisible();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    }
  });
}

test('changing a game server leaves the website skin unchanged and shares one settings fetch', async ({ page }) => {
  let settingsRequests = 0;
  await fixture(page, 'ember');
  await page.route('**/website/settings', route => { settingsRequests++; return route.fulfill({ json: { theme: 'ember', googleClientId: '' } }); });
  await page.route('**/website/data', route => route.fulfill({ json: { updatedAt: '2026-11-22T00:00:00Z', servers: [0, 1].map(id => ({ id, players: 3, capacity: 100, experienceRate: 1, masterExperienceRate: 1, maximumLevel: 400, maximumMasterLevel: 200, maximumCharactersPerAccount: 5, maximumPartySize: 5, classes: [], maps: [], events: [] })) } }));
  await page.goto('/home.html');
  await expect(page.locator('#server option')).toHaveCount(2);
  await page.locator('#server').selectOption('1');
  await expect(page.locator('html')).toHaveAttribute('data-website-theme', 'ember');
  await page.locator('#server').selectOption('0');
  await expect(page.locator('html')).toHaveAttribute('data-website-theme', 'ember');
  expect(settingsRequests).toBe(1);
});

test('host Brand logo and colors apply everywhere without tinting the gameplay HUD', async ({ page }) => {
  await fixture(page, 'azure');
  await page.route('**/website/settings', route => route.fulfill({ json: { theme: 'azure', googleClientId: '', brand: { logoUrl: '/images/host-mark.svg', accentColor: '#22aa88', backgroundColor: '#040608', panelColor: '#18212b', textColor: '#f0f1f2' } } }));
  await page.route('**/images/host-mark.svg', route => route.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" width="48" height="48"><path fill="#22aa88" d="M0 0h48v48H0z"/></svg>' }));
  for (const path of ['/home.html', '/account.html', '/topup.html', '/index.html', '/guide.html', '/calculator.html']) {
    await page.goto(path);
    await expect(page.locator('html')).toHaveCSS('--gold', '#22aa88');
    await expect(page.locator('img[data-brand-logo]')).toBeVisible();
    await expect(page.locator('img[data-brand-logo]')).toHaveAttribute('src', '/images/host-mark.svg');
    await expect(page.locator('.website-powered-by')).toContainText('HNMO');
  }
  await page.goto('/index.html');
  await expect(page.locator('#login')).toHaveCSS('--gold', '#22aa88');
  await expect(page.locator('.hud')).toHaveCSS('--gold', '#c9a24a');
});

test('unsafe Brand fields are rejected and the original game warm-up entry remains discoverable', async ({ page }) => {
  await fixture(page, 'azure');
  await page.route('**/website/settings', route => route.fulfill({ json: { theme: 'azure', brand: { logoUrl: 'javascript:alert(1)', accentColor: '#000000;display:none' } } }));
  await page.goto('/home.html');
  await expect(page.locator('html')).toHaveCSS('--gold', '#7bd8fa');
  await expect(page.locator('img[data-brand-logo]')).toBeHidden();
  const html = await (await page.request.get('/index.html')).text();
  expect(/<script[^>]+type="module"[^>]+src="([^"]+)"/.exec(html)?.[1]).toBe('/js/main.js');
});
