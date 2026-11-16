// HNMO Season Plan e2e tests: website #season-plan timeline and in-game F8 modal (F5 3F).
import { test, expect } from '@playwright/test';

type Win = Window & {
  __sp: { handle(bytes: Uint8Array): boolean; open(): void; isOpen(): boolean };
};

const samplePayload = {
  v: 1,
  enabled: true,
  now: Date.now(),
  maxBoxLevel: 3,
  gates: { ItemPlus10To15: true, MasterLevels: true, Harmony: false, Wing2: false, Fenrir: false, Items380: false, Wing3: false },
  stages: [
    {
      id: 'stage-1',
      nameEn: 'Stage 1: +9 Classic',
      nameVi: 'Giai đoạn 1: +9 Cổ điển',
      descriptionEn: 'Max +9 items.',
      descriptionVi: 'Tối đa +9.',
      unlocks: [],
      unlocked: true,
      unlockedAt: Date.now() - 86400_000,
      reason: 'date',
      unlockAt: Date.now() - 86400_000,
      requiredBossId: '',
      requiredBossKills: 0,
      currentBossKills: 0,
      requiredPlayerLevel: 0,
      requiredPlayersCount: 0,
      currentPlayers: 0,
      maxBoxLevel: 2,
    },
    {
      id: 'stage-4-wing2',
      nameEn: 'Stage 4: Second Wings',
      nameVi: 'Giai đoạn 4: Cánh Cấp 2',
      descriptionEn: 'Unlocks Wing 2 Chaos Mix.',
      descriptionVi: 'Mở xoay Cánh C2.',
      unlocks: ['Wing2'],
      unlocked: false,
      unlockedAt: null,
      reason: '',
      unlockAt: Date.now() + 7 * 86400_000,
      requiredBossId: 'red-dragon',
      requiredBossKills: 1,
      currentBossKills: 0,
      requiredPlayerLevel: 400,
      requiredPlayersCount: 10,
      currentPlayers: 4,
      maxBoxLevel: 4,
    },
  ],
};

test('public website #season-plan renders stages and unlock progress cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.route('**/website/season-plan', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(samplePayload),
  }));

  await page.goto('/home.html#season-plan');
  await expect(page.locator('#sp-timeline')).toContainText('Stage 1: +9 Classic');
  await expect(page.locator('#sp-timeline')).toContainText('OPEN');
  await expect(page.locator('#sp-timeline')).toContainText('Stage 4: Second Wings');
  await expect(page.locator('#sp-timeline')).toContainText('LOCKED');
  await expect(page.locator('#sp-timeline')).toContainText('Lv.400: 4/10');
  expect(errors).toEqual([]);
});

test('in-game F8 Season Plan window renders packet F5 3F timeline cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/index.html');

  await page.evaluate(async (json) => {
    const { SeasonPlanWindow } = await import('/js/ui/seasonPlanWindow.js' as string) as {
      SeasonPlanWindow: new (send: (n: string, f: Record<string, unknown>) => void) => Win['__sp'];
    };
    const { write } = await import('/js/net/packets.js' as string) as { write(g: string, n: string, v: Record<string, unknown>): Uint8Array };
    const w = window as unknown as Win;
    w.__sp = new SeasonPlanWindow(() => undefined);
    w.__sp.open();
    w.__sp.handle(write('ServerToClient', 'HnmoSeasonPlanState', { Json: json }));
  }, JSON.stringify(samplePayload));

  await expect(page.locator('#hnmo-season-plan-panel')).toBeVisible();
  await expect(page.locator('#hnmo-season-plan-panel')).toContainText('Stage 4: Second Wings');
  await expect(page.locator('#hnmo-season-plan-panel')).toContainText('red-dragon (0/1)');
  expect(errors).toEqual([]);
});
