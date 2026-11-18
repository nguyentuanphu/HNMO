// Standalone HNMO Stats / Build Calculator (ROADMAP 14.21).
// Calls GET/POST /website/build-calculator backed by server ItemAwareAttributeSystem.

interface CalcClassMeta {
  number: number;
  name: string;
  canCreate: boolean;
  isMaster: boolean;
  defaults: Record<string, number>;
}

interface CalcItemMeta {
  group: number;
  number: number;
  nameEn: string;
  nameVi: string;
  maxLevel: number;
  hasSkill: boolean;
  slots: number[];
  slotGroupName: string;
  classes: number[];
}

interface CalcMetadata {
  v: number;
  classes: CalcClassMeta[];
  items: CalcItemMeta[];
}

interface CalcResult {
  className: string;
  totalStrength: number;
  totalAgility: number;
  totalVitality: number;
  totalEnergy: number;
  totalLeadership: number;
  minPhysDmg: number;
  maxPhysDmg: number;
  minWizDmg: number;
  maxWizDmg: number;
  minCurseDmg: number;
  maxCurseDmg: number;
  defenseBase: number;
  defensePvm: number;
  defensePvp: number;
  defenseRatePvm: number;
  defenseRatePvp: number;
  attackRatePvm: number;
  attackRatePvp: number;
  attackSpeed: number;
  magicSpeed: number;
  maximumHealth: number;
  maximumMana: number;
  maximumShield: number;
  maximumAbility: number;
  criticalDamageChancePct: number;
  excellentDamageChancePct: number;
}

interface SlotDef {
  slot: number;
  en: string;
  vi: string;
}

const SLOTS: SlotDef[] = [
  { slot: 0, en: 'Right Hand', vi: 'Tay phải' },
  { slot: 1, en: 'Left Hand', vi: 'Tay trái' },
  { slot: 2, en: 'Helm', vi: 'Mũ' },
  { slot: 3, en: 'Armor', vi: 'Áo' },
  { slot: 4, en: 'Pants', vi: 'Quần' },
  { slot: 5, en: 'Gloves', vi: 'Găng' },
  { slot: 6, en: 'Boots', vi: 'Giày' },
  { slot: 7, en: 'Wings', vi: 'Cánh' },
  { slot: 8, en: 'Pet', vi: 'Thú' },
  { slot: 9, en: 'Pendant', vi: 'Dây chuyền' },
  { slot: 10, en: 'Left Ring', vi: 'Nhẫn trái' },
  { slot: 11, en: 'Right Ring', vi: 'Nhẫn phải' },
];

interface SelectedSlotState {
  itemKey: string;
  level: number;
  opt: number;
  luck: boolean;
  skill: boolean;
  exc: boolean;
}

let lang: 'en' | 'vi' = (localStorage.getItem('hnmo.lang') === 'vi' || navigator.language.toLowerCase().startsWith('vi')) ? 'vi' : 'en';
let meta: CalcMetadata | null = null;
const slotStates = new Map<number, SelectedSlotState>();
let lastResult: CalcResult | null = null;

const t = (en: string, vi: string) => (lang === 'vi' ? vi : en);
const el = <T extends HTMLElement>(id: string): T => document.getElementById(id) as T;

