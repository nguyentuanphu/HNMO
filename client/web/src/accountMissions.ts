import type { AccountSectionContext } from './accountSections.js';
import type { MissionBoard } from './ui/missionsWindow.js';

const texts = {
  title: ['Personal missions and daily rewards', 'Nhiệm vụ cá nhân và quà hằng ngày'],
  referral: ['Invite friends', 'Mời bạn bè'],
  character: ['Character', 'Nhân vật'],
  characterHint: ['Choose your character whose progress and rewards you want to view.', 'Chọn nhân vật để xem tiến trình và nhận thưởng.'],
  refresh: ['Refresh progress', 'Làm mới tiến trình'],
  refreshHint: ['Read the selected character’s current mission board.', 'Đọc tiến trình nhiệm vụ hiện tại của nhân vật đã chọn.'],
  loading: ['Loading…', 'Đang tải…'],
  unavailable: ['Mission data is unavailable. Try refreshing later.', 'Chưa đọc được dữ liệu nhiệm vụ. Vui lòng làm mới sau.'],
  off: ['The host has not enabled missions on this server.', 'Chủ máy chủ chưa bật nhiệm vụ trên máy chủ này.'],
  empty: ['Create a character in the game first.', 'Hãy tạo nhân vật trong game trước.'],
  offline: ['You can view saved progress here. To collect a reward or enter an invite code, keep this character connected in the game, then refresh.', 'Bạn có thể xem tiến trình đã lưu tại đây. Để nhận thưởng hoặc nhập mã mời, hãy cho nhân vật này vào game rồi làm mới.'],
  online: ['This character is in the game. Claims use the same rules as the game’s Missions window.', 'Nhân vật này đang trong game. Nhận thưởng tuân theo cùng quy tắc với cửa sổ Nhiệm vụ trong game.'],
  daily: ['Daily missions', 'Nhiệm vụ ngày'],
  weekly: ['Weekly missions', 'Nhiệm vụ tuần'],
  next: ['Next reset (Sydney): {0}', 'Làm mới tiếp theo (Sydney): {0}'],
  noMissions: ['No missions in this period.', 'Chưa có nhiệm vụ trong kỳ này.'],
  claim: ['Collect', 'Nhận thưởng'],
  claimHint: ['Collect this completed reward for the selected character.', 'Nhận phần thưởng đã hoàn thành cho nhân vật đã chọn.'],
  claimed: ['Collected', 'Đã nhận'],
  progress: ['Progress: {0} / {1}', 'Tiến trình: {0} / {1}'],
  streak: ['Daily streak', 'Chuỗi ngày hoàn thành'],
  streakProgress: ['{0} days in a row. Complete {1} daily missions a day; a bonus is available every {2} days.', 'Chuỗi {0} ngày. Hoàn thành {1} nhiệm vụ ngày mỗi ngày; nhận thưởng chuỗi sau mỗi {2} ngày.'],
  calendar: ['Login calendar', 'Lịch điểm danh'],
  calendarProgress: ['Month {0}: {1} days collected', 'Tháng {0}: đã nhận {1} ngày'],
  day: ['Day {0}', 'Ngày {0}'],
  earned: ['Earned today: {0}. Daily cap: {1}. Waiting for a later day: {2}.', 'Đã kiếm hôm nay: {0}. Giới hạn mỗi ngày: {1}. Đang chờ nhận vào ngày sau: {2}.'],
  referralOff: ['Invitations are not enabled on this server.', 'Máy chủ này chưa bật tính năng mời bạn.'],
  code: ['Your invite code', 'Mã mời của bạn'],
  noCode: ['Enter the game once to create your invite code, then refresh this page.', 'Vào game một lần để tạo mã mời của bạn rồi làm mới trang này.'],
  shareHint: ['Share this link with a friend; it contains your invite code, never your password.', 'Gửi liên kết này cho bạn bè; liên kết chứa mã mời, không chứa mật khẩu.'],
  referred: ['Invited: {0}. Qualified for rewards: {1}.', 'Đã mời: {0}. Đủ điều kiện nhận thưởng: {1}.'],
  referralReward: ['When a friend reaches level {0}: you receive {1} {2}; your friend receives {3} {2}. Server eligibility rules and the daily coin cap apply.', 'Khi người được mời đạt cấp {0}: bạn nhận {1} {2}; người được mời nhận {3} {2}. Áp dụng điều kiện của máy chủ và giới hạn coin mỗi ngày.'],
  invitedBy: ['Your inviter: {0}', 'Người đã mời bạn: {0}'],
  enter: ['Enter an invite code', 'Nhập mã mời'],
  enterHint: ['Enter your friend’s invite code or character name before reaching the qualifying level.', 'Nhập mã mời hoặc tên nhân vật của bạn bè trước khi đạt cấp đủ điều kiện.'],
  submit: ['Save inviter', 'Lưu người mời'],
  submitHint: ['Bind this account to its inviter; this can only be done once.', 'Gắn tài khoản với người mời; mỗi tài khoản chỉ thực hiện được một lần.'],
  Ok: ['Done. Your progress has been refreshed.', 'Đã thực hiện. Tiến trình đã được làm mới.'],
  Unknown: ['That mission is no longer available.', 'Nhiệm vụ này hiện không còn khả dụng.'],
  NotFinished: ['Complete the requirements before collecting.', 'Hãy hoàn thành điều kiện trước khi nhận thưởng.'],
  AlreadyClaimed: ['You have already collected this reward.', 'Bạn đã nhận phần thưởng này rồi.'],
  BagFull: ['Make room in your bag, then collect again. The reward stays available.', 'Hãy dọn chỗ trong túi rồi nhận lại. Phần thưởng vẫn được giữ.'],
  ZenFull: ['Your zen is full. Spend or deposit some, then collect again.', 'Zen đã đầy. Hãy dùng hoặc gửi bớt rồi nhận lại.'],
  Disabled: ['The host has turned this reward off.', 'Chủ máy chủ đã tắt phần thưởng này.'],
  NotFound: ['No inviter was found for that code or character name.', 'Không tìm thấy người mời theo mã hoặc tên nhân vật này.'],
  Self: ['You cannot invite your own account.', 'Bạn không thể tự mời tài khoản của mình.'],
  AlreadyInvited: ['This account already has an inviter.', 'Tài khoản này đã có người mời.'],
  SameAddress: ['Accounts on the same network cannot refer each other.', 'Các tài khoản cùng mạng không thể giới thiệu lẫn nhau.'],
  TooLate: ['This account has passed the level limit for entering an invite code.', 'Tài khoản đã vượt cấp cho phép nhập mã mời.'],
} as const;
type Text = keyof typeof texts;
type Reward = MissionBoard['streak']['bonus'];
type Board = Omit<MissionBoard, 'invite'> & {
  invite: MissionBoard['invite'] & { invitedCount?: number; qualifiedCount?: number };
};
interface Reply { status: string; boardJson: string | null; characterOnline: boolean }
interface Options {
  language(): 'en' | 'vi';
  request: AccountSectionContext['request'];
  error(error: unknown): void;
}

