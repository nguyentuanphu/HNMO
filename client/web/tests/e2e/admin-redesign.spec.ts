import { expect, test } from '@playwright/test';

test('guide.html is player-only without host sections', async ({ request }) => {
  const res = await request.get('/guide.html');
  expect(res.ok()).toBeTruthy();
  const html = await res.text();

  expect(html).toContain('Play on your phone');
  expect(html).toContain('Custom items');
  expect(html).not.toContain('id="world-bosses"');
  expect(html).not.toContain('id="top-races-host"');
  expect(html).not.toContain('id="resets-host"');
  expect(html).not.toContain('id="item-definitions-host"');
});

test('home.html organizes navigation links into primary bar and Explore dropdown', async ({ page }) => {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'HNMO', links: {} } }));
  await page.route('**/website/settings', (route) => route.fulfill({ json: { googleClientId: '' } }));
  await page.route('**/website/data', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/details', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/**', (route) => route.fulfill({ json: { enabled: false } }));

  await page.goto('/home.html');
  const navMore = page.locator('header details.nav-more');
  await expect(navMore).toHaveCount(1);
  await navMore.locator('summary').click();
  await expect(navMore.locator('.nav-more-menu a[href="#guilds"]')).toBeVisible();
  await expect(navMore.locator('.nav-more-menu a[href="#gifts"]')).toBeVisible();
});