function applyStaticLabels(): void {
  el('calc-lang-en').setAttribute('aria-pressed', String(lang === 'en'));
  el('calc-lang-vi').setAttribute('aria-pressed', String(lang === 'vi'));
  el('calc-title').textContent = t('Stats & Build Calculator', 'Bảng Tính Chỉ Số & Trang Bị');
  el('calc-home').textContent = t('Home', 'Trang chủ');
  el('calc-play').textContent = t('Play →', 'Vào game →');
  el('calc-h-char').textContent = t('Character & Base Stats', 'Nhân vật & Điểm cơ bản');
  el('calc-h-items').textContent = t('Equipped Items', 'Trang bị mặc trên người');
  el('calc-h-out').textContent = t('Computed Server Stats', 'Chỉ số tính bởi máy chủ');
  el('lbl-class').textContent = t('Class', 'Lớp nhân vật');
  el('lbl-level').textContent = t('Level', 'Cấp độ');
  el('lbl-mlevel').textContent = t('Master Level', 'Cấp Master');
  el('lbl-str').textContent = t('Strength', 'Sức mạnh');
  el('lbl-agi').textContent = t('Agility', 'Nhanh nhẹn');
  el('lbl-vit').textContent = t('Vitality', 'Thể lực');
  el('lbl-ene').textContent = t('Energy', 'Năng lượng');
  el('lbl-cmd').textContent = t('Leadership', 'Mệnh lệnh');
  el('calc-run').textContent = t('Calculate', 'Tính ngay');
  el('calc-share').textContent = t('Copy Share Link', 'Sao chép liên kết');
  el('calc-help').textContent = t(
    'Pick a class, stats, and items below. Every calculation calls the live server attribute engine so min/max damage, wizardry, defense, speed, HP/MP/SD/AG, and PvM/PvP rates match in-game values.',
    'Chọn hệ nhân vật, điểm tiềm năng và trang bị. Kết quả được máy chủ tính trực tiếp bằng công thức chuẩn của game (sát thương, phòng thủ, tốc độ đánh, HP/MP/SD/AG, tỷ lệ PvM/PvP).',
  );
  renderSlots();
  if (lastResult) renderResult(lastResult);
}

function renderSlots(): void {
  const host = el<HTMLDivElement>('calc-slots');
  host.replaceChildren();
  const classNum = Number.parseInt(el<HTMLSelectElement>('calc-class').value || '0', 10) || 0;
  const allItems = meta?.items ?? [];

  for (const s of SLOTS) {
    const state = slotStates.get(s.slot) ?? { itemKey: '', level: 9, opt: 3, luck: false, skill: false, exc: false };
    slotStates.set(s.slot, state);

    const row = document.createElement('div');
    row.className = 'item-row';

    const label = document.createElement('span');
    label.textContent = t(s.en, s.vi);
    label.style.color = '#f1cf7a';

    const sel = document.createElement('select');
    sel.title = t(`Select item for ${s.en}`, `Chọn đồ cho ${s.vi}`);
    const emptyOpt = document.createElement('option');
    emptyOpt.value = '';
    emptyOpt.textContent = t('-- Empty --', '-- Trống --');
    sel.append(emptyOpt);

    for (const it of allItems) {
      if (!it.slots.includes(s.slot)) continue;
      if (it.classes.length > 0 && !it.classes.includes(classNum)) continue;
      const opt = document.createElement('option');
      const key = `${it.group}:${it.number}`;
      opt.value = key;
      opt.textContent = lang === 'vi' ? it.nameVi : it.nameEn;
      opt.selected = state.itemKey === key;
      sel.append(opt);
    }
    sel.addEventListener('change', () => {
      state.itemKey = sel.value;
      void runCalculation();
    });

    const lvlIn = document.createElement('input');
    lvlIn.type = 'number';
    lvlIn.min = '0';
    lvlIn.max = '15';
    lvlIn.value = String(state.level);
    lvlIn.title = t('Upgrade level +0..+15', 'Cấp cường hóa +0..+15');
    lvlIn.addEventListener('input', () => {
      state.level = Math.max(0, Math.min(15, Number.parseInt(lvlIn.value || '0', 10) || 0));
    });

    const optIn = document.createElement('input');
    optIn.type = 'number';
    optIn.min = '0';
    optIn.max = '7';
    optIn.value = String(state.opt);
    optIn.title = t('Jewel of Life option level (0..7)', 'Cấp Option (0..7)');
    optIn.addEventListener('input', () => {
      state.opt = Math.max(0, Math.min(7, Number.parseInt(optIn.value || '0', 10) || 0));
    });

    const mkFlag = (lbl: string, checked: boolean, tip: string, onChange: (v: boolean) => void) => {
      const wrap = document.createElement('label');
      wrap.style.cssText = 'display:flex;align-items:center;gap:3px;cursor:pointer;';
      wrap.title = tip;
      const cb = document.createElement('input');
      cb.type = 'checkbox';
      cb.checked = checked;
      cb.addEventListener('change', () => {
        onChange(cb.checked);
        void runCalculation();
      });
      wrap.append(cb, document.createTextNode(lbl));
      return wrap;
    };

    row.append(
      label,
      sel,
      lvlIn,
      optIn,
      mkFlag('Luck', state.luck, 'Luck', (v) => { state.luck = v; }),
      mkFlag('Skill', state.skill, 'Skill', (v) => { state.skill = v; }),
      mkFlag('Exc', state.exc, t('Excellent damage/defense tier', 'Đồ Hoàn Hảo'), (v) => { state.exc = v; }),
    );
    host.append(row);
  }
}

