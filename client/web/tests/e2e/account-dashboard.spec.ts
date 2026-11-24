import { expect, test, type Page } from '@playwright/test';

const labels = {
  en: ['Overview', 'Coins', 'VIP', 'Rewards', 'Resets', 'Security', 'Community'],
  vi: ['Tổng quan', 'Coin', 'VIP', 'Phần thưởng', 'Reset', 'Bảo mật', 'Cộng đồng'],
};

async function mockAccount(page: Page, language: 'en' | 'vi'): Promise<void> {
  await page.route('**/portal/**', route => route.fulfill({ status: 503, json: { error: 'fixture-unavailable' } }));
  await page.addInitScript(lang => localStorage.setItem('hnmo.language', lang), language);
  await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'HNMO account fixture', links: { discord: 'https://discord.com/invite/hnmo', zalo: 'https://zalo.me/12345' } } }));
  await page.route('**/portal/session', async route => {
    if (route.request().method() === 'POST') {
      expect(route.request().postDataJSON()).toMatchObject({ loginName: 'ann', password: 'account-secret' });
      expect(route.request().headers()['x-hnmo-session']).toBe('1');
      return route.fulfill({ json: { authenticated: true } });
    }
    return route.fulfill({ json: { authenticated: false } });
  });
  await page.route('**/portal/status', route => route.fulfill({ json: { ready: true } }));
  await page.route('**/website/settings', route => route.fulfill({ json: { theme: process.env.HNMO_TEST_WEBSITE_THEME ?? 'classic' } }));
  await page.route('**/portal/account/missions/state', route => route.fulfill({ json: { status: 'off', boardJson: null, characterOnline: false } }));
  await page.route('**/portal/account/vip/state', route => route.fulfill({ json: { enabled: false, active: null, offers: [], pending: null, daily: { available: false, claimed: false } } }));
  await page.route('**/portal/account/email/state', route => route.fulfill({ json: { enabled: false, email: 'old@example.test' } }));
  await page.route('**/portal/login-ticket', async route => {
    expect(route.request().postDataJSON()).toEqual({}); expect(route.request().headers()['x-hnmo-session']).toBe('1');
    await route.fulfill({ json: { loginName: 'ann', ticket: 'account-ticket-fixture', expiresInSeconds: 60 } });
  });
  await page.route('**/website/event-alerts', route => route.fulfill({ json: { off: false, leadTimeMinutes: 5, categories: ['blood_castle'], vapidPublicKey: 'fixture-key' } }));
  const milestones = {
    enabled: true, totalVnd: 100_000, nextThresholdVnd: 200_000,
    steps: [{ id: 'first', thresholdVnd: 100_000, titleEn: 'First reward', titleVi: 'Quà đầu tiên', coinType: 'WCoinC', coins: 10, zen: 0, items: [], reached: true, claimed: false, claimable: true }],
  };
  await page.route('**/portal/topup-milestones/account', async route => {
    expect(route.request().postDataJSON()).toEqual({}); expect(route.request().headers()['x-hnmo-session']).toBe('1');
    await route.fulfill({ json: milestones });
  });
  await page.route('**/portal/topup-milestones/claim', async route => {
    expect(route.request().postDataJSON()).toEqual({ stepId: 'first' }); expect(route.request().headers()['x-hnmo-session']).toBe('1');
    await route.fulfill({ json: { ...milestones, steps: [{ ...milestones.steps[0], claimed: true, claimable: false }] } });
  });
  for (const feature of ['gift-codes', 'resets', 'account-safety']) {
    await page.route(`**/website/${feature}`, route => route.fulfill({ json: { enabled: false } }));
  }
  await page.route('**/portal/account/overview', async route => {
    const body = route.request().postDataJSON() as { loginName: string; password: string; page: number };
    expect(body).not.toHaveProperty('loginName'); expect(body).not.toHaveProperty('password'); expect(route.request().headers()['x-hnmo-session']).toBe('1');
    await route.fulfill({ json: {
      loginName: 'ann', accountOnline: true, balances: { wCoinC: 150, wCoinP: 20, goblinPoints: 30 }, vipLevel: 0,
      characters: [
        { name: '<AnnKnight>', className: 'Knight', classNameVi: 'Chiến binh', level: 300, resets: 2, map: 0, online: true },
        { name: 'AnnMage', className: 'Mage', classNameVi: 'Pháp sư', level: 200, resets: 0, map: 0, online: false },
        { name: 'AnnArcher', className: 'Archer', classNameVi: 'Cung thủ', level: 100, resets: 0, map: 0, online: null },
      ],
      storedItems: {
        inventory: { truncated: false, items: [{ character: 'AnnKnight', slot: 1, level: 1, name: 'Fixture sword', nameVi: 'Kiếm mẫu', serial: '0123456789abcdef0123456789abcdef' }] },
        vault: { truncated: false, items: [{ character: null, slot: 0, level: 0, name: 'Legacy fixture', nameVi: 'Vật phẩm cũ mẫu', serial: null }] },
      },
      history: [
        { id: 'one', coinType: 'WCoinC', amount: body.page === 0 ? 50 : 25, reason: body.page === 0 ? 'Quest reward' : 'Earlier reward', createdAt: '2026-11-15T14:30:00Z', pending: false },
        { id: 'purchase', coinType: 'WCoinC', amount: -10, reason: 'structured receipt', purchase: { nameEn: 'Test package', nameVi: 'Gói thử nghiệm', giftRecipient: null }, createdAt: '2026-11-15T14:30:00Z', pending: false },
        { id: 'vip', coinType: 'WCoinC', amount: -100, reason: null, vipPurchase: { nameEn: 'Bronze', nameVi: 'Đồng', days: 7 }, createdAt: '2026-11-15T14:30:00Z', pending: false },
        { id: 'vip-daily', coinType: 'GoblinPoints', amount: 10, reason: null, vipDaily: { nameEn: 'Bronze', nameVi: 'Đồng' }, createdAt: '2026-11-15T14:30:00Z', pending: true },
        { id: 'gift-box', coinType: 'WCoinC', amount: 10, reason: null, giftBox: { titleEn: '<Event gift>', titleVi: '<Quà sự kiện>' }, createdAt: '2026-11-15T14:30:00Z', pending: false },
        { id: 'vip-remove', coinType: 'WCoinC', amount: 0, reason: null, vipRemoved: true, createdAt: '2026-11-15T14:30:00Z', pending: false },
        { id: 'vault', coinType: 'WCoinC', amount: -5, reason: 'structured receipt', balanceChange: { action: 'ExtraVault', otherCharacter: null }, createdAt: '2026-11-15T14:30:00Z', pending: false },
        { id: 'store', coinType: 'WCoinP', amount: -3, reason: 'structured receipt', balanceChange: { action: 'PlayerStorePurchase', otherCharacter: '<OtherKnight>' }, createdAt: '2026-11-15T14:30:00Z', pending: false },
      ],
      page: body.page, hasMore: body.page === 0,
    } });
  });
}

