import { currentLanguage, onLanguageChange } from './i18n.js';

type Name = { en: string | null; vi: string | null };
type ItemChoice = { number: number; name: Name };
type RecipeChoice = { index: number; name: Name; npc: Name };
type Item = ItemChoice & {
  slot: Name | null; maximumItemLevel: number; dropsFromMonsters: boolean; minimumMonsterLevel: number; maximumDropLevel: number | null;
  classes: Name[]; requirements: { attribute: string; value: number; minimumItemLevel: number | null }[];
  sources: { map: Name; monster: Name; level: number | null; monsterPool: boolean; mapPool: boolean }[]; sourcesTruncated: boolean;
  worldPools: { name: Name; minimumMonsterLevel: number | null; maximumMonsterLevel: number | null }[];
  contents: { sourceLevel: number; requiredCharacterLevel: number; pools: { kind: string; money: number | null; minimumLevel: number; maximumLevel: number; items: Name[] }[] }[];
  foundInBoxes: { name: Name; level: number }[];
};
type Mix = RecipeChoice & {
  maps: Name[]; settings: null | {
    basePercent: number | null; maximumPercent: number; npcPriceDivisor: number | null; fixedZen: number; zenPerFinalPercentage: number;
    ingredients: { items: Name[]; acceptsAnyItem: boolean; minimumAmount: number; maximumAmount: number; minimumItemLevel: number; maximumItemLevel: number;
      npcPriceDivisor: number; addPercentage: number; reference: number; options: Name[] }[];
    results: { name: Name | null; randomMinimumLevel: number; randomMaximumLevel: number; reference: number; addLevel: number }[];
  };
};
type Guide = {
  groups: number[]; group: number | null; itemPage: number; itemPages: number; items: ItemChoice[]; item: Item | null;
  mixPage: number; mixPages: number; mixes: RecipeChoice[]; mix: Mix | null;
};
type Snapshot = { enabled: false } | { enabled: true; serverId: number; servers: { id: number; name: string | null }[]; guide: Guide };
type Selection = { serverId?: number; group?: number; page?: number; number?: number; mixPage?: number; mixIndex?: number };

