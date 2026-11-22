import { websiteAccountFixture } from './fixtures/websiteAccount.js';
import { expect, test, type Page } from '@playwright/test';

function makeState(language: 'en' | 'vi', status: string | null = null, collected = false) {
  const title = language === 'vi' ? 'Thưởng sự kiện' : 'Event Reward';
  const message = language === 'vi' ? 'Chúc bạn chơi game vui vẻ!' : 'Enjoy your reward!';
  const itemName = language === 'vi' ? 'Dao Găm' : 'Kris';
  const id = '11111111-1111-1111-1111-111111111111';
  const items = [{ group: 0, number: 0, name: itemName, level: 7, count: 1, durability: 100, skill: true, luck: true, optionLevel: 2, excellentOptions: 1 }];
  return {
    accountKey: 'ann',
    offset: 0,
    hasMore: false,
    letters: [
      {
        id,
        title,
        sentAtUtc: '2026-11-18T02:00:00Z',
        expiresAtUtc: '2026-11-25T02:00:00Z',
        collected,
        expired: false,
        pieces: 1,
        coins: 50,
        zen: 5000,
        coinType: 'WCoinC',
      },
    ],
    detail: {
      id,
      title,
      message,
      sentAtUtc: '2026-11-18T02:00:00Z',
      expiresAtUtc: '2026-11-25T02:00:00Z',
      collected,
      expired: false,
      collectedBy: collected ? 'AnnKnight' : null,
      coins: 50,
      zen: 5000,
      coinType: 'WCoinC',
      items,
    },
    characters: [{ id: '22222222-2222-2222-2222-222222222222', name: 'AnnKnight' }],
    status,
  };
}

async function mockAccountRewards(page: Page, language: 'en' | 'vi'): Promise<void> {
  let collected = false;
  await page.addInitScript(lang => localStorage.setItem('hnmo.language', lang), language);
  await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'HNMO', links: {} } }));
  await page.route('**/portal/status', route => route.fulfill({ json: { ready: true } }));
  await page.route('**/website/settings', route => route.fulfill({ json: {} }));
  for (const feature of ['gift-codes', 'resets', 'account-safety']) {
    await page.route(`**/website/${feature}`, route => route.fulfill({ json: { enabled: false } }));
  }
  await page.route('**/portal/account/missions/state', route => route.fulfill({ json: { status: 'off', boardJson: null, characterOnline: false } }));
  await page.route('**/portal/account/vip/state', route => route.fulfill({ json: { enabled: false, active: null, offers: [], pending: null, daily: { available: false, claimed: false } } }));
  await page.route('**/portal/account/email/state', route => route.fulfill({ json: { enabled: false, email: 'ann@example.test' } }));
  await page.route('**/portal/login-ticket', route => route.fulfill({ json: { loginName: 'ann', ticket: 't', expiresInSeconds: 60 } }));
  await page.route('**/portal/topup-milestones/account', route => route.fulfill({ json: { enabled: false, totalVnd: 0, nextThresholdVnd: null, steps: [] } }));
  await page.route('**/website/event-alerts', route => route.fulfill({ json: { off: true, leadTimeMinutes: 5, categories: [], vapidPublicKey: '' } }));
  await websiteAccountFixture(page, 'ann', 'secret');
  await page.route('**/portal/account/overview', route => route.fulfill({
    json: {
      loginName: 'ann',
      accountOnline: false,
      balances: { wCoinC: collected ? 150 : 100, wCoinP: 0, goblinPoints: 0 },
      vipLevel: 0,
      characters: [{ name: 'AnnKnight', className: 'Knight', classNameVi: 'Chiến binh', level: 200, resets: 1, map: 0, online: false }],
      history: [],
      page: 0,
      hasMore: false,
    },
  }));
  await page.route('**/portal/gift-box/state', route => route.fulfill({ json: makeState(language, null, collected) }));
  await page.route('**/portal/gift-box/collect', async route => {
    const body = route.request().postDataJSON() as { characterId: string; letterId: string };
    expect(body.characterId).toBe('22222222-2222-2222-2222-222222222222');
    expect(body.letterId).toBe('11111111-1111-1111-1111-111111111111');
    collected = true;
    await route.fulfill({ json: makeState(language, 'Applied', true) });
  });
}