function renderResult(r: CalcResult): void {
  lastResult = r;
  const grid = el<HTMLDivElement>('calc-stats-grid');
  grid.replaceChildren();
  const cards: Array<[string, string, string]> = [
    ['Phys Dmg', 'Sát thương vật lý', `${r.minPhysDmg} ~ ${r.maxPhysDmg}`],
    ['Wiz Dmg', 'Sát thương phép', `${r.minWizDmg} ~ ${r.maxWizDmg}`],
    ['Curse Dmg', 'Sát thương nguyền', `${r.minCurseDmg} ~ ${r.maxCurseDmg}`],
    ['Defense (PvM / PvP)', 'Phòng thủ (Quái / Người)', `${r.defensePvm} / ${r.defensePvp}`],
    ['Attack Speed', 'Tốc độ đánh', String(r.attackSpeed)],
    ['Magic Speed', 'Tốc độ phép', String(r.magicSpeed)],
    ['Max HP', 'Máu tối đa (HP)', r.maximumHealth.toLocaleString()],
    ['Max Mana', 'Năng lượng (MP)', r.maximumMana.toLocaleString()],
    ['Max SD / AG', 'Khiên SD / Nội lực AG', `${r.maximumShield.toLocaleString()} / ${r.maximumAbility.toLocaleString()}`],
    ['Attack Rate (PvM / PvP)', 'Chính xác (PvM / PvP)', `${r.attackRatePvm} / ${r.attackRatePvp}`],
    ['Defense Rate (PvM / PvP)', 'Né tránh (PvM / PvP)', `${r.defenseRatePvm} / ${r.defenseRatePvp}`],
    ['Crit / Exc %', 'Tỷ lệ Chí mạng / HH', `${r.criticalDamageChancePct}% / ${r.excellentDamageChancePct}%`],
  ];

  for (const [en, vi, val] of cards) {
    const card = document.createElement('div');
    card.className = 'stat-card';
    const lbl = document.createElement('div');
    lbl.className = 'stat-label';
    lbl.textContent = t(en, vi);
    const num = document.createElement('div');
    num.className = 'stat-val';
    num.textContent = val;
    card.append(lbl, num);
    grid.append(card);
  }
}

function buildInputPayload() {
  const items: Array<Record<string, unknown>> = [];
  for (const [slot, st] of slotStates.entries()) {
    if (!st.itemKey) continue;
    const [gStr, nStr] = st.itemKey.split(':');
    const group = Number.parseInt(gStr ?? '0', 10) || 0;
    const number = Number.parseInt(nStr ?? '0', 10) || 0;
    items.push({
      slot,
      group,
      number,
      level: st.level,
      optionLevel: st.opt,
      luck: st.luck,
      skill: st.skill,
      excellentOptions: st.exc ? 1 : 0,
    });
  }

  return {
    classNumber: Number.parseInt(el<HTMLSelectElement>('calc-class').value || '0', 10) || 0,
    level: Number.parseInt(el<HTMLInputElement>('calc-level').value || '1', 10) || 1,
    masterLevel: Number.parseInt(el<HTMLInputElement>('calc-mlevel').value || '0', 10) || 0,
    strength: Number.parseInt(el<HTMLInputElement>('calc-str').value || '0', 10) || 0,
    agility: Number.parseInt(el<HTMLInputElement>('calc-agi').value || '0', 10) || 0,
    vitality: Number.parseInt(el<HTMLInputElement>('calc-vit').value || '0', 10) || 0,
    energy: Number.parseInt(el<HTMLInputElement>('calc-ene').value || '0', 10) || 0,
    leadership: Number.parseInt(el<HTMLInputElement>('calc-cmd').value || '0', 10) || 0,
    items,
  };
}

