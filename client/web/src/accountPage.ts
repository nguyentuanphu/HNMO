import { enterGame, hasWebsiteSession, installSessionTransport, signInWebsite, signOutWebsite } from './websiteSession.js';
import { communityLinks, gameName, loadGameName } from './gameName.js';
import { accountSections, onAccountSectionsChanged, registerAccountSection, type AccountSectionContext } from './accountSections.js';
import { accountPageTexts, type AccountTextKey } from './accountPageTexts.js';
import { mountAccountPasswordForm } from './accountPassword.js';
import { AccountIntegrations } from './accountIntegrations.js';
import { AccountMissions } from './accountMissions.js';
import { AccountEmail } from './accountEmail.js';
import { AccountVip } from './accountVip.js';
import { AccountGiftBox } from './accountGiftBox.js';
import { appendStoredItemHistory } from './accountStoredItemHistory.js';
import { AccountMobileNavigation } from './accountMobileNavigation.js';
import { AccountTrackerPrivacy } from './accountTrackerPrivacy.js';

interface Credentials { loginName: string; password: string }
interface StoredItem {
  character: string | null;
  slot: number;
  level: number;
  name: string;
  nameVi?: string;
  serial: string | null;
}
interface StoredItems {
  truncated: boolean;
  items: readonly StoredItem[];
}
interface AccountSnapshot {
  loginName: string;
  accountOnline: boolean;
  balances: { wCoinC: number; wCoinP: number; goblinPoints: number };
  vipLevel: number;
  storedItems?: { inventory: StoredItems; vault: StoredItems };
  characters: readonly { name: string; className: string; classNameVi?: string; level: number; resets: number; map: number | null; online?: boolean | null }[];
  history: readonly { id: string; coinType: string; amount: number; reason: string | null; purchase?: { nameEn: string; nameVi: string; giftRecipient: string | null } | null; balanceChange?: { action: string; otherCharacter: string | null } | null; vipPurchase?: { nameEn: string; nameVi: string; days: number } | null; vipDaily?: { nameEn: string; nameVi: string } | null; vipRemoved?: boolean; giftBox?: { titleEn: string; titleVi: string } | null; createdAt: string; pending: boolean }[];
  page: number;
  hasMore: boolean;
}

let language: 'en' | 'vi' = navigator.language.toLowerCase().startsWith('vi') ? 'vi' : 'en';
try { const saved = localStorage.getItem('hnmo.language'); if (saved === 'en' || saved === 'vi') language = saved; } catch { /* Storage is optional. */ }
// Non-secret adapter identity for existing widgets; the bridge cookie supplies actual credentials.
let credentials: Credentials | null = null;
installSessionTransport();
let snapshot: AccountSnapshot | null = null;
let active = 'overview';
let generation = 0;
let loading = false;
let authEpoch = 0;
let signInNotice: AccountTextKey | null = null;
const loadedLegacy = new Set<string>();
const integrations = new AccountIntegrations({ getCredentials: () => credentials, getLanguage: () => language }, logOut);
const missions = new AccountMissions({ language: () => language, request, error: showError });
const email = new AccountEmail({ text: t, language: () => language, request, message: showMessage, error: showError });
const vip = new AccountVip({ language: () => language, accountKey: () => snapshot?.loginName ?? '', request, changed: () => { void refresh(snapshot?.page ?? 0); }, error: showError });
const giftBox = new AccountGiftBox({ language: () => language, request, changed: () => { void refresh(snapshot?.page ?? 0); }, error: () => showMessage('serverError') });
const trackerPrivacy = new AccountTrackerPrivacy(request, () => language);
const mobileNavigation = new AccountMobileNavigation(() => language, id => { void openSection(id); });

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
      const messages: Readonly<Record<string, AccountTextKey>> = { online: 'emailOnline', email: 'emailInvalid', 'email-taken': 'emailTaken', code: 'emailCodeInvalid', 'mail-off': 'emailOff', 'mail-wait': 'emailWait', 'mail-send': 'emailSendFailed' };
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
  const header = node('div');
  header.className = 'account-profile-banner';
  const realm = node('p', gameName());
  realm.dataset.gameName = '';
  const title = node('h2', `${t('overview')} — ${snapshot.loginName}`);
  title.id = 'account-title-overview';
  header.append(realm, title, node('p', t(snapshot.accountOnline ? 'accountOnline' : 'accountOffline')));
  root.replaceChildren(header, node('p', t('saved')), balances());
  const quick = node('div'); quick.className = 'account-actions';
  // The existing ticket feature owns #play and #playNowButton; this link leads to it.
  const play = link('play', 'playHint', '/account.html?play=1'); play.dataset.enterGame = ''; play.id = 'accountPlaySlot';
  quick.append(play, link('topup', 'topupHint', '/topup.html'), link('itemHistory', 'itemHistoryHint', '/tracker.html'));
  const characters = node('h3', t('characters'));
  characters.id = 'accountCharactersHeading';
  characters.tabIndex = -1;
  root.append(quick, characters);
  root.append(snapshot.characters.length ? table([t('character'), t('className'), t('level'), t('resetCount'), t('map'), t('status')],
    snapshot.characters.map(c => [c.name, (language === 'vi' ? c.classNameVi || c.className : c.className) || t('unknown'), String(c.level), String(c.resets), c.map === null ? t('unknown') : String(c.map), t(c.online === true ? 'online' : c.online === false ? 'offline' : 'presenceUnknown')])) : node('p', t('noCharacters')));
  root.append(node('p', t('lastSeenNote')), node('p', `VIP: ${snapshot.vipLevel}`));
  root.append(integrations.play);
}

