import { expect, test } from '@playwright/test';

// The private Site omits gameplay bootstrap. Exercise its actual adapter on clean hosted URLs.
const shell = `<!doctype html><html lang="en"><head><meta charset="utf-8">
<script type="module" src="/js/websiteThemePreviewEntry.js"></script>
<script type="module" src="/js/websiteThemeEntry.js"></script></head><body>
<div id="login"><form id="loginForm"><h1 data-game-name></h1><span id="loginLanguage"><button type="button" data-lang="en">EN</button><button type="button" data-lang="vi">VI</button></span></form>
<button id="websiteLoginButton" type="button">Sign in</button><div id="characters" hidden></div></div>
</body></html>`;

test('private game preview initializes branding and language on an extensionless host URL', async ({ page }) => {
  await page.route(url => url.pathname === '/game', route => route.fulfill({ contentType: 'text/html', body: shell }));
  await page.goto('/game');
  await expect(page.locator('#login h1')).toHaveText('HNMO');
  await page.locator('#loginLanguage [data-lang=vi]').click();
  await expect(page.locator('html')).toHaveAttribute('lang', 'vi');
  await expect(page.locator('#loginLanguage [data-lang=vi]')).toHaveAttribute('aria-pressed', 'true');
  await page.locator('#loginLanguage [data-lang=en]').click();
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
});

test('private approved profile follows the owner choice on an extensionless URL', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('hnmo.preview.owner.websiteTheme', 'parchment'));
  await page.route(url => url.pathname === '/account-profile-preview', route => route.fulfill({ contentType: 'text/html', body: shell }));
  await page.goto('/account-profile-preview?view=gallery&theme=ember');
  await expect(page).toHaveURL(/view=profile&theme=parchment/);
  await expect(page.locator('html')).toHaveAttribute('data-website-theme', 'parchment');
});

test('private ticket review at the site root reaches the game appearance without a second sign-in', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('hnmo.preview.sample.session', 'sample'));
  await page.route(url => url.pathname === '/' || url.pathname === '/game.html', route => route.fulfill({ contentType: 'text/html', body: shell }));
  await page.goto('/#ticket=fictional-example');
  await expect(page.locator('#login h1')).toHaveText('HNMO');
  await expect(page.locator('.website-preview-notice')).toContainText('fictional records');
  await expect(page.locator('#characters')).toContainText('Choose a character');
  await expect(page.locator('#websiteLoginButton')).toBeHidden();
  await expect(page).toHaveURL(/\/game\.html#ticket=fictional-example$/);
});
