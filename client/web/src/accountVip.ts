import type { VipCoinType, VipOfferView, VipOperation, VipStateView } from './vipModels.js';
import { vipText, type VipTextKey } from './vipTexts.js';

interface Options {
  language(): 'en' | 'vi';
  accountKey(): string;
  request<T>(path: string, body?: Readonly<Record<string, unknown>>): Promise<T>;
  changed(): void;
  error(error: unknown): void;
}

/** Uses the dashboard's authenticated request helper; stores no password or login ticket. */
export class AccountVip {
  readonly root = document.createElement('div');
  private state: VipStateView | null = null;
  private selected: VipOfferView | null = null;
  private operation: VipOperation | null = null;
  private busy = false;
  private authenticated = false;
  private helpOpen = false;
  private generation = 0;

  constructor(private readonly options: Options) { this.root.id = 'accountVip'; }

  async open(): Promise<void> {
    this.authenticated = true;
    await this.read();
  }

  reset(): void {
    this.generation++; this.authenticated = false; this.state = null;
    this.selected = null; this.operation = null; this.busy = false;
    this.root.hidden = true; this.root.replaceChildren();
  }

  refreshLanguage(authenticated: boolean): void {
    this.authenticated = authenticated;
    this.render();
  }

  private t(key: VipTextKey, ...values: readonly (string | number)[]): string { return vipText(this.options.language(), key, ...values); }

  private node<K extends keyof HTMLElementTagNameMap>(tag: K, text = ''): HTMLElementTagNameMap[K] {
    const node = document.createElement(tag); node.textContent = text; return node;
  }

  private button(label: VipTextKey, hint: VipTextKey, action: () => void): HTMLButtonElement {
    const button = this.node('button', this.t(label)); button.type = 'button';
    button.title = this.t(hint); button.disabled = this.busy;
    button.addEventListener('click', () => { if (!button.disabled && (!this.busy || label === 'help')) action(); }); return button;
  }

  private coin(coin: VipCoinType): string { return this.t(coin === 'WCoinC' ? 'wCoinC' : coin === 'WCoinP' ? 'wCoinP' : 'goblin'); }
  private name(value: { nameEn: string; nameVi: string }): string { return this.options.language() === 'vi' ? value.nameVi : value.nameEn; }

  private date(utc: string): string {
    const date = new Date(utc);
    if (!Number.isFinite(date.getTime())) return this.t('unknownDate');
    return new Intl.DateTimeFormat(this.options.language() === 'vi' ? 'vi-VN' : 'en-AU', {
      timeZone: 'Australia/Sydney', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
    }).format(date);
  }

  private remember(operation: VipOperation | null): void {
    this.operation = operation;
    const account = this.options.accountKey().trim().toUpperCase();
    if (!account) return;
    try {
      const key = `hnmo.vip.operation.${account}`;
      if (operation) sessionStorage.setItem(key, JSON.stringify(operation));
      else sessionStorage.removeItem(key);
    } catch { /* Recovery also reads the account-bound pending intent from the server. */ }
  }

  private savedOperation(): VipOperation | null {
    try {
      const account = this.options.accountKey().trim().toUpperCase();
      const raw: unknown = JSON.parse(sessionStorage.getItem(`hnmo.vip.operation.${account}`) ?? 'null');
      if (!raw || typeof raw !== 'object' || !('operationId' in raw) || !('levelId' in raw) || !('days' in raw) || !('offerToken' in raw)) return null;
      if (typeof raw.operationId !== 'string' || !/^[a-f0-9-]{36}$/i.test(raw.operationId) || typeof raw.levelId !== 'number' || !Number.isSafeInteger(raw.levelId)
        || typeof raw.days !== 'number' || !Number.isSafeInteger(raw.days) || typeof raw.offerToken !== 'string' || !/^[a-f0-9]{64}$/i.test(raw.offerToken)) return null;
      return { operationId: raw.operationId, levelId: raw.levelId, days: raw.days, offerToken: raw.offerToken };
    } catch { return null; }
  }

  private async read(): Promise<void> {
    const generation = ++this.generation; this.busy = true; this.render();
    try {
      const saved = this.savedOperation();
      const state = await this.options.request<VipStateView>('/portal/account/vip/state', saved ? { operationId: saved.operationId } : {});
      if (generation !== this.generation || !this.authenticated) return;
      this.state = state; this.selected = null; this.remember(state.pending ?? (state.status === 'Pending' ? saved : null));
    } catch (error: unknown) {
      if (generation === this.generation && this.authenticated) this.options.error(error);
    } finally {
      if (generation === this.generation) { this.busy = false; this.render(); }
    }
  }

  private async purchase(): Promise<void> {
    if (this.busy || !this.operation) return;
    const operation = this.operation; const generation = ++this.generation;
    this.busy = true; this.render();
    try {
      const state = await this.options.request<VipStateView>('/portal/account/vip/purchase', { ...operation });
      if (generation !== this.generation || !this.authenticated) return;
      this.state = state; this.selected = null;
      this.remember(state.pending ?? (state.status === 'Pending' ? operation : null));
      if (state.status === 'Applied') this.options.changed();
    } catch (error: unknown) {
      if (generation === this.generation && this.authenticated) this.options.error(error);
    } finally {
      if (generation === this.generation) { this.busy = false; this.render(); }
    }
  }

