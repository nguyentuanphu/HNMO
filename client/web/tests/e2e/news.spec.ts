import { expect, test, type Page } from '@playwright/test';

const NEWS_FEED = {
  v: 1,
  enabled: true,
  allowWebsite: true,
  showLoginPanel: true,
  posts: [
    {
      id: 'post-1',
      titleEn: 'Weekend 2x EXP',
      titleVi: 'Sự kiện x2 EXP cuối tuần',
      bodyEn: 'Runs Sat-Sun Sydney time.',
      bodyVi: 'Diễn ra Thứ 7 - Chủ nhật giờ Sydney.',
      imageUrl: '/images/icon-192.png',
      publishAtUtc: Date.now() - 60_000,
      publishSydney: '2026-11-18 20:00',
      pinned: true,
      author: 'GM',
    },
    {
      id: 'post-2',
      titleEn: 'Unsafe Image Filtered',
      titleVi: 'Kiểm tra lọc ảnh không an toàn',
      bodyEn: 'Second notice',
      bodyVi: 'Thông báo thứ hai',
      imageUrl: 'javascript:alert(1)',
      publishAtUtc: Date.now() - 120_000,
      publishSydney: '2026-11-18 18:00',
      pinned: false,
      author: 'Admin',
    },
  ],
};

async function routeWebsiteBasics(page: Page): Promise<void> {
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'HNMO', links: {} } }));
  await page.route('**/website/settings', (route) => route.fulfill({ json: { googleClientId: '' } }));
  await page.route('**/website/data', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/details', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/news', (route) => route.fulfill({ json: NEWS_FEED }));
}

test('website #news section displays bilingual pinned posts and Sydney times', async ({ page }) => {
  await routeWebsiteBasics(page);
  await page.goto('/home.html');
  await page.locator('#en').click();

  const section = page.locator('#news-list');
  await expect(section.locator('article.panel')).toHaveCount(2);
  await expect(section).toContainText('Weekend 2x EXP');
  await expect(section).toContainText('[Pinned]');
  await expect(section).toContainText('2026-11-18 20:00 Sydney');
  await expect(section.locator('img')).toHaveCount(1);
  await expect(section.locator('img')).toHaveAttribute('src', '/images/icon-192.png');

  await page.locator('#vi').click();
  await expect(section).toContainText('Sự kiện x2 EXP cuối tuần');
  await expect(section).toContainText('[Ghim]');
});

test('login screen news panel and C2 F5 2A HnmoNewsNotice chat handler work in EN and VI', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await routeWebsiteBasics(page);

  await page.goto('/index.html');
  const panel = page.locator('#loginNewsPanel');
  await expect(panel).toBeVisible();
  await expect(panel).toContainText('Weekend 2x EXP');

  await page.locator('#loginLanguage button[data-lang="vi"]').click();
  await expect(panel).toContainText('Sự kiện x2 EXP cuối tuần');
  await page.locator('#loginLanguage button[data-lang="en"]').click();

  const chatResult = await page.evaluate(async () => {
    const { write } = await import('/js/net/packets.js' as string) as {
      write(group: string, name: string, values: Record<string, unknown>): Uint8Array;
    };
    const { NewsChatHandler } = await import('/js/ui/newsPanel.js' as string) as {
      NewsChatHandler: new (message: (text: string) => void) => { handle(bytes: Uint8Array): boolean };
    };
    const { setLanguage } = await import('/js/i18n.js' as string) as {
      setLanguage(lang: 'en' | 'vi'): Promise<void>;
    };

    const messages: string[] = [];
    const handler = new NewsChatHandler((msg) => messages.push(msg));
    const pkt1 = write('ServerToClient', 'HnmoNewsNotice', {
      Json: JSON.stringify({
        v: 1,
        fresh: true,
        latest: { id: 'chat-1', titleEn: 'Boss Spawn', titleVi: 'Boss Xuat Hien', bodyEn: 'Kundun awaits', bodyVi: 'Kundun da ra' },
      }),
    });
    await setLanguage('en');
    const handled1 = handler.handle(pkt1);
    await setLanguage('vi');
    const handled2 = handler.handle(pkt1);
    return { header: [...pkt1.slice(0, 5)], handled1, handled2, messages };
  });

  expect(chatResult.header[0]).toBe(0xc2);
  expect(chatResult.header[3]).toBe(0xf5);
  expect(chatResult.header[4]).toBe(0x2a);
  expect(chatResult.handled1).toBe(true);
  expect(chatResult.handled2).toBe(true);
  expect(chatResult.messages[0]).toContain('[News] Boss Spawn');
  expect(chatResult.messages[1]).toContain('[Tin tức] Boss Xuat Hien');
  expect(errors).toEqual([]);
});
