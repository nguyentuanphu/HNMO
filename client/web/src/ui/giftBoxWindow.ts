import { AccountGiftBox } from '../accountGiftBox.js';
import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import type { GiftBoxStateView } from '../giftBoxTexts.js';
import { giftBoxText } from '../giftBoxTexts.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

interface Options {
  send(name: string, fields: PacketValues): void;
  message(text: string): void;
}

interface Waiting {
  resolve(value: GiftBoxStateView): void;
  reject(error: Error): void;
  timeout: ReturnType<typeof setTimeout>;
}

let nextRequestId = crypto.getRandomValues(new Uint32Array(1))[0]!;

/** In-game Mailbox / Gift Box window using packet pair F5 27 / F5 28 under session authentication. */
export class GiftBoxWindow {
  private readonly panel: HnmoPanel;
  private readonly box: AccountGiftBox;
  private readonly waiting = new Map<number, Waiting>();

  constructor(private readonly options: Options) {
    this.panel = createHnmoPanel('hnmo-giftbox-panel', this.title(), () => this.reset());
    this.box = new AccountGiftBox({
      language: () => this.language(),
      inGame: true,
      request: async <T>(path: string, body: Readonly<Record<string, unknown>> = {}): Promise<T> =>
        await this.request(path, body) as T,
      changed: () => this.options.send('CashShopPointInfoRequest', {}),
      error: () => this.options.message(giftBoxText(this.language(), 'connectionError')),
    });
    this.panel.body.append(this.box.root);
    this.decorate();
    onLanguageChange(() => {
      this.decorate();
      this.box.refreshLanguage(this.panel.isOpen());
    });
  }

  public isOpen(): boolean { return this.panel.isOpen(); }

  public open(): void {
    if (this.panel.isOpen()) return;
    this.panel.open();
    void this.box.open();
  }

  public close(): void { this.panel.close(); }

  public toggle(): void {
    if (this.panel.isOpen()) this.close();
    else this.open();
  }

  public reset(): void {
    this.box.reset();
    for (const pending of this.waiting.values()) {
      clearTimeout(pending.timeout);
      pending.reject(new Error('giftbox_closed'));
    }
    this.waiting.clear();
  }

  public handle(bytes: Uint8Array): boolean {
    if (bytes.length < 9 || bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x28) return false;
    try {
      const parsed = read<{ RequestId: number; Json: string }>('ServerToClient', 'HnmoGiftBoxState', bytes);
      const pending = this.waiting.get(parsed.RequestId);
      if (!pending) return true;
      const raw: unknown = JSON.parse(parsed.Json);
      if (!raw || typeof raw !== 'object' || !('letters' in raw) || !Array.isArray(raw.letters)) return true;
      const view = raw as GiftBoxStateView;
      this.waiting.delete(parsed.RequestId);
      clearTimeout(pending.timeout);
      pending.resolve(view);
    } catch {
      // Ignore malformed payload; bounded timeout allows retry.
    }
    return true;
  }

  private language(): 'en' | 'vi' { return currentLanguage() === 'vi' ? 'vi' : 'en'; }

  private title(): string { return giftBoxText(this.language(), 'gameTitle'); }

  private decorate(): void {
    this.panel.setTitle(this.title());
    const close = this.panel.panel.querySelector<HTMLButtonElement>('.hnmo-panel-close');
    if (close) {
      close.title = giftBoxText(this.language(), 'closeHint');
      close.setAttribute('aria-label', giftBoxText(this.language(), 'close'));
    }
  }

  private request(path: string, body: Readonly<Record<string, unknown>>): Promise<GiftBoxStateView> {
    const action = path.endsWith('/collect') ? 'collect' : 'state';
    if (!this.panel.isOpen()) return Promise.reject(new Error('giftbox_closed'));
    const json = JSON.stringify({
      action,
      offset: typeof body.offset === 'number' ? body.offset : 0,
      letterId: typeof body.letterId === 'string' ? body.letterId : undefined,
      language: typeof body.language === 'string' ? body.language : this.language(),
    });
    if (new TextEncoder().encode(json).length > 247) return Promise.reject(new Error('giftbox_request'));
    do { nextRequestId = (nextRequestId + 1) >>> 0; } while (nextRequestId === 0 || this.waiting.has(nextRequestId));
    const requestId = nextRequestId;
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        this.waiting.delete(requestId);
        reject(new Error('giftbox_timeout'));
      }, 30_000);
      this.waiting.set(requestId, { resolve, reject, timeout });
      try {
        this.options.send('HnmoGiftBoxRequest', { RequestId: requestId, Json: json });
      } catch (error: unknown) {
        clearTimeout(timeout);
        this.waiting.delete(requestId);
        reject(error);
      }
    });
  }
}
