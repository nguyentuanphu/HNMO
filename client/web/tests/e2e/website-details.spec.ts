// The website's schedule, rankings and drops (/website/details) and its account section. These tests use
// fixtures (routed responses), not a game server; see the PR notes for the checks against a real server.
import { expect, test, type Page } from '@playwright/test';

const SNAPSHOT = {
  updatedAt: new Date().toISOString(),
  servers: [{
    id: 0, players: 3, capacity: 100, experienceRate: 1, masterExperienceRate: 1, maximumLevel: 400, maximumMasterLevel: 200,
    maximumCharactersPerAccount: 5, maximumPartySize: 5, classes: [], maps: [],
    events: [
      { type: 'BloodCastle', typeName: { en: 'Blood Castle', vi: 'Huyết Lâu' }, name: 'Blood Castle 1', description: null, level: 1, minimumLevel: 15, maximumLevel: 80, durationSeconds: 1200, capacity: 10, entranceFee: 0 },
      { type: 'BloodCastle', typeName: { en: 'Blood Castle', vi: 'Huyết Lâu' }, name: 'Blood Castle 2', description: null, level: 2, minimumLevel: 81, maximumLevel: 130, durationSeconds: 1200, capacity: 10, entranceFee: 0 },
    ],
  }],
};
const DETAILS = {
  updatedAt: new Date().toISOString(),
  characters: [{ rank: 1, name: 'HeroOne', className: 'Blade Master', level: 400, masterLevel: 12, resets: 3 }],
  eventRankings: [{ type: 'BloodCastle', entries: [{ rank: 1, name: 'HeroOne', className: 'Blade Master', score: 900, games: 4 }] }],
  upcoming: [{ serverId: 0, events: [{ name: 'Blood Castle', nameVi: 'Huyết Lâu', startsAt: '2030-01-01T13:00:00Z' }] }],
  drops: [{ name: 'Box of Luck||vi=Hộp May Mắn', chancePercent: 0.5, minimumMonsterLevel: 10, maximumMonsterLevel: null, monster: null, itemCount: 2 }],
};

async function serve(page: Page, details: unknown | 503, googleClientId = ''): Promise<void> {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test realm', links: {} } }));
  await page.route('**/website/data', (route) => route.fulfill({ json: SNAPSHOT }));
  await page.route('**/website/details', (route) => (details === 503 ? route.fulfill({ status: 503 }) : route.fulfill({ json: details })));
  await page.route('**/website/settings', (route) => route.fulfill({ json: { googleClientId } }));
}

test('schedule, rankings and drops come from the server, in both languages', async ({ page }) => {
  await serve(page, DETAILS);
  await page.goto('/home.html');
  await page.locator('#en').click();
  await expect(page.locator('#upcoming .slot')).toHaveCount(1);
  await expect(page.locator('#upcoming')).toContainText('Blood Castle');
  await expect(page.locator('#ranking')).toContainText('HeroOne');
  await expect(page.locator('#event-table')).toContainText('900');
  await expect(page.locator('#drop-table')).toContainText('Box of Luck');
  await expect(page.locator('#drop-table')).toContainText('0.5%');
  // One card per event, its levels in a table.
  await expect(page.locator('#event-list .event')).toHaveCount(1);
  await expect(page.locator('#event-list .event tr')).toHaveCount(3);
  await page.locator('#vi').click();
  await expect(page.locator('#upcoming')).toContainText('Huyết Lâu');
  await expect(page.locator('#event-list .event h3')).toHaveText('Huyết Lâu');
  await expect(page.locator('#drop-table')).toContainText('Hộp May Mắn');
});

test('without the server nothing old is shown and the page says so', async ({ page }) => {
  await serve(page, 503);
  await page.goto('/home.html');
  await page.locator('#en').click();
  await expect(page.locator('#details-notice')).toContainText('unavailable');
  await expect(page.locator('#ranking td')).toHaveCount(1);
  await expect(page.locator('#ranking')).not.toContainText('HeroOne');
});

test('registration on the website', async ({ page }) => {
  await serve(page, DETAILS);
  let sent: Record<string, unknown> | null = null;
  await page.route('**/portal/register', async (route) => { sent = route.request().postDataJSON() as Record<string, unknown>; await route.fulfill({ json: { created: true } }); });
  await page.goto('/home.html');
  await page.locator('#en').click();
  // Google sign-in is hidden while the host hasn't set a client id.
  await expect(page.locator('#google-button')).toBeHidden();
  await expect(page.locator('#google-off')).toBeVisible();
  const form = page.locator('#register');
  await form.locator('input[name=loginName]').fill('newhero');
  await form.locator('input[name=password]').fill('secret1');
  await form.getByRole('button').click();
  await expect(page.locator('#register-message')).toContainText('Account created');
  expect(sent).toMatchObject({ loginName: 'newhero', password: 'secret1' });
});

test('Sign in with Google: a new player chooses a name, a known one may set a new password', async ({ page }) => {
  await serve(page, DETAILS, 'test-client.apps.googleusercontent.com');
  // A stand-in for Google's script: the button "signs in" with a fixed credential.
  await page.route('https://accounts.google.com/gsi/client', (route) => route.fulfill({
    contentType: 'text/javascript',
    body: `window.google = { accounts: { id: {
      initialize(o) { window.__googleCallback = o.callback; },
      renderButton(el) { const b = document.createElement('button'); b.id = 'fake-google'; b.textContent = 'Google'; b.onclick = () => window.__googleCallback({ credential: 'credential-1' }); el.append(b); }
    } } };`,
  }));
  const calls: Record<string, unknown>[] = [];
  let created = false;
  await page.route('**/portal/google', async (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    calls.push(body);
    if (body.loginName) { created = true; return route.fulfill({ json: { created: true } }); }
    if (body.newPassword) return route.fulfill({ json: { loginName: 'gnew', passwordChanged: true } });
    return route.fulfill({ json: created ? { loginName: 'gnew' } : { needsAccount: true, email: 'p@example.com' } });
  });
  await page.goto('/home.html');
  await page.locator('#en').click();
  await page.locator('#fake-google').click();
  await expect(page.locator('#google-account')).toBeVisible();
  await page.locator('#google-account input[name=loginName]').fill('gnew');
  await page.locator('#google-account input[name=password]').fill('pass123');
  await page.locator('#google-account').getByRole('button').click();
  await expect(page.locator('#google-login')).toHaveText('gnew');
  await page.locator('#google-linked input[name=newPassword]').fill('newpass9');
  await page.locator('#google-linked').getByRole('button').click();
  await expect(page.locator('#google-message')).toContainText('new game password');
  expect(calls.every((call) => call.credential === 'credential-1')).toBe(true);
});