function renderStoredItems(): void {
  const root = element('account-items');
  const title = node('h2', t('storedItems'));
  title.id = 'account-title-items';
  root.replaceChildren(title);
  if (!snapshot) return;
  const saved = snapshot.storedItems;
  if (!saved) {
    root.append(node('p', t('storedItemsUnavailable')));
    return;
  }

  const search = node('input');
  search.type = 'search';
  search.autocomplete = 'off';
  const label = node('label', t('storedItemsSearch'));
  label.append(search);
  const details = node('dialog');
  details.className = 'account-item-details';
  details.setAttribute('aria-labelledby', 'account-item-details-title');
  const results = node('div');
  const inventory = node('section');
  const vault = node('section');
  results.append(inventory, vault);
  const showMatches = () => {
    const query = search.value.trim().toLowerCase();
    renderStorage(inventory, 'storedInventory', saved.inventory, query, details);
    renderStorage(vault, 'storedVault', saved.vault, query, details);
  };
  search.addEventListener('input', showMatches);
  showMatches();
  root.append(node('p', t('storedItemsNote')), label, results, details);
}

function renderStorage(root: HTMLElement, label: AccountTextKey, storage: StoredItems, query: string, details: HTMLDialogElement): void {
  const list = node('ul');
  for (const item of storage.items) {
    const terms = [item.name, item.nameVi, item.character, item.serial];
    if (query && !terms.some(value => value?.toLowerCase().includes(query))) continue;
    const display = language === 'vi' ? item.nameVi || item.name : item.name;
    const row = node('li', item.character ? `${item.character} · ` : '');
    const name = `${display || t('unknown')}${item.level > 0 ? ` +${item.level}` : ''}`;
    const serial = validItemSerial(item.serial);
    row.append(serial ? itemHistoryLink(name, serial) : document.createTextNode(name));
    const open = button('storedItemDetails', 'storedItemDetailsHint', () => showStoredItem(details, label, name, item, serial));
    open.setAttribute('aria-label', `${t('storedItemDetails')} · ${name}`);
    row.append(' ', open);
    list.append(row);
  }
  const emptyLabel = query && storage.items.length ? 'noMatchingStoredItems' : 'noStoredItems';
  root.replaceChildren(node('h3', t(label)), list.childElementCount ? list : node('p', t(emptyLabel)));
  if (storage.truncated) root.append(node('p', t('storedItemsTruncated')));
}

function validItemSerial(value: string | null): string | null {
  return value && /^[a-f0-9]{32}$/i.test(value) && value !== '0'.repeat(32) ? value : null;
}

function itemHistoryLink(name: string, serial: string): HTMLAnchorElement {
  const history = node('a', name);
  history.href = `/tracker.html?serial=${encodeURIComponent(serial)}`;
  history.title = t('itemHistoryHint');
  return history;
}

