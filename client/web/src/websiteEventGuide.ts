import { currentLanguage, onLanguageChange } from './i18n.js';
import { websiteEventTime } from './websiteEventTime.js';

type Name = { en: string | null; vi: string | null };
type Currency = { zen: number; coins: number; coinType: string };
type TimedReward = { kind: string; amount: number; rank: number | null; success: string[]; requiredKill: Name | null; pool: Name[] };
type Tier = {
  tier: number; name: Name; map: Name | null; minimumLevel: number; maximumLevel: number;
  specialMinimumLevel: number; specialMaximumLevel: number; requiresMasterClass: boolean;
  entranceFee: number; durationSeconds: number; ticket: Name | null; ticketLevel: number | null; rewards: TimedReward[];
};
type Invasion = { singleMap: boolean; spawns: { monster: Name; count: number; everyMap: boolean; maps: Name[]; loot: Name[] }[] };
type Boss = {
  map: Name | null; participationMinimumDamage: number; participationMinimumPercent: number | null; topAlsoGetParticipation: boolean;
  rewards: (Currency & { kind: string; items: { item: Name; level: number; count: number }[] })[];
};
type EventRow = {
  name: Name; id: string; category: string; nextStartUtc: string | null; endsAtUtc: string | null; isActive: boolean;
  scheduledEndUtc?: string | null;
  tiers: Tier[]; invasion: Invasion | null; boss: Boss | null; winner: Currency | null;
};
type Quest = {
  name: Name; group: number; number: number; npc: Name; maps: Name[]; minimumLevel: number; maximumLevel: number; requiredStartMoney: number; repeatable: boolean; qualifiedClass: Name | null;
  kills: { name: Name; count: number }[]; items: { name: Name; count: number; dropGroup?: {
    chance: number | null; monster: Name | null; minimumMonsterLevel: number | null; maximumMonsterLevel: number | null;
  } | null }[];
  rewards: { kind: string; value: number | null; name: Name | null; level: number | null; attribute: string | null }[];
};
type Guide = { events: EventRow[]; eventsTruncated: boolean; quests: Quest[]; questsTruncated: boolean };
type Snapshot = { enabled: false } | { enabled: true; serverId: number; servers: { id: number; name: string | null }[]; guide: Guide };

