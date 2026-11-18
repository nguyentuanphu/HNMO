// HNMO Teleport NPC dialog (F5 4A / F5 4B): shows admin-configured map warps with level, reset, VIP, and Zen requirements.

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

export interface TeleportDestinationEntry {
  id: string;
  nameEn: string;
  nameVi: string;
  mapNumber: number;
  targetX: number;
  targetY: number;
  minLevel: number;
  maxLevel: number;
  minResets: number;
  zenCost: number;
  vipOnly: boolean;
  allowPk: boolean;
  eligible: boolean;
  reasonEn: string;
  reasonVi: string;
}

export interface TeleportNpcStatePayload {
  v: number;
  enabled: boolean;
  open: boolean;
  closeAfterWarp: boolean;
  me: {
    level: number;
    resets: number;
    vipLevel: number;
    zen: number;
    isPk: boolean;
  };
  destinations: TeleportDestinationEntry[];
  event?: { ok: boolean; textEn: string; textVi: string } | null;
}

function tx(en: string, vi: string): string {
  return currentLanguage() === 'vi' ? vi : en;
}

/** Modal panel listing Teleport NPC map destinations (opened automatically on C2 F5 4B). */
export class TeleportNpcWindow {
  private readonly panel: HnmoPanel;
  private state: TeleportNpcStatePayload | null = null;

  constructor(private readonly send: (name: string, fields: PacketValues) => void) {
    this.panel = createHnmoPanel('hnmo-teleport-npc-panel', tx('Teleport NPC', 'NPC Dịch Chuyển'));
    onLanguageChange(() => {
      this.panel.setTitle(tx('Teleport NPC', 'NPC Dịch Chuyển'));
      if (this.panel.isOpen()) this.render();
    });
  }

  /** Handles incoming S2C packets and extracts HnmoTeleportNpcState (C2 F5 4B). */
  handle(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x4b) return false;
    try {
      const pkt = read<{ Json?: string }>('ServerToClient', 'HnmoTeleportNpcState', bytes);
      if (!pkt?.Json) return true;
      this.applyJson(pkt.Json);
    } catch {
      // Ignore malformed payloads cleanly.
    }
    return true;
  }

  /** Applies a serialized TeleportNpcStatePayload (also usable in tests). */
  applyJson(json: string): void {
    try {
      this.state = JSON.parse(json) as TeleportNpcStatePayload;
    } catch {
      return;
    }
    if (this.state.open) {
      this.panel.open();
    } else if (this.state.event?.ok && this.state.closeAfterWarp && this.panel.isOpen()) {
      this.panel.close();
      return;
    }
    if (this.panel.isOpen()) this.render();
  }

  /** Opens the panel and requests the latest destinations list. */
  open(): void {
    this.send('HnmoTeleportNpcCommand', { Action: 0, DestinationId: '' });
    this.panel.open();
    this.render();
  }

  /** Closes the Teleport NPC panel. */
  close(): void {
    this.panel.close();
  }

  private render(): void {
    const body = this.panel.body;
    body.replaceChildren();

    const s = this.state;
    if (!s || !s.enabled) {
      const msg = document.createElement('p');
      msg.textContent = tx('Teleport NPC is not available right now.', 'NPC Dịch Chuyển hiện đang tạm khóa.');
      body.append(msg);
      return;
    }

    const summary = document.createElement('div');
    summary.className = 'hnmo-panel-summary';
    summary.textContent = tx(
      `Your Level: ${s.me.level} | Resets: ${s.me.resets} | Zen: ${s.me.zen.toLocaleString()}`,
      `Cấp độ: ${s.me.level} | Reset: ${s.me.resets} | Zen: ${s.me.zen.toLocaleString()}`,
    );
    body.append(summary);

    if (s.event) {
      const banner = document.createElement('div');
      banner.className = s.event.ok ? 'hnmo-notice-ok' : 'hnmo-notice-err';
      banner.textContent = currentLanguage() === 'vi' ? s.event.textVi : s.event.textEn;
      body.append(banner);
    }

    if (s.destinations.length === 0) {
      const empty = document.createElement('p');
      empty.textContent = tx('No destinations configured yet.', 'Chưa có điểm dịch chuyển nào.');
      body.append(empty);
      return;
    }

    const list = document.createElement('div');
    list.style.display = 'flex';
    list.style.flexDirection = 'column';
    list.style.gap = '6px';
    list.style.marginTop = '8px';

    for (const dest of s.destinations) {
      const row = document.createElement('div');
      row.style.display = 'flex';
      row.style.justifyContent = 'space-between';
      row.style.alignItems = 'center';
      row.style.gap = '10px';
      row.style.padding = '6px 8px';
      row.style.border = '1px solid rgba(255,255,255,0.12)';
      row.style.borderRadius = '6px';

      const info = document.createElement('div');
      const title = document.createElement('strong');
      title.textContent = currentLanguage() === 'vi' ? (dest.nameVi || dest.nameEn) : (dest.nameEn || dest.nameVi);
      const reqs: string[] = [];
      if (dest.minLevel > 0 || dest.maxLevel > 0) {
        const cap = dest.maxLevel > 0 ? `${dest.minLevel}-${dest.maxLevel}` : `${dest.minLevel}+`;
        reqs.push(tx(`Lv ${cap}`, `Cấp ${cap}`));
      }
      if (dest.minResets > 0) reqs.push(tx(`${dest.minResets} RS`, `${dest.minResets} RS`));
      if (dest.vipOnly) reqs.push('VIP');
      if (!dest.allowPk) reqs.push(tx('No PK', 'Cấm PK'));
      reqs.push(dest.zenCost > 0 ? `${dest.zenCost.toLocaleString()} Zen` : tx('Free', 'Miễn phí'));

      const meta = document.createElement('div');
      meta.style.fontSize = '12px';
      meta.style.opacity = '0.82';
      meta.textContent = reqs.join(' · ');
      info.append(title, meta);

      if (!dest.eligible && (dest.reasonEn || dest.reasonVi)) {
        const reason = document.createElement('div');
        reason.style.fontSize = '11px';
        reason.style.color = '#fca5a5';
        reason.textContent = currentLanguage() === 'vi' ? dest.reasonVi : dest.reasonEn;
        info.append(reason);
      }

      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'hnmo-btn';
      btn.disabled = !dest.eligible;
      btn.textContent = tx('Warp', 'Dịch Chuyển');
      btn.addEventListener('click', () => {
        this.send('HnmoTeleportNpcCommand', { Action: 1, DestinationId: dest.id });
      });

      row.append(info, btn);
      list.append(row);
    }

    body.append(list);
  }
}
