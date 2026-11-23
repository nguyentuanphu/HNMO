import { mkdir } from 'node:fs/promises';
import { test, expect } from '@playwright/test';

const name = (en: string, vi: string) => ({ en, vi });
const initial = { id: 2, name: name('Test Fighter', 'Đấu sĩ thử nghiệm'), canGetCreated: true, creationLevel: 82, isMasterClass: false };
const advanced = { id: 3, name: name('Test Master', 'Bậc thầy thử nghiệm'), canGetCreated: false, creationLevel: null, isMasterClass: true };
const servers = [{ id: 5, name: 'First test realm' }, { id: 8, name: 'Second test realm' }];

for (const [language, viewport] of [['en', { width: 1440, height: 900 }], ['vi', { width: 390, height: 844 }]] as const) {
  test(`live class fields, formula and skills follow the selected realm (${language})`, async ({ page }) => {
    await page.setViewportSize(viewport);
    const translated = language === 'vi';
    await page.route('**/website/class-guide**', async route => {
      const url = new URL(route.request().url());
      const serverId = Number(url.searchParams.get('serverId') ?? 5);
      const classId = Number(url.searchParams.get('classId') ?? 2);
      const detail = classId === 3 ? advanced : initial;
      await route.fulfill({ json: {
        enabled: true, serverId, servers, classes: [initial, advanced], details: {
          id: detail.id, home: name('Test field', 'Bãi thử nghiệm'), nextClass: detail.id === 2 ? advanced : null,
          stats: [{ attribute: 'Base Strength', value: serverId === 5 ? 47 : 63, increasableByPlayer: true }],
          constants: [{ attribute: 'Maximum Health', value: 150, aggregate: 'AddRaw' }],
          formulas: [{ target: 'Maximum Health', input: 'Total Vitality', operand: null, coefficient: 2.75, operation: 'Multiply', aggregate: 'AddFinal' }],
          skills: [{ id: 18, name: name('Test Strike', 'Đòn thử nghiệm'), range: 7, attackDamage: 96, masterRank: null,
            requirements: [{ attribute: 'Total Strength', value: 179 }], costs: [{ attribute: 'Current Mana', value: 30 }] }],
          truncated: false,
        },
      } });
    });
    await page.goto('/guide.html#classes');
    if (translated) await page.locator('[data-site-language="vi"]').last().click();
    const guide = page.locator('#classGuide');
    await expect(guide.getByRole('heading', { name: translated ? 'Đấu sĩ thử nghiệm' : 'Test Fighter' })).toBeVisible();
    await expect(guide).toContainText('82');
    await expect(guide).toContainText('47');
    const realmResponse = page.waitForResponse(response => response.url().includes('/website/class-guide?serverId=8'));
    await guide.getByLabel(translated ? 'Máy chủ' : 'Server', { exact: true }).selectOption('8');
    await realmResponse;
    await expect(guide).toContainText('63');
    await expect(guide).not.toContainText('47');
    await guide.locator('#classGuideFormulas summary').click();
    await expect(guide.locator('#classGuideFormulas')).toHaveAttribute('open', '');
    await expect(guide.locator('#classGuideFormulas')).toContainText(translated ? 'Tổng thể lực × 2,75' : 'Total Vitality × 2.75');
    await guide.locator('#classGuideSkills summary').click();
    await expect(guide.locator('#classGuideSkills')).toContainText(translated ? 'Đòn thử nghiệm' : 'Test Strike');
    await expect(guide.locator('#classGuideSkills')).toContainText('179');
    await expect(guide.locator('#classGuideSkills')).toContainText('30');
    expect(await guide.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await mkdir('docs/screenshots/class-guide', { recursive: true });
    await guide.screenshot({ path: `docs/screenshots/class-guide/class-${language}-${viewport.width}.png` });
    const classResponse = page.waitForResponse(response => response.url().includes('serverId=8&classId=3'));
    await guide.getByRole('button', { name: translated ? /Lớp kế tiếp/ : /Next class/ }).click();
    await classResponse;
    await expect(guide.getByRole('heading', { name: translated ? 'Bậc thầy thử nghiệm' : 'Test Master' })).toBeVisible();
    await expect(guide).not.toContainText('82');
  });
}

test('disabled and unavailable class configuration do not show example facts', async ({ page }) => {
  await page.route('**/website/class-guide**', route => route.fulfill({ json: { enabled: false } }));
  await page.goto('/guide.html#classes');
  const guide = page.locator('#classGuide');
  await expect(guide.getByRole('status')).toHaveText('The host has turned off the live class guide.');
  await expect(guide.getByRole('combobox')).toHaveCount(0);
  await page.unroute('**/website/class-guide**');
  await page.route('**/website/class-guide**', route => route.fulfill({ status: 503 }));
  await page.reload();
  await expect(guide.getByRole('status')).toHaveText('Class configuration is unavailable.');
  await expect(guide.getByRole('button', { name: 'Try again' })).toBeVisible();
  await expect(guide.getByRole('combobox')).toHaveCount(0);
});
