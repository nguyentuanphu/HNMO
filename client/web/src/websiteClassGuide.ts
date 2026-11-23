import { currentLanguage, onLanguageChange } from './i18n.js';

type Name = { en: string | null; vi: string | null };
type ClassRow = { id: number; name: Name; canGetCreated: boolean; creationLevel: number | null; isMasterClass: boolean };
type AttributeValue = { attribute: string; value: number };
type Formula = { target: string; input: string; operand: string | null; coefficient: number | null; operation: string; aggregate: string };
type Skill = { id: number; name: Name; range: number; attackDamage: number; masterRank: number | null; requirements: AttributeValue[]; costs: AttributeValue[] };
type Details = {
  id: number; home: Name | null; nextClass: ClassRow | null; stats: (AttributeValue & { increasableByPlayer: boolean })[];
  constants: (AttributeValue & { aggregate: string })[]; formulas: Formula[]; skills: Skill[]; truncated: boolean;
};
type Snapshot = { enabled: false } | {
  enabled: true; serverId: number; servers: { id: number; name: string | null }[]; classes: ClassRow[]; details: Details | null;
};

const copy = {
  heading: ['Classes', 'Lớp nhân vật'], server: ['Server', 'Máy chủ'], class: ['Class', 'Lớp nhân vật'],
  loading: ['Loading class configuration…', 'Đang tải cấu hình lớp nhân vật…'],
  disabled: ['The host has turned off the live class guide.', 'Máy chủ đã tắt hướng dẫn lớp nhân vật trực tiếp.'],
  unavailable: ['Class configuration is unavailable.', 'Chưa đọc được cấu hình lớp nhân vật.'],
  empty: ['This server reports no active classes.', 'Máy chủ này chưa công bố lớp nhân vật đang hoạt động.'],
  retry: ['Try again', 'Thử lại'], home: ['Starting map', 'Bản đồ khởi đầu'], next: ['Next class', 'Lớp kế tiếp'],
  creation: ['Account level required to create', 'Cấp nhân vật cần có trong tài khoản để tạo'],
  direct: ['Available at character creation', 'Có thể chọn khi tạo nhân vật'],
  promotion: ['Direct creation restricted', 'Hạn chế tạo trực tiếp'],
  master: ['Master class', 'Lớp Master'], stats: ['Starting attributes', 'Chỉ số khởi đầu'],
  increase: ['Can allocate points', 'Có thể cộng điểm'], constants: ['Configured base values', 'Giá trị cơ sở đã cấu hình'],
  formulas: ['Class formula contributions', 'Thành phần công thức của lớp'],
  formulaNote: ['The server combines these class contributions with equipment and other active effects.', 'Máy chủ kết hợp các thành phần này với trang bị và hiệu ứng đang áp dụng.'],
  skills: ['Qualified skills', 'Kỹ năng của lớp'], damage: ['Configured attack damage', 'Sát thương kỹ năng đã cấu hình'],
  range: ['Range', 'Tầm sử dụng'], rank: ['Master rank', 'Bậc Master'], requirements: ['Requirements', 'Yêu cầu'], costs: ['Costs to use', 'Chi phí khi dùng'],
  limited: ['Only the first configured entries are shown.', 'Chỉ hiển thị các mục đầu trong cấu hình.'],
} as const;

