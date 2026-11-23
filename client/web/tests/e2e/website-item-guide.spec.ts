import { mkdir } from 'node:fs/promises';
import { expect, test } from '@playwright/test';

const name = (en: string, vi: string) => ({ en, vi });
const first = { number: 18, name: name('Test blade', 'Kiếm thử nghiệm') };
const box = { number: 19, name: name('Test box', 'Hộp thử nghiệm') };
const last = { number: 43, name: name('Test armor', 'Giáp thử nghiệm') };
const simple = { index: 0, name: name('Test mix', 'Công thức thử nghiệm'), npc: name('Test crafter', 'Thợ thử nghiệm') };
const specialized = { index: 1, name: name('Special test mix', 'Công thức đặc biệt thử nghiệm'), npc: simple.npc };
const servers = [{ id: 0, name: 'First test server' }, { id: 2, name: 'Second test server' }];

for (const [language, viewport] of [['en', { width: 1440, height: 900 }], ['vi', { width: 390, height: 844 }]] as const) {
  test(`checks configured drop restrictions, box contents and mix quotes (${language})`, async ({ page }) => {
    await page.setViewportSize(viewport);
    const vi = language === 'vi';
    await page.route('**/website/item-guide**', async route => {
      const query = new URL(route.request().url()).searchParams;
      const serverId = Number(query.get('serverId') ?? 0);
      const itemPage = Number(query.get('page') ?? 1);
      const choices = itemPage === 1 ? [first, box] : [last];
      const selected = choices.find(row => row.number === Number(query.get('number'))) ?? choices[0];
      const selectedMix = query.get('mixIndex') === '1' ? specialized : simple;
      const empty = serverId === 2;
      await route.fulfill({ json: { enabled: true, serverId, servers, guide: {
        groups: empty ? [] : [3], group: empty ? null : 3, itemPage, itemPages: 2, items: empty ? [] : choices,
        item: empty ? null : { ...selected, slot: name('Test hand slot', 'Ô tay thử nghiệm'), maximumItemLevel: 11,
          dropsFromMonsters: true, minimumMonsterLevel: 31, maximumDropLevel: 62, classes: [name('Test knight', 'Hiệp sĩ thử nghiệm')],
          requirements: selected.number === 18 ? [{ attribute: 'Total Strength', value: 150, minimumItemLevel: 4 }] : [],
          sources: selected.number === 18 ? [{ map: name('Test field', 'Bãi thử nghiệm'), monster: name('Test target', 'Quái thử nghiệm'), level: 45, monsterPool: false, mapPool: true }] : [],
          sourcesTruncated: false, worldPools: [], foundInBoxes: [], contents: selected.number === 19 ? [
            { sourceLevel: 5, requiredCharacterLevel: 77, pools: [{ kind: 'None', money: null, minimumLevel: 2, maximumLevel: 7, items: [first.name] },
              { kind: 'Money', money: 917, minimumLevel: 0, maximumLevel: 0, items: [] }] },
          ] : [],
        },
        mixes: empty ? [] : [simple, specialized], mixPage: 1, mixPages: 1,
        mix: empty ? null : { ...selectedMix, maps: [name('Test workshop', 'Xưởng thử nghiệm')], settings: selectedMix.index === 0 ? {
          basePercent: 61, maximumPercent: 77, npcPriceDivisor: null, fixedZen: 444, zenPerFinalPercentage: 0,
          ingredients: [{ items: [first.name], acceptsAnyItem: false, minimumAmount: 3, maximumAmount: 4, minimumItemLevel: 6,
            maximumItemLevel: 9, npcPriceDivisor: 0, addPercentage: 3, reference: 2, options: [name('Test option', 'Thuộc tính thử nghiệm')] }],
          results: [{ name: null, randomMinimumLevel: 0, randomMaximumLevel: 0, reference: 2, addLevel: 1 }],
        } : null },
      } } });
    });
    await page.goto('/guide.html#item-guide');
    if (vi) await page.locator('[data-site-language="vi"]').last().click();
    const guide = page.locator('#itemGuide');
    const itemPanel = guide.locator('.item-guide-panels > section').first();
    const recipePanel = guide.locator('.item-guide-panels > section').last();
    await expect(itemPanel.getByRole('heading', { name: vi ? 'Kiếm thử nghiệm' : 'Test blade', exact: true })).toBeVisible();
    await expect(itemPanel).toContainText(vi ? 'Tổng sức mạnh ≥ 150' : 'Total Strength ≥ 150');
    await expect(itemPanel).toContainText('31–62');
    await expect(itemPanel).toContainText(vi ? 'Bãi thử nghiệm' : 'Test field');
    await itemPanel.getByLabel(vi ? 'Vật phẩm' : 'Item', { exact: true }).selectOption('19');
    await expect(itemPanel.getByRole('heading', { name: vi ? 'Hộp thử nghiệm' : 'Test box', exact: true })).toBeVisible();
    await itemPanel.locator('summary').click();
    await expect(itemPanel).toContainText('917 Zen');
    await expect(itemPanel).toContainText('2–7');
    await expect(itemPanel).toContainText(vi ? 'Cấp cần để mở · 77' : 'Opening level required · 77');
    await expect(recipePanel).toContainText('61%');
    await expect(recipePanel).toContainText('77%');
    await expect(recipePanel).toContainText('444 Zen');
    await expect(recipePanel).toContainText(vi ? 'Thuộc tính thử nghiệm' : 'Test option');
    expect(await guide.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await mkdir('docs/screenshots/item-guide', { recursive: true });
    await guide.screenshot({ path: `docs/screenshots/item-guide/guide-${language}-${viewport.width}.png` });
    await recipePanel.getByLabel(vi ? 'Công thức' : 'Recipe', { exact: true }).selectOption('1');
    await expect(recipePanel).toContainText(vi ? 'Xem báo giá của công thức này trong game.' : 'Get this recipe’s quote in game.');
    await expect(recipePanel).not.toContainText('61%');
    await itemPanel.getByRole('button', { name: vi ? 'Trang sau' : 'Next', exact: true }).click();
    await expect(itemPanel.getByRole('heading', { name: vi ? 'Giáp thử nghiệm' : 'Test armor', exact: true })).toBeVisible();
    await guide.getByLabel(vi ? 'Máy chủ' : 'Server', { exact: true }).selectOption('2');
    await expect(guide).toContainText(vi ? 'Máy chủ này chưa công bố vật phẩm đang hoạt động.' : 'This server reports no active items.');
    await expect(recipePanel).not.toContainText('444 Zen');
  });
}

test('item guide reports disabled and unavailable states without showing a catalogue', async ({ page }) => {
  await page.route('**/website/item-guide**', route => route.fulfill({ json: { enabled: false } }));
  await page.goto('/guide.html#item-guide');
  const guide = page.locator('#itemGuide');
  await expect(guide.getByRole('status')).toHaveText('The host has turned off the live item and mix guide.');
  await expect(guide.getByRole('combobox')).toHaveCount(0);
  await page.unroute('**/website/item-guide**');
  await page.route('**/website/item-guide**', route => route.fulfill({ status: 503 }));
  await page.reload();
  await expect(guide.getByRole('status')).toHaveText('Item and mix configuration is unavailable.');
  await expect(guide.getByRole('button', { name: 'Try again' })).toBeVisible();
});
