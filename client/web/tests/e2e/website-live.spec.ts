// Checks against a running server and bridge. Opt in with LIVE_SERVER=1 and BASE_URL.
// Set LIVE_SERVER_ADMIN_PATH for a server with a customized admin route.
import { expect, test } from '@playwright/test';

test.skip(!process.env.LIVE_SERVER, 'needs a running server (LIVE_SERVER=1)');

test('live data, rankings and the schedule come from the server', async ({ request }) => {
  const data = await request.get('/website/data');
  expect(data.status()).toBe(200);
  const snapshot = await data.json() as { servers: { players: number; capacity: number }[] };
  expect(snapshot.servers.length).toBeGreaterThan(0);
  expect(snapshot.servers[0]!.capacity).toBeGreaterThan(0);
  const details = await request.get('/website/details');
  expect(details.status()).toBe(200);
  const body = await details.json() as { characters: unknown[] | null; upcoming: unknown[] | null; drops: unknown[] | null };
  expect(Array.isArray(body.characters)).toBe(true);
  expect(Array.isArray(body.upcoming)).toBe(true);
  expect(Array.isArray(body.drops)).toBe(true);
});

test('nothing secret reaches the browser', async ({ request }) => {
  const settings = await (await request.get('/website/settings')).json() as Record<string, unknown>;
  expect(Object.keys(settings).sort()).toEqual(['brand', 'connectServerPort', 'googleClientId', 'requireWebsiteLogin', 'theme']);
  expect(settings.brand).toEqual(expect.any(Object));
  const publicBrandFields = ['accentColor', 'backgroundColor', 'logoUrl', 'panelColor', 'textColor'];
  for (const key of Object.keys(settings.brand as Record<string, unknown>)) {
    expect(publicBrandFields).toContain(key);
  }
  for (const path of ['/website/data', '/website/details', '/website/settings', '/topup/info']) {
    const text = await (await request.get(path)).text();
    expect(text).not.toMatch(/adminPath|apiKey|password|secure-?token|SepayApiKey|CassoSecureToken/i);
  }
});

test('the admin panel opens at the admin path only, with a login', async ({ request }) => {
  const adminPath = process.env.LIVE_SERVER_ADMIN_PATH ?? 'adminpanel';
  expect(adminPath).toMatch(/^[A-Za-z0-9][A-Za-z0-9_-]{2,39}$/);
  const route = `/${adminPath}`;
  const admin = await request.get(`${route}/`, { maxRedirects: 0 });
  // 302 to the login page once an admin user exists, 403 before (never open without a login).
  expect([302, 403]).toContain(admin.status());
  if (admin.status() === 302) expect(admin.headers()['location']).toContain(`${route}/login`);
  // The website's own settings aren't reachable through the admin path.
  expect([403, 404]).toContain((await request.get(`${route}/api/website/settings/host`, { maxRedirects: 0 })).status());
  // A visitor can't fake the website's prefix header.
  expect((await request.get('/', { headers: { 'X-HNMO-Website-Prefix': '/evil' }, maxRedirects: 0 })).status()).toBe(302);
});
