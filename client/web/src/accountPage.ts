import { communityLinks, gameName, loadGameName } from './gameName.js';
import { accountSections, onAccountSectionsChanged, registerAccountSection, type AccountSectionContext } from './accountSections.js';
import { accountPageTexts, type AccountTextKey } from './accountPageTexts.js';
import { mountAccountPasswordForm } from './accountPassword.js';
import { AccountIntegrations } from './accountIntegrations.js';
import { AccountMissions } from './accountMissions.js';
import { AccountEmail } from './accountEmail.js';
import { AccountVip } from './accountVip.js';

interface Credentials { loginName: string; password: string }
interface AccountSnapshot {
  loginName: string;
  accountOnline: boolean;
  balances: { wCoinC: number; wCoinP: number; goblinPoints: number };
  vipLevel: number;
  characters: readonly { name: string; className: string; classNameVi?: string; level: number; resets: number; map: number | null; online?: boolean | null }[];
  history: readonly { id: string; coinType: string; amount: number; reason: string | null; purchase?: { nameEn: string; nameVi: string; giftRecipient: string | null } | null; balanceChange?: { action: string; otherCharacter: string | null } | null; vipPurchase?: { nameEn: string; nameVi: string; days: number } | null; vipDaily?: { nameEn: string; nameVi: string } | null; vipRemoved?: boolean; createdAt: string; pending: boolean }[];
  page: number;
  hasMore: boolean;
}

let language: 'en' | 'vi' = navigator.language.toLowerCase().startsWith('vi') ? 'vi' : 'en';
try { const saved = localStorage.getItem('hnmo.language'); if (saved === 'en' || saved === 'vi') language = saved; } catch { /* Storage is optional. */ }
let credentials: Credentials | null = null;
let snapshot: AccountSnapshot | null = null;
let active = 'overview';
let generation = 0;
let loading = false;
const loadedLegacy = new Set<string>();
const integrations = new AccountIntegrations({ getCredentials: () => credentials, getLanguage: () => language }, logOut);
const missions = new AccountMissions({ language: () => language, request, error: showError });
const email = new AccountEmail({ text: t, language: () => language, request, message: showMessage, error: showError });
const vip = new AccountVip({ language: () => language, accountKey: () => snapshot?.loginName ?? '', request, changed: () => { void refresh(snapshot?.page ?? 0); }, error: showError });

function element<T extends HTMLElement>(id: string): T {
  const found = document.getElementById(id);
  if (!found) throw new Error(`Missing account element: ${id}`);
  return found as T;
}

function t(key: AccountTextKey): string { return accountPageTexts[key][language === 'vi' ? 1 : 0]; }

function node<K extends keyof HTMLElementTagNameMap>(tag: K, text = ''): HTMLElementTagNameMap[K] {
  const result = document.createElement(tag);
  result.textContent = text;
  return result;
}

function button(label: AccountTextKey, hint: AccountTextKey, action: () => void): HTMLButtonElement {
  const result = node('button', t(label));
  result.type = 'button';
  result.title = t(hint);
  result.addEventListener('click', action);
  return result;
}

function link(label: AccountTextKey, hint: AccountTextKey, url: string): HTMLAnchorElement {
  const result = node('a', t(label));
  result.href = url;
  result.title = t(hint);
  return result;
}

function panel(id: string, label: AccountTextKey, note?: AccountTextKey): HTMLElement {
  const result = node('section');
  result.id = `account-${id}`;
  result.className = 'panel account-panel';
  result.dataset.accountPanel = id;
  result.setAttribute('aria-labelledby', `account-title-${id}`);
  const title = node('h2', t(label));
  title.id = `account-title-${id}`;
  title.dataset.at = label;
  result.append(title);
  if (note) { const paragraph = node('p', t(note)); paragraph.dataset.at = note; result.append(paragraph); }
  element('accountPanels').append(result);
  registerAccountSection({ id, element: result, label: { en: accountPageTexts[label][0], vi: accountPageTexts[label][1] } });
  return result;
}

class AccountRequestError extends Error {
  constructor(readonly key: AccountTextKey) { super(key); }
}

async function request<T>(path: string, body: Readonly<Record<string, unknown>> = {}): Promise<T> {
  if (!credentials) throw new AccountRequestError('loginError');
  if (!path.startsWith('/portal/') || path.includes('..') || path.includes('\\')) throw new Error('Account features must use a same-origin portal endpoint.');
  const response = await fetch(path, {
    method: 'POST', cache: 'no-store', credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...body, ...credentials }),
  });
  if (!response.ok) {
    if (path.startsWith('/portal/account/email/')) {
      const problem: unknown = await response.json().catch(() => null);
      const messages: Readonly<Record<string, AccountTextKey>> = { online: 'emailOnline', email: 'emailInvalid', code: 'emailCodeInvalid', 'mail-off': 'emailOff', 'mail-wait': 'emailWait', 'mail-send': 'emailSendFailed' };
      if (problem && typeof problem === 'object' && 'error' in problem && typeof problem.error === 'string' && messages[problem.error]) throw new AccountRequestError(messages[problem.error]);
    }
    if (path === '/portal/account/password' && response.status === 409) throw new AccountRequestError('passwordOnline');
    if (path === '/portal/account/password' && response.status === 400) throw new AccountRequestError('passwordInvalid');
    throw new AccountRequestError(response.status === 401 ? 'loginError' : response.status === 429 ? 'rateError' : response.status === 503 ? 'setupError' : 'serverError');
  }
  return await response.json() as T;
}