async function signIn(page: Page): Promise<void> {
  await page.goto('/account.html');
  await page.locator('#accountUser').fill('ann');
  await page.locator('#accountSecret').fill('account-secret');
  await page.locator('#accountSignInButton').click();
  await expect(page.locator('#accountLoginPanel')).toBeHidden();
}

async function section(page: Page, width: number, index: number, language: 'en' | 'vi'): Promise<void> {
  if (width <= 600) await page.locator('#accountSectionSelect').selectOption({ label: labels[language][index] });
  else await page.locator('#accountNav').getByRole('button', { name: labels[language][index], exact: true }).click();
}


async function checkPasswordChange(page: Page, width: number, language: 'en' | 'vi'): Promise<void> {
  await signIn(page);
  await section(page, width, 5, language);
  const passwordForm = page.locator('#accountPasswordChange');
  await expect(passwordForm).toBeVisible();
  const savePassword = passwordForm.getByRole('button', { name: language === 'en' ? 'Change password' : 'Đổi mật khẩu', exact: true });
  await expect(savePassword).toHaveAttribute('title', /.+/);
  for (const input of await passwordForm.locator('input').all()) {
    await expect(input).toHaveAttribute('type', 'password');
    await expect(input).toHaveAttribute('title', /.+/);
  }
  let passwordRequests = 0;
  await page.route('**/portal/account/password', async route => {
    const body = route.request().postDataJSON() as { loginName: string; password: string; newPassword: string };
    expect(body).toEqual({ newPassword: 'replacement-123' });
    passwordRequests++;
    await route.fulfill(passwordRequests === 1 ? { status: 409, json: { error: 'online' } } : { json: { changed: true } });
  });
  await passwordForm.locator('#accountNewPassword').fill('mật-khẩu');
  await passwordForm.locator('#accountConfirmPassword').fill('mật-khẩu');
  await savePassword.click();
  await expect(page.locator('#accountMessage')).toHaveText(language === 'en'
    ? 'Use 3–20 letters, numbers, spaces or symbols from an English keyboard.'
    : 'Mật khẩu cần có 3–20 ký tự gồm chữ, số, dấu cách hoặc ký hiệu trên bàn phím tiếng Anh.');
  expect(passwordRequests).toBe(0);
  await passwordForm.locator('#accountNewPassword').fill('replacement-123');
  await passwordForm.locator('#accountConfirmPassword').fill('different');
  await savePassword.click();
  await expect(page.locator('#accountMessage')).toHaveText(language === 'en' ? 'The two new passwords do not match.' : 'Hai lần nhập mật khẩu mới không khớp.');
  expect(passwordRequests).toBe(0);
  await passwordForm.locator('#accountConfirmPassword').fill('replacement-123');
  await savePassword.click();
  await expect(page.locator('#accountMessage')).toHaveText(language === 'en'
    ? 'Log out of the game, then try changing your password again.'
    : 'Hãy thoát game rồi thử đổi mật khẩu lại.');
  await expect(page.locator('#accountLoginPanel')).toBeHidden();
  await savePassword.click();
  await expect(page.locator('#accountLoginPanel')).toBeVisible();
  await expect(page.locator('#accountMessage')).toHaveText(language === 'en'
    ? 'Your password was changed. Sign in with your new password.'
    : 'Đã đổi mật khẩu. Hãy đăng nhập bằng mật khẩu mới.');
  expect(passwordRequests).toBe(2);
  const storage = await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }));
  expect(storage).not.toContain('account-secret');
  expect(storage).not.toContain('replacement-123');
}

