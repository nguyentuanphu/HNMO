// HNMO Mount Chain e2e tests: public website #mounts table and in-game Shift+U modal (F5 43).
import { test, expect } from '@playwright/test';

type Win = Window & {
  __mc: { handle(bytes: Uint8Array): boolean; open(): void; isOpen(): boolean };
};

const samplePayload = {
  v: 1,
  enabled: true,
  goldenDropChancePercent: 100,
  shardsPerGoldenBoss: 2,
  shards: 14,
  activeTierId: 'horn-uniria',
  totalCollectors: 7,
  tiers: [
    {
      id: 'horn-uniria',
      nameEn: 'Horn of Uniria',
      nameVi: 'Kỳ Lân (Uniria)',
      shardsRequired: 3,
      previousTierId: '',
      previousMountsRequired: 0,
      zenCost: 500_000,
      shardsOnSalvage: 2,
      pveDefenseBonus: 5,
      bonusExpPercent: 2,
      bonusZenPercent: 5,
      collectorsCount: 7,
      unlocked: true,
      unlockedAt: Date.now() - 3600_000,
      bagDuplicates: 1,
      canCraft: true,
    },
    {
      id: 'golden-wolf',
      nameEn: 'Golden Fenrir',
      nameVi: 'Sói Hoàng Kim',
      shardsRequired: 20,
      previousTierId: 'fenrir-wolf',
      previousMountsRequired: 2,
      zenCost: 6_000_000,
      shardsOnSalvage: 12,
      pveDefenseBonus: 30,
      bonusExpPercent: 8,
      bonusZenPercent: 20,
      collectorsCount: 1,
      unlocked: false,
      unlockedAt: null,
      bagDuplicates: 0,
      canCraft: false,
    },
  ],
};

test('public website #mounts renders mount chain tiers and collectors cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.route('**/website/mounts', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(samplePayload),
  }));

  await page.goto('/home.html#mounts');
  await expect(page.locator('#mounts-table')).toContainText('Horn of Uniria');
  await expect(page.locator('#mounts-table')).toContainText('Golden Fenrir');
  expect(errors).toEqual([]);
});

test('in-game Shift+U Mount Chain window renders F5 43 collection and shards cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/index.html');

  await page.evaluate(async (json) => {
    const { MountChainWindow } = await import('/js/ui/mountChainWindow.js' as string) as {
      MountChainWindow: new (send: (n: string, f: Record<string, unknown>) => void) => Win['__mc'];
    };
    const { write } = await import('/js/net/packets.js' as string) as { write(g: string, n: string, v: Record<string, unknown>): Uint8Array };
    const w = window as unknown as Win;
    w.__mc = new MountChainWindow(() => undefined);
    w.__mc.open();
    w.__mc.handle(write('ServerToClient', 'HnmoMountChainState', { Json: json }));
  }, JSON.stringify(samplePayload));

  await expect(page.locator('#hnmo-mount-chain-panel')).toBeVisible();
  await expect(page.locator('#hnmo-mount-chain-panel')).toContainText('Horn of Uniria');
  await expect(page.locator('#hnmo-mount-chain-panel')).toContainText('ACTIVE');
  expect(errors).toEqual([]);
});