/** The website uses the existing authoritative mission rules, never a separate reward balance. */
export class AccountMissions {
  readonly rewards = document.createElement('section');
  readonly referrals = document.createElement('section');
  private names: readonly string[] = [];
  private selected = '';
  private reply: Reply | null = null;
  private board: Board | null = null;
  private busy = false;
  private message: Text | null = null;
  private sequence = 0;

  constructor(private readonly options: Options) {
    this.rewards.id = 'account-missions';
    this.referrals.id = 'account-referrals';
  }

  updateCharacters(names: readonly string[]): void {
    this.names = names;
    if (!names.includes(this.selected)) { this.selected = names[0] ?? ''; this.reply = null; this.board = null; }
    this.render();
  }

  async open(section: string): Promise<void> {
    if (section === 'rewards' || section === 'community') await this.load();
  }

  private t(key: Text, ...values: (string | number)[]): string {
    let text: string = texts[key][this.options.language() === 'vi' ? 1 : 0];
    values.forEach((value, index) => { text = text.replaceAll(`{${index}}`, String(value)); });
    return text;
  }

  private node<K extends keyof HTMLElementTagNameMap>(tag: K, text = ''): HTMLElementTagNameMap[K] {
    const node = document.createElement(tag); node.textContent = text; return node;
  }