for (const width of [375, 820, 1280]) {
  for (const language of ['en', 'vi'] as const) {
    test(`account dashboard sections, history and logout (${language}, ${width}px)`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await mockAccount(page, language); await signIn(page);
      await expect(page.locator('#account-overview')).toContainText('<AnnKnight>');
      await expect(page.locator('#account-overview')).toContainText('300');
      await expect(page.locator('#account-overview')).toContainText(language === 'vi' ? 'Chiến binh' : 'Knight');
      for (const status of language === 'vi' ? ['Đang trong game', 'Đã thoát game', 'Chưa kiểm tra được'] : ['Online', 'Offline', 'Unavailable']) {
        await expect(page.locator('#account-overview table')).toContainText(status);
      }
      await expect(page.locator('#accountPlaySlot')).toHaveAttribute('href', '/account.html?play=1');
      await expect(page.locator('#playSignedIn')).toBeVisible();
      await expect(page.locator('#playSignedName')).toHaveText('ann');
      await expect(page.locator('#playPassword')).toHaveValue('');
      await expect(page.locator('#playNowButton')).toHaveAttribute('title', /.+/);
      await expect(page.locator('#account-overview a[href="/topup.html"]')).toBeVisible();
      await expect(page.locator('#account-overview annknight')).toHaveCount(0);
      await page.locator('#accountHelpButton').click();
      await expect(page.locator('#accountHelp')).toBeVisible();
      await expect(page.locator('#accountHelp')).toContainText(language === 'en' ? 'Sydney' : 'Sydney');
      for (let index = 0; index < labels[language].length; index++) {
        await section(page, width, index, language);
        const id = ['overview', 'coins', 'vip', 'rewards', 'resets', 'security', 'community'][index];
        await expect(page.locator(`#account-${id}`)).toBeVisible();
        await expect(page.locator('#accountPanels > [data-account-panel]:visible')).toHaveCount(1);
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
      }
      await expect(page.locator('#account-community a[href="https://discord.com/invite/hnmo"]')).toBeVisible();
      await expect(page.locator('#account-event-alerts button').first()).toHaveText(language === 'en' ? 'Enable Browser Alerts' : 'Bật Thông Báo Trình Duyệt');
      for (const control of await page.locator('#account-event-alerts button, #account-event-alerts input').all()) await expect(control).toHaveAttribute('title', /.+/);
      await section(page, width, 1, language);
      await expect(page.locator('#account-coins')).toContainText(language === 'en' ? 'Cash shop: Test package' : 'Mua ở cửa hàng: Gói thử nghiệm');
      await expect(page.locator('#account-coins')).toContainText(language === 'en' ? 'VIP removed by the host' : 'Chủ máy chủ đã thu hồi VIP');
      await expect(page.locator('#account-coins')).toContainText(language === 'en' ? 'Gift collected: <Event gift>' : 'Đã nhận quà: <Quà sự kiện>');
      await expect(page.locator('#account-coins event')).toHaveCount(0);
      await expect(page.locator('#account-coins')).toContainText('-10');
      await expect(page.locator('#account-coins')).toContainText(language === 'en' ? 'Extra vault purchase' : 'Mua thêm rương');
      await expect(page.locator('#account-coins')).toContainText(language === 'en' ? 'Player store purchase from <OtherKnight>' : 'Mua tại cửa hàng của <OtherKnight>');
      await expect(page.locator('#account-coins otherknight')).toHaveCount(0);
      const claim = page.locator('#topup-milestones-section button');
      await expect(claim).toHaveText(language === 'en' ? 'Claim' : 'Nhận thưởng');
      await expect(claim).toHaveAttribute('title', /.+/);
      await claim.click();
      await expect(claim).toBeDisabled();
      await expect(claim).toHaveText(language === 'en' ? 'Claimed' : 'Đã nhận');
      // 14:30 UTC is 01:30 on the following Sydney day in November.
      await expect(page.locator('#account-coins')).toContainText('01:30');
      await expect(page.locator('#account-coins')).toContainText('Quest reward');
      await page.locator('#account-coins').getByRole('button', { name: language === 'en' ? 'Next' : 'Trang sau', exact: true }).click();
      await expect(page.locator('#account-coins')).toContainText('Earlier reward');
      await expect(page.locator('#account-coins').getByRole('button', { name: language === 'en' ? 'Next' : 'Trang sau', exact: true })).toBeDisabled();
      expect(await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }))).not.toContain('account-secret');
      await expect(page.locator('#accountSecret')).toHaveValue('');
      for (const button of await page.locator('#accountToolbar button:visible, #account-coins button:visible').all()) await expect(button).toHaveAttribute('title', /.+/);
      await page.screenshot({ path: `test-results/account-${language}-${width}.png`, fullPage: true });
      await page.locator('#accountLogout').click();
      await expect(page.locator('#accountLoginPanel')).toBeVisible();
      await expect(page.locator('#accountSecret')).toHaveValue('');
      await expect(page.locator('#accountPanels')).not.toContainText('<AnnKnight>');

      await checkPasswordChange(page, width, language);
    });
  }
}

