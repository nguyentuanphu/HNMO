// Checks against a REAL running server (not fixtures). Opt-in: LIVE_SERVER=1 BASE_URL=http://host:port.
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
  expect(Object.keys(settings)).toEqual(['googleClientId']);
  for (const path of ['/website/data', '/website/details', '/website/settings', '/topup/info']) {
    const text = await (await request.get(path)).text();
    expect(text).not.toMatch(/adminPath|apiKey|password|secure-?token|SepayApiKey|CassoSecureToken/i);
  }
});

test('the admin panel opens at the admin path only, with a login', async ({ request }) => {
  const admin = await request.get('/adminpanel/', { maxRedirects: 0 });
  // 302 to the login page once an admin user exists, 403 before (never open without a login).
  expect([302, 403]).toContain(admin.status());
  if (admin.status() === 302) expect(admin.headers()['location']).toContain('/adminpanel/login');
  // The website's own settings aren't reachable through the admin path.
  expect([403, 404]).toContain((await request.get('/adminpanel/api/website/settings/host', { maxRedirects: 0 })).status());
  // A visitor can't fake the website's prefix header.
  expect((await request.get('/', { headers: { 'X-HNMO-Website-Prefix': '/evil' }, maxRedirects: 0 })).status()).toBe(302);
});