  private async claimDaily(): Promise<void> {
    if (this.busy || !this.state?.daily.available) return;
    const generation = ++this.generation; this.busy = true; this.render();
    try {
      const state = await this.options.request<VipStateView>('/portal/account/vip/daily');
      if (generation !== this.generation || !this.authenticated) return;
      this.state = state;
      if (state.status === 'DailyApplied') this.options.changed();
    } catch (error: unknown) {
      if (generation === this.generation && this.authenticated) this.options.error(error);
    } finally {
      if (generation === this.generation) { this.busy = false; this.render(); }
    }
  }

  private status(): string {
    const status = this.state?.status;
    if (status === 'Applied') return this.t('applied');
    if (status === 'DailyApplied') return this.t('dailyApplied');
    if (status === 'Pending' || this.operation) return this.t('pending');
    if (status === 'Online') return this.t('online');
    if (status === 'PriceChanged') return this.t('changed');
    if (status === 'Conflict') return this.t('conflict');
    const keys: Readonly<Record<string, VipTextKey>> = { Disabled: 'off', InsufficientCoins: 'insufficient', DifferentActiveLevel: 'different', Invalid: 'invalid', UnknownLevel: 'invalid', UnknownDuration: 'invalid' };
    if (status === 'Refused') return this.t(keys[this.state?.rule ?? ''] ?? 'invalid');
    return '';
  }

  private benefits(value: { experiencePercent: number; zenPercent: number; dailyCoins: number; dailyCoinType: VipCoinType }): HTMLElement {
    const result = this.node('ul');
    result.append(this.node('li', this.t('experience', value.experiencePercent)), this.node('li', this.t('zen', value.zenPercent)));
    if (value.dailyCoins > 0) result.append(this.node('li', this.t('daily', value.dailyCoins, this.coin(value.dailyCoinType))));
    return result;
  }

  private render(): void {
    this.root.hidden = !this.authenticated;
    if (!this.authenticated) { this.root.replaceChildren(); return; }
    this.root.replaceChildren(this.node('h3', this.t('title')));
    const help = this.button('help', 'helpHint', () => { this.helpOpen = !this.helpOpen; this.render(); });
    help.disabled = false; help.setAttribute('aria-expanded', String(this.helpOpen)); help.setAttribute('aria-controls', 'accountVipHelp');
    const explanation = this.node('p', this.t('helpBody')); explanation.id = 'accountVipHelp'; explanation.hidden = !this.helpOpen;
    this.root.append(help, explanation, this.button('refresh', 'refreshHint', () => { void this.read(); }));
    const status = this.node('p', this.busy ? this.t('loading') : this.status()); status.setAttribute('role', 'status'); this.root.append(status);
    if (!this.state) { if (!this.busy) status.textContent = this.t('unavailable'); return; }
    if (!this.state.enabled) this.root.append(this.node('p', this.t('off')));
    if (this.state.active) {
      const active = this.state.active;
      this.root.append(this.node('h4', this.name(active)), this.node('p', this.t('until', this.date(active.untilUtc))), this.benefits(active));
      if (active.dailyCoins > 0) {
        this.root.append(this.node('p', this.t('dailyNote')));
        const claim = this.button('claim', 'claimHint', () => { void this.claimDaily(); });
        claim.disabled ||= !this.state.daily.available; this.root.append(claim);
        if (this.state.daily.claimed) this.root.append(this.node('p', this.t('claimed')));
      }
    } else this.root.append(this.node('p', this.t('none')));
    if (this.operation) {
      this.root.append(this.button('retry', 'retryHint', () => { void this.purchase(); })); return;
    }
    if (this.selected) {
      const offer = this.selected;
      this.root.append(this.node('p', this.t('review', this.name(offer), offer.days, offer.price, this.coin(offer.coinType))));
      this.root.append(this.button('confirm', 'confirmHint', () => {
        this.remember({ operationId: crypto.randomUUID(), levelId: offer.levelId, days: offer.days, offerToken: offer.offerToken });
        void this.purchase();
      }), this.button('cancel', 'cancelHint', () => { this.selected = null; this.render(); })); return;
    }
    if (!this.state.enabled) return;
    this.root.append(this.node('h4', this.t('offers')));
    for (const offer of this.state.offers) {
      const card = this.node('div'); card.className = 'account-card';
      card.append(this.node('h4', this.name(offer)), this.node('p', this.t('days', offer.days)), this.node('p', this.t('price', offer.price, this.coin(offer.coinType))), this.benefits(offer));
      const review = this.button('choose', 'chooseHint', () => { this.selected = offer; this.render(); });
      review.disabled ||= this.state.active !== null && this.state.active.levelId !== offer.levelId;
      card.append(review); this.root.append(card);
    }
  }
}