test('a rejected login exposes no account data', async ({ page }) => {
  await mockAccount(page, 'en');
  let release: () => void = () => { throw new Error('No pending account request.'); };
  let arrived: () => void = () => {};
  const pending = new Promise<void>(resolve => { release = resolve; });
  const received = new Promise<void>(resolve => { arrived = resolve; });
  await page.route('**/portal/session', async route => {
    if (route.request().method() !== 'POST') return route.fulfill({ json: { authenticated: false } });
    arrived(); await pending;
    await route.fulfill({ status: 401, json: { error: 'login' } });
  });
  await page.goto('/account.html');
  await page.locator('#accountUser').fill('ann'); await page.locator('#accountSecret').fill('wrong');
  await page.locator('#accountSignInButton').click();
  await received;
  await expect(page.locator('#accountNav')).toBeHidden();
  await expect(page.locator('#accountToolbar')).toBeHidden();
  release();
  await expect(page.locator('#accountMessage')).toContainText('incorrect');
  await expect(page.locator('#accountLoginPanel')).toBeVisible();
  await expect(page.locator('#accountToolbar')).toBeHidden();
  await expect(page.locator('#accountPanels')).not.toContainText('<AnnKnight>');
});

test('another feature can register a bilingual account section without editing the page', async ({ page }) => {
  await mockAccount(page, 'en'); await signIn(page);
  await page.evaluate(async path => {
    const registry = await import(path) as { registerAccountSection(section: { id: string; label: { en: string; vi: string }; element: HTMLElement }): () => void };
    const element = document.createElement('section'); element.id = 'extension-fixture'; element.textContent = 'Extension fixture content';
    registry.registerAccountSection({ id: 'extension-fixture', label: { en: 'Extra rewards', vi: 'Quà bổ sung' }, element });
  }, '/js/accountSections.js');
  await page.locator('#accountNav').getByRole('button', { name: 'Extra rewards' }).click();
  await expect(page.locator('#extension-fixture')).toBeVisible();
  await page.locator('header [data-account-language="vi"]').click();
  await expect(page.locator('#accountNav').getByRole('button', { name: 'Quà bổ sung' })).toHaveAttribute('aria-current', 'true');
});