async function runCalculation(): Promise<void> {
  const status = el('calc-status');
  status.textContent = t('Computing...', 'Đang tính...');
  try {
    const res = await fetch('/website/build-calculator', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(buildInputPayload()),
    });
    if (!res.ok) {
      status.textContent = t(`Error (${res.status})`, `Lỗi (${res.status})`);
      return;
    }
    const result = (await res.json()) as CalcResult;
    renderResult(result);
    status.textContent = t(`Calculated for ${result.className}.`, `Đã tính cho ${result.className}.`);
  } catch {
    status.textContent = t('Server unavailable.', 'Máy chủ không phản hồi.');
  }
}

async function init(): Promise<void> {
  const params = new URLSearchParams(window.location.search);
  const urlLang = params.get('lang');
  if (urlLang === 'en' || urlLang === 'vi') lang = urlLang;

  el('calc-lang-en').addEventListener('click', () => { lang = 'en'; localStorage.setItem('hnmo.lang', 'en'); applyStaticLabels(); });
  el('calc-lang-vi').addEventListener('click', () => { lang = 'vi'; localStorage.setItem('hnmo.lang', 'vi'); applyStaticLabels(); });
  el('calc-help-btn').addEventListener('click', () => {
    const h = el('calc-help');
    h.hidden = !h.hidden;
  });
  el('calc-run').addEventListener('click', () => { void runCalculation(); });
  el('calc-share').addEventListener('click', () => {
    const p = new URLSearchParams({
      lang,
      c: el<HTMLSelectElement>('calc-class').value,
      lvl: el<HTMLInputElement>('calc-level').value,
      ml: el<HTMLInputElement>('calc-mlevel').value,
      str: el<HTMLInputElement>('calc-str').value,
      agi: el<HTMLInputElement>('calc-agi').value,
      vit: el<HTMLInputElement>('calc-vit').value,
      ene: el<HTMLInputElement>('calc-ene').value,
      cmd: el<HTMLInputElement>('calc-cmd').value,
    });
    const url = `${window.location.origin}${window.location.pathname}?${p.toString()}`;
    void navigator.clipboard?.writeText(url);
    el('calc-status').textContent = t('Copied build link!', 'Đã sao chép liên kết!');
  });

  if (params.has('lvl')) el<HTMLInputElement>('calc-level').value = params.get('lvl') ?? '400';
  if (params.has('ml')) el<HTMLInputElement>('calc-mlevel').value = params.get('ml') ?? '0';
  if (params.has('str')) el<HTMLInputElement>('calc-str').value = params.get('str') ?? '500';
  if (params.has('agi')) el<HTMLInputElement>('calc-agi').value = params.get('agi') ?? '300';
  if (params.has('vit')) el<HTMLInputElement>('calc-vit').value = params.get('vit') ?? '150';
  if (params.has('ene')) el<HTMLInputElement>('calc-ene').value = params.get('ene') ?? '100';
  if (params.has('cmd')) el<HTMLInputElement>('calc-cmd').value = params.get('cmd') ?? '0';

  applyStaticLabels();

  try {
    const res = await fetch('/website/build-calculator');
    if (res.ok) {
      meta = (await res.json()) as CalcMetadata;
      const clsSel = el<HTMLSelectElement>('calc-class');
      clsSel.replaceChildren();
      for (const c of meta.classes) {
        const opt = document.createElement('option');
        opt.value = String(c.number);
        opt.textContent = c.name;
        clsSel.append(opt);
      }
      if (params.has('c')) clsSel.value = params.get('c') ?? clsSel.value;
      clsSel.addEventListener('change', () => {
        renderSlots();
        void runCalculation();
      });
      renderSlots();
      await runCalculation();
    }
  } catch {
    el('calc-status').textContent = t('Server metadata unavailable.', 'Chưa tải được dữ liệu máy chủ.');
  }
}

void init();