const attributes: Record<string, string> = {
  'Base Strength': 'Sức mạnh cơ sở', 'Base Agility': 'Nhanh nhẹn cơ sở', 'Base Vitality': 'Thể lực cơ sở', 'Base Energy': 'Năng lượng cơ sở',
  'Base Leadership': 'Mệnh lệnh cơ sở', 'Total Strength': 'Tổng sức mạnh', 'Total Agility': 'Tổng nhanh nhẹn',
  'Total Vitality': 'Tổng thể lực', 'Total Energy': 'Tổng năng lượng', 'Total Leadership': 'Tổng mệnh lệnh',
  'Total Strength and Agility': 'Tổng sức mạnh và nhanh nhẹn', Level: 'Cấp', 'Master Level': 'Cấp Master', 'Total Level': 'Tổng cấp',
  'Points per Level up': 'Điểm mỗi cấp', 'Master points per master Level up': 'Điểm mỗi cấp Master',
  'Maximum Health': 'Máu tối đa', 'Maximum Mana': 'Mana tối đa', 'Maximum Shield': 'Khiên SD tối đa',
  'Maximum shield level pow intermediate': 'Giá trị trung gian tính khiên SD theo cấp', 'Maximum Ability': 'Thể lực AG tối đa',
  'Current Health': 'Máu hiện tại', 'Current Mana': 'Mana hiện tại', 'Current Ability': 'Thể lực AG hiện tại',
  'Minimum Physical Base Damage': 'Sát thương vật lý cơ sở tối thiểu', 'Maximum Physical Base Damage': 'Sát thương vật lý cơ sở tối đa',
  'Minimum Wizardry Base Damage': 'Sát thương phép cơ sở tối thiểu', 'Maximum Wizardry Base Damage': 'Sát thương phép cơ sở tối đa',
  'Minimum Curse Base Damage': 'Sát thương lời nguyền cơ sở tối thiểu', 'Maximum Curse Base Damage': 'Sát thương lời nguyền cơ sở tối đa',
  'Base Defense': 'Phòng thủ cơ sở', 'Final Defense': 'Phòng thủ cuối', 'Attack Speed': 'Tốc độ đánh',
  'Attack Rate (PvM)': 'Tỷ lệ đánh trúng quái', 'Defense Rate (PvM)': 'Tỷ lệ né đòn quái',
  'Archery Attack Mode': 'Chế độ tấn công bằng cung', 'Archery Maximum Damage': 'Sát thương cung tối đa',
  'Archery Minimum Damage': 'Sát thương cung tối thiểu', 'Attack Speed Any': 'Tốc độ đánh và phép',
  'Base Max Damage Bonus (physical and wizardry)': 'Thưởng sát thương cơ sở tối đa (vật lý và phép)',
  'Base Min Damage Bonus (physical and wizardry)': 'Thưởng sát thương cơ sở tối thiểu (vật lý và phép)',
  'Berserker Mana Multiplier': 'Hệ số mana của Cuồng nộ', 'Current Shield': 'Khiên SD hiện tại',
  'Curse Base Damage (min and max)': 'Sát thương lời nguyền cơ sở (tối thiểu và tối đa)',
  'Defense Increase Bonus (absolute) With equipped Horse (MST)': 'Điểm phòng thủ tăng khi cưỡi ngựa (cây Master)',
  'Defense Increase Bonus (absolute) With equipped Shield (MST)': 'Điểm phòng thủ tăng khi trang bị khiên (cây Master)',
  'Defense Rate (PvM) Increase Bonus (absolute) With equipped Shield (MST)': 'Tỷ lệ né đòn quái tăng khi trang bị khiên (cây Master)',
  'Final Berserker health decrement': 'Mức giảm máu cuối của Cuồng nộ',
  'Flag, if the character is located in a safezone of a game map': 'Nhân vật đang ở vùng an toàn',
  'Flag, if the character is resting (sitting, leaning or hanging)': 'Nhân vật đang nghỉ (ngồi, dựa hoặc bám)',
  'Is Horse Equipped': 'Đang cưỡi ngựa', 'Is Shield Equipped': 'Đang trang bị khiên',
  'Maximum Physical Base Damage By Weapon': 'Sát thương vật lý cơ sở tối đa từ vũ khí',
  'Minimum Physical Base Damage By Weapon': 'Sát thương vật lý cơ sở tối thiểu từ vũ khí',
  'Melee Attack Mode': 'Chế độ cận chiến', 'Melee Maximum Damage': 'Sát thương cận chiến tối đa',
  'Melee Minimum Damage': 'Sát thương cận chiến tối thiểu', 'Nearby party member count': 'Số thành viên tổ đội ở gần',
  'Physical Base Damage (min and max)': 'Sát thương vật lý cơ sở (tối thiểu và tối đa)',
  'Physical Base Damage Increase': 'Mức tăng sát thương vật lý cơ sở', Resets: 'Số lần reset',
  'Shield Defense (item)': 'Phòng thủ từ trang bị khiên', 'Shield Item Defense Increase Multiplier': 'Hệ số tăng phòng thủ của khiên',
  'Stats defense': 'Phòng thủ từ chỉ số', 'Stats max wiz and curse base dmg': 'Sát thương phép và lời nguyền cơ sở tối đa từ chỉ số',
  'Stats min wiz and curse base dmg': 'Sát thương phép và lời nguyền cơ sở tối thiểu từ chỉ số',
  'Swell Life Health Increase': 'Mức tăng máu của Gồng máu', 'Swell Life Mana Increase': 'Mức tăng mana của Gồng máu',
  'Temp Defense Bonus multiplier with Shield': 'Hệ số phòng thủ tạm thời khi trang bị khiên',
  'The amount of ammo which is equipped.': 'Số đạn đang trang bị', 'TotalEnergy minus 15': 'Tổng năng lượng trừ 15',
  'Wizardry Base Damage (min and max)': 'Sát thương phép cơ sở (tối thiểu và tối đa)', 'Wizardry Base Damage Increase': 'Mức tăng sát thương phép cơ sở',
};

