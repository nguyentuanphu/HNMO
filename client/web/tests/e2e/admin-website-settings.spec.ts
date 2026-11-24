import { readFileSync } from 'node:fs';
import { expect, test } from '@playwright/test';
import { XMLParser } from 'fast-xml-parser';

test.use({ baseURL: process.env.ADMIN_URL ?? 'http://127.0.0.1:8095' });

const panels: readonly { id: string; fields: Readonly<Record<string, Readonly<Record<string, string>>>> }[] = [
  {
    id: '7a3e9c51-2d84-4b6f-9e07-51c8d2a4f6b3',
    fields: {
      WebsiteTheme: { Caption: 'GlobalDescription' },
      PlugInResources: {
        WebsiteConfiguration_AdminPath_Caption: 'WebsiteConfiguration_AdminPath_Description',
        WebsiteConfiguration_GoogleClientId_Caption: 'WebsiteConfiguration_GoogleClientId_Description',
        WebsiteConfiguration_RequireWebsiteLogin_Caption: 'WebsiteConfiguration_RequireWebsiteLogin_Description',
      },
      WebsiteServerPage: {
        ClientRulesEn: 'Description',
        ClientRulesVi: 'Description',
        NotForSaleEn: 'Description',
        NotForSaleVi: 'Description',
        TrackerEnabled: 'TrackerEnabledDescription',
        TrackerNames: 'TrackerNamesDescription',
        ClassGuide: 'ClassGuideDescription',
        EventGuide: 'EventGuideDescription',
        ItemGuide: 'ItemGuideDescription',
        CombatGuide: 'CombatGuideDescription',
        BannedCharacters: 'BannedCharactersDescription',
        ContentRoadmapEn: 'ContentDescription',
        ContentRoadmapVi: 'ContentDescription',
        ContentAboutEn: 'ContentDescription',
        ContentAboutVi: 'ContentDescription',
        ContentTermsEn: 'ContentDescription',
        ContentTermsVi: 'ContentDescription',
        ContentPrivacyEn: 'ContentDescription',
        ContentPrivacyVi: 'ContentDescription',
      },
    },
  },
  {
    id: '5b7e2c1a-9d43-4f6e-8a21-3c0f7d9b4e62',
    fields: {
      WebsiteBrand: {
        LogoCaption: 'LogoDescription',
        AccentCaption: 'AccentDescription',
        BackgroundCaption: 'BackgroundDescription',
        PanelCaption: 'PanelDescription',
        TextCaption: 'TextDescription',
      },
    },
  },
];

function resources(file: string, locale: string): Map<string, string> {
  const suffix = locale === 'en' ? '' : `.${locale}`;
  const resource = new URL(`../../../../server/src/GameLogic/Properties/${file}${suffix}.resx`, import.meta.url);
  const parser = new XMLParser({ ignoreAttributes: false, parseTagValue: false });
  const { root } = parser.parse(readFileSync(resource, 'utf8')) as {
    root: { data: { '@_name': string; value: string }[] };
  };
  return new Map(root.data.map(record => [record['@_name'], record.value]));
}

for (const [locale, width] of [['en', 1440], ['vi', 390], ['de', 1440], ['zh-CN', 390]] as const) {
  test.describe(locale, () => {
    test.use({ locale });
    test(`admin website and branding controls show localized help (${locale}, ${width}px)`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      for (const panel of panels) {
        const query = new URLSearchParams({ id: panel.id, culture: locale, 'ui-culture': locale });
        await page.goto(`/plugins?${query}`);
        const dialog = page.getByRole('dialog');
        const row = page.locator('tr').filter({ has: page.locator(`td[title="${panel.id}" i]`) });
        await expect(row).toBeVisible();
        if (!await dialog.isVisible()) await row.locator('button.btn-secondary').click();
        await expect(dialog).toBeVisible();
        const form = dialog.locator('form');
        for (const [file, fields] of Object.entries(panel.fields)) {
          const labels = resources(file, locale);
          for (const [captionKey, helpKey] of Object.entries(fields)) {
            const caption = labels.get(captionKey);
            const help = labels.get(helpKey);
            expect(caption, captionKey).toBeTruthy();
            expect(help, helpKey).toBeTruthy();
            const escapedCaption = caption!.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
            const field = form.locator('label').filter({ hasText: new RegExp(`^${escapedCaption}(?:\\s|$)`) });
            await expect(field).toBeVisible();
            await expect(field).toContainText(help!);
          }
        }
        if (panel === panels[0]) {
          await page.screenshot({
            path: `docs/screenshots/website-admin/settings-${locale}-${width}.png`,
            mask: [form.locator('input:not([type=checkbox]), textarea')],
          });
        }
      }
    });
  });
}
