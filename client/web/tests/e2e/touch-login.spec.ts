// A phone held sideways (short screen): the login form fits and scrolls, its Connect button can be
// reached and pressed, and the game's touch buttons stay hidden until the world shows; then the bar
// has the item-names (Alt) toggle.
import { test, expect, devices } from '@playwright/test';

// iPhone 13 held sideways, in Chromium (the test machine has no WebKit).
const { defaultBrowserType: _browser, ...phone } = devices['iPhone 13 landscape'];
void _browser;
test.use(phone);

test('the login works on a phone held sideways; touch buttons only in the world', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  await page.goto('/index.html?devlogin=1');
  await expect(page.locator('#touchBar')).toBeHidden();
  await expect(page.locator('#touchFight')).toBeHidden();
  await page.screenshot({ path: 'test-results/touch-login.png' });
  await page.fill('#username', 'test5');
  await page.fill('#password', 'test5');
  const connect = page.locator('button.primary');
  await connect.scrollIntoViewIfNeeded();
  await expect(connect).toBeInViewport();
  await connect.tap();
  await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 60_000 });
  if (await page.isVisible('#servers .choice')) await page.tap('#servers .choice >> nth=0');
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
  await page.tap('#characters .choice >> nth=0');
  await page.waitForFunction(() => (window as unknown as { __viewerReady?: boolean }).__viewerReady, null, { timeout: 180_000 });
  await expect(page.locator('#touchBar')).toBeVisible();
  const alt = page.locator('#touchBar button', { hasText: 'Alt' });
  await expect(alt).toBeVisible();
  await alt.tap();
  await expect(alt).toHaveClass(/on/);
  await page.screenshot({ path: 'test-results/touch-world.png' });
});