const aggregates: Record<string, readonly [string, string]> = {
  AddRaw: ['Add to base', 'Cộng vào cơ sở'], Multiplicate: ['Multiply base', 'Nhân cơ sở'],
  AddFinal: ['Add to final value', 'Cộng vào giá trị cuối'], Maximum: ['Apply highest contribution', 'Áp dụng thành phần cao nhất'],
};

function node<K extends keyof HTMLElementTagNameMap>(tag: K, content?: string): HTMLElementTagNameMap[K] {
  const element = document.createElement(tag);
  if (content !== undefined) element.textContent = content;
  return element;
}

function languageIndex(): 0 | 1 { return currentLanguage() === 'vi' ? 1 : 0; }
function text(key: keyof typeof copy): string { return copy[key][languageIndex()]; }
function name(value: Name, fallback = ''): string { return (languageIndex() ? value.vi : value.en) || value.en || value.vi || fallback; }
function attribute(label: string): string { return languageIndex() && Object.hasOwn(attributes, label) ? attributes[label] : label; }
function number(value: number): string { return new Intl.NumberFormat(languageIndex() ? 'vi-VN' : 'en-AU', { maximumSignificantDigits: 8 }).format(value); }

function attributeList(values: readonly AttributeValue[]): HTMLDListElement {
  const list = node('dl');
  for (const value of values) list.append(node('dt', attribute(value.attribute)), node('dd', number(value.value)));
  return list;
}

function formulaExpression(formula: Formula): string {
  const input = attribute(formula.input);
  const operand = formula.operand !== null ? attribute(formula.operand) : formula.coefficient === null ? '' : number(formula.coefficient);
  switch (formula.operation) {
    case 'Multiply': return `${input} × ${operand}`;
    case 'Add': return `${input} + ${operand}`;
    case 'Exponentiate': return `${input} ^ ${operand}`;
    case 'ExponentiateByAttribute': return `${operand} ^ ${input}`;
    case 'Maximum': return `max(${input}, ${operand})`;
    case 'Minimum': return `min(${input}, ${operand})`;
    default: return '';
  }
}

function disclosure(id: string, label: string): HTMLDetailsElement {
  const result = node('details');
  result.id = id;
  result.open = (document.getElementById(id) as HTMLDetailsElement | null)?.open ?? false;
  result.append(node('summary', label));
  return result;
}

function skillRow(skill: Skill): HTMLElement {
  const row = node('article');
  row.append(node('h4', name(skill.name, `#${skill.id}`)));
  const values = node('dl');
  values.append(node('dt', text('range')), node('dd', number(skill.range)));
  if (skill.attackDamage !== 0) values.append(node('dt', text('damage')), node('dd', number(skill.attackDamage)));
  if (skill.masterRank !== null) values.append(node('dt', text('rank')), node('dd', number(skill.masterRank)));
  row.append(values);
  if (skill.requirements.length) row.append(node('h5', text('requirements')), attributeList(skill.requirements));
  if (skill.costs.length) row.append(node('h5', text('costs')), attributeList(skill.costs));
  return row;
}