function showError(error: unknown): void {
  showMessage(error instanceof AccountRequestError ? error.key : 'serverError');
}

function showMessage(key: AccountTextKey): void {
  const message = element('accountMessage');
  message.dataset.at = key;
  message.textContent = t(key);
}

function table(headers: readonly string[], rows: readonly (readonly string[])[]): HTMLElement {
  const scroll = node('div'); scroll.className = 'account-scroll';
  const grid = node('table');
  const head = node('thead'); const heading = node('tr');
  for (const label of headers) { const cell = node('th', label); cell.scope = 'col'; heading.append(cell); }
  head.append(heading); grid.append(head);
  const body = node('tbody');
  for (const values of rows) { const row = node('tr'); for (const value of values) row.append(node('td', value)); body.append(row); }
  grid.append(body); scroll.append(grid); return scroll;
}

function sydney(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? t('unknown') : new Intl.DateTimeFormat(language === 'vi' ? 'vi-VN' : 'en-AU', {
    timeZone: 'Australia/Sydney', dateStyle: 'medium', timeStyle: 'short', hourCycle: 'h23',
  }).format(date);
}

function balances(): HTMLElement {
  const result = node('dl'); result.className = 'account-balances';
  for (const [label, value] of [['WCoin (C)', snapshot?.balances.wCoinC], ['WCoin (P)', snapshot?.balances.wCoinP], ['Goblin Points', snapshot?.balances.goblinPoints]] as const) {
    const coin = node('div'); coin.append(node('dt', label), node('dd', String(value ?? 0))); result.append(coin);
  }
  return result;
}

function renderOverview(): void {
  if (!snapshot) return;
  const root = element('account-overview');
  root.replaceChildren(node('h2', `${t('overview')} — ${snapshot.loginName}`), node('p', t('saved')),
    node('p', t(snapshot.accountOnline ? 'accountOnline' : 'accountOffline')), balances());
  const quick = node('div'); quick.className = 'account-actions';
  // The existing ticket feature owns #play and #playNowButton; this link leads to it.
  const play = link('play', 'playHint', '#play'); play.id = 'accountPlaySlot';
  quick.append(play, link('topup', 'topupHint', '/topup.html'));
  root.append(quick, node('h3', t('characters')));
  root.append(snapshot.characters.length ? table([t('character'), t('className'), t('level'), t('resetCount'), t('map'), t('status')],
    snapshot.characters.map(c => [c.name, (language === 'vi' ? c.classNameVi || c.className : c.className) || t('unknown'), String(c.level), String(c.resets), c.map === null ? t('unknown') : String(c.map), t(c.online === true ? 'online' : c.online === false ? 'offline' : 'presenceUnknown')])) : node('p', t('noCharacters')));
  root.append(node('p', t('lastSeenNote')), node('p', `VIP: ${snapshot.vipLevel}`));
  root.append(integrations.play);
}

function renderCoins(): void {
  if (!snapshot) return;
  const root = element('account-coins');
  root.replaceChildren(node('h2', t('coins')), balances(), link('topup', 'topupHint', '/topup.html'), node('h3', t('history')), node('p', t('historyNote')));
  root.append(snapshot.history.length ? table([t('date'), t('coins'), t('amount'), t('reason'), t('status')], snapshot.history.map(g =>
    [sydney(g.createdAt), g.coinType, String(g.amount), historyReason(g), t(g.pending ? 'pending' : 'applied')])) : node('p', t('noHistory')));
  const actions = node('div'); actions.className = 'account-actions';
  const previous = button('previous', 'previousHint', () => { void refresh((snapshot?.page ?? 0) - 1); });
  const next = button('next', 'nextHint', () => { void refresh((snapshot?.page ?? 0) + 1); });
  previous.disabled = loading || snapshot.page === 0; next.disabled = loading || !snapshot.hasMore;
  actions.append(previous, node('span', String(snapshot.page + 1)), next); root.append(actions, node('h3', t('milestones')), integrations.milestones);
}