for (const language of ['en', 'vi'] as const) {
  test(`Account rewards page mailbox collects localized gift (${language})`, async ({ page }) => {
    await mockAccountRewards(page, language);
    await page.goto('/account.html');
    await page.locator('#accountUser').fill('ann');
    await page.locator('#accountSecret').fill('secret');
    await page.locator('#accountSignInButton').click();
    await page.locator('#accountNav').getByRole('button', { name: language === 'vi' ? 'Phần thưởng' : 'Rewards', exact: true }).click();

    const box = page.locator('#accountGiftBox');
    await expect(box).toContainText(language === 'vi' ? 'Thưởng sự kiện' : 'Event Reward');
    await expect(box).toContainText(language === 'vi' ? 'Dao Găm' : 'Kris');
    await box.getByRole('button', { name: language === 'vi' ? 'Nhận quà' : 'Collect Reward', exact: true }).click();
    await expect(box).toContainText(language === 'vi' ? 'Đã nhận quà thành công vào tài khoản / hành trang.' : 'Reward collected and delivered cleanly.');
    await expect(box).toContainText('AnnKnight');
  });

  test(`In-game Gift Box window uses packets F5 27 and F5 28 (${language})`, async ({ page }) => {
    await page.setViewportSize({ width: language === 'vi' ? 375 : 1280, height: 820 });
    await page.route('**/giftbox-game-fixture.html', route => route.fulfill({
      contentType: 'text/html',
      body: `<!doctype html><html><meta charset="utf-8"><meta name="viewport" content="width=device-width"><body>
        <button id="open">Open</button><div id="sent" hidden></div>
        <script type="module">
          import { GiftBoxWindow } from '/js/ui/giftBoxWindow.js';
          import { setLanguage } from '/js/i18n.js';
          import { write, read } from '/js/net/packets.js';
          await setLanguage('${language}');
          const initial = ${JSON.stringify(makeState(language, null, false))};
          const fullReply = ${JSON.stringify(makeState(language, 'InventoryFull', false))};
          const win = new GiftBoxWindow({
            send: (name, fields) => {
              const bytes = write('ClientToServer', name, fields);
              const row = document.createElement('div'); row.textContent = JSON.stringify({ name, fields, bytes: [...bytes] }); document.getElementById('sent').append(row);
              if (name !== 'HnmoGiftBoxRequest') return;
              const req = JSON.parse(read('ClientToServer', name, bytes).Json);
              const reply = req.action === 'collect' ? fullReply : initial;
              queueMicrotask(() => win.handle(write('ServerToClient', 'HnmoGiftBoxState', { RequestId: fields.RequestId, Json: JSON.stringify(reply) })));
            },
            message: () => {},
          });
          document.getElementById('open').onclick = () => win.open();
          document.body.dataset.ready = 'true';
        </script></body></html>`,
    }));

    await page.goto('/giftbox-game-fixture.html');
    await expect(page.locator('body')).toHaveAttribute('data-ready', 'true');
    await page.locator('#open').click();

    const panel = page.locator('#hnmo-giftbox-panel');
    await expect(panel).toBeVisible();
    await expect(panel).toContainText(language === 'vi' ? 'Thưởng sự kiện' : 'Event Reward');
    await panel.getByRole('button', { name: language === 'vi' ? 'Nhận quà' : 'Collect Reward', exact: true }).click();
    await expect(panel).toContainText(language === 'vi' ? 'Hành trang không đủ ô trống.' : 'Inventory bag does not have enough free space.');

    const packets = await page.locator('#sent > div').allTextContents();
    const parsed = packets.map(p => JSON.parse(p) as { name: string; bytes: number[] }).filter(p => p.name === 'HnmoGiftBoxRequest');
    expect(parsed).toHaveLength(2);
    expect(parsed[0].bytes.slice(0, 4)).toEqual([0xc1, parsed[0].bytes.length, 0xf5, 0x27]);
    expect(parsed[1].bytes.slice(0, 4)).toEqual([0xc1, parsed[1].bytes.length, 0xf5, 0x27]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  });
}
