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
