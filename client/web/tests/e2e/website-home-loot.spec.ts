import { expect, test } from '@playwright/test';
import type { ServerGuideFacts, ServerGuideView } from '../../src/websiteServerGuideModels.js';

const facts: ServerGuideFacts = {
  maximumInventoryMoney: 100, maximumVaultMoney: 200, classes: [], helper: null, inventory: null, vault: null,
  partyExperience: [], vip: [],
  catalog: { truncated: false, products: [{ nameEn: 'Fixture shield', nameVi: 'Khiên mẫu', itemLevel: 4, quantity: 1, durationSeconds: 172800, price: 71, coinType: 'WCoinC', bundle: true }] },
};

const view: ServerGuideView = {
  updatedAt: '2026-11-23T00:00:00Z', clientRules: { en: '', vi: '' }, notForSale: { en: '', vi: '' },
  servers: [{ id: 7, facts, extras: {
    systems: { antiCheatEnabled: false, activityLogEnabled: true, onlyValuableDrops: false },
    availability: { helper: false, multiVault: false, shop: true, vip: false },
    quests: [], questsTruncated: false,
    crafts: [{
      nameEn: 'Shield mix', nameVi: 'Ép khiên', npcEn: 'Crafter', npcVi: 'Thợ chế tạo',
      itemEn: 'Fixture shield', itemVi: 'Khiên mẫu', randomMinimumLevel: 4, randomMaximumLevel: 5,
      upgrade: false, addLevel: 0, basePercent: null, maximumPercent: 81,
      fixedZen: 5, zenPerFinalPercentage: 23, excellentChance: null, maximumExcellentOptions: 0,
      ingredients: [{
        minimumAmount: 2, maximumAmount: 4, minimumItemLevel: 3, maximumItemLevel: 5,
        anyItem: false, choicesTruncated: false, items: [{ nameEn: 'Fixture shard', nameVi: 'Mảnh mẫu' }], options: [],
      }],
      ingredientsTruncated: false,
    }],
    craftsTruncated: false,
    loot: {
      gear: [{ nameEn: 'Fixture shield', nameVi: 'Khiên mẫu', slotEn: 'Off hand', slotVi: 'Tay phụ',
        source: 'field', sourceEn: 'Fixture guard', sourceVi: 'Vệ binh mẫu', mapEn: 'Fixture forest', mapVi: 'Rừng mẫu', maximumLevel: 11 }],
      gearTruncated: false,
      groups: [{ nameEn: 'Fixture group', nameVi: 'Nhóm mẫu', minimumChance: 0.0123, maximumChance: 0.0456 }],
      groupsTruncated: false,
    },
  } }, { id: 8, facts: { ...facts, catalog: { products: [], truncated: false } } }],
};

for (const [width, language] of [[390, 'vi'], [1440, 'en']] as const) {
  test(`configured home sources, mixes and shop on a ${width}px ${language} page`, async ({ page }) => {
    await page.setViewportSize({ width, height: 940 });
    await page.addInitScript(language => localStorage.setItem('hnmo.language', language), language);
    await page.route('**/website/**', route => route.fulfill({ status: 503 }));
    await page.route('**/website/data', route => route.fulfill({ json: { updatedAt: view.updatedAt, servers: [7, 8].map(id => ({
      id, players: 0, capacity: 100, experienceRate: 1, masterExperienceRate: 1, maximumLevel: 400, maximumMasterLevel: 100,
      maximumPartySize: 5, maximumCharactersPerAccount: 5, classes: [], maps: [], events: [],
    })) } }));
    await page.route('**/website/server-guide', route => route.fulfill({ json: view }));
    const vi = language === 'vi';
    await page.goto('/home.html');
    const guide = page.locator('#server-guide');
    const gear = guide.locator('article').filter({ has: page.getByRole('heading', { name: vi ? 'Cấp trang bị cao nhất theo ô' : 'Highest equipment levels by slot' }) });
    const drops = guide.locator('article').filter({ has: page.getByRole('heading', { name: vi ? 'Phân tích nhóm rơi đồ ở bãi' : 'Field drop group analysis' }) });
    await expect(gear).toContainText(vi ? 'Khiên mẫu +11' : 'Fixture shield +11');
    await expect(gear).toContainText(vi ? 'Vệ binh mẫu tại Rừng mẫu' : 'Fixture guard in Fixture forest');
    await expect(drops).toContainText(vi ? /1,23\s*%–4,56\s*%/ : '1.23%–4.56%');
    await expect(guide).toContainText(vi ? 'Tùy nguyên liệu' : 'Depends on ingredients');
    await expect(guide).toContainText(vi ? '2–4 × Mảnh mẫu' : '2–4 × Fixture shard');
    await expect(guide).toContainText(vi ? '2 ngày' : '2 days');
    await expect(guide).toContainText(vi ? 'Cửa hàng coin: Có.' : 'Cash shop: Yes.');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await gear.screenshot({ path: `docs/screenshots/home-loot/gear-${language}-${width}.png` });
    await drops.screenshot({ path: `docs/screenshots/home-loot/drops-${language}-${width}.png` });
    await page.locator('#server').selectOption('8');
    await expect(gear).toHaveCount(0);
    await expect(drops).toHaveCount(0);
    await expect(guide).not.toContainText(vi ? 'Khiên mẫu' : 'Fixture shield');
  });
}
