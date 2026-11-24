import { mkdir } from 'node:fs/promises';
import { expect, test } from '@playwright/test';

for (const [language, viewport] of [['en', {width:1440,height:900}], ['vi', {width:390,height:844}]] as const) {
  test(`separates recorded combat modes in ${language}`, async ({ page }) => {
    const vi = language === 'vi';
    await page.setViewportSize(viewport);
    await page.addInitScript(language => localStorage.setItem('hnmo.language', language), language);
    const time = '2026-11-24T08:00:00+11:00';
    await page.route('**/website/tracker/pvp', route => route.fulfill({ json: {
      enabled: true, retentionDays: 30, groups: [
        {mode:'field', events:[{time, winner:null, defeated:null, serverId:1,mapNumber:7}]},
        {mode:'duel', events:[{time, winner:'FictionalA', defeated:'FictionalB',serverId:1,mapNumber:8}]},
        {mode:'arena', events:[{time, winner:'FictionalC', defeated:null,serverId:2,mapNumber:9}]},
        {mode:'siege', events:[]},
      ],
    } }));
    await page.goto('/pvp-history.html');
    const main = page.locator('#pvp-history');
    const rows = main.locator('#pvp-results');
    await expect(rows).toContainText(vi ? 'Bản đồ 7' : 'Map 7');
    await expect(rows).not.toContainText('FictionalA');
    await main.getByRole('button', { name: vi ? 'Đấu tay đôi' : 'Duel', exact:true }).click();
    await expect(rows).toContainText(vi ? 'FictionalA hạ FictionalB' : 'FictionalA defeated FictionalB');
    await expect(rows).toContainText('Sydney');
    await expect(main.locator('#pvp-retention')).toContainText(vi ? '30 ngày' : '30 days');
    expect(await main.evaluate(node => node.scrollWidth <= node.clientWidth)).toBe(true);
    await mkdir('docs/screenshots/pvp-history', { recursive:true });
    await main.screenshot({ path:`docs/screenshots/pvp-history/tracker-${language}-${viewport.width}.png` });
    await main.getByRole('button', { name: vi ? 'Đấu trường' : 'Arena', exact:true }).click();
    await expect(rows).toContainText(vi ? 'Người chơi ẩn danh' : 'Hidden player');
    await main.getByRole('button', { name: vi ? 'Công thành' : 'Siege', exact:true }).click();
    await expect(main.getByRole('status')).toContainText(vi ? 'Chưa có bản ghi' : 'No public records');
  });
}
