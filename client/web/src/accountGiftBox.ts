import { formatSydneyTime, giftBoxText, statusMessage, type GiftBoxStateView } from './giftBoxTexts.js';

export interface AccountGiftBoxOptions {
  language: () => 'en' | 'vi';
  request: <T>(path: string, body: Readonly<Record<string, unknown>>) => Promise<T>;
  inGame?: boolean;
  changed?: () => void;
  error?: () => void;
}

const CSS = `
.hnmo-gb-wrap { display: grid; grid-template-columns: minmax(220px, 1fr) minmax(260px, 1.4fr); gap: 12px; }
@media (max-width: 720px) { .hnmo-gb-wrap { grid-template-columns: 1fr; } }
.hnmo-gb-list { display: flex; flex-direction: column; gap: 6px; max-height: 360px; overflow-y: auto; }
.hnmo-gb-row { text-align: left; padding: 8px 10px; border-radius: 8px; border: 1px solid rgba(255,220,150,.22); background: rgba(20,16,12,.72); color: #f3e2bd; cursor: pointer; }
.hnmo-gb-row.active { border-color: #ffd98a; background: rgba(90,68,26,.85); }
.hnmo-gb-detail { padding: 12px; border-radius: 8px; border: 1px solid rgba(255,220,150,.22); background: rgba(16,13,10,.8); color: #f3e2bd; }
.hnmo-gb-badge { display: inline-block; font-size: 11px; padding: 2px 6px; border-radius: 999px; margin-left: 6px; background: rgba(255,255,255,.12); }
.hnmo-gb-status { margin: 6px 0; padding: 8px 10px; border-radius: 6px; background: rgba(45,65,95,.55); color: #f4efe0; font-size: 13px; }
.hnmo-gb-actions { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; margin-top: 10px; }
.hnmo-gb-btn { padding: 7px 12px; border-radius: 6px; border: 1px solid rgba(255,220,150,.45); background: rgba(110,80,30,.9); color: #fff4d2; font-weight: 600; cursor: pointer; min-height: 38px; touch-action: manipulation; }
.hnmo-gb-btn:disabled { opacity: 0.45; cursor: default; }
`;

let stylesInjected = false;

export class AccountGiftBox {
  public readonly root: HTMLElement;

  private readonly heading = document.createElement('h3');
  private readonly note = document.createElement('p');
  private readonly statusBar = document.createElement('div');
  private readonly listEl = document.createElement('div');
  private readonly detailEl = document.createElement('div');
  private readonly pagerEl = document.createElement('div');
  private readonly refreshBtn = document.createElement('button');
  private readonly prevBtn = document.createElement('button');
  private readonly nextBtn = document.createElement('button');

  private state: GiftBoxStateView | null = null;
  private offset = 0;
  private selectedLetterId: string | null = null;
  private selectedCharacterId = '';
  private busy = false;
  private statusOverride = '';

  constructor(private readonly options: AccountGiftBoxOptions) {
    if (!stylesInjected && typeof document !== 'undefined') {
      stylesInjected = true;
      const style = document.createElement('style');
      style.textContent = CSS;
      document.head.append(style);
    }

    this.root = document.createElement('section');
    this.root.id = options.inGame ? 'hnmoGiftBoxGame' : 'accountGiftBox';

    const header = document.createElement('div');
    header.className = 'hnmo-gb-actions';
    this.heading.style.margin = '0';
    this.refreshBtn.type = 'button';
    this.refreshBtn.className = 'hnmo-gb-btn';
    this.refreshBtn.addEventListener('click', () => { void this.loadState(this.offset, this.selectedLetterId); });
    header.append(this.heading, this.refreshBtn);

    this.note.style.margin = '6px 0';
    this.note.style.fontSize = '13px';
    this.statusBar.className = 'hnmo-gb-status';
    this.statusBar.hidden = true;

    const grid = document.createElement('div');
    grid.className = 'hnmo-gb-wrap';

    const leftCol = document.createElement('div');
    this.listEl.className = 'hnmo-gb-list';
    this.pagerEl.className = 'hnmo-gb-actions';
    this.prevBtn.type = 'button';
    this.prevBtn.className = 'hnmo-gb-btn';
    this.nextBtn.type = 'button';
    this.nextBtn.className = 'hnmo-gb-btn';
    this.prevBtn.addEventListener('click', () => { void this.loadState(Math.max(0, this.offset - 20), this.selectedLetterId); });
    this.nextBtn.addEventListener('click', () => { void this.loadState(this.offset + 20, this.selectedLetterId); });
    this.pagerEl.append(this.prevBtn, this.nextBtn);
    leftCol.append(this.listEl, this.pagerEl);

    this.detailEl.className = 'hnmo-gb-detail';
    grid.append(leftCol, this.detailEl);

    this.root.append(header, this.note, this.statusBar, grid);
    this.refreshLanguage(false);
  }