  private button(key: Text, hint: Text, action: () => void, disabled = false): HTMLButtonElement {
    const button = this.node('button', this.t(key)); button.type = 'button'; button.title = this.t(hint);
    button.disabled = this.busy || disabled; button.addEventListener('click', action); return button;
  }

  private async load(action?: string, argument = ''): Promise<void> {
    if (!this.selected || this.busy) return;
    const sequence = ++this.sequence;
    this.busy = true; this.message = null; this.render();
    try {
      const reply = await this.options.request<Reply>(`/portal/account/missions/${action ? 'claim' : 'state'}`, {
        characterName: this.selected, ...(action ? { action, argument } : {}),
      });
      if (sequence !== this.sequence) return;
      this.reply = reply;
      const candidate = reply.boardJson ? JSON.parse(reply.boardJson) as Board : null;
      if (candidate && (candidate.v !== 1 || !Array.isArray(candidate.missions) || !candidate.calendar || !candidate.invite || !candidate.streak)) throw new Error('Invalid mission board.');
      this.board = candidate;
      if (action && candidate?.event?.result && candidate.event.result in texts) this.message = candidate.event.result as Text;
    } catch (error) {
      this.reply = null; this.board = null; this.options.error(error);
    } finally { if (sequence === this.sequence) { this.busy = false; this.render(); } }
  }

  private render(): void {
    for (const [root, title] of [[this.rewards, 'title'], [this.referrals, 'referral']] as const) {
      root.replaceChildren(this.node('h3', this.t(title)));
      if (!this.names.length) { root.append(this.node('p', this.t('empty'))); continue; }
      const label = this.node('label', this.t('character')); const select = this.node('select');
      select.title = this.t('characterHint'); select.disabled = this.busy;
      for (const name of this.names) { const option = this.node('option', name); option.value = name; select.append(option); }
      select.value = this.selected;
      select.addEventListener('change', () => { this.selected = select.value; void this.load(); });
      label.append(select); root.append(label, this.button('refresh', 'refreshHint', () => { void this.load(); }));
      const status = this.node('p'); status.setAttribute('role', 'status');
      status.textContent = this.busy ? this.t('loading')
        : this.reply?.status === 'off' ? this.t('off') : this.board ? this.t(this.reply?.characterOnline ? 'online' : 'offline') : this.t('unavailable');
      root.append(status);
      if (this.message && !this.busy) root.append(this.node('p', this.t(this.message)));
    }
    if (!this.board) return;
    this.renderRewards(this.board); this.renderReferrals(this.board);
  }

  private renderRewards(board: Board): void {
    const claimable = this.reply?.characterOnline === true;
    for (const period of ['daily', 'weekly'] as const) {
      this.rewards.append(this.node('h4', this.t(period)), this.node('p', this.t('next', this.date(period === 'daily' ? board.nextDaily : board.nextWeekly))));
      const missions = board.missions.filter(m => m.period === period);
      if (!missions.length) this.rewards.append(this.node('p', this.t('noMissions')));
      for (const mission of missions) {
        const row = this.node('article'); row.className = 'account-mission'; row.dataset.missionId = mission.id;
        row.append(this.node('strong', this.options.language() === 'vi' ? mission.titleVi : mission.titleEn),
          this.node('p', this.t('progress', mission.value, mission.target)), this.node('p', this.reward(mission.reward)),
          this.button(mission.claimed ? 'claimed' : 'claim', 'claimHint', () => { void this.load('mission', mission.id); }, !claimable || mission.claimed || mission.value < mission.target));
        this.rewards.append(row);
      }
    }
    const streak = this.node('article'); streak.dataset.reward = 'streak';
    streak.append(this.node('h4', this.t('streak')), this.node('p', this.t('streakProgress', board.streak.days, board.streak.required, board.streak.bonusDay)),
      this.node('p', this.reward(board.streak.bonus)), this.button('claim', 'claimHint', () => { void this.load('streak'); }, !claimable || !board.streak.bonusClaimable));
    this.rewards.append(streak);
    if (board.calendar.enabled) {
      const calendar = this.node('article'); calendar.dataset.reward = 'calendar';
      calendar.append(this.node('h4', this.t('calendar')), this.node('p', this.t('calendarProgress', board.calendar.month, board.calendar.claimedDays)));
      const days = this.node('ol'); days.className = 'account-calendar';
      for (let day = 1; day <= board.calendar.daysInMonth; day++) {
        const entry = this.node('li', `${this.t('day', day)}: ${board.calendar.rewards[day - 1] ?? 0} ${board.calendar.coinType}`);
        if (day <= board.calendar.claimedDays) { entry.append(this.node('span', ` · ${this.t('claimed')}`)); entry.dataset.claimed = 'true'; }
        days.append(entry);
      }
      calendar.append(days, this.button(board.calendar.claimedToday ? 'claimed' : 'claim', 'claimHint', () => { void this.load('calendar'); }, !claimable || board.calendar.claimedToday || board.calendar.claimedDays >= board.calendar.daysInMonth));
      this.rewards.append(calendar);
    }
    this.rewards.append(this.node('p', this.t('earned', board.earned.today, board.earned.cap, board.earned.owed)));
  }