function historyReason(entry: AccountSnapshot['history'][number]): string {
  if (entry.vipRemoved) return t('vipRemovalReason');
  if (entry.vipPurchase) return t('vipPurchaseReason').replace('{0}', language === 'vi' ? entry.vipPurchase.nameVi : entry.vipPurchase.nameEn).replace('{1}', String(entry.vipPurchase.days));
  if (entry.vipDaily) return t('vipDailyReason').replace('{0}', language === 'vi' ? entry.vipDaily.nameVi : entry.vipDaily.nameEn);
  if (entry.balanceChange) {
    const keys: Readonly<Record<string, AccountTextKey>> = { ExtraVault: 'coinExtraVault', MarketPurchase: 'coinMarketPurchase', MarketSale: 'coinMarketSale', PlayerStorePurchase: 'coinPlayerStorePurchase', PlayerStoreSale: 'coinPlayerStoreSale', Reset: 'coinReset', Relife: 'coinRelife', ClearStats: 'coinClearStats', ClearMaster: 'coinClearMaster', PlayTime: 'coinPlayTime' };
    const key = keys[entry.balanceChange.action];
    if (key) return t(key).replace('{0}', entry.balanceChange.otherCharacter || t('unknown'));
  }
  if (!entry.purchase) return entry.reason || '—';
  const name = (language === 'vi' ? entry.purchase.nameVi || entry.purchase.nameEn : entry.purchase.nameEn) || t('unknown');
  return entry.purchase.giftRecipient
    ? t('giftPurchaseReason').replace('{0}', entry.purchase.giftRecipient).replace('{1}', name)
    : t('purchaseReason').replace('{0}', name);
}

function renderCommunity(): void {
  const root = element('account-community');
  root.replaceChildren(node('h2', t('community')), node('p', t('communityNote')));
  const entries = Object.entries(communityLinks());
  if (!entries.length) root.append(node('p', t('noLinks')));
  for (const [kind, url] of entries) {
    const anchor = node('a', { discord: 'Discord', zalo: 'Zalo', facebook: 'Facebook' }[kind] ?? kind);
    anchor.href = url; anchor.target = '_blank'; anchor.rel = 'noopener noreferrer'; anchor.title = t('linkHint'); root.append(anchor);
  }
  root.append(missions.referrals, node('h3', t('eventAlerts')), integrations.alerts);
}

function translate(): void {
  element('accountDashboard').lang = language;
  element('accountLoginPanel').lang = language;
  for (const root of [element('accountDashboard'), element('accountLoginPanel')]) {
    for (const entry of root.querySelectorAll<HTMLElement>('[data-at]')) {
      const key = entry.dataset.at as AccountTextKey;
      if (key in accountPageTexts) entry.textContent = t(key);
    }
    for (const entry of root.querySelectorAll<HTMLElement>('[data-at-title]')) {
      const key = entry.dataset.atTitle as AccountTextKey;
      if (key in accountPageTexts) entry.title = t(key);
    }
  }
  for (const item of document.querySelectorAll<HTMLElement>('[data-account-language]')) item.setAttribute('aria-pressed', String(item.dataset.accountLanguage === language));
  for (const prefix of ['gift', 'reset', 'safety', 'play']) document.querySelector<HTMLButtonElement>(`[data-${prefix}-lang="${language}"]`)?.click();
  integrations.refreshLanguage();
  email.refreshLanguage(snapshot !== null);
  vip.refreshLanguage(snapshot !== null);
  missions.updateCharacters(snapshot?.characters.map(c => c.name) ?? []);
  document.title = `${gameName()} — ${t('account')}`;
  element<HTMLSelectElement>('accountSectionSelect').setAttribute('aria-label', t('menu'));
  renderOverview(); renderCoins(); renderCommunity(); renderNavigation();
  element('accountPasswordChange').hidden = !snapshot;
}

function renderNavigation(): void {
  const nav = element('accountNav'); const select = element<HTMLSelectElement>('accountSectionSelect');
  nav.hidden = !snapshot; nav.replaceChildren(); select.replaceChildren();
  const sections = accountSections();
  if (!sections.some(s => s.id === active)) active = sections[0]?.id ?? 'overview';
  for (const section of sections) {
    if (section.element.parentElement !== element('accountPanels')) element('accountPanels').append(section.element);
    section.element.dataset.accountPanel = section.id;
    const item = node('button', section.label[language]); item.type = 'button'; item.title = t('sectionHint');
    item.setAttribute('aria-controls', section.element.id);
    item.setAttribute('aria-current', String(section.id === active));
    item.addEventListener('click', () => { void openSection(section.id); }); nav.append(item);
    const option = node('option', section.label[language]); option.value = section.id; select.append(option);
    section.element.hidden = snapshot ? section.id !== active : !['rewards', 'resets', 'security'].includes(section.id);
  }
  select.value = active;
}

