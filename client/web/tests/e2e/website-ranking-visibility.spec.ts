import { mkdir } from 'node:fs/promises';
import { expect, test } from '@playwright/test';

for (const [lang, width] of [['en', 1440], ['vi', 390]] as const) {
  test(`shows anonymous rankings and only published character bans in ${lang}`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.addInitScript(language => localStorage.setItem('hnmo.language', language), lang);
    let bans: string[] | null = ['TestKnight'];
    await page.route('**/website/details', route => route.fulfill({ json: {
      characters: [{ rank: 1, name: null, className: 'Test class', level: 50, masterLevel: 0, resets: 0 }],
      eventRankings: [], upcoming: [], drops: [], bannedCharacters: bans,
    } }));
    await page.goto('/home.html');
    await expect(page.locator('#ranking')).toContainText('—');
    await expect(page.locator('#ranking')).not.toContainText('null');
    await expect(page.locator('#banned-characters-title')).toHaveText(lang === 'vi' ? 'Nhân vật đang bị khóa' : 'Active character bans');
    await expect(page.locator('#banned-characters-list')).toHaveText('TestKnight');
    await mkdir('docs/screenshots/ranking-visibility', { recursive: true });
    await page.locator('#rankings').screenshot({ path: `docs/screenshots/ranking-visibility/rankings-${lang}-${width}.png` });
    bans = null;
    await page.reload();
    await expect(page.locator('#ranking')).toContainText('Test class');
    await expect(page.locator('#banned-characters')).toBeHidden();
  });
}
