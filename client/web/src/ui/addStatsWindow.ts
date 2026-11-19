// HNMO in-game Bulk Add Stats window (Shift+A / /addstats / docked button beside CharacterWindow C):
// lets players distribute hundreds or thousands of Level-Up Points across STR/AGI/VIT/ENE/(CMD) in one step.

import { BaseClass } from '../game/characterCalc.js';
import type { CharacterStats } from '../game/stats.js';
import { currentLanguage, onLanguageChange } from '../i18n.js';
import type { PacketValues } from '../net/packets.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

type StatKey = 'str' | 'agi' | 'vit' | 'ene' | 'cmd';

interface StatFieldDef {
  key: StatKey;
  labelEn: string;
  labelVi: string;
  getCurrent: (stats: CharacterStats) => number;
}

const STAT_DEFS: readonly StatFieldDef[] = [
  { key: 'str', labelEn: 'Strength (STR)', labelVi: 'Sức Mạnh (STR)', getCurrent: (s) => s.strength },
  { key: 'agi', labelEn: 'Agility (AGI)', labelVi: 'Nhanh Nhẹn (AGI)', getCurrent: (s) => s.agility },
  { key: 'vit', labelEn: 'Vitality (VIT)', labelVi: 'Thể Lực (VIT)', getCurrent: (s) => s.vitality },
  { key: 'ene', labelEn: 'Energy (ENE)', labelVi: 'Năng Lượng (ENE)', getCurrent: (s) => s.energy },
  { key: 'cmd', labelEn: 'Command (CMD)', labelVi: 'Mệnh Lệnh (CMD)', getCurrent: (s) => s.leadership },
];

function tx(en: string, vi: string): string {
  return currentLanguage() === 'vi' ? vi : en;
}

/** Interactive bulk stat-point distribution modal (Shift+A). */
export class AddStatsWindow {
  private readonly panel: HnmoPanel;
  private readonly dockBtn: HTMLButtonElement;
  private readonly draft: Record<StatKey, number> = { str: 0, agi: 0, vit: 0, ene: 0, cmd: 0 };
  private characterWindowOpen = false;
  private unsubscribeStats: (() => void) | null = null;

  constructor(
    private readonly stats: CharacterStats,
    private readonly classIndex: number,
    private readonly send: (name: string, fields: PacketValues) => void,
    private readonly message: (text: string) => void,
  ) {
    this.panel = createHnmoPanel('hnmo-add-stats-panel', tx('Add Stats (Shift+A)', 'Cộng Điểm Tiềm Năng (Shift+A)'));
    this.dockBtn = document.createElement('button');
    this.dockBtn.type = 'button';
    this.dockBtn.id = 'hnmo-add-stats-dock-btn';
    Object.assign(this.dockBtn.style, {
      position: 'fixed',
      right: '200px',
      top: '76px',
      zIndex: '62',
      padding: '6px 10px',
      background: '#2c2214',
      border: '1px solid #9b7a3c',
      color: '#f2cf78',
      borderRadius: '4px',
      cursor: 'pointer',
      font: '600 12.5px system-ui, -apple-system, sans-serif',
      display: 'none',
    });
    this.dockBtn.addEventListener('click', () => this.open());
    document.body.appendChild(this.dockBtn);

    this.unsubscribeStats = this.stats.onChange(() => {
      this.updateDockButton();
      if (this.panel.isOpen()) this.render();
    });
    onLanguageChange(() => {
      this.panel.setTitle(tx('Add Stats (Shift+A)', 'Cộng Điểm Tiềm Năng (Shift+A)'));
      this.updateDockButton();
      if (this.panel.isOpen()) this.render();
    });
    this.updateDockButton();
  }

  /** Updates dock button visibility when CharacterWindow (C) opens or closes. */
  setCharacterWindowOpen(open: boolean): void {
    this.characterWindowOpen = open;
    this.updateDockButton();
  }

  /** Opens the Add Stats modal. */
  open(): void {
    this.panel.open();
    this.render();
  }

  /** Closes the Add Stats modal. */
  close(): void {
    this.panel.close();
  }

  /** Toggles the Add Stats modal. */
  toggle(): void {
    if (this.panel.isOpen()) this.close();
    else this.open();
  }

  /** Cleans up DOM button and listeners if torn down. */
  dispose(): void {
    this.unsubscribeStats?.();
    this.unsubscribeStats = null;
    this.dockBtn.remove();
    this.panel.close();
  }

  private updateDockButton(): void {
    const pts = this.stats.levelUpPoints;
    this.dockBtn.textContent = pts > 0
      ? tx(`+ Add Stats (${pts})`, `+ Cộng điểm (${pts})`)
      : tx('+ Add Stats', '+ Cộng điểm');
    this.dockBtn.title = tx('Open fast multi-point Add Stats window (Shift+A)', 'Mở cửa sổ cộng nhiều điểm nhanh (Shift+A)');
    this.dockBtn.style.display = this.characterWindowOpen ? 'block' : 'none';
  }

  private plannedTotal(): number {
    return this.draft.str + this.draft.agi + this.draft.vit + this.draft.ene + this.draft.cmd;
  }

