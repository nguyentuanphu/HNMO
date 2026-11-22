import { expect, test } from '@playwright/test';
import type { ServerGuideFacts, ServerGuideView } from '../../src/websiteServerGuideModels.js';

test.use({ timezoneId: 'America/New_York' });

const facts: ServerGuideFacts = {
  maximumInventoryMoney: 123456, maximumVaultMoney: 987654,
  classes: [{ id: 0, nameEn: 'Wizard', nameVi: 'Pháp sư', requiredLevel: 0, shieldConfigured: true, maximumMasterTier: 8 }],
  helper: { minLevel: 90, vipMinLevel: 10, maxLevel: 400, payIntervalSeconds: 300, costPerStage: [20, 50] },
  inventory: { maximumExtensions: 4 }, vault: { defaultVaultCount: 2, maxVaultsPerAccount: 5, vipExtraVaults: 1, purchaseExtraVaultCost: 200, purchaseCoinType: 'WCoinC' },
  partyExperience: [{ size: 3, percent: 180, differentClassesPercent: 230 }],
  vip: [{ id: 1, nameEn: 'Silver', nameVi: 'Bạc', experiencePercent: 5, zenPercent: 0, dailyCoins: 10, dailyCoinType: 'GoblinPoints', offers: [{ days: 7, price: 111, coinType: 'WCoinC' }] }],
  catalog: { products: [{ nameEn: 'Mana potion', nameVi: 'Bình Mana', itemLevel: 0, quantity: 10, durationSeconds: 0, price: 33, coinType: 'WCoinC', bundle: true }], truncated: false },
};

for (const width of [390, 1440]) for (const language of ['en', 'vi'] as const) {
  test(`live server guide follows selection and language at ${width}px (${language})`, async ({ page }) => {
    await page.setViewportSize({ width, height: 1000 });
    await page.addInitScript(lang => localStorage.setItem('hnmo.language', lang), language);
    await page.route('**/website/**', route => route.fulfill({ status: 503 }));
    await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'HNMO guide fixture' } }));
    await page.route('**/website/data', route => route.fulfill({ json: { updatedAt: '2026-11-22T00:00:00Z', worlds: [7, 8].map((id, index) => ({ worldId: String(id), season: index === 0 ? 's6' : 's9', seasonBadge: index === 0 ? 'S6.3' : 'S9', nameEn: 'World', nameVi: 'Thế giới', isRunning: true, isOpen: true, openingDateUtc: null, openingDateSydney: '', experienceMultiplier: 5, dropMultiplier: 1, zenMultiplier: 1, eventPresetEn: 'Standard', eventPresetVi: 'Tiêu chuẩn', totalPlayers: 1, totalCapacity: 100, servers: [{ id, players: 1, capacity: 100, isRunning: true }] })), servers: [7, 8].map(id => ({ id, players: 1, capacity: 100, experienceRate: 5, masterExperienceRate: 2, maximumLevel: 400, maximumMasterLevel: 200, maximumPartySize: 5, maximumCharactersPerAccount: 5, classes: [], maps: [], events: [] })) } }));
    await page.route('**/website/details', route => route.fulfill({ json: { updatedAt: '2026-11-22T00:00:00Z', characters: [], eventRankings: [], drops: [], upcoming: [{ serverId: 7, events: [{ name: 'Blood Castle', nameVi: 'Huyết Lâu', startsAt: '2026-11-22T00:00:00Z' }, { name: 'Invalid example', nameVi: 'Giờ mẫu không hợp lệ', startsAt: 'invalid' }] }] } }));
    let available = true;
    const view: ServerGuideView = { updatedAt: '2026-11-22T00:00:00Z', clientRules: { en: '<img src=x onerror=alert(1)>One client.', vi: 'Một client.' }, notForSale: { en: '', vi: '' }, servers: [{ id: 7, facts, extras: { systems: { antiCheatEnabled: true, activityLogEnabled: true, onlyValuableDrops: true }, quests: [{ nameEn: 'Promotion', nameVi: 'Thăng lớp', npcEn: 'Guide', npcVi: 'Hướng đạo', minimumLevel: 150, maximumLevel: 400, requiredStartMoney: 1000, classEn: '', classVi: '' }], questsTruncated: false, crafts: [{ nameEn: 'Example recipe', nameVi: 'Công thức mẫu', npcEn: 'Crafter', npcVi: 'Thợ chế tạo', itemEn: 'Wings', itemVi: 'Cánh', randomMinimumLevel: 0, randomMaximumLevel: 3, addLevel: 0, upgrade: false, basePercent: null, maximumPercent: 80, fixedZen: 1234, zenPerFinalPercentage: 20, excellentChance: 25, maximumExcellentOptions: 2 }], craftsTruncated: false } }, { id: 8, facts: { ...facts, helper: null, vip: [], catalog: { products: null, truncated: false } } }] };
    await page.route('**/website/server-guide', route => route.fulfill(available ? { json: view } : { status: 503 }));
    await page.goto('/home.html');
    const guide = page.locator('#server-guide');
    await expect(guide).toContainText(language === 'vi' ? 'Thông tin máy chủ' : 'Server rules at a glance');
    await expect(page.locator('#upcoming time')).toHaveCount(1);
    await expect(page.locator('#upcoming time')).toContainText('11:00');
    await expect(page.locator('#upcoming time')).toContainText('19:00');
    await expect(page.locator('#upcoming time')).toContainText('Sydney');
    await expect(page.locator('.server-hero-brand [data-game-name]')).toHaveText('HNMO guide fixture');
    await expect(page.locator('#server-guide-season')).toHaveText('S6.3');
    await expect(guide).toContainText(language === 'vi' ? 'Tùy nguyên liệu' : 'Depends on ingredients');
    await expect(guide).toContainText(language === 'vi' ? 'Thăng lớp' : 'Promotion');
    await expect(guide).toContainText('80%');
    await expect(guide).toContainText('123456'); await expect(guide).toContainText('111 WCoinC');
    await expect(guide).toContainText('230%'); await expect(guide).toContainText(language === 'vi' ? 'Bình Mana' : 'Mana potion');
    await expect(guide.locator('img')).toHaveCount(0);
    await expect(guide).not.toContainText(language === 'vi' ? 'Zen rơi thêm' : 'Dropped zen +');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.screenshot({ path: `docs/screenshots/server-guide/home-${language}-${width}.png`, fullPage: true });
    await page.locator('#server').selectOption('8'); await expect(page.locator('#server-guide-season')).toHaveText('S9'); await expect(guide).not.toContainText('111 WCoinC');
    await expect(guide).toContainText(language === 'vi' ? 'chưa công bố gói VIP' : 'No paid VIP offer');
    await page.locator('#server').selectOption('7'); await expect(guide).toContainText('111 WCoinC');
    available = false; await guide.getByRole('button').click();
    await expect(guide).toContainText(language === 'vi' ? 'Đã xóa quy định và giá cũ' : 'Old rules and prices have been cleared');
    await expect(guide).not.toContainText('111 WCoinC'); await expect(guide.locator('table')).toHaveCount(0);
  });
}
