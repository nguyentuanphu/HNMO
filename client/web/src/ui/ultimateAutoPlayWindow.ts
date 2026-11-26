// Compact floating Ultimate Autoplay HUD pill (#hnmo-ultimate-hud).
// The full configuration now lives inside Window 1 (mu_helper_config) on its Ultimate tab (Alt+Z).

import { currentLanguage } from '../i18n.js';
import type { AutoPlayExtras } from '../game/autoPlayExtras.js';
import { hudScale } from './nativeText.js';

const tx = (en: string, vi: string) => (currentLanguage() === 'vi' ? vi : en);

function perHour(value: number, elapsedMs: number): number {
  if (elapsedMs < 5000) return 0;
  return Math.round((value * 3_600_000) / elapsedMs);
}

export interface UltimateAutoPlayWindowOptions {
  extras: AutoPlayExtras;
  isActive: () => boolean;
  toggleActive: () => void;
  mapName: (map: number) => string;
  currentMap: () => number | null;
  message: (text: string) => void;
  onRequestPolicy?: () => void;
  onAutoPickSkills?: () => boolean;
  /** Opens Window 1 on its Ultimate tab (Alt+Z). */
  onOpenSettings?: () => void;
}

export class UltimateAutoPlayWindow {
  private hud: HTMLElement | null = null;
  private hudRefs: {
    dot: HTMLElement;
    statusText: Text;
    vipTag: HTMLElement;
    killsLabel: Text;
    killsVal: HTMLElement;
    expVal: HTMLElement;
    toggleBtn: HTMLButtonElement;
    townBtn: HTMLButtonElement;
    openBtn: HTMLButtonElement;
  } | null = null;
  private lastHudTick = 0;

  constructor(private readonly opts: UltimateAutoPlayWindowOptions) {
    this.mountHud();
  }

  get isOpen(): boolean { return false; }

  toggle(): void {
    this.opts.onOpenSettings?.();
  }

  open(): void {
    this.opts.onOpenSettings?.();
  }

  close(): void {}

  /** Refreshes HUD badge immediately after a live policy change or settings save. */
  refreshPolicy(): void {
    const showHud = this.opts.extras.policy.enabled && this.opts.extras.settings.showUltimateHud;
    if (this.hud) {
      this.hud.style.display = showHud ? 'flex' : 'none';
      if (showHud) this.updateHudContent();
    }
  }

  /** Refreshes the compact floating HUD pill (throttled to ~2 Hz). */
  refresh(now = performance.now()): void {
    if (now - this.lastHudTick < 500) return;
    this.lastHudTick = now;
    const showHud = this.opts.extras.policy.enabled && this.opts.extras.settings.showUltimateHud;
    if (this.hud) {
      this.hud.style.display = showHud ? 'flex' : 'none';
      if (showHud) this.updateHudContent();
    }
  }