const copy = {
  heading: ['Items, drops and mixes', 'Vật phẩm, nguồn rơi và ép đồ'], server: ['Server', 'Máy chủ'], group: ['Item group', 'Nhóm vật phẩm'],
  items: ['Items and boxes', 'Vật phẩm và hộp'], item: ['Item', 'Vật phẩm'], mixes: ['Mix recipes', 'Công thức ép'], mix: ['Recipe', 'Công thức'],
  previous: ['Previous', 'Trang trước'], next: ['Next', 'Trang sau'], page: ['Page', 'Trang'],
  loading: ['Loading item and mix configuration…', 'Đang tải cấu hình vật phẩm và ép đồ…'],
  off: ['The host has turned off the live item and mix guide.', 'Máy chủ đã tắt hướng dẫn vật phẩm và ép đồ trực tiếp.'],
  unavailable: ['Item and mix configuration is unavailable.', 'Chưa đọc được cấu hình vật phẩm và ép đồ.'], retry: ['Try again', 'Thử lại'],
  noItems: ['This server reports no active items.', 'Máy chủ này chưa công bố vật phẩm đang hoạt động.'],
  noMixes: ['This server reports no recipes at active NPCs.', 'Máy chủ này chưa công bố công thức ở NPC đang hoạt động.'],
  slot: ['Equipment slot', 'Ô trang bị'], cap: ['Maximum enhancement', 'Cấp nâng tối đa'], classes: ['Eligible classes', 'Lớp có thể dùng'],
  requirements: ['Configured requirements', 'Yêu cầu đã cấu hình'], fromLevel: ['from item level', 'từ cấp vật phẩm'],
  drop: ['Configured drop sources', 'Nguồn rơi đã cấu hình'], dropBand: ['Item monster-level interval', 'Khoảng cấp quái của vật phẩm'],
  normalPool: ['Eligible for the standard monster item pool', 'Có thể nằm trong nhóm vật phẩm rơi thông thường'],
  monsterPool: ['Monster pool', 'Nhóm rơi của quái'], mapPool: ['Map pool', 'Nhóm rơi của bản đồ'], worldPool: ['Configured world pools', 'Nhóm rơi đã cấu hình của thế giới'],
  lvl: ['Level', 'Cấp'], limited: ['Only the first configured sources are shown.', 'Chỉ hiển thị các nguồn đầu trong cấu hình.'],
  box: ['Box contents', 'Vật phẩm trong hộp'], inBoxes: ['Listed in box pools', 'Có trong nhóm phần thưởng của hộp'],
  boxLevel: ['Box level', 'Cấp hộp'], openLevel: ['Opening level required', 'Cấp cần để mở'],
  resultLevels: ['Result item levels', 'Cấp vật phẩm nhận được'],
  npc: ['NPC', 'NPC'], map: ['Map', 'Bản đồ'], quote: ['Get this recipe’s quote in game.', 'Xem báo giá của công thức này trong game.'],
  chance: ['Base success', 'Tỷ lệ cơ sở'], maxChance: ['Success cap', 'Giới hạn tỷ lệ thành công'], cash: ['Fixed cost', 'Phí cố định'],
  eachPercent: ['Zen per final percentage point', 'Zen trên mỗi điểm phần trăm cuối'], divisor: ['NPC-price divisor', 'Số chia giá NPC'],
  ingredients: ['Ingredients', 'Nguyên liệu'], quantity: ['Quantity', 'Số lượng'], eligible: ['Any eligible item', 'Vật phẩm thỏa điều kiện'],
  options: ['Required options', 'Thuộc tính yêu cầu'], perItem: ['Bonus per item', 'Tỷ lệ thêm mỗi vật phẩm'],
  ref: ['Ingredient reference', 'Mã nguyên liệu được dùng lại'], results: ['Possible results', 'Kết quả có thể nhận'], add: ['Added levels', 'Cấp tăng thêm'],
} as const;

const attributeNames: Record<string, string> = {
  Level: 'Cấp', 'Master Level': 'Cấp Master', 'Total Strength': 'Tổng sức mạnh', 'Total Agility': 'Tổng nhanh nhẹn',
  'Total Energy': 'Tổng năng lượng', 'Total Vitality': 'Tổng thể lực', 'Total Leadership': 'Tổng mệnh lệnh',
  'Total Strength Requirement Value': 'Yêu cầu sức mạnh', 'Total Agility Requirement Value': 'Yêu cầu nhanh nhẹn',
  'Total Energy Requirement Value': 'Yêu cầu năng lượng', 'Total Vitality Requirement Value': 'Yêu cầu thể lực',
  'Total Leadership Requirement Value': 'Yêu cầu mệnh lệnh',
};
const poolNames: Record<string, readonly [string, string]> = {
  Ancient: ['Ancient pool', 'Nhóm đồ Thần'], Excellent: ['Excellent pool', 'Nhóm đồ Hoàn Hảo'],
  RandomItem: ['Random item pool', 'Nhóm vật phẩm ngẫu nhiên'], SocketItem: ['Socket item pool', 'Nhóm đồ Socket'], Jewel: ['Jewel pool', 'Nhóm ngọc'],
};

