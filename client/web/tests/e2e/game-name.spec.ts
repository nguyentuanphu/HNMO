import { expect, test } from '@playwright/test';

// The game's name players see comes from the admin panel (the "Game name" plug-in) through
// /game-name.json; the default is "HenryN Mu Online".
test('the page shows the game name from the server', async ({ page, request }) => {
  const served = await request.get('/game-name.json');
  expect(served.ok()).toBe(true);
  const { name } = await served.json() as { name: string };
  expect(name.length).toBeGreaterThan(0);
  await page.goto('/');
  await expect(page.locator('#login h1, h1[data-game-name]').first()).toHaveText(name);
  await expect(page).toHaveTitle(name);
});

test('a name changed in the admin panel replaces the default everywhere', async ({ page }) => {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test Realm' } }));
  await page.goto('/');
  await expect(page.locator('h1[data-game-name]').first()).toHaveText('Test Realm');
  await expect(page).toHaveTitle('Test Realm');
  await expect(page.locator('#guideButton')).toHaveAttribute('aria-label', 'Open the Test Realm guide');
  await page.goto('/guide.html');
  await expect(page.locator('h1')).toHaveText('Test Realm Guide');
  await expect(page).toHaveTitle('Test Realm Guide');
  await page.goto('/account.html');
  await expect(page.locator('h1')).toHaveText('Test Realm');
  // Nothing a player sees says HNMO any more (the file name "Start HNMO" in the guide is a real file).
  await page.goto('/');
  expect(await page.locator('body').innerText()).not.toContain('HNMO');
});

test('community links set in the admin panel show on the login screen and in the guide', async ({ page }) => {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test Realm', links: { zalo: 'https://zalo.me/g/test', discord: 'javascript:alert(1)' } } }));
  await page.goto('/');
  const links = page.locator('#communityLinks');
  await expect(links).toBeVisible();
  await expect(links.locator('a.community-zalo')).toHaveAttribute('href', 'https://zalo.me/g/test');
  // Anything but https is left out.
  await expect(links.locator('a')).toHaveCount(1);
  await page.goto('/guide.html');
  await expect(page.locator('[data-community-links] a')).toHaveText('Zalo');
});

test('without links nothing is shown', async ({ page }) => {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test Realm', links: {} } }));
  await page.goto('/');
  await page.waitForTimeout(500);
  await expect(page.locator('#communityLinks')).toBeHidden();
});