for (const language of ['en', 'vi'] as const) {
  test(`email confirmation uses the new mailbox and keeps secrets out of storage (${language})`, async ({ page }) => {
    const width = language === 'en' ? 1280 : 375;
    await page.setViewportSize({ width, height: 900 });
    await mockAccount(page, language);
    let currentEmail = 'old@example.test';
    await page.route('**/portal/account/email/state', async route => {
      expect(route.request().postDataJSON()).toEqual({}); expect(route.request().headers()['x-hnmo-session']).toBe('1');
      await route.fulfill({ json: { enabled: true, email: currentEmail } });
    });
    let requests = 0;
    await page.route('**/portal/account/email/request', async route => {
      expect(route.request().postDataJSON()).toEqual({ newEmail: 'new@example.test', language });
      requests++;
      await route.fulfill(requests === 1 ? { status: 409, json: { error: 'online' } } : { json: { sent: true, expiresAt: '2026-11-17T00:15:00Z' } });
    });
    let confirmations = 0;
    const mailedCode = 'ABCDEF0123456789ABCDEF01';
    await page.route('**/portal/account/email/confirm', async route => {
      const body = route.request().postDataJSON() as { loginName: string; password: string; code: string };
      expect(body).toEqual({ code: confirmations === 0 ? '0'.repeat(24) : mailedCode });
      confirmations++;
      if (confirmations === 1) { await route.fulfill({ status: 400, json: { error: 'code' } }); return; }
      currentEmail = 'new@example.test';
      await route.fulfill({ json: { changed: true } });
    });
    await signIn(page); await section(page, width, 5, language);
    const email = page.locator('#accountEmailChange');
    await expect(email).toBeVisible();
    await expect(email).toContainText('old@example.test');
    await email.locator('[data-email-action="request"] input').fill('new@example.test');
    await email.locator('[data-email-action="request"] button').click();
    await expect(page.locator('#accountMessage')).toContainText(language === 'en' ? 'Leave the game' : 'Hãy thoát game');
    await email.locator('[data-email-action="request"] button').click();
    await expect(page.locator('#accountMessage')).toContainText(language === 'en' ? 'Code sent' : 'Đã gửi mã');
    await expect(email).toContainText('old@example.test');
    await email.locator('[data-email-action="confirm"] input').fill('0'.repeat(24));
    await email.locator('[data-email-action="confirm"] button').click();
    await expect(page.locator('#accountMessage')).toContainText(language === 'en' ? 'incorrect, expired or already used' : 'không đúng, đã hết hạn hoặc đã dùng');
    await email.locator('[data-email-action="confirm"] input').fill(mailedCode);
    await email.locator('[data-email-action="confirm"] button').click();
    await expect(email).toContainText('new@example.test');
    await expect(page.locator('#accountMessage')).toContainText(language === 'en' ? 'confirmed and saved' : 'Đã xác nhận và lưu');
    await expect(email.locator('[data-email-action="confirm"] input')).toHaveValue('');
    for (const control of await email.locator('input, button').all()) await expect(control).toHaveAttribute('title', /.+/);
    const storage = await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }));
    expect(storage).not.toContain('account-secret'); expect(storage).not.toContain(mailedCode);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(requests).toBe(2); expect(confirmations).toBe(2);
  });

  test(`mission claims, calendar, referrals and offline protection (${language})`, async ({ page }) => {
    const width = language === 'en' ? 1280 : 375;
    await page.setViewportSize({ width, height: 900 });
    await mockAccount(page, language);
    const reward = { zen: 1000, coins: 5, coinType: 'WCoinC', items: [{ group: 14, number: 13, level: 0, count: 1, nameEn: 'Test jewel', nameVi: 'Ngọc thử nghiệm' }] };
    const board = {
      v: 1, now: Date.parse('2026-11-15T14:30:00Z'), day: '2026-11-16', week: '2026-11-16',
      nextDaily: Date.parse('2026-11-16T13:00:00Z'), nextWeekly: Date.parse('2026-11-22T13:00:00Z'),
      missions: [
        { id: 'hunt', period: 'daily', kind: 'KillMonster', titleEn: '<Hunt monsters>', titleVi: '<Săn quái>', value: 10, target: 10, claimed: false, reward },
        { id: 'weekly', period: 'weekly', kind: 'KillMonster', titleEn: 'Weekly hunt', titleVi: 'Săn quái tuần', value: 1, target: 20, claimed: false, reward },
      ],
      streak: { days: 3, todayCounted: true, required: 3, bonusDay: 3, bonusClaimable: true, bonus: reward },
      calendar: { enabled: true, month: '2026-11', claimedDays: 2, claimedToday: false, daysInMonth: 30, coinType: 'WCoinC', rewards: Array.from({ length: 30 }, () => 5) },
      earned: { today: 10, cap: 300, owed: 2 },
      invite: { enabled: true, code: 'INVITE123', invitedBy: null as string | null, rewarded: false, requiredLevel: 100, inviterCoins: 20, invitedCoins: 10, coinType: 'WCoinC', invitedCount: 4, qualifiedCount: 2 },
      event: undefined as { type: string; result: string } | undefined,
    };
    const claims: string[] = [];
    await page.route('**/portal/account/missions/*', async route => {
      const body = route.request().postDataJSON() as { loginName: string; password: string; characterName: string; action?: string; argument?: string };
      expect(body).not.toHaveProperty('loginName'); expect(body).not.toHaveProperty('password'); expect(route.request().headers()['x-hnmo-session']).toBe('1');
      expect(['<AnnKnight>', 'AnnMage']).toContain(body.characterName);
      const online = body.characterName === '<AnnKnight>';
      if (route.request().url().endsWith('/claim')) {
        expect(online).toBe(true); claims.push(body.action!);
        if (body.action === 'mission') {
          expect(body.argument).toBe('hunt'); board.missions[0].claimed = true;
        } else if (body.action === 'calendar') {
          expect(body.argument).toBe(''); board.calendar.claimedToday = true; board.calendar.claimedDays++;
        } else if (body.action === 'streak') {
          expect(body.argument).toBe(''); board.streak.bonusClaimable = false;
        } else {
          expect(body.action).toBe('invite'); expect(body.argument).toBe('FRIEND'); board.invite.invitedBy = 'friend';
        }
        board.event = { type: body.action!, result: 'Ok' };
      }
      await route.fulfill({ json: { status: 'ok', boardJson: JSON.stringify(board), characterOnline: online } });
    });
    await signIn(page); await section(page, width, 3, language);
    const missions = page.locator('#account-missions');
    await expect(missions).toContainText(language === 'en' ? '<Hunt monsters>' : '<Săn quái>');
    await expect(missions.locator('hunt')).toHaveCount(0);
    await expect(missions).toContainText(language === 'en' ? 'Test jewel' : 'Ngọc thử nghiệm');
    await expect(missions).toContainText('00:00'); // Sydney midnight, not the UTC reset hour.
    const claim = language === 'en' ? 'Collect' : 'Nhận thưởng';
    await missions.locator('[data-mission-id="hunt"]').getByRole('button', { name: claim, exact: true }).click();
    await expect(missions.locator('[data-mission-id="hunt"] button')).toBeDisabled();
    await missions.locator('[data-reward="streak"] button').click();
    await expect(missions.locator('[data-reward="streak"] button')).toBeDisabled();
    await missions.locator('[data-reward="calendar"] button').click();
    await expect(missions.locator('[data-reward="calendar"] button')).toBeDisabled();
    await expect(missions.locator('.account-calendar li')).toHaveCount(30);
    await expect(missions.locator('.account-calendar [data-claimed]')).toHaveCount(3);
    await missions.locator('select').selectOption('AnnMage');
    await expect(missions).toContainText(language === 'en' ? 'keep this character connected' : 'hãy cho nhân vật này vào game');
    for (const control of await missions.locator('article button').all()) await expect(control).toBeDisabled();
    await section(page, width, 6, language);
    const referral = page.locator('#account-referrals');
    await expect(referral.locator('a')).toHaveAttribute('href', /account\.html\?ref=INVITE123#community$/);
    await expect(referral).toContainText(language === 'en' ? 'Invited: 4. Qualified for rewards: 2.' : 'Đã mời: 4. Đủ điều kiện nhận thưởng: 2.');
    await expect(referral.locator('form button')).toBeDisabled();
    await referral.locator('select').selectOption('<AnnKnight>');
    await expect(referral.locator('form button')).toBeEnabled();
    await referral.locator('form input').fill('FRIEND'); await referral.locator('form button').click();
    await expect(referral).toContainText(language === 'en' ? 'Your inviter: friend' : 'Người đã mời bạn: friend');
    expect(claims).toEqual(['mission', 'streak', 'calendar', 'invite']);
    for (const control of await referral.locator('input, select, button, a').all()) await expect(control).toHaveAttribute('title', /.+/);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }))).not.toContain('account-secret');
  });
}

