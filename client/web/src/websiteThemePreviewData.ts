/** Fictional API replies for the private design Site only. Never imported by production entry points. */
const reward = { zen: 1000, coins: 5, coinType: 'WCoinC', items: [] };
const missions = [
  { id: 'hunt', period: 'daily', kind: 'KillMonster', titleEn: 'Example: hunt monsters', titleVi: 'Ví dụ: săn quái', value: 8, target: 10, claimed: false, reward },
  { id: 'weekly', period: 'weekly', kind: 'KillMonster', titleEn: 'Example: weekly adventure', titleVi: 'Ví dụ: hành trình tuần', value: 30, target: 100, claimed: false, reward },
];
const board = {
  v: 1, now: Date.parse('2026-11-22T00:00:00Z'), day: '2026-11-22', week: '2026-11-16', nextDaily: Date.parse('2026-11-22T13:00:00Z'), nextWeekly: Date.parse('2026-11-22T13:00:00Z'), missions,
  streak: { days: 3, todayCounted: true, required: 3, bonusDay: 3, bonusClaimable: false, bonus: reward },
  calendar: { enabled: true, month: '2026-11', claimedDays: 3, claimedToday: true, daysInMonth: 30, coinType: 'WCoinC', rewards: Array.from({ length: 30 }, () => 5) },
  earned: { today: 25, cap: 300, owed: 0 }, online: { enabled: true, coinsToday: 25, cap: 300, minutesToday: 90, coinsPerHour: 5, coinType: 'WCoinC', autoPlayHalf: true },
  milestones: { enabled: true, coinType: 'WCoinC', levels: [{ level: 300, coins: 50 }], coinsPerReset: 5 },
  invite: { enabled: true, code: 'EXAMPLE123', rewarded: false, requiredLevel: 100, inviterCoins: 20, invitedCoins: 10, coinType: 'WCoinC', invitedCount: 4, qualifiedCount: 2 },
};
const characters = [
  { name: 'ExplorerKnight', className: 'Knight', classNameVi: 'Chiến binh', level: 300, resets: 12, map: 0, online: false },
  { name: 'StarlightMage', className: 'Mage', classNameVi: 'Pháp sư', level: 210, resets: 3, map: 2, online: false },
];
const offer = { levelId: 1, nameEn: 'Silver example', nameVi: 'Bạc mẫu', days: 7, price: 100, coinType: 'WCoinC', offerToken: 'a'.repeat(64), experiencePercent: 5, zenPercent: 5, dailyCoins: 10, dailyCoinType: 'GoblinPoints' };
const milestones = { enabled: true, totalVnd: 100000, nextThresholdVnd: 200000, steps: [{ id: 'sample', thresholdVnd: 100000, titleEn: 'Example milestone', titleVi: 'Mốc thưởng mẫu', coinType: 'WCoinC', coins: 20, zen: 0, items: [], reached: true, claimed: true, claimable: false }] };
const letter = { id: 'sample-letter', title: 'Quà mẫu / Example gift', sentAtUtc: '2026-11-21T09:00:00Z', expiresAtUtc: '2026-11-25T09:00:00Z', collected: false, expired: false, pieces: 0, coins: 20, zen: 1000, coinType: 'WCoinC' };

/** Unknown routes deliberately stay unavailable; the Site does not invent feature rules or forward credentials. */
export function websitePreviewReply(path: string): unknown | undefined {
  if (path === '/portal/status') return { ready: true };
  if (path === '/topup/info') return { enabled: false };
  if (path === '/portal/account/overview') return {
    loginName: 'Explorer', accountOnline: false, balances: { wCoinC: 2400, wCoinP: 120, goblinPoints: 650 }, vipLevel: 1, characters,
    history: [
      { id: 'sample-receipt', coinType: 'WCoinC', amount: -100, reason: null, vipPurchase: { nameEn: 'Silver example', nameVi: 'Bạc mẫu', days: 7 }, createdAt: '2026-11-21T09:00:00Z', pending: false },
      { id: 'sample-reward', coinType: 'WCoinC', amount: 50, reason: 'Example reward / Thưởng mẫu', createdAt: '2026-11-20T09:00:00Z', pending: false },
    ], page: 0, hasMore: false,
  };
  if (path === '/portal/account/vip/state') return { enabled: true, active: { ...offer, untilUtc: '2026-11-25T09:00:00Z' }, offers: [offer], pending: null, daily: { available: false, claimed: true } };
  if (path === '/portal/account/email/state') return { enabled: true, email: 'explorer@example.test' };
  if (path === '/portal/account/missions/state') return { status: 'ok', boardJson: JSON.stringify(board), characterOnline: false };
  if (path === '/portal/topup-milestones/account' || path === '/website/topup-milestones') return milestones;
  if (path === '/portal/gift-box/state') return { accountKey: 'EXPLORER', offset: 0, hasMore: false, letters: [letter], detail: { ...letter, message: 'Dữ liệu mẫu, không gửi hay nhận tài sản thật. / Sample data; no real assets are sent or collected.', collectedBy: null, items: [] }, characters: [{ id: 'sample-character', name: 'ExplorerKnight' }], status: null };
  if (path === '/portal/login-ticket') return { loginName: 'Explorer', ticket: 'sample-ticket-no-server', expiresInSeconds: 60 };
  if (path === '/website/data') return { updatedAt: '2026-11-22T00:00:00Z', servers: [0, 1].map(id => ({ id, description: id === 0 ? 'Atlas · Season 6.3' : 'Aurora · Season 9', players: id === 0 ? 24 : 12, capacity: 100, experienceRate: 5, masterExperienceRate: 2, maximumLevel: 400, maximumMasterLevel: 200, maximumCharactersPerAccount: 5, maximumPartySize: 5, classes: [{ id: 0, name: 'Wizard||vi=Pháp sư', requiredLevel: 0 }], maps: [{ id: 0, name: 'Lorencia', expMultiplier: 1 }], events: [] })) };
  if (path === '/website/details') return { updatedAt: '2026-11-22T00:00:00Z', characters: [{ rank: 1, name: 'ExplorerKnight', className: 'Knight', level: 300, masterLevel: 0, resets: 12 }], eventRankings: [], upcoming: [{ serverId: 0, events: [{ name: 'Blood Castle', nameVi: 'Huyết Lâu', startsAt: '2026-11-23T09:00:00Z' }] }], drops: [{ name: 'Example jewel||vi=Ngọc mẫu', chancePercent: 0.5, minimumMonsterLevel: 10, maximumMonsterLevel: null, monster: null, itemCount: 1 }] };
  if (path === '/website/missions') return { missions, nextDaily: board.nextDaily, earnedDailyCap: 300 };
  if (path === '/website/news') return { enabled: true, allowWebsite: true, posts: [{ id: 'sample-news', titleEn: 'Example continent news', titleVi: 'Tin lục địa mẫu', bodyEn: 'The original website sections and feature modules are retained. These records are fictional.', bodyVi: 'Giữ nguyên các mục và mô-đun tính năng website hiện có. Các bản ghi ở đây là dữ liệu mẫu.', publishAtUtc: Date.parse('2026-11-22T00:00:00Z'), publishSydney: '22/11/2026 11:00', pinned: true }] };
  if (path === '/website/event-alerts') return { off: true, categories: [], leadTimeMinutes: 5, vapidPublicKey: '' };
  if (['/website/gift-codes', '/website/resets', '/website/account-safety'].includes(path)) return { enabled: false };
  return undefined;
}
