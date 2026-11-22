import { test, expect } from '@playwright/test';

test.describe('Website Stats / Build Calculator (ROADMAP 14.21)', () => {
  test('renders calculator shell and computes mocked server build output cleanly', async ({ page }) => {
    await page.route('**/website/build-calculator', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            v: 1,
            classes: [{ number: 16, name: 'Dark Knight', canCreate: true, isMaster: false, defaults: {} }],
            items: [{ group: 0, number: 1, nameEn: 'Short Sword', nameVi: 'Kiếm ngắn', maxLevel: 15, hasSkill: true, slots: [0, 1], slotGroupName: 'Weapons', classes: [] }],
          }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            className: 'Dark Knight',
            totalStrength: 500, totalAgility: 300, totalVitality: 150, totalEnergy: 100, totalLeadership: 0,
            minPhysDmg: 120, maxPhysDmg: 195, minWizDmg: 10, maxWizDmg: 20, minCurseDmg: 0, maxCurseDmg: 0,
            defenseBase: 75, defensePvm: 150, defensePvp: 180, defenseRatePvm: 100, defenseRatePvp: 200,
            attackRatePvm: 450, attackRatePvp: 520, attackSpeed: 35, magicSpeed: 15,
            maximumHealth: 1100, maximumMana: 350, maximumShield: 2400, maximumAbility: 450,
            criticalDamageChancePct: 5, excellentDamageChancePct: 10,
          }),
        });
      }
    });

    await page.route('**/website/settings', route => route.fulfill({ json: { theme: 'classic' } }));
    await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'HNMO calculator fixture' } }));
    await page.goto('/calculator.html');
    await expect(page.locator('#calc-class')).toHaveValue('16');
    await page.locator('#calc-run').click();
    await expect(page.locator('#calc-stats-grid')).toContainText('195');
    await expect(page.locator('#calc-stats-grid')).toContainText(/2[,.]400/);
    for (const width of [1280, 375]) {
      await page.setViewportSize({ width, height: 900 });
      await expect(page.locator('#calc-run')).toBeVisible();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    }
    await page.locator('#calc-lang-vi').click();
    await expect(page.locator('#calc-run')).toHaveText('Tính ngay');
    await page.locator('#calc-run').click();
    await expect(page.locator('#calc-stats-grid')).toContainText('195');
  });
});
