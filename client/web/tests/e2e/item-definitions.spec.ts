// Verifies guide docs, /website/items bridge route handling, and live loadItemCatalog + EN/VI tooltip & glow merge.
import { test, expect } from '@playwright/test';
import type { ItemCatalog, ItemDefinition, ItemModelSource, WebsiteItemOverride } from '../../src/game/items.js';
import type { TooltipLine } from '../../src/game/itemTooltip.js';
import type { Item } from '../../src/game/inventory.js';
import type { Language } from '../../src/i18n.js';

interface ItemsModule {
  loadItemCatalog(): Promise<ItemCatalog>;
  applyItemOverrides(catalog: ItemCatalog, overrides: WebsiteItemOverride[]): void;
  itemDefinition(group: number, number: number): ItemDefinition | null;
  itemName(group: number, number: number): string;
  itemModel(group: number, number: number): ItemModelSource | null;
}

interface TooltipModule {
  loadTooltipData(): Promise<unknown>;
  tooltipLines(item: Item): TooltipLine[];
}

interface I18nModule {
  setLanguage(language: Language): Promise<void>;
}

test('loadItemCatalog merges /website/items overrides and renders EN/VI tooltips', async ({ page, request }) => {
  const guide = await request.get('/guide.html');
  expect(guide.ok()).toBeTruthy();
  expect(await guide.text()).toContain('Custom items');

  await page.route('**/website/items', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        version: 1,
        count: 1,
        items: [
          {
            group: 0,
            number: 0,
            name: { en: 'Admin Forged Kris', vi: 'Dao Găm Admin' },
            width: 2,
            height: 2,
            level: 33,
            requirements: { level: 90, strength: 210 },
            glowColor: '#ffaa00',
            tooltip: { en: 'Special web note', vi: 'Ghi chú đặc biệt' },
          },
        ],
      }),
    });
  });

  await page.goto('/index.html');
  const result = await page.evaluate(async () => {
    const loadModule = <T>(path: string): Promise<T> => import(path) as Promise<T>;
    const itemsMod = await loadModule<ItemsModule>('/js/game/items.js');
    const tooltipMod = await loadModule<TooltipModule>('/js/game/itemTooltip.js');
    const i18nMod = await loadModule<I18nModule>('/js/i18n.js');

    await itemsMod.loadItemCatalog();
    await tooltipMod.loadTooltipData();

    const def = itemsMod.itemDefinition(0, 0);
    const enName = itemsMod.itemName(0, 0);
    const dummyItem: Item = {
      group: 0,
      number: 0,
      level: 7,
      durability: 20,
      skill: false,
      luck: false,
      optionLevel: 0,
      optionType: 0,
      excellent: 0,
      ancientSet: 0,
      ancientBonus: 0,
      harmony: 0,
      guardian: false,
      sockets: [255, 255, 255, 255, 255],
      socketBonus: 255,
      length: 12,
      width: 2,
      height: 2,
    };
    await i18nMod.setLanguage('vi');
    const viName = itemsMod.itemName(0, 0);
    const viLines = tooltipMod.tooltipLines(dummyItem).map((l) => l.text);
    await i18nMod.setLanguage('en');
    const enLines = tooltipMod.tooltipLines(dummyItem).map((l) => l.text);

    return {
      width: def?.width,
      height: def?.height,
      glowColor: itemsMod.itemModel(0, 0)?.glow.color,
      enName,
      viName,
      hasViTip: viLines.some((t) => t.includes('Ghi chú đặc biệt')),
      hasEnTip: enLines.some((t) => t.includes('Special web note')),
    };
  });

  expect(result.width).toBe(2);
  expect(result.height).toBe(2);
  expect(result.glowColor).toBe('#ffaa00');
  expect(result.enName).toBe('Admin Forged Kris');
  expect(result.viName).toBe('Dao Găm Admin');
  expect(result.hasViTip).toBeTruthy();
  expect(result.hasEnTip).toBeTruthy();
});