const copy = {
  heading: ['Events and quests', 'Sự kiện và nhiệm vụ'], server: ['Server', 'Máy chủ'], next: ['Next start', 'Lần bắt đầu kế tiếp'],
  scheduled: ['Events and invasions', 'Sự kiện và xâm lăng'], quests: ['NPC quests', 'Nhiệm vụ NPC'],
  loading: ['Loading event and quest configuration…', 'Đang tải cấu hình sự kiện và nhiệm vụ…'],
  off: ['The host has turned off the live event and quest guide.', 'Máy chủ đã tắt hướng dẫn sự kiện và nhiệm vụ trực tiếp.'],
  unavailable: ['The event and quest configuration is unavailable.', 'Chưa đọc được cấu hình sự kiện và nhiệm vụ.'],
  noEvents: ['This server reports no scheduled events.', 'Máy chủ này chưa công bố sự kiện theo lịch.'],
  noQuests: ['This server reports no NPC quests.', 'Máy chủ này chưa công bố nhiệm vụ NPC.'],
  noStart: ['Next start unavailable', 'Chưa có giờ bắt đầu kế tiếp'], retry: ['Try again', 'Thử lại'],
  current: ['Running now', 'Đang diễn ra'], ends: ['Ends', 'Kết thúc'], sydney: ['Sydney', 'Sydney'], visitor: ['Your time', 'Giờ của bạn'],
  showVisitor: ['Show my time', 'Hiển thị giờ của tôi'],
  event: ['Event', 'Sự kiện'], invasion: ['Invasion', 'Xâm lăng'], boss: ['World boss', 'Boss thế giới'], custom: ['Host event', 'Sự kiện máy chủ'],
  details: ['Entry and rewards', 'Điều kiện vào và phần thưởng'], tier: ['Tier', 'Bậc'], map: ['Map', 'Bản đồ'],
  levels: ['Level', 'Cấp'], specialLevels: ['Special class levels', 'Cấp của lớp đặc biệt'], master: ['Master class required', 'Yêu cầu lớp Master'],
  fee: ['Entry fee', 'Phí tham gia'], startMoney: ['Quest start fee', 'Phí nhận nhiệm vụ'], duration: ['Duration', 'Thời lượng'], seconds: ['seconds', 'giây'],
  ticket: ['Ticket', 'Vé'], rewards: ['Configured rewards', 'Phần thưởng đã cấu hình'],
  possible: ['Possible items', 'Vật phẩm có thể nhận'], rank: ['Rank', 'Hạng'], requiresKill: ['Required defeat', 'Yêu cầu hạ mục tiêu'],
  oneMap: ['One map is selected for the invasion.', 'Xâm lăng diễn ra trên một bản đồ được chọn.'],
  oneOfMaps: ['One of', 'Một trong các bản đồ'], allMaps: ['All listed maps', 'Tất cả bản đồ được liệt kê'],
  npcs: ['Quest giver', 'NPC giao nhiệm vụ'], repeatable: ['Repeatable', 'Có thể làm lại'], class: ['Class', 'Lớp nhân vật'],
  kills: ['Defeat', 'Tiêu diệt'], items: ['Collect', 'Thu thập'], limited: ['The guide shows the first entries in this list.', 'Hướng dẫn hiển thị các mục đầu của danh sách.'],
  groupChance: ['Configured drop group chance', 'Tỷ lệ nhóm rơi theo cấu hình'],
  monsterLevels: ['Monster level', 'Cấp quái'],
  scheduledEnd: ['Scheduled end', 'Giờ kết thúc dự kiến'],
  winner: ['Winner', 'Người thắng'], contribution: ['Participation threshold', 'Ngưỡng tham gia'], damage: ['damage', 'sát thương'],
  topAlso: ['Top ranks can also earn participation rewards.', 'Các hạng cao cũng có thể nhận thưởng tham gia.'],
} as const;

const rewardNames: Record<string, readonly [string, string]> = {
  Money: ['Zen', 'Zen'], Experience: ['EXP', 'EXP'], ExperiencePerRemainingSeconds: ['EXP per remaining second', 'EXP mỗi giây còn lại'],
  Item: ['Item', 'Vật phẩm'], ItemDrop: ['Item drop', 'Vật phẩm rơi'], Score: ['Score', 'Điểm sự kiện'], GensAttribution: ['Gens contribution', 'Cống hiến Gens'],
  LevelUpPoints: ['Stat points', 'Điểm chỉ số'], Attribute: ['Attribute', 'Chỉ số'], Skill: ['Skill learned', 'Học kỹ năng'],
  CharacterEvolutionFirstToSecond: ['Second class promotion', 'Chuyển lớp lần hai'], CharacterEvolutionSecondToThird: ['Master class promotion', 'Chuyển lớp Master'],
  top1: ['First place', 'Hạng nhất'], top2: ['Second place', 'Hạng nhì'], top3: ['Third place', 'Hạng ba'], top10: ['Top 10 tier', 'Bậc tốp 10'],
  participation: ['Participation', 'Tham gia'], lastHit: ['Last hit', 'Đòn kết liễu'],
};

const successNames: Record<string, readonly [string, string]> = {
  Winner: ['Winner', 'Người thắng'], WinningParty: ['Winning party', 'Tổ đội thắng'],
  WinnerOrInWinningParty: ['Winner or winning party', 'Người thắng hoặc tổ đội thắng'], Loser: ['Other participants', 'Người tham gia còn lại'],
  Alive: ['Survived', 'Sống sót'], Dead: ['Defeated', 'Bị hạ gục'], WinnerExists: ['There is a winner', 'Có người thắng'],
  WinnerNotExists: ['No winner', 'Không có người thắng'],
};