function showStoredItem(dialog: HTMLDialogElement, location: AccountTextKey, name: string, item: StoredItem, serial: string | null): void {
  const title = node('h3', name);
  title.id = 'account-item-details-title';
  const facts = node('dl');
  const fields = [[t('storedItemLocation'), t(location)], [t('storedItemSlot'), String(item.slot)]];
  if (item.character) fields.push([t('character'), item.character]);
  for (const [label, value] of fields) facts.append(node('dt', label), node('dd', value));
  const close = button('storedItemClose', 'storedItemCloseHint', () => dialog.close());
  close.autofocus = true;
  dialog.replaceChildren(title, facts);
  if (serial) {
    const value = node('dd');
    value.append(node('code', serial));
    facts.append(node('dt', t('storedItemSerial')), value);
    dialog.append(node('p', t('storedItemHistoryLimit')), itemHistoryLink(t('itemHistory'), serial));
    appendStoredItemHistory(dialog, serial, language);
  } else {
    dialog.append(node('p', t('storedItemNoSerial')));
  }
  const actions = node('div');
  actions.className = 'account-actions';
  actions.append(close);
  dialog.append(actions);
  dialog.showModal();
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
  if (entry.giftBox) return t('giftBoxHistoryReason').replace('{0}', language === 'vi' ? entry.giftBox.titleVi : entry.giftBox.titleEn);
  if (entry.vipRemoved) return t('vipRemovalReason');
  if (entry.vipPurchase) return t('vipPurchaseReason').replace('{0}', language === 'vi' ? entry.vipPurchase.nameVi : entry.vipPurchase.nameEn).replace('{1}', String(entry.vipPurchase.days));
  if (entry.vipDaily) return t('vipDailyReason').replace('{0}', language === 'vi' ? entry.vipDaily.nameVi : entry.vipDaily.nameEn);
  if (entry.balanceChange) {
    const keys: Readonly<Record<string, AccountTextKey>> = { ExtraVault: 'coinExtraVault', MarketPurchase: 'coinMarketPurchase', MarketSale: 'coinMarketSale', PlayerStorePurchase: 'coinPlayerStorePurchase', PlayerStoreSale: 'coinPlayerStoreSale', Reset: 'coinReset', Relife: 'coinRelife', ClearStats: 'coinClearStats', ClearMaster: 'coinClearMaster', PlayTime: 'coinPlayTime', StackUnlock: 'coinStackUnlock', MarketAccess: 'coinMarketAccess' };
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
  const statusKey = element('accountMessage').dataset.at as AccountTextKey | undefined;
  if (statusKey && statusKey in accountPageTexts) element('accountMessage').textContent = t(statusKey);
  element('accountRetrySession').textContent = t('refresh'); element('accountRetrySession').title = t('refreshHint');
  element('accountRetryLogout').textContent = t('logout'); element('accountRetryLogout').title = t('logoutHint');
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
  trackerPrivacy.refreshLanguage(snapshot !== null);
  vip.refreshLanguage(snapshot !== null);
  giftBox.refreshLanguage(snapshot !== null && active === 'rewards');
  missions.updateCharacters(snapshot?.characters.map(c => c.name) ?? []);
  document.title = `${gameName()} — ${t('account')}`;
  renderOverview(); renderCoins(); renderCommunity(); renderStoredItems(); renderNavigation();
  element('accountPasswordChange').hidden = !snapshot;
}

function renderNavigation(): void {
  const nav = element('accountNav');
  nav.hidden = !snapshot; nav.replaceChildren();
  const sections = accountSections();
  if (!sections.some(s => s.id === active)) active = sections[0]?.id ?? 'overview';
  for (const section of sections) {
    if (section.element.parentElement !== element('accountPanels')) element('accountPanels').append(section.element);
    section.element.dataset.accountPanel = section.id;
    const item = node('button', section.label[language]); item.type = 'button'; item.title = t('sectionHint');
    item.dataset.accountSection = section.id;
    item.setAttribute('aria-controls', section.element.id);
    item.setAttribute('aria-current', String(section.id === active));
    item.addEventListener('click', () => { void openSection(section.id); }); nav.append(item);
    section.element.hidden = !snapshot || section.id !== active;
  }
  mobileNavigation.update(sections, active, snapshot !== null);
}

function loadLegacy(id: string): void {
  if (id === 'overview') return;
  const prefix = { overview: 'play', rewards: 'gift', resets: 'resets', security: 'safety' }[id];
  if (!prefix || !credentials || !snapshot || loadedLegacy.has(prefix)) return;
  const form = element<HTMLFormElement>(`${prefix}Login`);
  if (form.hidden) return;
  element<HTMLInputElement>(`${prefix}Account`).value = credentials.loginName;
  element<HTMLInputElement>(`${prefix}Password`).value = credentials.password;
  form.requestSubmit(); form.hidden = true;
  loadedLegacy.add(prefix);
}

async function openSection(id: string): Promise<void> {
  active = id; renderNavigation(); loadLegacy(id);
  if (credentials && snapshot) {
    try { await integrations.refresh(id); } catch (error) { showError(error); }
    await missions.open(id);
    if (id === 'security') {
      void trackerPrivacy.open();
      try { await email.open(); } catch (error) { showError(error); }
    }
    if (id === 'vip') await vip.open();
    if (id === 'rewards') await giftBox.open();
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
    snapshot = data; signInNotice = null; element('accountMessage').textContent = ''; delete element('accountMessage').dataset.at;
    translate(); return true;
  } catch (error) { if (current === generation) showError(error); return false; }
  finally { if (current === generation) { loading = false; renderCoins(); } }
}

async function showSignedIn(): Promise<void> {
  element('accountLoginPanel').hidden = true; element('accountRegistration').hidden = true;
  for (const id of ['play', 'gift', 'resets', 'safety']) element(id).hidden = false;
  element('accountDashboard').hidden = false; element('accountPanels').hidden = false; element('accountToolbar').hidden = false;
  element('accountDashboard').classList.add('authenticated');
  element('registerForm').hidden = true; element('setupPanel').hidden = true;
  element('playLogin').hidden = true; element('playSignedIn').hidden = false; element('playSignedName').textContent = snapshot?.loginName ?? '';
  if (new URLSearchParams(location.search).get('play') === '1') { await enterGame().catch(() => showMessage('serverError')); return; }
  const hash = location.hash.slice(1);
  const requested = ({ gift: 'rewards', safety: 'security', play: 'overview' } as Readonly<Record<string, string>>)[hash] ?? hash;
  await openSection(accountSections().some(s => s.id === requested) ? requested : 'overview');
}

async function restoreSession(): Promise<void> {
  const epoch = authEpoch;
  element('accountLoginPanel').hidden = true; element('accountRetrySession').hidden = true; element('accountRetryLogout').hidden = true;
  showMessage(signInNotice ?? 'checkingSession');
  try {
    const authenticated = await hasWebsiteSession();
    if (epoch !== authEpoch) return;
    if (!authenticated) {
      element('accountLoginPanel').hidden = false;
      if (signInNotice) showMessage(signInNotice); else { element('accountMessage').textContent = ''; delete element('accountMessage').dataset.at; }
      return;
    }
    credentials = { loginName: '', password: 'session' };
    if (await refresh()) { credentials.loginName = snapshot!.loginName; await showSignedIn(); }
    else { credentials = null; element('accountRetrySession').hidden = !element('accountLoginPanel').hidden; }
  } catch {
    if (epoch !== authEpoch) return;
    showMessage('serverError'); element('accountRetrySession').hidden = false;
  }
}

function mount(): void {
  panel('overview', 'overview').append(integrations.play); panel('coins', 'coins');
  panel('vip', 'vip').append(vip.root);
  panel('rewards', 'rewards', 'rewardsNote').append(giftBox.root, missions.rewards, element('gift'));
  panel('resets', 'resets').append(element('resets'));
  const security = panel('security', 'security', 'securityNote'); security.append(element('safety'));
  mountAccountPasswordForm(security, { text: t, request, message: showMessage, error: showError, changed: passwordChanged });
  security.append(email.root, trackerPrivacy.root);
  panel('community', 'community');
  panel('items', 'storedItems');
  onAccountSectionsChanged(() => { renderNavigation(); });
  element('accountHelpButton').addEventListener('click', () => {
    const help = element('accountHelp'); help.hidden = !help.hidden;
    element('accountHelpButton').setAttribute('aria-expanded', String(!help.hidden));
  });
  element('accountRefresh').addEventListener('click', () => { void refresh(snapshot?.page ?? 0); });
  element('accountLogout').addEventListener('click', logOut);
  element('accountRetryLogout').addEventListener('click', logOut);
  element('accountRetrySession').addEventListener('click', () => { void restoreSession(); });
  element('playNowButton').addEventListener('click', event => { event.stopImmediatePropagation(); void enterGame().catch(() => showMessage('serverError')); }, { capture: true });
  for (const entry of document.querySelectorAll<HTMLElement>('[data-account-language]')) entry.addEventListener('click', () => {
    language = entry.dataset.accountLanguage === 'vi' ? 'vi' : 'en';
    try { localStorage.setItem('hnmo.language', language); } catch { /* Storage is optional. */ }
    translate();
    if (credentials) void openSection(active);
  });
  element<HTMLFormElement>('accountSignIn').addEventListener('submit', async event => {
    event.preventDefault(); if (loading) return;
    const epoch = ++authEpoch;
    element<HTMLButtonElement>('accountSignInButton').disabled = true;
    try {
      const response = await signInWebsite(element<HTMLInputElement>('accountUser').value, element<HTMLInputElement>('accountSecret').value);
      if (epoch !== authEpoch) return;
      if (!response.ok) { showMessage(response.status === 401 ? 'loginError' : response.status === 503 ? 'setupError' : response.status === 429 ? 'rateError' : 'serverError'); return; }
      credentials = { loginName: element<HTMLInputElement>('accountUser').value, password: 'session' };
      element('accountLoginPanel').hidden = true;
      if (!await refresh()) { credentials = null; snapshot = null; renderNavigation(); element('accountRetrySession').hidden = !element('accountLoginPanel').hidden; return; }
      element<HTMLFormElement>('accountSignIn').reset();
      translate(); await showSignedIn();
    } catch { showMessage('serverError'); }
    finally { element<HTMLButtonElement>('accountSignInButton').disabled = false; }

  });
  translate();
  try {
    const notice = sessionStorage.getItem('hnmo.account.notice');
    sessionStorage.removeItem('hnmo.account.notice');
    if (notice === 'passwordChanged') { signInNotice = 'passwordChanged'; showMessage('passwordChanged'); }
  } catch { /* The sign-in form still works when storage is unavailable. */ }
}

function logOut(): void {
  element('accountRetryLogout').hidden = true; element('accountRetrySession').hidden = true;
  showMessage('loggingOut');
  void signOutWebsite().then(() => location.replace('/account.html')).catch(() => {
    showMessage('logoutFailed');
    element('accountRetryLogout').hidden = false;
  });
  authEpoch++;
  element('accountDashboard').hidden = true; element('accountPanels').hidden = true; element('accountLoginPanel').hidden = true;
  vip.reset();
  giftBox.reset();
  credentials = null; snapshot = null; renderStoredItems(); trackerPrivacy.reset(); generation++; element<HTMLFormElement>('accountSignIn').reset();
  // Reload clears credentials retained by the existing gift/reset/safety modules as well.
}


function passwordChanged(): void {
  // Only the translated notice key survives the reload, never credentials or account data.
  try { sessionStorage.setItem('hnmo.account.notice', 'passwordChanged'); } catch { /* Storage is optional. */ }
  logOut();
}

mount();
window.addEventListener('hnmo-account-ready', () => { void restoreSession().catch(() => showMessage('serverError')); });
window.addEventListener('hnmo-session-expired', () => { credentials = null; snapshot = null; renderStoredItems(); trackerPrivacy.reset(); generation++; element('accountDashboard').hidden = true; element('accountPanels').hidden = true; element('accountLoginPanel').hidden = false; showMessage('loginError'); });
window.addEventListener('hnmo-website-session-changed', () => {
  authEpoch++; generation++; snapshot = null; credentials = null; renderStoredItems(); trackerPrivacy.reset();
  element('accountDashboard').hidden = true; element('accountPanels').hidden = true;
  location.reload();
});
window.addEventListener('pageshow', event => {
  if (!event.persisted) return;
  authEpoch++; generation++; snapshot = null; credentials = null; renderStoredItems(); trackerPrivacy.reset(); loadedLegacy.clear();
  element('accountDashboard').hidden = true; element('accountPanels').hidden = true; element('accountLoginPanel').hidden = false;
  void restoreSession().catch(() => showMessage('serverError'));
});
void restoreSession().catch(() => showMessage('serverError'));
void loadGameName().then(() => {
  document.title = `${gameName()} — ${t('account')}`;
  renderCommunity();
});