  public async open(): Promise<void> {
    await this.loadState(this.offset, this.selectedLetterId);
  }

  public reset(): void {
    this.state = null;
    this.offset = 0;
    this.selectedLetterId = null;
    this.selectedCharacterId = '';
    this.statusOverride = '';
    this.statusBar.hidden = true;
    this.render();
  }

  public refreshLanguage(reloadIfLoaded = true): void {
    const lang = this.options.language();
    this.heading.textContent = giftBoxText(lang, 'heading');
    this.note.textContent = giftBoxText(lang, 'helpHint');
    this.refreshBtn.textContent = giftBoxText(lang, 'reload');
    this.prevBtn.textContent = giftBoxText(lang, 'prev');
    this.nextBtn.textContent = giftBoxText(lang, 'next');
    if (reloadIfLoaded && this.state) {
      void this.loadState(this.offset, this.selectedLetterId);
    } else {
      this.render();
    }
  }

  private async loadState(offset: number, letterId: string | null): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    this.refreshBtn.disabled = true;
    try {
      const view = await this.options.request<GiftBoxStateView>('/portal/gift-box/state', {
        offset,
        letterId,
        language: this.options.language(),
      });
      this.offset = view.offset;
      this.state = view;
      this.selectedLetterId = view.detail?.id ?? view.letters[0]?.id ?? null;
      if (view.letters.length > 0 && !view.detail && this.selectedLetterId) {
        const withDetail = await this.options.request<GiftBoxStateView>('/portal/gift-box/state', {
          offset: this.offset,
          letterId: this.selectedLetterId,
          language: this.options.language(),
        });
        this.state = withDetail;
      }
      this.statusOverride = statusMessage(this.options.language(), this.state.status);
    } catch {
      this.statusOverride = giftBoxText(this.options.language(), 'connectionError');
      this.options.error?.();
    } finally {
      this.busy = false;
      this.refreshBtn.disabled = false;
      this.render();
    }
  }

  private async collect(letterId: string): Promise<void> {
    if (this.busy || !this.state) return;
    const charId = this.selectedCharacterId || this.state.characters[0]?.id || '';
    if (!this.options.inGame && !charId) return;

    this.busy = true;
    this.render();
    try {
      const updated = await this.options.request<GiftBoxStateView>('/portal/gift-box/collect', {
        offset: this.offset,
        letterId,
        characterId: charId || undefined,
        language: this.options.language(),
      });
      this.state = updated;
      this.statusOverride = statusMessage(this.options.language(), updated.status);
      if (updated.status === 'Applied') {
        this.options.changed?.();
      }
    } catch {
      this.statusOverride = giftBoxText(this.options.language(), 'connectionError');
      this.options.error?.();
    } finally {
      this.busy = false;
      this.render();
    }
  }

  private render(): void {
    const lang = this.options.language();
    if (this.statusOverride) {
      this.statusBar.textContent = this.statusOverride;
      this.statusBar.hidden = false;
    } else {
      this.statusBar.hidden = true;
    }

    this.listEl.replaceChildren();
    this.detailEl.replaceChildren();

    const state = this.state;
    this.prevBtn.disabled = this.busy || this.offset <= 0;
    this.nextBtn.disabled = this.busy || !(state?.hasMore ?? false);

    if (!state || state.letters.length === 0) {
      const empty = document.createElement('div');
      empty.textContent = giftBoxText(lang, 'empty');
      this.listEl.append(empty);
      const prompt = document.createElement('div');
      prompt.textContent = giftBoxText(lang, 'selectPrompt');
      this.detailEl.append(prompt);
      return;
    }

    for (const letter of state.letters) {
      const row = document.createElement('button');
      row.type = 'button';
      row.className = `hnmo-gb-row${letter.id === state.detail?.id ? ' active' : ''}`;
      const titleSpan = document.createElement('strong');
      titleSpan.textContent = letter.title;
      const badge = document.createElement('span');
      badge.className = 'hnmo-gb-badge';
      badge.textContent = letter.collected
        ? giftBoxText(lang, 'collected')
        : letter.expired
          ? giftBoxText(lang, 'expired')
          : giftBoxText(lang, 'ready');
      const meta = document.createElement('div');
      meta.style.fontSize = '12px';
      meta.style.opacity = '0.8';
      meta.textContent = formatSydneyTime(letter.sentAtUtc, lang);
      row.append(titleSpan, badge, meta);
      row.addEventListener('click', () => { void this.loadState(this.offset, letter.id); });
      this.listEl.append(row);
    }

    const detail = state.detail;
    if (!detail) {
      const prompt = document.createElement('div');
      prompt.textContent = giftBoxText(lang, 'selectPrompt');
      this.detailEl.append(prompt);
      return;
    }

    const h4 = document.createElement('h4');
    h4.textContent = detail.title;
    h4.style.margin = '0 0 6px';

    const times = document.createElement('div');
    times.style.fontSize = '12px';
    times.style.opacity = '0.85';
    times.textContent = `${giftBoxText(lang, 'sentAt')}: ${formatSydneyTime(detail.sentAtUtc, lang)} | ${giftBoxText(lang, 'expiresAt')}: ${formatSydneyTime(detail.expiresAtUtc, lang)}`;

    const body = document.createElement('p');
    body.style.whiteSpace = 'pre-wrap';
    body.textContent = detail.message;

    const rewards = document.createElement('ul');
    rewards.style.paddingLeft = '18px';
    if (detail.coins > 0) {
      const li = document.createElement('li');
      li.textContent = `${giftBoxText(lang, 'coins')}: ${detail.coins.toLocaleString()} ${detail.coinType}`;
      rewards.append(li);
    }
    if (detail.zen > 0) {
      const li = document.createElement('li');
      li.textContent = `${giftBoxText(lang, 'zen')}: ${detail.zen.toLocaleString()}`;
      rewards.append(li);
    }
    for (const item of detail.items) {
      const li = document.createElement('li');
      const flags = [
        item.level > 0 ? `+${item.level}` : '',
        item.luck ? 'Luck' : '',
        item.skill ? 'Skill' : '',
        item.optionLevel > 0 ? `+Opt${item.optionLevel}` : '',
        item.excellentOptions > 0 ? 'Exc' : '',
      ].filter(Boolean).join(' ');
      li.textContent = `${item.count}x ${item.name}${flags ? ` (${flags})` : ''}`;
      rewards.append(li);
    }

    this.detailEl.append(h4, times, body, rewards);

    if (detail.collected) {
      const claimed = document.createElement('div');
      claimed.className = 'hnmo-gb-badge';
      claimed.textContent = detail.collectedBy
        ? `${giftBoxText(lang, 'claimedBy')}: ${detail.collectedBy}`
        : giftBoxText(lang, 'collected');
      this.detailEl.append(claimed);
      return;
    }

    if (detail.expired) {
      const exp = document.createElement('div');
      exp.className = 'hnmo-gb-badge';
      exp.textContent = giftBoxText(lang, 'expired');
      this.detailEl.append(exp);
      return;
    }

    const actions = document.createElement('div');
    actions.className = 'hnmo-gb-actions';
    if (!this.options.inGame && state.characters.length > 0) {
      const select = document.createElement('select');
      select.setAttribute('aria-label', giftBoxText(lang, 'character'));
      for (const ch of state.characters) {
        const opt = document.createElement('option');
        opt.value = ch.id;
        opt.textContent = ch.name;
        select.append(opt);
      }
      if (!this.selectedCharacterId || !state.characters.some(c => c.id === this.selectedCharacterId)) {
        this.selectedCharacterId = state.characters[0].id;
      }
      select.value = this.selectedCharacterId;
      select.addEventListener('change', () => { this.selectedCharacterId = select.value; });
      actions.append(select);
    }

    const collectBtn = document.createElement('button');
    collectBtn.type = 'button';
    collectBtn.className = 'hnmo-gb-btn';
    collectBtn.textContent = this.busy ? giftBoxText(lang, 'collecting') : giftBoxText(lang, 'collect');
    collectBtn.disabled = this.busy || (!this.options.inGame && state.characters.length === 0);
    collectBtn.addEventListener('click', () => { void this.collect(detail.id); });
    actions.append(collectBtn);
    this.detailEl.append(actions);
  }
}
