import { expect, test, type Page } from '@playwright/test';

const SNAPSHOT = {
  updatedAt: new Date().toISOString(),
  worlds: [
    {
      worldId: 'main',
      nameEn: 'World 1',
      nameVi: 'Thế giới 1',
      isOpen: true,
      isRunning: true,
      openingDateSydney: '',
      totalPlayers: 5,
      experienceMultiplier: 1,
      dropMultiplier: 1,
      zenMultiplier: 1,
      eventPresetEn: 'Standard',
      eventPresetVi: 'Chuẩn',
      servers: [
        { id: 0, players: 2, capacity: 100, isRunning: true },
        { id: 1, players: 3, capacity: 100, isRunning: true },
      ],
    },
  ],
  servers: [
    {
      id: 0,
      description: 'Server 1 (Free)',
      vipOnly: false,
      pvpEnabled: true,
      players: 2,
      capacity: 100,
      experienceRate: 1,
      masterExperienceRate: 1,
      dropRate: 1,
      zenRate: 1,
      maximumLevel: 400,
      maximumMasterLevel: 200,
      maximumCharactersPerAccount: 5,
      maximumPartySize: 5,
      classes: [],
      maps: [],
      events: [],
    },
    {
      id: 1,
      description: 'Server 2 (VIP)',
      vipOnly: true,
      pvpEnabled: true,
      players: 3,
      capacity: 100,
      experienceRate: 5,
      masterExperienceRate: 5,
      dropRate: 2.5,
      zenRate: 3.0,
      maximumLevel: 400,
      maximumMasterLevel: 200,
      maximumCharactersPerAccount: 5,
      maximumPartySize: 5,
      classes: [],
      maps: [],
      events: [],
    },
  ],
};

async function routeBasics(page: Page): Promise<void> {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'HNMO', links: {} } }));
  await page.route('**/website/settings', (route) => route.fulfill({ json: { googleClientId: '' } }));
  await page.route('**/website/data', (route) => route.fulfill({ json: SNAPSHOT }));
  await page.route('**/website/details', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/news', (route) => route.fulfill({ json: { v: 1, enabled: false, posts: [] } }));
}

test('website shows VIP lock badge and per-server Drop/Zen rates in EN and VI', async ({ page }) => {
  await routeBasics(page);
  await page.goto('/home.html');
  await page.locator('#en').click();

  await expect(page.locator('#worlds-group')).toContainText('VIP only');
  await page.locator('#worlds-group button', { hasText: 'Server 1' }).click();
  await expect(page.locator('#stats')).toContainText('2.5×');
  await expect(page.locator('#stats')).toContainText('3×');

  await page.locator('#vi').click();
  await expect(page.locator('#worlds-group')).toContainText('Chỉ VIP');
});

test('server picker labels VIP server and LoginRefusedError explains refusal with /account.html#vip link', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await routeBasics(page);
  await page.goto('/index.html');

  const result = await page.evaluate(async () => {
    const { describeServerChoice } = await import('/js/main.js' as string) as {
      describeServerChoice(id: number, load: number, meta?: Record<string, unknown>, vi?: boolean): string;
    };
    const { LoginRefusedError, loginResultMessage } = await import('/js/net/session.js' as string) as {
      LoginRefusedError: new (code: number, message: string) => Error & { code: number; isVipRefusal: boolean };
      loginResultMessage(code: number): string;
    };
    const { setLanguage } = await import('/js/i18n.js' as string) as {
      setLanguage(lang: 'en' | 'vi'): Promise<void>;
    };

    const enChoice = describeServerChoice(1, 20, {
      worldLabel: 'World 1',
      description: 'Arena',
      vipOnly: true,
      experienceRate: 5,
      dropRate: 2.5,
      zenRate: 3,
      pvpEnabled: true,
    }, false);
    const viChoice = describeServerChoice(1, 20, {
      worldLabel: 'Thế giới 1',
      description: 'Arena',
      vipOnly: true,
      experienceRate: 5,
      dropRate: 2.5,
      zenRate: 3,
      pvpEnabled: false,
    }, true);

    await setLanguage('en');
    const errEn = new LoginRefusedError(9, loginResultMessage(9));
    await setLanguage('vi');
    const errVi = new LoginRefusedError(10, loginResultMessage(10));

    return {
      enChoice,
      viChoice,
      enMsg: errEn.message,
      enIsVip: errEn.isVipRefusal,
      viMsg: errVi.message,
      viIsVip: errVi.isVipRefusal,
    };
  });

  expect(result.enChoice).toContain('[VIP only]');
  expect(result.enChoice).toContain('Drop 2.5×');
  expect(result.viChoice).toContain('[Phòng VIP]');
  expect(result.viChoice).toContain('Zen 3×');
  expect(result.enIsVip).toBe(true);
  expect(result.enMsg).toContain('VIP');
  expect(result.viIsVip).toBe(true);
  expect(result.viMsg).toContain('VIP');
  expect(errors).toEqual([]);
});
