// The season of a world (S6.3 or S9): players pick it on the website and see it in the game's server list;
// the game then loads that season's data. Needs only the web client (no game server).
import { expect, test, type Page } from '@playwright/test';

function world(worldId: string, nameEn: string, nameVi: string, serverId: number, season?: 's6' | 's9') {
  return {
    worldId, nameEn, nameVi,
    ...(season ? { season, seasonBadge: season === 's9' ? 'S9' : 'S6.3' } : {}),
    isOpen: true, isRunning: true, openingDateSydney: '', totalPlayers: 1,
    experienceMultiplier: 1, dropMultiplier: 1, zenMultiplier: 1, eventPresetEn: 'Standard', eventPresetVi: 'Chuẩn',
    servers: [{ id: serverId, players: 1, capacity: 100, isRunning: true }],
  };
}

function server(id: number, description: string) {
  return {
    id, description, vipOnly: false, pvpEnabled: true, players: 1, capacity: 100,
    experienceRate: 1, masterExperienceRate: 1, dropRate: 1, zenRate: 1,
    maximumLevel: 400, maximumMasterLevel: 200, maximumCharactersPerAccount: 5, maximumPartySize: 5,
    classes: [], maps: [], events: [],
  };
}

const TWO_SEASONS = {
  updatedAt: new Date().toISOString(),
  worlds: [world('main', 'Origin', 'Khởi Nguyên', 0, 's6'), world('nine', 'Acheron', 'Acheron Mới', 5, 's9')],
  servers: [server(0, 'Classic'), server(5, 'New lands')],
};

// A server from before worlds had a season sends no season at all.
const OLD_SERVER = {
  updatedAt: new Date().toISOString(),
  worlds: [world('main', 'Origin', 'Khởi Nguyên', 0), world('second', 'Second', 'Thứ Hai', 1)],
  servers: [server(0, 'Classic'), server(1, 'Classic 2')],
};

async function routeWebsite(page: Page, snapshot: unknown): Promise<void> {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'HNMO', links: {} } }));
  await page.route('**/website/settings', (route) => route.fulfill({ json: { googleClientId: '' } }));
  await page.route('**/website/data', (route) => route.fulfill({ json: snapshot }));
  await page.route('**/website/details', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/news', (route) => route.fulfill({ json: { v: 1, enabled: false, posts: [] } }));
}

test('the website shows each world with its season and lets players pick S6.3 or S9, in EN and VI', async ({ page }) => {
  await routeWebsite(page, TWO_SEASONS);
  await page.goto('/home.html');
  await page.locator('#en').click();

  const worlds = page.locator('#worlds-group');
  await expect(worlds.locator('.world-card')).toHaveCount(2);
  await expect(worlds.locator('.world-card', { hasText: 'Origin' }).locator('.season-badge')).toHaveText('S6.3');
  await expect(worlds.locator('.world-card', { hasText: 'Acheron' }).locator('.season-badge')).toHaveText('S9');

  const picker = worlds.locator('.season-picker');
  await expect(picker.locator('button')).toHaveText(['All', 'S6.3', 'S9']);
  await expect(picker.locator('button[data-season="all"]')).toHaveAttribute('aria-pressed', 'true');

  await picker.locator('button[data-season="s9"]').click();
  await expect(worlds.locator('.world-card')).toHaveCount(1);
  await expect(worlds.locator('.world-card')).toContainText('Acheron');
  await expect(worlds.locator('.season-picker button[data-season="s9"]')).toHaveAttribute('aria-pressed', 'true');

  await worlds.locator('.season-picker button[data-season="s6"]').click();
  await expect(worlds.locator('.world-card')).toHaveCount(1);
  await expect(worlds.locator('.world-card')).toContainText('Origin');

  await page.locator('#vi').click();
  await expect(worlds.locator('.season-picker')).toContainText('Phiên bản');
  await expect(worlds.locator('.season-picker button[data-season="all"]')).toHaveText('Tất cả');
  await expect(worlds.locator('.world-card')).toContainText('Khởi Nguyên');
});

