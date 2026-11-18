// HNMO Party Options e2e tests: website #party-spots table and in-game Auto-Party / Password drawer (F5 45).
import { test, expect } from '@playwright/test';

type Win = Window & {
  __po: { handle(bytes: Uint8Array): boolean; open(): void; isOpen(): boolean };
};

const samplePayload = {
  v: 1,
  enabled: true,
  allowPasswordJoin: true,
  allowAutoSpotParty: true,
  huntingSpots: ['Lorencia', 'Devias', 'LostTower 7'],
  hasParty: false,
  isLeader: true,
  ownAd: null,
  ads: [
    {
      leaderName: 'DarkLord99',
      mapName: 'LostTower',
      spotName: 'LostTower 7',
      minLevel: 80,
      maxLevel: 350,
      hasPassword: false,
      memberCount: 3,
      maxSize: 5,
      canJoin: true,
    },
    {
      leaderName: 'ElfGuild',
      mapName: 'Icarus',
      spotName: 'Icarus Blue',
      minLevel: 150,
      maxLevel: 400,
      hasPassword: true,
      memberCount: 2,
      maxSize: 5,
      canJoin: false,
    },
  ],
};

test('public website #party-spots renders hunting spot parties without exposing passwords', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.route('**/website/party-spots', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(samplePayload),
  }));

  await page.goto('/home.html#party-spots');
  await expect(page.locator('#party-spots-table')).toContainText('DarkLord99');
  await expect(page.locator('#party-spots-table')).toContainText('LostTower 7 (LostTower)');
  await expect(page.locator('#party-spots-table')).toContainText('Password (/partypass)');
  expect(errors).toEqual([]);
});

test('in-game Auto-Party & Password drawer renders F5 45 spot parties and password box cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/index.html');

  await page.evaluate(async (json) => {
    const { PartyOptionsWindow } = await import('/js/ui/partyOptionsWindow.js' as string) as {
      PartyOptionsWindow: new (send: (n: string, f: Record<string, unknown>) => void) => Win['__po'];
    };
    const { write } = await import('/js/net/packets.js' as string) as { write(g: string, n: string, v: Record<string, unknown>): Uint8Array };
    const w = window as unknown as Win;
    w.__po = new PartyOptionsWindow(() => undefined);
    w.__po.open();
    w.__po.handle(write('ServerToClient', 'HnmoPartyOptionsState', { Json: json }));
  }, JSON.stringify(samplePayload));

  await expect(page.locator('#hnmo-party-options-panel')).toBeVisible();
  await expect(page.locator('#hnmo-party-options-panel')).toContainText('LostTower 7');
  await expect(page.locator('#hnmo-party-options-panel')).toContainText('DarkLord99');
  expect(errors).toEqual([]);
});