function node<K extends keyof HTMLElementTagNameMap>(tag: K, value?: string): HTMLElementTagNameMap[K] {
  const element = document.createElement(tag);
  if (value !== undefined) element.textContent = value;
  return element;
}
function locale(): 'en' | 'vi' { return currentLanguage() === 'vi' ? 'vi' : 'en'; }
function text(key: keyof typeof copy): string { return copy[key][locale() === 'vi' ? 1 : 0]; }
function name(value: Name | null): string { return (locale() === 'vi' ? value?.vi : value?.en) || value?.en || value?.vi || ''; }
function number(value: number): string { return new Intl.NumberFormat(locale() === 'vi' ? 'vi-VN' : 'en-AU', { maximumFractionDigits: 3 }).format(value); }
function localizedList(values: Name[]): string { return values.map(name).filter(Boolean).join(', '); }
function label(dictionary: Record<string, readonly [string, string]>, key: string): string {
  return Object.hasOwn(dictionary, key) ? dictionary[key][locale() === 'vi' ? 1 : 0] : '';
}
function summary(value: string): HTMLDetailsElement {
  const details = node('details');
  details.append(node('summary', value));
  return details;
}
function fact(parent: HTMLElement, key: keyof typeof copy, value: string): void {
  if (value) parent.append(node('p', `${text(key)} · ${value}`));
}
function currencies(value: Currency): string {
  return [value.zen ? `${number(value.zen)} Zen` : '', value.coins ? `${number(value.coins)} ${value.coinType}` : ''].filter(Boolean).join(' + ');
}
function levels(from: number, to: number): string {
  if (from > 0 && to > 0) return from === to ? number(from) : `${number(from)}–${number(to)}`;
  if (from > 0) return `≥ ${number(from)}`;
  return to > 0 ? `≤ ${number(to)}` : '';
}
function list(parent: HTMLElement, title: string, entries: string[]): void {
  if (!entries.some(Boolean)) return;
  const rows = node('ul');
  rows.append(...entries.filter(Boolean).map(entry => node('li', entry)));
  parent.append(node('h5', title), rows);
}

function renderTier(tier: Tier): HTMLElement {
  const card = node('article');
  card.append(node('h4', [name(tier.name), `${text('tier')} ${number(tier.tier)}`].filter(Boolean).join(' · ')));
  fact(card, 'map', name(tier.map));
  fact(card, 'levels', levels(tier.minimumLevel, tier.maximumLevel));
  const special = levels(tier.specialMinimumLevel, tier.specialMaximumLevel);
  if (special !== levels(tier.minimumLevel, tier.maximumLevel)) fact(card, 'specialLevels', special);
  if (tier.requiresMasterClass) card.append(node('p', text('master')));
  fact(card, 'fee', `${number(tier.entranceFee)} Zen`);
  fact(card, 'duration', `${number(tier.durationSeconds)} ${text('seconds')}`);
  const ticket = name(tier.ticket);
  if (ticket) fact(card, 'ticket', tier.ticketLevel !== null && tier.ticketLevel > 0 ? `${ticket} +${number(tier.ticketLevel)}` : ticket);
  list(card, text('rewards'), tier.rewards.map(reward => {
    const heading = label(rewardNames, reward.kind);
    if (!heading) return '';
    const conditions = reward.success.map(value => label(successNames, value)).filter(Boolean);
    if (reward.rank !== null) conditions.push(`${text('rank')} ${number(reward.rank)}`);
    if (reward.requiredKill) conditions.push(`${text('requiresKill')} ${name(reward.requiredKill)}`);
    const pool = localizedList(reward.pool);
    return [`${number(reward.amount)} ${heading}`, ...conditions, pool && `${text('possible')} ${pool}`].filter(Boolean).join(' · ');
  }));
  return card;
}

function renderInvasion(invasion: Invasion): HTMLElement[] {
  const rows = invasion.spawns.map(spawn => {
    const card = node('article');
    card.append(node('h4', `${number(spawn.count)} × ${name(spawn.monster)}`));
    fact(card, invasion.singleMap ? 'map' : spawn.everyMap ? 'allMaps' : 'oneOfMaps', localizedList(spawn.maps));
    fact(card, 'possible', localizedList(spawn.loot));
    return card;
  });
  return invasion.singleMap ? [node('p', text('oneMap')), ...rows] : rows;
}