for (const language of ['en', 'vi'] as const) {
  test(`the authenticated account VIP section buys a reviewed offer (${language})`, async ({ page }) => {
    const width = language === 'vi' ? 375 : 1280;
    await page.setViewportSize({ width, height: 900 });
    await mockAccount(page, language);
    let bought = false;
    const offer = { levelId: 1, nameEn: 'Bronze', nameVi: 'Đồng', days: 7, price: 100, coinType: 'WCoinC', offerToken: 'a'.repeat(64), experiencePercent: 5, zenPercent: 5, dailyCoins: 10, dailyCoinType: 'GoblinPoints' };
    const state = () => ({ enabled: true, active: bought ? { ...offer, untilUtc: '2026-11-17T14:30:00Z' } : null, offers: [offer], pending: null, daily: { available: bought, claimed: false } });
    await page.route('**/portal/account/overview', async route => {
      expect(route.request().postDataJSON()).not.toHaveProperty('password'); expect(route.request().headers()['x-hnmo-session']).toBe('1');
      await route.fulfill({ json: {
        loginName: 'ann', accountOnline: false, balances: { wCoinC: bought ? 50 : 150, wCoinP: 20, goblinPoints: 30 }, vipLevel: bought ? 1 : 0,
        characters: [], history: bought ? [{ id: 'vip', coinType: 'WCoinC', amount: -100, reason: null, vipPurchase: { nameEn: 'Bronze', nameVi: 'Đồng', days: 7 }, createdAt: '2026-11-15T14:30:00Z', pending: false }] : [], page: 0, hasMore: false,
      } });
    });
    await page.route('**/portal/account/vip/state', async route => {
      expect(route.request().postDataJSON()).not.toHaveProperty('password'); expect(route.request().headers()['x-hnmo-session']).toBe('1');
      await route.fulfill({ json: state() });
    });
    await page.route('**/portal/account/vip/purchase', async route => {
      const body = route.request().postDataJSON() as { loginName: string; password: string; operationId: string; levelId: number; days: number; offerToken: string };
      expect(body).toMatchObject({ levelId: 1, days: 7, offerToken: offer.offerToken });
      expect(body.operationId).toMatch(/^[a-f0-9-]{36}$/i);
      expect(bought).toBe(false); bought = true;
      await route.fulfill({ json: { ...state(), status: 'Applied' } });
    });
    await signIn(page); await section(page, width, 2, language);
    const vip = page.locator('#accountVip');
    await expect(vip).toBeVisible();
    await vip.getByRole('button', { name: language === 'vi' ? 'Xem lại giao dịch' : 'Review purchase', exact: true }).click();
    expect(bought).toBe(false);
    await vip.getByRole('button', { name: language === 'vi' ? 'Xác nhận mua' : 'Confirm purchase', exact: true }).click();
    await expect(vip).toContainText(language === 'vi' ? 'Giao dịch VIP đã được ghi nhận' : 'Your VIP purchase is recorded');
    await expect(vip).toContainText('18/11/2026');
    await expect(vip).toContainText('01:30');
    await section(page, width, 1, language);
    await expect(page.locator('#account-coins')).toContainText(language === 'vi' ? 'VIP Đồng: 7 ngày' : 'VIP Bronze: 7 days');
    const storage = await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }));
    expect(storage).not.toContain('account-secret');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  });
}