function node<K extends keyof HTMLElementTagNameMap>(tag: K, value?: string): HTMLElementTagNameMap[K] {
  const element = document.createElement(tag);
  if (value !== undefined) element.textContent = value;
  return element;
}
function vi(): boolean { return currentLanguage() === 'vi'; }
function text(key: keyof typeof copy): string { return copy[key][vi() ? 1 : 0]; }
function name(value: Name | null): string { return (vi() ? value?.vi : value?.en) || value?.en || value?.vi || ''; }
function number(value: number): string { return new Intl.NumberFormat(vi() ? 'vi-VN' : 'en-AU').format(value); }
function range(min: number, max: number): string { return min === max ? number(min) : `${number(min)}–${number(max)}`; }
function band(min: number | null, max: number | null): string {
  return min === null ? max === null ? '' : `≤ ${number(max)}` : max === null ? `≥ ${number(min)}` : range(min, max);
}
function joined(values: Name[]): string { return values.map(name).filter(Boolean).join(', '); }
function fact(parent: HTMLElement, key: keyof typeof copy, value: string): void {
  if (value) parent.append(node('p', `${text(key)} · ${value}`));
}
function list(parent: HTMLElement, title: keyof typeof copy, values: string[]): void {
  const items = values.filter(Boolean);
  if (!items.length) return;
  const result = node('ul'); result.append(...items.map(value => node('li', value)));
  parent.append(node('h4', text(title)), result);
}

function renderItem(item: Item): HTMLElement {
  const card = node('article');
  card.append(node('h4', name(item.name) || `#${item.number}`));
  fact(card, 'slot', name(item.slot));
  fact(card, 'cap', `+${number(item.maximumItemLevel)}`);
  fact(card, 'classes', joined(item.classes));
  list(card, 'requirements', item.requirements.map(row => {
    const attr = vi() && Object.hasOwn(attributeNames, row.attribute) ? attributeNames[row.attribute] : row.attribute;
    return `${attr} ≥ ${number(row.value)}${row.minimumItemLevel && row.minimumItemLevel > 0 ? ` · ${text('fromLevel')} +${number(row.minimumItemLevel)}` : ''}`;
  }));
  if (item.dropsFromMonsters) card.append(node('p', text('normalPool')));
  if (item.dropsFromMonsters || item.worldPools.length || item.sources.some(row => row.mapPool)) {
    fact(card, 'dropBand', band(item.minimumMonsterLevel, item.maximumDropLevel));
  }
  list(card, 'drop', item.sources.map(row => [name(row.map), name(row.monster), row.level !== null && `${text('lvl')} ${number(row.level)}`,
    row.monsterPool && text('monsterPool'), row.mapPool && text('mapPool')].filter(Boolean).join(' · ')));
  if (item.sourcesTruncated) card.append(node('p', text('limited')));
  list(card, 'worldPool', item.worldPools.map(pool => [name(pool.name), band(pool.minimumMonsterLevel, pool.maximumMonsterLevel)].filter(Boolean).join(' · ')));
  list(card, 'inBoxes', item.foundInBoxes.map(box => `${name(box.name)} +${number(box.level)}`));
  if (item.contents.length) card.append(node('h4', text('box')));
  for (const box of item.contents) {
    const details = node('details'); details.append(node('summary', `${text('boxLevel')} +${number(box.sourceLevel)}`));
    if (box.requiredCharacterLevel > 0) fact(details, 'openLevel', number(box.requiredCharacterLevel));
    const pools = node('ul');
    for (const pool of box.pools) {
      const category = Object.hasOwn(poolNames, pool.kind) ? poolNames[pool.kind][vi() ? 1 : 0] : '';
      const result = pool.money !== null ? `${number(pool.money)} Zen` : [category, joined(pool.items), `${text('resultLevels')} ${range(pool.minimumLevel, pool.maximumLevel)}`].filter(Boolean).join(' · ');
      if (result) pools.append(node('li', result));
    }
    if (pools.childElementCount) details.append(pools);
    card.append(details);
  }
  return card;
}

