import { mkdir } from 'node:fs/promises';
import { expect, test } from '@playwright/test';

const rules = {
  enabled: true,
  hit: {
    multiplier: 2, emptyShare: 0.5, duelFactor: 0.6,
    penalties: [{ levelGap: 300, penalty: 0.15 }, { levelGap: 200, penalty: 0.10 }, { levelGap: 100, penalty: 0.05 }],
    example: { attackRate: 900, defenseRate: 600, attackerLevel: 200, defenderLevel: 300, chance: 0.43 },
  },
  servers: [
    {
      id: 1, name: 'Fictional realm', moreBosses: false,
      loot: { splitExpByDamage: true, dropToTopDamageDealer: true, example: { firstDamage: 750, finishingDamage: 250, firstShare: 0.75, finishingShare: 0.25 } },
      limits: { pvpReduction: 12, siegeMapReduction: 34, damageCap: 950 },
      bosses: [{
        name: { en: 'Fictional guardian', vi: 'Hộ vệ hư cấu' }, partyRank: true, topAlsoGetParticipation: false,
        participationMinimumPercent: 3, participationMinimumDamage: 2500, hasParticipation: true, hasLastHit: true, topRanks: [1, 10],
      }],
    },
    {
      id: 2, name: 'Second fictional realm', moreBosses: false,
      loot: { splitExpByDamage: false, dropToTopDamageDealer: false, example: { firstDamage: 750, finishingDamage: 250, firstShare: 0, finishingShare: 1 } },
      limits: null, bosses: [],
    },
  ],
};

for (const [language, viewport] of [['en', { width: 1440, height: 900 }], ['vi', { width: 390, height: 844 }]] as const) {
  test(`shows the server's combat and loot rules in ${language}`, async ({ page }) => {
    await page.setViewportSize(viewport);
    const vi = language === 'vi';
    await page.route('**/website/combat-guide', route => route.fulfill({ json: rules }));
    await page.goto('/guide.html#combat-guide');
    if (vi) await page.locator('[data-site-language="vi"]').last().click();
    const guide = page.locator('#combat-guide');
    await expect(guide.getByRole('heading', { name: vi ? 'Quy tắc PvP và chiến lợi phẩm' : 'PvP and loot rules' })).toBeVisible();
    const picker = guide.getByLabel(vi ? 'Máy chủ' : 'Game server', { exact: true });
    await expect(picker).toHaveValue('1');
    await expect(guide).toContainText('43%');
    await expect(guide).toContainText('75%');
    await expect(guide).toContainText('12%');
    await expect(guide).toContainText(vi ? 'Hộ vệ hư cấu' : 'Fictional guardian');
    await expect(guide).toContainText('4–10');
    expect(await guide.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await mkdir('docs/screenshots/combat-guide', { recursive: true });
    await guide.screenshot({ path: `docs/screenshots/combat-guide/guide-${language}-${viewport.width}.png` });
    await picker.selectOption('2');
    await expect(guide).toContainText(vi ? 'Người kết liễu hoặc nhóm của họ có quyền nhặt ban đầu.' : 'The finishing player or their party gets initial pickup rights.');
    await expect(guide).toContainText(vi ? 'Máy chủ này chưa cấu hình boss thế giới.' : 'No world bosses are configured on this server.');
  });
}

test('hides the combat navigation and section when the host disables it', async ({ page }) => {
  await page.route('**/website/combat-guide', route => route.fulfill({ json: { enabled: false } }));
  const data = page.waitForResponse('**/website/combat-guide');
  await page.goto('/guide.html');
  await data;
  await expect(page.locator('#combat-guide')).toBeHidden();
  await expect(page.locator('[data-combat-guide-link]')).toBeHidden();
  await expect(page.locator('#controls')).toBeVisible();
});
