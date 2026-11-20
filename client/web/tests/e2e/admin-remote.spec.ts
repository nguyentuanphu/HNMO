// The admin panel from afar (owner's choices, 2026-11-21), played by the machine: on the host computer (ADMIN_URL) an
// admin user is made and sets up the 6-digit code; then, through the website's admin path (BASE_URL + ADMIN_PATH),
// the password alone is not enough, the code signs in, the licence card of Host settings is there (ports are not),
// the Arena's panel under the same path ("arena/") opens without signing in again, and the browser is trusted:
// signed out and in again, the code isn't asked.
// Opt-in (ADMIN_REMOTE=1): it makes an admin user, so run it on a test save only and remove the user afterwards.
import { test, expect, type Page } from '@playwright/test';
import { createHmac } from 'node:crypto';

const hostUrl = (process.env.ADMIN_URL ?? 'http://127.0.0.1:8095').replace(/\/$/, '');
const adminPath = (process.env.ADMIN_PATH ?? '/adminpanel').replace(/\/$/, '');
const USER = process.env.ADMIN_REMOTE_USER ?? 'remote-test';
const PASSWORD = 'Remote-test-password-1';

/** RFC 6238 code of a base32 key, as an authenticator app shows it. */
function totp(key: string, at = Date.now()): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = '';
  for (const c of key.replace(/[\s=-]/g, '').toUpperCase()) bits += alphabet.indexOf(c).toString(2).padStart(5, '0');
  const bytes = Buffer.from(bits.match(/.{8}/g)!.map((b) => parseInt(b, 2)));
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(at / 30_000)));
  const mac = createHmac('sha1', bytes).update(counter).digest();
  const offset = mac[mac.length - 1] & 15;
  return String((mac.readUInt32BE(offset) & 0x7fffffff) % 1_000_000).padStart(6, '0');
}

/** Signs in with the password; done when the page leaves the login form or asks for the code. */
async function signIn(page: Page, base: string): Promise<void> {
  await page.goto(`${base}/login`, { waitUntil: 'networkidle' });
  // The form answers once the page's live connection is up: type and send again until it does.
  await expect.poll(async () => {
    if (!page.url().includes('/login') || await page.locator('#login-code').isVisible()) return true;
    await page.fill('#login-name', USER);
    await page.fill('#login-password', PASSWORD);
    await page.click('button[type=submit]');
    return false;
  }, { timeout: 60_000, intervals: [2000] }).toBe(true);
}

test('the admin panel from afar: the 6-digit code, the licence, one path for every panel', async ({ browser, baseURL }) => {
  test.skip(!process.env.ADMIN_REMOTE, 'Makes an admin user: set ADMIN_REMOTE=1 on a test save (and ADMIN_PATH to the website admin path).');
  test.setTimeout(5 * 60_000);
  const site = `${(baseURL ?? '').replace(/\/$/, '')}${adminPath}`;

  // On the host computer: the first admin user, signed in, sets up the authenticator.
  const host = await (await browser.newContext()).newPage();
  await host.goto(`${hostUrl}/users`, { waitUntil: 'networkidle' });
  await host.getByRole('button', { name: 'Create User' }).click();
  await host.fill('#LoginName', USER);
  await host.fill('#Password', PASSWORD);
  await host.getByRole('button', { name: 'Submit' }).click();
  await expect(host.locator('table')).toContainText(USER);
  await signIn(host, hostUrl);
  await host.goto(`${hostUrl}/account/security`, { waitUntil: 'networkidle' });
  await host.locator('button[title]').filter({ hasText: /authenticator|xác thực/i }).first().click();
  const key = (await host.locator('.card code').first().textContent())!.trim();
  await host.fill('input[autocomplete=one-time-code]', totp(key));
  await host.locator('.input-group button').click();
  await expect(host.locator('.card').first()).toContainText(/enabled|bật/i);

  // From afar: the password alone is not enough; the code of the next time step (a used code is refused).
  const far = await (await browser.newContext()).newPage();
  await signIn(far, site);
  await expect(far.locator('#login-code')).toBeVisible({ timeout: 30_000 });
  await expect.poll(() => Math.floor(Date.now() / 30_000), { timeout: 40_000, intervals: [500] }).toBeGreaterThan(Math.floor(Date.now() / 30_000));
  await far.fill('#login-code', totp(key));
  await far.click('button[type=submit]');
  await expect(far.locator('.sidebar .hn-brand')).toBeVisible({ timeout: 60_000 });
  await far.screenshot({ path: 'test-results/admin-remote-home.png' });

  // Host settings from afar: the licence card, not the ports.
  await far.goto(`${site}/host-settings`, { waitUntil: 'networkidle' });
  await expect(far.locator('#license-card')).toBeVisible();
  await expect(far.locator('#web-port')).toHaveCount(0);
  await far.screenshot({ path: 'test-results/admin-remote-licence.png' });

  // The Arena's panel under the same path, signed in already.
  await far.goto(`${site}/arena/`, { waitUntil: 'networkidle' });
  await expect(far.locator('.sidebar .hn-brand')).toBeVisible({ timeout: 60_000 });
  expect(far.url()).not.toContain('/login');
  await far.screenshot({ path: 'test-results/admin-remote-arena.png' });

  // A trusted browser (the box was ticked with the code): signed out and in again, only the password is asked.
  await far.goto(`${site}/`, { waitUntil: 'networkidle' });
  await far.getByRole('button', { name: /Logout|Đăng xuất/ }).click();
  await far.waitForURL(/\/login/, { timeout: 30_000 });
  await signIn(far, site);
  await expect(far.locator('.sidebar .hn-brand')).toBeVisible({ timeout: 60_000 });
  await expect(far.locator('#login-code')).toHaveCount(0);

  await far.context().close();
  await host.context().close();
});