  private mountHud(): void {
    if (this.hud) return;
    const hud = document.createElement('div');
    hud.id = 'hnmo-ultimate-hud';
    const bottomOffset = () => `${Math.round(54 * hudScale() + 8)}px`;
    Object.assign(hud.style, {
      position: 'fixed', left: '12px', bottom: bottomOffset(), zIndex: '22',
      display: this.opts.extras.settings.showUltimateHud ? 'flex' : 'none',
      alignItems: 'center', gap: '8px', padding: '6px 10px',
      borderRadius: '8px', border: '1px solid rgba(212,175,55,0.45)',
      background: 'rgba(14,16,22,0.88)', color: '#f3e9d2',
      fontFamily: 'system-ui, -apple-system, sans-serif', fontSize: '12px',
      boxShadow: '0 4px 16px rgba(0,0,0,0.45)', pointerEvents: 'auto',
    });
    window.addEventListener('resize', () => { hud.style.bottom = bottomOffset(); });
    hud.addEventListener('pointerdown', (e) => e.stopPropagation());
    hud.innerHTML = `
      <span style="display:inline-flex;align-items:center;gap:5px;font-weight:600;color:#e7c873">
        <span data-uap-ref="dot" style="width:8px;height:8px;border-radius:50%;background:#94a3b8;display:inline-block"></span>
        <span data-uap-ref="status"></span>
        <span data-uap-ref="vip" style="display:none;padding:1px 5px;border-radius:4px;background:#78350f;color:#fde68a;font-size:10px;font-weight:700">VIP</span>
      </span>
      <span data-uap-hud-detail="1" style="color:#cbd5e1"><span data-uap-ref="killsLabel"></span>: <b data-uap-ref="kills">0</b></span>
      <span data-uap-hud-detail="1" style="color:#cbd5e1">EXP/h: <b data-uap-ref="exp">0</b></span>
      <button type="button" data-uap-hud="toggle" data-uap-hud-detail="1" style="${btnStyle('#166534')}"></button>
      <button type="button" data-uap-hud="town" data-uap-hud-detail="1" style="${btnStyle('#334155')}"></button>
      <button type="button" data-uap-hud="open" data-uap-hud-detail="1" style="${btnStyle('#78350f')}"></button>
      <button type="button" data-uap-hud="minimize" style="${btnStyle('#1e293b')}" aria-label="Minimize Ultimate HUD">−</button>
    `;
    const dot = hud.querySelector<HTMLElement>('[data-uap-ref="dot"]')!;
    const statusHost = hud.querySelector<HTMLElement>('[data-uap-ref="status"]')!;
    const statusText = document.createTextNode('');
    statusHost.appendChild(statusText);
    const vipTag = hud.querySelector<HTMLElement>('[data-uap-ref="vip"]')!;
    const killsHost = hud.querySelector<HTMLElement>('[data-uap-ref="killsLabel"]')!;
    const killsLabel = document.createTextNode('');
    killsHost.appendChild(killsLabel);
    const killsVal = hud.querySelector<HTMLElement>('[data-uap-ref="kills"]')!;
    const expVal = hud.querySelector<HTMLElement>('[data-uap-ref="exp"]')!;
    const toggleBtn = hud.querySelector<HTMLButtonElement>('[data-uap-hud="toggle"]')!;
    const townBtn = hud.querySelector<HTMLButtonElement>('[data-uap-hud="town"]')!;
    const openBtn = hud.querySelector<HTMLButtonElement>('[data-uap-hud="open"]')!;
    const minimizeBtn = hud.querySelector<HTMLButtonElement>('[data-uap-hud="minimize"]')!;
    let minimized = false;
    minimizeBtn.addEventListener('click', () => {
      minimized = !minimized;
      hud.dataset.minimized = minimized ? 'true' : 'false';
      minimizeBtn.textContent = minimized ? '+' : '−';
      minimizeBtn.setAttribute('aria-label', minimized ? 'Expand Ultimate HUD' : 'Minimize Ultimate HUD');
      for (const el of hud.querySelectorAll<HTMLElement>('[data-uap-hud-detail]')) {
        el.style.display = minimized ? 'none' : '';
      }
    });

    toggleBtn.addEventListener('click', () => this.opts.toggleActive());
    townBtn.addEventListener('click', () => {
      if (this.opts.extras.onTrip) {
        this.opts.message(tx('Ultimate Auto play: already heading to town.', 'Autoplay: đang trên đường về thành.'));
        return;
      }
      this.opts.extras.goToTown();
      this.opts.message(tx('Ultimate Auto play: returning to Lorencia.', 'Autoplay: đang quay về Lorencia.'));
    });
    openBtn.addEventListener('click', () => this.opts.onOpenSettings?.());

    document.body.appendChild(hud);
    this.hud = hud;
    this.hudRefs = { dot, statusText, vipTag, killsLabel, killsVal, expVal, toggleBtn, townBtn, openBtn };
    this.updateHudContent();
  }

  private updateHudContent(): void {
    const refs = this.hudRefs;
    if (!this.hud || !refs) return;
    const active = this.opts.isActive();
    const onTrip = this.opts.extras.onTrip;
    const stats = this.opts.extras.stats;
    const dotColor = onTrip ? '#f59e0b' : active ? '#22c55e' : '#94a3b8';
    const statusText = onTrip
      ? tx(`Ultimate: Trip: ${this.opts.extras.tripStepLabel}`, `Ultimate: Về thành: ${this.opts.extras.tripStepLabel}`)
      : active ? 'Ultimate: ' + tx('ON', 'BẬT') : 'Ultimate: ' + tx('OFF', 'TẮT');
    const expHr = perHour(stats.expGained, stats.elapsedMs).toLocaleString();

    if (refs.dot.style.background !== dotColor) refs.dot.style.background = dotColor;
    if (refs.statusText.data !== statusText) refs.statusText.data = statusText;
    const vipDisplay = this.opts.extras.policy.isVip ? 'inline-block' : 'none';
    if (refs.vipTag.style.display !== vipDisplay) refs.vipTag.style.display = vipDisplay;
    const killsTitle = tx('Kills', 'Quái');
    if (refs.killsLabel.data !== killsTitle) refs.killsLabel.data = killsTitle;
    const killsStr = String(stats.kills);
    if (refs.killsVal.textContent !== killsStr) refs.killsVal.textContent = killsStr;
    if (refs.expVal.textContent !== expHr) refs.expVal.textContent = expHr;

    const toggleBg = active ? '#991b1b' : '#166534';
    if (refs.toggleBtn.style.background !== toggleBg) refs.toggleBtn.style.background = toggleBg;
    const toggleLabel = active ? tx('Stop', 'Dừng') : tx('Start', 'Chạy');
    if (refs.toggleBtn.textContent !== toggleLabel) refs.toggleBtn.textContent = toggleLabel;
    const townLabel = tx('Town', 'Về thành');
    if (refs.townBtn.textContent !== townLabel) refs.townBtn.textContent = townLabel;
    const openLabel = tx('Ultimate (Alt+Z)', 'Cài đặt (Alt+Z)');
    if (refs.openBtn.textContent !== openLabel) refs.openBtn.textContent = openLabel;
  }
}

function btnStyle(bg: string): string {
  return `background:${bg};color:#f8fafc;border:1px solid rgba(255,255,255,0.2);border-radius:5px;padding:4px 9px;font-size:12px;cursor:pointer;font-weight:600`;
}