function renderBoss(boss: Boss): HTMLElement {
  const card = node('article');
  fact(card, 'map', name(boss.map));
  if (boss.rewards.some(reward => reward.kind === 'participation')) {
    const threshold = [boss.participationMinimumPercent !== null ? `${number(boss.participationMinimumPercent)}%` : '',
      boss.participationMinimumDamage > 0 ? `${number(boss.participationMinimumDamage)} ${text('damage')}` : ''];
    fact(card, 'contribution', threshold.filter(Boolean).join(locale() === 'vi' ? ' hoặc ' : ' or '));
    if (boss.topAlsoGetParticipation) card.append(node('p', text('topAlso')));
  }
  list(card, text('rewards'), boss.rewards.map(reward => {
    const items = reward.items.map(entry => `${number(entry.count)} × ${name(entry.item)}${entry.level > 0 ? ` +${number(entry.level)}` : ''}`);
    return [label(rewardNames, reward.kind), currencies(reward), ...items].filter(Boolean).join(' · ');
  }));
  return card;
}

function renderEvent(row: EventRow, showVisitor: boolean): HTMLElement {
  const card = node('article');
  const category = row.category === 'event' || row.category === 'invasion' || row.category === 'boss' ? row.category : 'custom';
  card.append(node('h4', name(row.name) || row.id), node('p', text(category)));
  if (row.isActive) card.append(node('p', text('current')));
  const instant = row.isActive ? row.endsAtUtc : row.nextStartUtc;
  const times = instant ? websiteEventTime(instant, locale()) : null;
  if (times) {
    card.append(node('p', `${text(row.isActive ? 'ends' : 'next')} · ${times.sydney} (${text('sydney')})`));
    if (showVisitor && times.visitor !== times.sydney) fact(card, 'visitor', times.visitor);
  } else if (!row.isActive) card.append(node('p', text('noStart')));
  const scheduledEnd = row.scheduledEndUtc ? websiteEventTime(row.scheduledEndUtc, locale()) : null;
  if (scheduledEnd) fact(card, 'scheduledEnd', `${scheduledEnd.sydney} (${text('sydney')})`);
  const details = summary(text('details'));
  details.append(...row.tiers.map(renderTier));
  if (row.invasion) details.append(...renderInvasion(row.invasion));
  if (row.boss) details.append(renderBoss(row.boss));
  if (row.winner) fact(details, 'winner', currencies(row.winner));
  if (details.childElementCount > 1) card.append(details);
  return card;
}

function renderQuest(quest: Quest): HTMLElement {
  const card = summary(name(quest.name) || `#${quest.group}:${quest.number}`);
  fact(card, 'npcs', name(quest.npc));
  fact(card, 'map', localizedList(quest.maps));
  fact(card, 'levels', levels(quest.minimumLevel, quest.maximumLevel));
  fact(card, 'class', name(quest.qualifiedClass));
  if (quest.requiredStartMoney > 0) fact(card, 'startMoney', `${number(quest.requiredStartMoney)} Zen`);
  if (quest.repeatable) card.append(node('p', text('repeatable')));
  list(card, text('kills'), quest.kills.map(entry => `${number(entry.count)} × ${name(entry.name)}`));
  list(card, text('items'), quest.items.map(entry => {
    const group = entry.dropGroup;
    const levelRange = group ? levels(group.minimumMonsterLevel ?? 0, group.maximumMonsterLevel ?? 0) : '';
    const info = [
      `${number(entry.count)} × ${name(entry.name)}`,
      group ? name(group.monster) : '',
      levelRange ? `${text('monsterLevels')} ${levelRange}` : '',
      typeof group?.chance === 'number' ? `${text('groupChance')} ${number(group.chance * 100)}%` : '',
    ];
    return info.filter(Boolean).join(' · ');
  }));
  list(card, text('rewards'), quest.rewards.map(reward => {
    const kind = label(rewardNames, reward.kind);
    if (!kind) return '';
    const description = name(reward.name) || reward.attribute || '';
    const value = reward.value === null ? '' : number(reward.value);
    const level = reward.level !== null && reward.level > 0 ? `+${number(reward.level)}` : '';
    return [value, kind, description, level].filter(Boolean).join(' ');
  }));
  return card;
}

