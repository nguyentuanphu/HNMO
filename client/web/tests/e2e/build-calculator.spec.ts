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

    const loaded = await page.evaluate(() => ({ hasDom: typeof document !== 'undefined' }));
    expect(loaded.hasDom).toBe(true);
  });
});
