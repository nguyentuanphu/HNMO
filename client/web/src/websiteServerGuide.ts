import { parseServerGuide, type ServerGuideFacts, type ServerGuideView, type GuideVip } from './websiteServerGuideModels.js';
import { serverGuideCopy, type ServerGuideText } from './websiteServerGuideCopy.js';

const root = document.getElementById('server-guide');
const selector = document.getElementById('server');
const REFRESH_INTERVAL_MS = 30_000;
let snapshot: ServerGuideView | null = null;
let pending = false;
function vi(): boolean { return document.documentElement.lang === 'vi'; }
function t(key: ServerGuideText, ...values: (string | number)[]): string {
  return values.reduce<string>((copy, value, index) => copy.replaceAll(`{${index}}`, String(value)), serverGuideCopy[key][vi() ? 1 : 0]);
}
function node<K extends keyof HTMLElementTagNameMap>(tag: K, text = ''): HTMLElementTagNameMap[K] { const item = document.createElement(tag); item.textContent = text; return item; }
function card(key: ServerGuideText): HTMLElement { const result = node('article'); result.className = 'server-guide-card'; result.append(node('h3', t(key))); return result; }
function line(parent: HTMLElement, key: ServerGuideText, ...values: (string | number)[]): void { parent.append(node('p', t(key, ...values))); }
function name(value: { nameEn: string; nameVi: string }): string { return (vi() ? value.nameVi : value.nameEn) || t('unknown'); }
function table(labels: ServerGuideText[], rows: (string | number)[][]): HTMLElement {
  const scroll = node('div'); scroll.className = 'server-guide-scroll'; const table = node('table'); const head = node('tr');
  for (const label of labels) { const cell = node('th', t(label)); cell.scope = 'col'; head.append(cell); }
  table.append(head);
  for (const values of rows) { const row = node('tr'); for (const value of values) row.append(node('td', String(value))); table.append(row); }
  scroll.append(table); return scroll;
}
function progression(facts: ServerGuideFacts): HTMLElement {
  const result = card('classes');
  result.append(table(['class', 'creation', 'shield', 'master'], facts.classes.map(item => [name(item), item.requiredLevel, t(item.shieldConfigured ? 'yes' : 'no'), item.maximumMasterTier ?? t('unknown')])));
  return result;
}
function helper(facts: ServerGuideFacts): HTMLElement {
  const result = card('helper'); const value = facts.helper;
  if (!value) { result.append(node('p', t('unknown'))); return result; }
  line(result, 'helperLevels', value.minLevel, value.maxLevel, value.vipMinLevel);
  line(result, 'helperCost', value.costPerStage.join(' / '), value.payIntervalSeconds); return result;
}
function storage(facts: ServerGuideFacts): HTMLElement {
  const result = card('storage');
  line(result, 'bagZen', facts.maximumInventoryMoney); line(result, 'vaultZen', facts.maximumVaultMoney);
  if (facts.inventory) line(result, 'bagExtensions', facts.inventory.maximumExtensions);
  if (facts.vault) {
    line(result, 'vaultPages', facts.vault.defaultVaultCount, facts.vault.vipExtraVaults, facts.vault.maxVaultsPerAccount);
    if (facts.vault.purchaseExtraVaultCost > 0) line(result, 'vaultBuy', facts.vault.purchaseExtraVaultCost, facts.vault.purchaseCoinType);
    else line(result, 'vaultBuyOff');
  }
  return result;
}
function party(facts: ServerGuideFacts): HTMLElement {
  const result = card('party'); line(result, 'partyNote');
  result.append(table(['members', 'normal', 'mixed'], facts.partyExperience.map(item => [item.size, `${item.percent}%`, `${item.differentClassesPercent > 0 ? item.differentClassesPercent : item.percent}%`])));
  return result;
}
function vipLevel(level: GuideVip): HTMLElement {
  const result = node('div'); result.className = 'server-guide-offer'; result.append(node('h4', name(level)));
  if (level.experiencePercent > 0) line(result, 'expBenefit', level.experiencePercent);
  if (level.zenPercent > 0) line(result, 'zenBenefit', level.zenPercent);
  if (level.dailyCoins > 0) line(result, 'dailyBenefit', level.dailyCoins, level.dailyCoinType);
  for (const offer of level.offers) line(result, 'offer', offer.days, offer.price, offer.coinType);
  return result;
}
function vip(facts: ServerGuideFacts): HTMLElement {
  const result = card('vip');
  if (!facts.vip.length) { line(result, 'vipOff'); return result; }
  for (const level of facts.vip) result.append(vipLevel(level));
  const link = node('a', t('manageVip')); link.href = '/account.html#vip'; link.title = t('manageVip'); result.append(link); return result;
}
function catalog(facts: ServerGuideFacts): HTMLElement {
  const result = card('catalog');
  const products = facts.catalog.products;
  if (!products?.length) { line(result, 'catalogOff'); return result; }
  result.append(table(['item', 'quantity', 'price'], products.map(item => [`${name(item)} +${item.itemLevel}`, item.quantity, `${item.price} ${item.coinType}${item.bundle ? ` (${t('bundle')})` : ''}`])));
  line(result, 'bundleNote'); if (facts.catalog.truncated) line(result, 'catalogBound'); return result;
}
function policies(view: ServerGuideView): HTMLElement {
  const result = card('policies');
  for (const [key, lines] of [['clientRules', view.clientRules], ['notForSale', view.notForSale]] as const) {
    result.append(node('h4', t(key))); const content = lines[vi() ? 'vi' : 'en'];
    if (!content.trim()) { line(result, 'unpublished'); continue; }
    const list = node('ul'); for (const text of content.split(/\r?\n/).filter(value => value.trim())) list.append(node('li', text)); result.append(list);
  }
  return result;
}
function render(): void {
  if (!root) return;
  const heading = node('div'); heading.className = 'section-head'; const title = node('div'); title.append(node('h2', t('title')), node('p', t('source')));
  const refresh = node('button', t('refresh')); refresh.type = 'button'; refresh.disabled = pending; refresh.title = t('refreshHint'); refresh.onclick = () => { void update(); };
  heading.append(title, refresh); root.replaceChildren(heading);
  const id = selector instanceof HTMLSelectElement ? selector.value : '';
  const server = id ? snapshot?.servers.find(item => String(item.id) === id) : snapshot?.servers[0];
  if (!server || !snapshot) { const error = node('p', t('unavailable')); error.setAttribute('role', 'status'); root.append(error); return; }
  const grid = node('div'); grid.className = 'server-guide-grid';
  grid.append(progression(server.facts), helper(server.facts), storage(server.facts), party(server.facts), vip(server.facts), catalog(server.facts), policies(snapshot));
  const time = new Intl.DateTimeFormat(vi() ? 'vi-VN' : 'en-AU', { timeZone: 'Australia/Sydney', dateStyle: 'medium', timeStyle: 'short' }).format(new Date(snapshot.updatedAt));
  root.append(grid, node('p', t('updated', time)));
}
async function update(): Promise<void> {
  if (pending || !root) return;
  pending = true; render();
  try {
    const response = await fetch('/website/server-guide', { signal: AbortSignal.timeout(15_000) });
    if (!response.ok) throw new Error('server-guide-unavailable');
    const data: unknown = await response.json(); snapshot = parseServerGuide(data);
  } catch { snapshot = null; }
  finally { pending = false; render(); }
}
if (root) {
  new MutationObserver(render).observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
  if (selector) { selector.addEventListener('change', render); new MutationObserver(render).observe(selector, { childList: true }); }
  document.addEventListener('visibilitychange', () => { if (!document.hidden) void update(); });
  setInterval(() => { if (!document.hidden) void update(); }, REFRESH_INTERVAL_MS);
  void update();
}