for (const [language, width] of [['en', 1440], ['vi', 390]] as const) {
  test(`private saved storage supports search and links only existing serials (${language}, ${width}px)`, async ({ page }) => {
    const isVi = language === 'vi';
    const sectionName = isVi ? 'Vật phẩm và kho' : 'Items and vault';
    const swordName = isVi ? 'Kiếm mẫu +1' : 'Fixture sword +1';
    const legacyName = isVi ? 'Vật phẩm cũ mẫu' : 'Legacy fixture';
    await page.setViewportSize({ width, height: 900 });
    await mockAccount(page, language);
    await signIn(page);
    const storage = page.locator('#account-items');
    await expect(storage).toBeHidden();
    if (width === 390) await page.locator('#accountSectionSelect').selectOption({ label: sectionName });
    else await page.locator('#accountNav').getByRole('button', { name: sectionName, exact: true }).click();
    const sword = storage.getByRole('link', { name: swordName });
    await expect(sword).toHaveAttribute('href', '/tracker.html?serial=0123456789abcdef0123456789abcdef');
    await expect(storage.getByText(legacyName)).toBeVisible();
    await expect(storage.getByRole('link', { name: legacyName })).toHaveCount(0);
    await page.screenshot({ path: `docs/screenshots/item-history/account-items-${language}-${width}.png`, fullPage: true });

    const detailsLabel = isVi ? 'Chi tiết vật phẩm' : 'Item details';
    const openDetails = storage.getByRole('button', { name: `${detailsLabel} · ${swordName}` });
    await openDetails.click();
    const details = page.getByRole('dialog', { name: swordName });
    await expect(details.getByText('0123456789abcdef0123456789abcdef')).toBeVisible();
    await expect(details.getByText('AnnKnight')).toBeVisible();
    expect(await details.evaluate(element => {
      const probe = document.createElement('div');
      probe.style.backgroundColor = 'var(--panel)';
      element.append(probe);
      const panelColor = getComputedStyle(probe).backgroundColor;
      probe.remove();
      return getComputedStyle(element).backgroundColor === panelColor;
    })).toBe(true);
    await expect(details.getByRole('link', { name: isVi ? 'Lịch sử vật phẩm' : 'Item history' }))
      .toHaveAttribute('href', '/tracker.html?serial=0123456789abcdef0123456789abcdef');
    await page.screenshot({ path: `docs/screenshots/item-history/account-item-details-${language}-${width}.png` });
    await page.keyboard.press('Escape');
    await expect(details).toBeHidden();
    await expect(openDetails).toBeFocused();

    const search = storage.getByRole('searchbox', { name: isVi
      ? 'Tìm vật phẩm đã lưu theo tên, nhân vật hoặc số sê-ri'
      : 'Find saved items by name, character or serial' });
    await search.fill('ANNKNIGHT');
    await expect(sword).toBeVisible();
    await expect(storage.getByText(legacyName)).toHaveCount(0);
    await search.fill('0123456789abcdef0123456789abcdef');
    await expect(sword).toBeVisible();
    await search.fill(legacyName);
    await expect(sword).toHaveCount(0);
    await expect(storage.getByText(legacyName)).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);

    await storage.getByRole('button', { name: `${detailsLabel} · ${legacyName}` }).click();
    const legacyDetails = page.getByRole('dialog', { name: legacyName });
    await expect(legacyDetails.getByRole('link')).toHaveCount(0);
    await page.evaluate(() => window.dispatchEvent(new Event('hnmo-session-expired')));
    await expect(legacyDetails).toHaveCount(0);
    await expect(storage).toBeHidden();
    await expect(storage.getByText(legacyName)).toHaveCount(0);
  });
}