test('a server without seasons shows every world as S6.3 and no season choice', async ({ page }) => {
  await routeWebsite(page, OLD_SERVER);
  await page.goto('/home.html');
  await page.locator('#en').click();

  const worlds = page.locator('#worlds-group');
  await expect(worlds.locator('.world-card')).toHaveCount(2);
  await expect(worlds.locator('.season-badge')).toHaveText(['S6.3', 'S6.3']);
  await expect(worlds.locator('.season-picker')).toHaveCount(0);
});

test("the game's server list shows the season and the game loads the picked world's data", async ({ page }) => {
  await routeWebsite(page, TWO_SEASONS);
  await page.goto('/index.html');

  const result = await page.evaluate(async () => {
    const { describeServerChoice } = await import('/js/main.js' as string) as {
      describeServerChoice(id: number, load: number, meta?: Record<string, unknown>, vi?: boolean): string;
    };
    const { setSeason, currentSeason, seasonDataUrl } = await import('/js/dataRoot.js' as string) as {
      setSeason(key: 's6' | 's9'): void; currentSeason(): string; seasonDataUrl(url: string): string;
    };
    const { resolveDataUrl } = await import('/js/bmd.js' as string) as { resolveDataUrl(url: string): string };
    const { dataUrl } = await import('/js/audio/audioSystem.js' as string) as { dataUrl(file: string): string };

    const six = describeServerChoice(0, 10, { worldLabel: 'Origin', season: 's6', description: 'Classic' }, false);
    const nine = describeServerChoice(5, 10, { worldLabel: 'Acheron', season: 's9', description: 'New lands' }, false);
    const alone = describeServerChoice(0, 10, undefined, false);

    const before = { season: currentSeason(), model: resolveDataUrl('/data/Monster/Monster01.bmd'), sound: dataUrl('Data/Sound/a.wav') };
    setSeason('s9');
    const inNine = {
      season: currentSeason(),
      model: resolveDataUrl('/data/Monster/Monster01.bmd'),
      map: resolveDataUrl('/data/World83/EncTerrain83.map'),
      sound: dataUrl('Data/Sound/a.wav'),
      twice: seasonDataUrl('/data/s9/Monster/Monster01.bmd'),
      other: resolveDataUrl('/website/data'),
    };
    setSeason('s6');
    const after = { season: currentSeason(), model: resolveDataUrl('/data/Monster/Monster01.bmd') };
    return { six, nine, alone, before, inNine, after };
  });

  expect(result.six).toContain('season-badge season-s6');
  expect(result.six).toContain('>S6.3<');
  expect(result.nine).toContain('season-badge season-s9');
  expect(result.nine).toContain('>S9<');
  expect(result.alone).not.toContain('season-badge');

  expect(result.before).toEqual({ season: 's6', model: '/data/Monster/Monster01.bmd', sound: '/data/Sound/a.wav' });
  expect(result.inNine).toEqual({
    season: 's9',
    model: '/data/s9/Monster/Monster01.bmd',
    map: '/data/s9/World83/EncTerrain83.map',
    sound: '/data/s9/Sound/a.wav',
    twice: '/data/s9/Monster/Monster01.bmd',
    other: '/website/data',
  });
  expect(result.after).toEqual({ season: 's6', model: '/data/Monster/Monster01.bmd' });
});

test('the web server says which seasons it has data for, and a Season 9 address falls back to the base game', async ({ request }) => {
  const seasons = await request.get('/data/seasons.json');
  expect(seasons.ok()).toBeTruthy();
  expect((await seasons.json()).s6).toBe(true);

  // Whatever a season's pack doesn't have comes from the base game: the same bytes as the Season 6 address.
  const [base, nine] = await Promise.all([request.get('/data/Local/pet.bmd'), request.get('/data/s9/Local/pet.bmd')]);
  expect(base.ok()).toBeTruthy();
  expect(nine.ok()).toBeTruthy();
  expect((await nine.body()).equals(await base.body())).toBe(true);
});