function renderMix(mix: Mix): HTMLElement {
  const card = node('article');
  card.append(node('h4', name(mix.name) || `#${mix.index}`));
  fact(card, 'npc', name(mix.npc)); fact(card, 'map', joined(mix.maps));
  const config = mix.settings;
  if (!config) { card.append(node('p', text('quote'))); return card; }
  if (config.basePercent !== null) fact(card, 'chance', `${number(config.basePercent)}%`);
  fact(card, 'maxChance', `${number(config.maximumPercent)}%`);
  fact(card, 'cash', `${number(config.fixedZen)} Zen`);
  if (config.zenPerFinalPercentage) fact(card, 'eachPercent', `${number(config.zenPerFinalPercentage)} Zen`);
  if (config.npcPriceDivisor !== null) fact(card, 'divisor', number(config.npcPriceDivisor));
  list(card, 'ingredients', config.ingredients.map(material => [
    joined(material.items) || (material.acceptsAnyItem ? text('eligible') : ''),
    `${text('quantity')} ${range(material.minimumAmount, material.maximumAmount)}`,
    `${text('lvl')} ${range(material.minimumItemLevel, material.maximumItemLevel)}`,
    material.options.length ? `${text('options')} ${joined(material.options)}` : '',
    material.addPercentage > 0 ? `${text('perItem')} ${number(material.addPercentage)}%` : '',
    material.npcPriceDivisor > 0 ? `${text('divisor')} ${number(material.npcPriceDivisor)}` : '',
    material.reference > 0 ? `${text('ref')} ${number(material.reference)}` : '',
  ].filter(Boolean).join(' · ')));
  list(card, 'results', config.results.map(result => [name(result.name),
    result.reference > 0 ? `${text('ref')} ${number(result.reference)}` : '',
    `${text('resultLevels')} ${range(result.randomMinimumLevel, result.randomMaximumLevel)}`,
    result.addLevel > 0 ? `${text('add')} ${number(result.addLevel)}` : '',
  ].filter(Boolean).join(' · ')));
  return card;
}

function selector(id: string): { field: HTMLElement; label: HTMLLabelElement; select: HTMLSelectElement } {
  const field = node('div'); const label = node('label'); const select = node('select');
  label.htmlFor = select.id = id; field.className = 'item-guide-field'; field.append(label, select);
  return { field, label, select };
}

function pageControls(update: (page: number) => void): { root: HTMLElement; render: (page: number, count: number, loading: boolean) => void } {
  const root = node('div'); root.className = 'item-guide-pages';
  const previous = node('button'); const next = node('button'); const status = node('span'); let current = 1;
  previous.type = next.type = 'button';
  previous.addEventListener('click', () => update(current - 1)); next.addEventListener('click', () => update(current + 1));
  root.append(previous, status, next);
  return { root, render(page, count, loading) {
    current = page;
    previous.textContent = text('previous'); next.textContent = text('next'); status.textContent = `${text('page')} ${number(page)} / ${number(count)}`;
    previous.disabled = loading || page <= 1; next.disabled = loading || page >= count; root.hidden = count <= 1;
  } };
}

