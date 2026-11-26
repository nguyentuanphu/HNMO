import { mkdir } from 'node:fs/promises';
import { expect, test } from '@playwright/test';

test.use({ timezoneId: 'America/Los_Angeles' });
const name = (en: string, vi: string) => ({ en, vi });
const servers = [{ id: 1, name: 'Test server A' }, { id: 7, name: 'Test server B' }];
const timedEvent = {
  id: 'test-event', category: 'event', name: name('Test event', 'Sự kiện thử nghiệm'),
  nextStartUtc: '2026-11-24T01:30:00Z', endsAtUtc: null, isActive: false, invasion: null, boss: null, winner: null,
  tiers: [{ tier: 2, name: name('Test challenge', 'Thử thách mẫu'), map: name('Test hall', 'Điện thử nghiệm'),
    minimumLevel: 73, maximumLevel: 118, specialMinimumLevel: 0, specialMaximumLevel: 0, requiresMasterClass: false,
    entranceFee: 62, durationSeconds: 180, ticket: name('Test ticket', 'Vé thử nghiệm'), ticketLevel: 2,
    rewards: [{ kind: 'ExperiencePerRemainingSeconds', amount: 37, rank: null, success: ['Winner'], requiredKill: null, pool: [] }],
  }],
};
const invasion = {
  id: 'test-invasion', category: 'invasion', name: name('Test invasion', 'Xâm lăng thử nghiệm'),
  nextStartUtc: null, endsAtUtc: null, isActive: false, tiers: [], boss: null, winner: null,
  invasion: { singleMap: false, spawns: [{ monster: name('Test invader', 'Quái xâm lăng thử nghiệm'), count: 5,
    everyMap: false, maps: [name('Test field', 'Bãi thử nghiệm')], loot: [name('Test token', 'Thẻ thử nghiệm')] }] },
};
const siege = {
  id: 'event-castle-siege', category: 'event', name: name('Castle Siege', 'Công thành chiến'),
  nextStartUtc: '2026-11-24T07:23:00Z', scheduledEndUtc: '2026-11-24T08:02:00Z',
  endsAtUtc: null, isActive: false, tiers: [], invasion: null, boss: null, winner: null,
};
const quest = {
  group: 1, number: 2, name: name('Test quest', 'Nhiệm vụ thử nghiệm'), npc: name('Test guide', 'NPC thử nghiệm'),
  maps: [name('Test field', 'Bãi thử nghiệm')], minimumLevel: 30, maximumLevel: 94, requiredStartMoney: 123,
  repeatable: true, qualifiedClass: null, items: [{
    name: name('Test artifact', 'Cổ vật thử nghiệm'), count: 4,
    dropGroup: { chance: 0.15, monster: name('Test invader', 'Quái xâm lăng thử nghiệm'), minimumMonsterLevel: 24, maximumMonsterLevel: 34 },
  }], kills: [{ name: name('Test invader', 'Quái xâm lăng thử nghiệm'), count: 7 }],
  rewards: [{ kind: 'Money', value: 1456, name: null, level: null, attribute: null }],
};

for (const [language, viewport] of [['en', { width: 1440, height: 900 }], ['vi', { width: 390, height: 844 }]] as const) {
  test(`event times, invasion loot, and quest rewards follow the active server (${language})`, async ({ page }) => {
    await page.setViewportSize(viewport);
    const vi = language === 'vi';
    await page.route('**/website/event-guide**', async route => {
      const serverId = Number(new URL(route.request().url()).searchParams.get('serverId') ?? 1);
      await route.fulfill({ json: { enabled: true, serverId, servers, guide: {
        events: serverId === 1 ? [timedEvent, invasion, siege] : [], eventsTruncated: false,
        quests: serverId === 1 ? [quest] : [], questsTruncated: false,
      } } });
    });
    await page.goto('/guide.html#event-guide');
    if (vi) await page.locator('[data-site-language="vi"]').last().click();
    const guide = page.locator('#eventGuide');
    const event = guide.getByRole('article').filter({ has: page.getByRole('heading', { name: vi ? 'Sự kiện thử nghiệm' : 'Test event', exact: true }) });
    await expect(event).toContainText(vi ? '12:30 24 thg 11, 2026 (Sydney)' : '24 Nov 2026, 12:30 (Sydney)');
    const visitorDate = vi ? '17:30 23 thg 11, 2026' : '23 Nov 2026, 17:30';
    await expect(event).not.toContainText(visitorDate);
    await guide.getByRole('checkbox', { name: vi ? 'Hiển thị giờ của tôi' : 'Show my time' }).check();
    await expect(event).toContainText(visitorDate);
    await event.locator('summary').click();
    await expect(event).toContainText(vi ? '37 EXP mỗi giây còn lại' : '37 EXP per remaining second');
    await expect(event).toContainText('62 Zen');
    const siegeCard = guide.getByRole('article').filter({ has: page.getByRole('heading', { name: vi ? 'Công thành chiến' : 'Castle Siege', exact: true }) });
    await expect(siegeCard).toContainText(vi ? '18:23 24 thg 11, 2026 (Sydney)' : '24 Nov 2026, 18:23 (Sydney)');
    await expect(siegeCard).toContainText(vi ? '19:02 24 thg 11, 2026 (Sydney)' : '24 Nov 2026, 19:02 (Sydney)');
    const invasionCard = guide.getByRole('article').filter({ has: page.getByRole('heading', { name: vi ? 'Xâm lăng thử nghiệm' : 'Test invasion', exact: true }) });
    await invasionCard.locator('summary').click();
    await expect(invasionCard).toContainText(vi ? 'Thẻ thử nghiệm' : 'Test token');
    const questCard = guide.locator('details').filter({ has: page.locator('summary', { hasText: vi ? 'Nhiệm vụ thử nghiệm' : 'Test quest' }) });
    await questCard.locator('summary').click();
    await expect(questCard).toContainText(vi ? '1.456 Zen' : '1,456 Zen');
    await expect(questCard).toContainText('123 Zen');
    await expect(questCard).toContainText(vi ? 'Tỷ lệ nhóm rơi theo cấu hình 15%' : 'Configured drop group chance 15%');
    await expect(questCard).toContainText('24–34');
    expect(await guide.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await mkdir('docs/screenshots/event-guide', { recursive: true });
    await guide.screenshot({ path: `docs/screenshots/event-guide/guide-${language}-${viewport.width}.png` });
    await guide.getByLabel(vi ? 'Máy chủ' : 'Server', { exact: true }).selectOption('7');
    await expect(guide).toContainText(vi ? 'Máy chủ này chưa công bố sự kiện theo lịch.' : 'This server reports no scheduled events.');
    await expect(event).toHaveCount(0);
  });
}

test('disabled and unavailable event guides keep the selection and rewards hidden', async ({ page }) => {
  await page.route('**/website/event-guide**', route => route.fulfill({ json: { enabled: false } }));
  await page.goto('/guide.html#event-guide');
  const guide = page.locator('#eventGuide');
  await expect(guide.getByRole('status')).toHaveText('The host has turned off the live event and quest guide.');
  await expect(guide.getByRole('combobox')).toHaveCount(0);
  await page.unroute('**/website/event-guide**');
  await page.route('**/website/event-guide**', route => route.fulfill({ status: 503 }));
  await page.reload();
  await expect(guide.getByRole('status')).toHaveText('The event and quest configuration is unavailable.');
  await expect(guide.getByRole('button', { name: 'Try again' })).toBeVisible();
});