  private renderReferrals(board: Board): void {
    const invite = board.invite;
    if (!invite.enabled) { this.referrals.append(this.node('p', this.t('referralOff'))); return; }
    this.referrals.append(this.node('p', this.t('referred', invite.invitedCount ?? 0, invite.qualifiedCount ?? 0)),
      this.node('p', this.t('referralReward', invite.requiredLevel, invite.inviterCoins, invite.coinType, invite.invitedCoins)));
    if (invite.code) {
      const label = this.node('label', this.t('code')); const code = this.node('input'); code.value = invite.code; code.readOnly = true; code.title = this.t('code'); label.append(code);
      const url = new URL('/account.html', location.origin); url.searchParams.set('ref', invite.code); url.hash = 'community';
      const link = this.node('a', url.toString()); link.href = url.toString(); link.title = this.t('shareHint');
      this.referrals.append(label, link);
    } else this.referrals.append(this.node('p', this.t('noCode')));
    if (invite.invitedBy) { this.referrals.append(this.node('p', this.t('invitedBy', invite.invitedBy))); return; }
    const form = this.node('form'); const label = this.node('label', this.t('enter')); const input = this.node('input');
    input.maxLength = 64; input.required = true; input.title = this.t('enterHint');
    // Referral codes are public. A shared link never carries account credentials.
    const code = new URL(location.href).searchParams.get('ref');
    if (code && /^[a-z0-9-]{1,64}$/i.test(code)) input.value = code;
    input.disabled = this.busy || !this.reply?.characterOnline;
    const submit = this.button('submit', 'submitHint', () => { if (form.reportValidity()) void this.load('invite', input.value.trim()); }, !this.reply?.characterOnline);
    label.append(input); form.append(label, submit);
    form.addEventListener('submit', event => { event.preventDefault(); if (!submit.disabled && form.reportValidity()) void this.load('invite', input.value.trim()); });
    this.referrals.append(form);
  }

  private reward(reward: Reward): string {
    const parts: string[] = [];
    if (reward.zen) parts.push(`${reward.zen} Zen`);
    if (reward.coins) parts.push(`${reward.coins} ${reward.coinType}`);
    for (const item of reward.items as (Reward['items'][number] & { nameEn?: string; nameVi?: string })[]) {
      const name = (this.options.language() === 'vi' ? item.nameVi || item.nameEn : item.nameEn) || `${item.group}:${item.number}`;
      parts.push(`${name}${item.level ? ` +${item.level}` : ''} ×${item.count}`);
    }
    return parts.join(' · ');
  }

  private date(value: number): string {
    return new Intl.DateTimeFormat(this.options.language() === 'vi' ? 'vi-VN' : 'en-AU', {
      timeZone: 'Australia/Sydney', dateStyle: 'medium', timeStyle: 'short', hourCycle: 'h23',
    }).format(new Date(value));
  }
}