function mount(root: HTMLElement): void {
  const server = selector('itemGuideServer'); const group = selector('itemGuideGroup'); const item = selector('itemGuideItem'); const mix = selector('itemGuideMix');
  const status = node('p'); status.setAttribute('role', 'status');
  const retry = node('button'); retry.type = 'button';
  const itemPanel = node('section'); const itemHeading = node('h3'); const itemBody = node('div');
  const mixPanel = node('section'); const mixHeading = node('h3'); const mixBody = node('div');
  const itemPages = pageControls(page => { void load({ page, number: undefined }); });
  const mixPages = pageControls(mixPage => { void load({ mixPage, mixIndex: undefined }); });
  itemPanel.append(itemHeading, group.field, item.field, itemPages.root, itemBody);
  mixPanel.append(mixHeading, mix.field, mixPages.root, mixBody);
  const panels = node('div'); panels.className = 'item-guide-panels'; panels.append(itemPanel, mixPanel);
  root.replaceChildren(server.field, status, retry, panels);
  let data: Snapshot | undefined;
  let selection: Selection = {};
  let request: AbortController | undefined;
  let failed = false;

  function render(): void {
    for (const title of document.querySelectorAll('[data-item-guide-title]')) title.textContent = text('heading');
    server.label.textContent = text('server'); group.label.textContent = text('group'); item.label.textContent = text('item'); mix.label.textContent = text('mix');
    itemHeading.textContent = text('items'); mixHeading.textContent = text('mixes'); retry.textContent = text('retry');
    const active = data?.enabled === true && !failed;
    panels.hidden = server.field.hidden = !active;
    retry.hidden = !failed || request !== undefined;
    status.textContent = request ? text('loading') : failed ? text('unavailable') : data?.enabled === false ? text('off') : '';
    status.hidden = status.textContent === '';
    if (!data?.enabled || failed) { itemBody.replaceChildren(); mixBody.replaceChildren(); return; }
    const guide = data.guide;
    server.select.replaceChildren(...data.servers.map(row => new Option(row.name ?? `#${row.id}`, String(row.id))));
    server.select.value = String(selection.serverId ?? data.serverId);
    group.select.replaceChildren(...guide.groups.map(id => new Option(`${text('group')} ${number(id)}`, String(id))));
    group.select.value = String(selection.group ?? guide.group ?? '');
    item.select.replaceChildren(...guide.items.map(row => new Option(name(row.name) || `#${row.number}`, String(row.number))));
    item.select.value = String(selection.number ?? guide.item?.number ?? '');
    mix.select.replaceChildren(...guide.mixes.map(row => new Option([name(row.name) || `#${row.index}`, name(row.npc)].filter(Boolean).join(' · '), String(row.index))));
    mix.select.value = String(selection.mixIndex ?? guide.mix?.index ?? '');
    group.select.disabled = request !== undefined || guide.groups.length === 0;
    item.select.disabled = request !== undefined || guide.items.length === 0;
    mix.select.disabled = request !== undefined || guide.mixes.length === 0;
    itemPages.render(guide.itemPage, guide.itemPages, request !== undefined);
    mixPages.render(guide.mixPage, guide.mixPages, request !== undefined);
    if (request) { itemBody.replaceChildren(); mixBody.replaceChildren(); return; }
    itemBody.replaceChildren(guide.item ? renderItem(guide.item) : node('p', text('noItems')));
    mixBody.replaceChildren(guide.mix ? renderMix(guide.mix) : node('p', text('noMixes')));
  }

  async function load(patch: Selection = {}): Promise<void> {
    request?.abort();
    const controller = new AbortController();
    request = controller; failed = false; selection = { ...selection, ...patch };
    render();
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(selection)) if (value !== undefined) query.set(key, String(value));
    try {
      const response = await fetch(`/website/item-guide${query.size ? `?${query}` : ''}`, { cache: 'no-store', signal: controller.signal });
      if (!response.ok) throw new Error('Item guide unavailable');
      const snapshot: Snapshot = await response.json();
      if (snapshot.enabled !== false && (snapshot.enabled !== true || !Array.isArray(snapshot.servers)
        || !Array.isArray(snapshot.guide?.groups) || !Array.isArray(snapshot.guide.items) || !Array.isArray(snapshot.guide.mixes))) throw new Error('Invalid item guide');
      if (!controller.signal.aborted) {
        data = snapshot;
        if (snapshot.enabled) selection = { serverId: snapshot.serverId, group: snapshot.guide.group ?? undefined,
          page: snapshot.guide.itemPage, number: snapshot.guide.item?.number,
          mixPage: snapshot.guide.mixPage, mixIndex: snapshot.guide.mix?.index };
      }
    } catch {
      if (!controller.signal.aborted) failed = true;
    } finally {
      if (request === controller) { request = undefined; render(); }
    }
  }

  server.select.addEventListener('change', () => { void load({ serverId: Number(server.select.value), group: undefined, page: undefined, number: undefined, mixPage: undefined, mixIndex: undefined }); });
  group.select.addEventListener('change', () => { void load({ group: Number(group.select.value), page: undefined, number: undefined }); });
  item.select.addEventListener('change', () => { void load({ number: Number(item.select.value) }); });
  mix.select.addEventListener('change', () => { void load({ mixIndex: Number(mix.select.value) }); });
  retry.addEventListener('click', () => { selection = {}; void load(); });
  onLanguageChange(render);
  void load();
}

const root = document.getElementById('itemGuide');
if (root) mount(root);