function renderClass(details: Details, selected: ClassRow, select: (id: number) => void): HTMLElement {
  const article = node('article');
  const selection = selected.canGetCreated
    ? selected.creationLevel ? `${text('creation')} ${number(selected.creationLevel)}` : text('direct')
    : text('promotion');
  article.append(node('h3', name(selected.name, `#${selected.id}`)), node('p', selection));
  if (selected.isMasterClass) article.append(node('p', text('master')));
  const home = details.home && name(details.home);
  if (home) article.append(node('p', `${text('home')} · ${home}`));
  const next = details.nextClass;
  if (next) {
    const link = node('button', `${text('next')} · ${name(next.name, `#${next.id}`)}`);
    link.type = 'button';
    link.addEventListener('click', () => select(next.id));
    article.append(link);
  }

  if (details.stats.length) {
    const list = node('dl');
    for (const row of details.stats) {
      list.append(node('dt', attribute(row.attribute)), node('dd', row.increasableByPlayer ? `${number(row.value)} · ${text('increase')}` : number(row.value)));
    }
    article.append(node('h4', text('stats')), list);
  }
  if (details.constants.length) {
    const list = node('dl');
    for (const row of details.constants) {
      const aggregate = aggregates[row.aggregate]?.[languageIndex()];
      if (aggregate) list.append(node('dt', attribute(row.attribute)), node('dd', `${number(row.value)} · ${aggregate}`));
    }
    article.append(node('h4', text('constants')), list);
  }
  if (details.formulas.length) {
    const formulas = disclosure('classGuideFormulas', text('formulas'));
    const list = node('dl');
    for (const formula of details.formulas) {
      const expression = formulaExpression(formula);
      const aggregate = aggregates[formula.aggregate]?.[languageIndex()];
      if (expression && aggregate) list.append(node('dt', attribute(formula.target)), node('dd', `${expression} · ${aggregate}`));
    }
    formulas.append(node('p', text('formulaNote')), list);
    article.append(formulas);
  }
  if (details.skills.length) {
    const skills = disclosure('classGuideSkills', `${text('skills')} (${number(details.skills.length)})`);
    const list = node('div');
    list.className = 'class-guide-skills';
    list.append(...details.skills.map(skillRow));
    skills.append(list);
    article.append(skills);
  }
  if (details.truncated) article.append(node('p', text('limited')));
  return article;
}

function mount(root: HTMLElement): void {
  const heading = document.getElementById('classes')!;
  const fields = node('div'); fields.className = 'class-guide-fields'; fields.hidden = true;
  const serverField = node('div'); const serverLabel = node('label'); const server = node('select');
  const classField = node('div'); const classLabel = node('label'); const characterClass = node('select');
  serverLabel.htmlFor = server.id = 'classGuideServer';
  classLabel.htmlFor = characterClass.id = 'classGuideClass';
  serverField.append(serverLabel, server); classField.append(classLabel, characterClass); fields.append(serverField, classField);
  const status = node('p'); status.setAttribute('role', 'status');
  const retry = node('button'); retry.type = 'button'; retry.hidden = true;
  const result = node('div');
  root.replaceChildren(fields, status, retry, result);
  let snapshot: Snapshot | undefined;
  let pending: AbortController | undefined;
  let problem = false;
  let selection: { serverId?: number; classId?: number } = {};

  function render(): void {
    heading.textContent = text('heading');
    serverLabel.textContent = text('server'); classLabel.textContent = text('class'); retry.textContent = text('retry');
    root.setAttribute('aria-busy', String(pending !== undefined));
    retry.hidden = !problem || pending !== undefined;
    const visible = snapshot?.enabled === true && !problem;
    fields.hidden = !visible;
    status.textContent = pending ? text('loading') : problem ? text('unavailable') : !snapshot?.enabled ? text('disabled') : !snapshot.details ? text('empty') : '';
    status.hidden = status.textContent === '';
    if (!visible || !snapshot?.enabled) { result.replaceChildren(); return; }
    server.replaceChildren(...snapshot.servers.map(row => new Option(row.name ?? `#${row.id}`, String(row.id))));
    server.value = String(selection.serverId ?? snapshot.serverId);
    characterClass.replaceChildren(...snapshot.classes.map(row => new Option(name(row.name, `#${row.id}`), String(row.id))));
    characterClass.value = String(selection.classId ?? snapshot.details?.id ?? '');
    characterClass.disabled = snapshot.classes.length === 0 || (pending !== undefined && selection.classId === undefined);
    const details = snapshot.details;
    const selected = snapshot.classes.find(row => row.id === details?.id);
    if (pending || !details || !selected) result.replaceChildren();
    else result.replaceChildren(renderClass(details, selected, id => { void load(snapshot?.enabled ? snapshot.serverId : undefined, id); }));
  }

  async function load(serverId?: number, classId?: number): Promise<void> {
    pending?.abort();
    const request = new AbortController();
    pending = request; selection = { serverId, classId }; problem = false;
    render();
    const query = new URLSearchParams();
    if (serverId !== undefined) query.set('serverId', String(serverId));
    if (classId !== undefined) query.set('classId', String(classId));
    try {
      const response = await fetch(`/website/class-guide${query.size ? `?${query}` : ''}`, { cache: 'no-store', signal: request.signal });
      if (!response.ok) throw new Error('Class guide response unavailable');
      const data: Snapshot = await response.json();
      if (data.enabled !== false && (data.enabled !== true || !Array.isArray(data.servers) || !Array.isArray(data.classes))) throw new Error('Invalid class guide response');
      if (!request.signal.aborted) snapshot = data;
    } catch {
      if (!request.signal.aborted) problem = true;
    } finally {
      if (pending === request) { pending = undefined; render(); }
    }
  }

  server.addEventListener('change', () => { void load(Number(server.value)); });
  characterClass.addEventListener('change', () => { void load(Number(server.value), Number(characterClass.value)); });
  retry.addEventListener('click', () => { void load(selection.serverId, selection.classId); });
  onLanguageChange(render);
  void load();
}

const root = document.getElementById('classGuide');
if (root) mount(root);