  private visibleDefs(): readonly StatFieldDef[] {
    return this.classIndex === BaseClass.DARK_LORD
      ? STAT_DEFS
      : STAT_DEFS.filter((d) => d.key !== 'cmd');
  }

  private addDelta(key: StatKey, delta: number): void {
    const remaining = Math.max(0, this.stats.levelUpPoints - this.plannedTotal());
    const grant = Math.min(Math.max(0, delta), remaining);
    if (grant <= 0) return;
    this.draft[key] += grant;
    this.render();
  }

  private apply(): void {
    const total = this.plannedTotal();
    if (total <= 0) {
      this.message(tx('Enter at least 1 stat point to add.', 'Vui lòng nhập ít nhất 1 điểm để cộng.'));
      return;
    }
    if (total > this.stats.levelUpPoints) {
      this.message(tx('Requested points exceed available Level-Up Points.', 'Tổng điểm vượt quá điểm tiềm năng hiện có.'));
      return;
    }

    const payload = JSON.stringify({
      str: this.draft.str,
      agi: this.draft.agi,
      vit: this.draft.vit,
      ene: this.draft.ene,
      cmd: this.classIndex === BaseClass.DARK_LORD ? this.draft.cmd : 0,
    });
    this.send('HnmoRankingCommand', { Action: 1, Payload: payload });
    this.resetDraft();
  }

  private resetDraft(): void {
    this.draft.str = 0;
    this.draft.agi = 0;
    this.draft.vit = 0;
    this.draft.ene = 0;
    this.draft.cmd = 0;
    if (this.panel.isOpen()) this.render();
  }

  private render(): void {
    const body = this.panel.body;
    body.innerHTML = '';

    const available = Math.max(0, this.stats.levelUpPoints);
    const total = this.plannedTotal();
    const remaining = Math.max(0, available - total);

    const headerCard = document.createElement('div');
    headerCard.className = 'hnmo-card';
    const leftInfo = document.createElement('div');
    leftInfo.innerHTML = `<span>${tx('Available points:', 'Điểm tiềm năng hiện có:')}</span> <strong>${available.toLocaleString()}</strong> · <span class="hnmo-muted">${tx('Remaining:', 'Còn lại:')} <strong>${remaining.toLocaleString()}</strong></span>`;
    headerCard.appendChild(leftInfo);

    const clearBtn = document.createElement('button');
    clearBtn.type = 'button';
    clearBtn.className = 'hnmo-btn';
    clearBtn.textContent = tx('Reset', 'Đặt lại');
    clearBtn.addEventListener('click', () => this.resetDraft());
    headerCard.appendChild(clearBtn);
    body.appendChild(headerCard);

    for (const def of this.visibleDefs()) {
      const current = def.getCurrent(this.stats);
      const pending = this.draft[def.key];

      const row = document.createElement('div');
      row.className = 'hnmo-card';
      row.style.flexWrap = 'wrap';

      const labelBox = document.createElement('div');
      labelBox.style.minWidth = '175px';
      labelBox.innerHTML = `<div><strong>${tx(def.labelEn, def.labelVi)}</strong></div><div class="hnmo-muted">${current.toLocaleString()} → <strong style="color:#7cc98a">${(current + pending).toLocaleString()}</strong></div>`;
      row.appendChild(labelBox);

      const controls = document.createElement('div');
      controls.className = 'hnmo-row';

      const input = document.createElement('input');
      input.type = 'number';
      input.min = '0';
      input.max = String(available);
      input.className = 'hnmo-input';
      input.style.width = '88px';
      input.value = String(pending);
      input.addEventListener('change', () => {
        const parsed = Math.max(0, Math.floor(Number(input.value) || 0));
        const otherTotal = this.plannedTotal() - this.draft[def.key];
        const maxForThis = Math.max(0, available - otherTotal);
        this.draft[def.key] = Math.min(parsed, maxForThis);
        this.render();
      });
      controls.appendChild(input);

      for (const step of [10, 100, 500]) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'hnmo-btn';
        btn.textContent = `+${step}`;
        btn.disabled = remaining <= 0;
        btn.addEventListener('click', () => this.addDelta(def.key, step));
        controls.appendChild(btn);
      }

      const maxBtn = document.createElement('button');
      maxBtn.type = 'button';
      maxBtn.className = 'hnmo-btn';
      maxBtn.textContent = tx('Max', 'Tối đa');
      maxBtn.disabled = remaining <= 0;
      maxBtn.addEventListener('click', () => this.addDelta(def.key, remaining));
      controls.appendChild(maxBtn);

      row.appendChild(controls);
      body.appendChild(row);
    }

    const footer = document.createElement('div');
    footer.className = 'hnmo-row';
    footer.style.justifyContent = 'flex-end';

    const applyBtn = document.createElement('button');
    applyBtn.type = 'button';
    applyBtn.id = 'hnmo-add-stats-apply';
    applyBtn.className = 'hnmo-btn';
    applyBtn.textContent = total > 0
      ? tx(`Apply (+${total.toLocaleString()} pts)`, `Cộng ngay (+${total.toLocaleString()} điểm)`)
      : tx('Apply', 'Cộng điểm');
    applyBtn.disabled = total <= 0 || total > available;
    applyBtn.addEventListener('click', () => this.apply());
    footer.appendChild(applyBtn);

    body.appendChild(footer);
  }
}