function mount(root: HTMLElement): void {
  const fields = node('div'); fields.className = 'event-guide-fields';
  const label = node('label'); const server = node('select'); label.htmlFor = server.id = 'eventGuideServer';
  const timeLabel = node('label'); const time = node('input'); time.type = 'checkbox'; time.id = 'eventGuideVisitorTime';
  timeLabel.htmlFor = time.id;
  const timeField = node('div'); timeField.className = 'event-guide-time'; timeField.append(time, timeLabel);
  fields.append(label, server, timeField);
  const state = node('p'); state.setAttribute('role', 'status');
  const retry = node('button'); retry.type = 'button';
  const result = node('div');
  root.replaceChildren(fields, state, retry, result);
  let data: Snapshot | undefined;
  let request: AbortController | undefined;
  let selection: number | undefined;
  let failed = false;

  function render(): void {
    for (const title of document.querySelectorAll('[data-event-guide-title]')) title.textContent = text('heading');
    label.textContent = text('server'); timeLabel.textContent = text('showVisitor'); retry.textContent = text('retry');
    fields.hidden = data?.enabled !== true || failed;
    retry.hidden = !failed || request !== undefined;
    state.textContent = request ? text('loading') : failed ? text('unavailable') : data?.enabled === false ? text('off') : '';
    state.hidden = state.textContent === '';
    if (!data?.enabled || failed) { result.replaceChildren(); return; }
    server.replaceChildren(...data.servers.map(row => new Option(row.name ?? `#${row.id}`, String(row.id))));
    server.value = String(selection ?? data.serverId);
    if (request) { result.replaceChildren(); return; }
    const { events, eventsTruncated, quests, questsTruncated } = data.guide;
    const eventsPanel = node('div'); eventsPanel.className = 'event-guide-cards';
    eventsPanel.append(...events.map(event => renderEvent(event, time.checked)));
    const questsPanel = node('div'); questsPanel.className = 'event-guide-cards';
    questsPanel.append(...quests.map(renderQuest));
    result.replaceChildren(node('h3', text('scheduled')), events.length ? eventsPanel : node('p', text('noEvents')));
    if (eventsTruncated) result.append(node('p', text('limited')));
    result.append(node('h3', text('quests')), quests.length ? questsPanel : node('p', text('noQuests')));
    if (questsTruncated) result.append(node('p', text('limited')));
  }

  async function load(serverId?: number): Promise<void> {
    request?.abort();
    const controller = new AbortController();
    request = controller; selection = serverId; failed = false;
    render();
    try {
      const query = serverId === undefined ? '' : `?${new URLSearchParams({ serverId: String(serverId) })}`;
      const response = await fetch(`/website/event-guide${query}`, { cache: 'no-store', signal: controller.signal });
      if (!response.ok) throw new Error('Event guide unavailable');
      const snapshot: Snapshot = await response.json();
      if (snapshot.enabled !== false && (snapshot.enabled !== true || !Array.isArray(snapshot.servers)
        || !Array.isArray(snapshot.guide?.events) || !Array.isArray(snapshot.guide.quests))) throw new Error('Invalid event guide');
      if (!controller.signal.aborted) data = snapshot;
    } catch {
      if (!controller.signal.aborted) failed = true;
    } finally {
      if (request === controller) { request = undefined; render(); }
    }
  }

  server.addEventListener('change', () => { void load(Number(server.value)); });
  time.addEventListener('change', render);
  retry.addEventListener('click', () => { void load(selection); });
  onLanguageChange(render);
  void load();
}

const guide = document.getElementById('eventGuide');
if (guide) mount(guide);