function loadLegacy(id: string): void {
  const prefix = { overview: 'play', rewards: 'gift', resets: 'resets', security: 'safety' }[id];
  if (!prefix || !credentials || !snapshot || loadedLegacy.has(prefix)) return;
  const form = element<HTMLFormElement>(`${prefix}Login`);
  if (form.hidden) return;
  element<HTMLInputElement>(`${prefix}Account`).value = credentials.loginName;
  element<HTMLInputElement>(`${prefix}Password`).value = credentials.password;
  form.requestSubmit();
  loadedLegacy.add(prefix);
}

async function openSection(id: string): Promise<void> {
  active = id; renderNavigation(); loadLegacy(id);
  if (credentials && snapshot) {
    try { await integrations.refresh(id); } catch (error) { showError(error); }
    await missions.open(id);
    if (id === 'security') { try { await email.open(); } catch (error) { showError(error); } }
    if (id === 'vip') await vip.open();
  }
  const section = accountSections().find(s => s.id === id);
  if (credentials && snapshot && section?.open) {
    const context: AccountSectionContext = { language, request };
    try { await section.open(context); } catch (error) { showError(error); }
  }
}

async function refresh(page = 0): Promise<boolean> {
  const current = ++generation; loading = true; renderCoins();
  try {
    const data = await request<AccountSnapshot>('/portal/account/overview', { page });
    if (current !== generation) return false;
    snapshot = data; element('accountMessage').textContent = ''; delete element('accountMessage').dataset.at;
    translate(); return true;
  } catch (error) { if (current === generation) showError(error); return false; }
  finally { if (current === generation) { loading = false; renderCoins(); } }
}

function mount(): void {
  panel('overview', 'overview').append(integrations.play); panel('coins', 'coins');
  panel('vip', 'vip').append(vip.root);
  panel('rewards', 'rewards', 'rewardsNote').append(missions.rewards, element('gift'));
  panel('resets', 'resets').append(element('resets'));
  const security = panel('security', 'security', 'securityNote'); security.append(element('safety'));
  mountAccountPasswordForm(security, { text: t, request, message: showMessage, error: showError, changed: passwordChanged });
  security.append(email.root);
  panel('community', 'community');
  onAccountSectionsChanged(() => { renderNavigation(); });
  element<HTMLSelectElement>('accountSectionSelect').addEventListener('change', event => { void openSection((event.target as HTMLSelectElement).value); });
  element('accountHelpButton').addEventListener('click', () => {
    const help = element('accountHelp'); help.hidden = !help.hidden;
    element('accountHelpButton').setAttribute('aria-expanded', String(!help.hidden));
  });
  element('accountRefresh').addEventListener('click', () => { void refresh(snapshot?.page ?? 0); });
  element('accountLogout').addEventListener('click', logOut);
  for (const entry of document.querySelectorAll<HTMLElement>('[data-account-language]')) entry.addEventListener('click', () => {
    language = entry.dataset.accountLanguage === 'vi' ? 'vi' : 'en';
    try { localStorage.setItem('hnmo.language', language); } catch { /* Storage is optional. */ }
    translate();
    if (credentials) void openSection(active);
  });
  element<HTMLFormElement>('accountSignIn').addEventListener('submit', async event => {
    event.preventDefault(); if (loading) return;
    credentials = { loginName: element<HTMLInputElement>('accountUser').value, password: element<HTMLInputElement>('accountSecret').value };
    element<HTMLButtonElement>('accountSignInButton').disabled = true;
    const success = await refresh();
    element<HTMLButtonElement>('accountSignInButton').disabled = false;
    if (!success) { credentials = null; snapshot = null; renderNavigation(); return; }
    element<HTMLFormElement>('accountSignIn').reset();
    element('accountLoginPanel').hidden = true; element('accountToolbar').hidden = false;
    element('accountDashboard').classList.add('authenticated');
    element('registerForm').hidden = true; element('coinsForm').hidden = true; element('setupPanel').hidden = true;
    const requested = location.hash.slice(1);
    void openSection(accountSections().some(s => s.id === requested) ? requested : 'overview');
  });
  translate();
  try {
    const notice = sessionStorage.getItem('hnmo.account.notice');
    sessionStorage.removeItem('hnmo.account.notice');
    if (notice === 'passwordChanged') showMessage('passwordChanged');
  } catch { /* The sign-in form still works when storage is unavailable. */ }
}

function logOut(): void {
  vip.reset();
  credentials = null; snapshot = null; generation++; element<HTMLFormElement>('accountSignIn').reset();
  // Reload clears credentials retained by the existing gift/reset/safety modules as well.
  location.reload();
}

function passwordChanged(): void {
  // Only the translated notice key survives the reload, never credentials or account data.
  try { sessionStorage.setItem('hnmo.account.notice', 'passwordChanged'); } catch { /* Storage is optional. */ }
  logOut();
}

mount();
void loadGameName().then(() => translate());
