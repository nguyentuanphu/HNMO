import { AccountVip } from '../accountVip.js';
import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import type { VipStateView } from '../vipModels.js';
import { vipText } from '../vipTexts.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

interface Options {
  send(name: string, fields: PacketValues): void;
  message(text: string): void;
}
interface Waiting {
  resolve(value: VipStateView): void;
  reject(error: Error): void;
  timeout: ReturnType<typeof setTimeout>;
}

let nextRequestId = crypto.getRandomValues(new Uint32Array(1))[0]!;

/** The game uses its authenticated connection, never a website password or a client-supplied account name. */
export class VipWindow {
  private readonly panel: HnmoPanel;
  private readonly account: AccountVip;
  private readonly waiting = new Map<number, Waiting>();
  private accountKey = '';

  constructor(private readonly options: Options) {
    this.panel = createHnmoPanel('hnmo-vip-panel', this.title(), () => this.reset());
    this.account = new AccountVip({
      language: () => this.language(), accountKey: () => this.accountKey,
      request: async <T>(path: string, body: Readonly<Record<string, unknown>> = {}): Promise<T> => await this.request(path, body) as T,
      changed: () => this.options.send('CashShopPointInfoRequest', {}),
      error: () => this.options.message(vipText(this.language(), 'connectionError')),
    });
    this.panel.body.append(this.account.root);
    this.decorate();
    onLanguageChange(() => { this.decorate(); this.account.refreshLanguage(this.panel.isOpen()); });
  }

  isOpen(): boolean { return this.panel.isOpen(); }
  open(): void { if (this.panel.isOpen()) return; this.panel.open(); void this.account.open(); }
  close(): void { this.panel.close(); }
  toggle(): void { if (this.panel.isOpen()) this.close(); else this.open(); }

  /** Rejects requests on disconnect or close; a late reply cannot update another game session. */
  reset(): void {
    this.account.reset();
    for (const pending of this.waiting.values()) { clearTimeout(pending.timeout); pending.reject(new Error('vip_closed')); }
    this.waiting.clear(); this.accountKey = '';
  }

  handle(bytes: Uint8Array): boolean {
    if (bytes.length < 9 || bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x26) return false;
    try {
      const parsed = read<{ RequestId: number; Json: string }>('ServerToClient', 'HnmoVipState', bytes);
      const pending = this.waiting.get(parsed.RequestId);
      if (!pending) return true;
      const raw: unknown = JSON.parse(parsed.Json);
      if (!raw || typeof raw !== 'object' || !('enabled' in raw) || typeof raw.enabled !== 'boolean'
        || !('offers' in raw) || !Array.isArray(raw.offers) || !('daily' in raw) || !raw.daily || typeof raw.daily !== 'object'
        || !('accountKey' in raw) || typeof raw.accountKey !== 'string' || raw.accountKey.length > 10) return true;
      const state = raw as VipStateView;
      this.waiting.delete(parsed.RequestId); clearTimeout(pending.timeout);
      this.accountKey = state.accountKey!; pending.resolve(state);
    } catch { /* An invalid reply cannot confirm a purchase. The bounded request timeout permits a safe retry. */ }
    return true;
  }

  private language(): 'en' | 'vi' { return currentLanguage() === 'vi' ? 'vi' : 'en'; }
  private title(): string { return vipText(this.language(), 'gameTitle'); }
  private decorate(): void {
    this.panel.setTitle(this.title());
    const close = this.panel.panel.querySelector<HTMLButtonElement>('.hnmo-panel-close');
    if (close) { close.title = vipText(this.language(), 'closeHint'); close.setAttribute('aria-label', vipText(this.language(), 'close')); }
  }

  private request(path: string, body: Readonly<Record<string, unknown>>): Promise<VipStateView> {
    const action = path.slice(path.lastIndexOf('/') + 1);
    if (!this.panel.isOpen() || !['state', 'purchase', 'daily'].includes(action)) return Promise.reject(new Error('vip_closed'));
    const json = JSON.stringify({ action, ...body });
    if (new TextEncoder().encode(json).length > 247) return Promise.reject(new Error('vip_request'));
    do { nextRequestId = (nextRequestId + 1) >>> 0; } while (nextRequestId === 0 || this.waiting.has(nextRequestId));
    const requestId = nextRequestId;
    return new Promise((resolve, reject) => {
      // This is a transport deadline, not an automatic purchase retry. The component retains its operation identifier.
      const timeout = setTimeout(() => { this.waiting.delete(requestId); reject(new Error('vip_timeout')); }, 30_000);
      this.waiting.set(requestId, { resolve, reject, timeout });
      try { this.options.send('HnmoVipRequest', { RequestId: requestId, Json: json }); }
      catch (error: unknown) { clearTimeout(timeout); this.waiting.delete(requestId); reject(error); }
    });
  }
}
