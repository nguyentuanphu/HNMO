import { expect, test, type Page } from '@playwright/test';

const labels = {
  en: ['Overview', 'Coins', 'VIP', 'Rewards', 'Resets', 'Security', 'Community'],
  vi: ['Tổng quan', 'Coin', 'VIP', 'Phần thưởng', 'Reset', 'Bảo mật', 'Cộng đồng'],
};

async function mockAccount(page: Page, language: 'en' | 'vi'): Promise<void> {
  await page.addInitScript(lang => localStorage.setItem('hnmo.language', lang), language);
  await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'HNMO account fixture', links: { discord: 'https://discord.com/invite/hnmo', zalo: 'https://zalo.me/12345' } } }));
  await page.route('**/portal/status', route => route.fulfill({ json: { ready: true } }));
  await page.route('**/website/settings', route => route.fulfill({ json: {} }));
  await page.route('**/portal/account/missions/state', route => route.fulfill({ json: { status: 'off', boardJson: null, characterOnline: false } }));
  await page.route('**/portal/login-ticket', async route => {
    expect(route.request().postDataJSON()).toEqual({ loginName: 'ann', password: 'account-secret' });
    await route.fulfill({ json: { loginName: 'ann', ticket: 'account-ticket-fixture', expiresInSeconds: 60 } });
  });
  await page.route('**/website/event-alerts', route => route.fulfill({ json: { off: false, leadTimeMinutes: 5, categories: ['blood_castle'], vapidPublicKey: 'fixture-key' } }));
  const milestones = {
    enabled: true, totalVnd: 100_000, nextThresholdVnd: 200_000,
    steps: [{ id: 'first', thresholdVnd: 100_000, titleEn: 'First reward', titleVi: 'Quà đầu tiên', coinType: 'WCoinC', coins: 10, zen: 0, items: [], reached: true, claimed: false, claimable: true }],
  };
  await page.route('**/portal/topup-milestones/account', async route => {
    expect(route.request().postDataJSON()).toEqual({ loginName: 'ann', password: 'account-secret' });
    await route.fulfill({ json: milestones });
  });
  await page.route('**/portal/topup-milestones/claim', async route => {
    expect(route.request().postDataJSON()).toEqual({ loginName: 'ann', password: 'account-secret', stepId: 'first' });
    await route.fulfill({ json: { ...milestones, steps: [{ ...milestones.steps[0], claimed: true, claimable: false }] } });
  });
  for (const feature of ['gift-codes', 'resets', 'account-safety']) {
    await page.route(`**/website/${feature}`, route => route.fulfill({ json: { enabled: false } }));
  }
  await page.route('**/portal/account/overview', async route => {
    const body = route.request().postDataJSON() as { loginName: string; password: string; page: number };
    expect(body.loginName).toBe('ann'); expect(body.password).toBe('account-secret');
    await route.fulfill({ json: {
      loginName: 'ann', accountOnline: true, balances: { wCoinC: 150, wCoinP: 20, goblinPoints: 30 }, vipLevel: 0,
      characters: [
        { name: '<AnnKnight>', className: 'Knight', classNameVi: 'Chiến binh', level: 300, resets: 2, map: 0, online: true },
        { name: 'AnnMage', className: 'Mage', classNameVi: 'Pháp sư', level: 200, resets: 0, map: 0, online: false },
        { name: 'AnnArcher', className: 'Archer', classNameVi: 'Cung thủ', level: 100, resets: 0, map: 0, online: null },
      ],
      history: [
        { id: 'one', coinType: 'WCoinC', amount: body.page === 0 ? 50 : 25, reason: body.page === 0 ? 'Quest reward' : 'Earlier reward', createdAt: '2026-11-15T14:30:00Z', pending: false },
        { id: 'purchase', coinType: 'WCoinC', amount: -10, reason: 'structured receipt', purchase: { nameEn: 'Test package', nameVi: 'Gói thử nghiệm', giftRecipient: null }, createdAt: '2026-11-15T14:30:00Z', pending: false },
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
    expect(body).toEqual({ loginName: 'ann', password: 'account-secret', newPassword: 'replacement-123' });
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
      await expect(page.locator('#accountPlaySlot')).toHaveAttribute('href', '#play');
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
      await expect(page.locator('#account-coins')).toContainText('-10');
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
  await page.route('**/portal/account/overview', async route => {
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
  await page.locator('#accountToolbar [data-account-language="vi"]').click();
  await expect(page.locator('#accountNav').getByRole('button', { name: 'Quà bổ sung' })).toHaveAttribute('aria-current', 'true');
});

for (const language of ['en', 'vi'] as const) {
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
      expect(body.loginName).toBe('ann'); expect(body.password).toBe('account-secret');
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
