// Ultimate Autoplay HUD pill & tabbed configuration/telemetry panel (Alt+Z).
// Built on top of AutoPlay + AutoPlayExtras without altering base MUHelper windows.
// Respects server-sent Free vs VIP policy (HnmoUltimateAutoPlayPolicy, F5 57).

import { currentLanguage } from '../i18n.js';
import type { AutoPlayExtras, AutoPlayExtrasSettings, UltimateFeatureKey, UltimatePreset } from '../game/autoPlayExtras.js';
import type { TargetPriority } from '../game/autoPlay.js';

const tx = (en: string, vi: string) => (currentLanguage() === 'vi' ? vi : en);

function formatDuration(ms: number): string {
  const totalSec = Math.floor(ms / 1000);
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

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
}

type Tab = 'survival' | 'combat' | 'stats';

export class UltimateAutoPlayWindow {
  private hud: HTMLElement | null = null;
  private hudMinimized = false;
  private hudRefs: {
    dot: HTMLElement;
    statusText: Text;
    vipTag: HTMLElement;
    body: HTMLElement;
    killsLabel: Text;
    killsVal: HTMLElement;
    expVal: HTMLElement;
    toggleBtn: HTMLButtonElement;
    townBtn: HTMLButtonElement;
    openBtn: HTMLButtonElement;
    minimizeBtn: HTMLButtonElement;
  } | null = null;
  private panel: HTMLElement | null = null;
  private tab: Tab = 'survival';
  private openState = false;
  private draft: AutoPlayExtrasSettings;
  private lastHudTick = 0;

  constructor(private readonly opts: UltimateAutoPlayWindowOptions) {
    this.draft = { ...opts.extras.settings, patrolSpots: [...opts.extras.settings.patrolSpots] };
    this.mountHud();
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.openState) {
        event.preventDefault();
        this.close();
      }
    });
  }

  get isOpen(): boolean { return this.openState; }

  toggle(): void {
    if (this.openState) this.close();
    else this.open();
  }

  open(): void {
    this.opts.onRequestPolicy?.();
    this.openState = true;
    this.draft = { ...this.opts.extras.settings, patrolSpots: [...this.opts.extras.settings.patrolSpots] };
    this.renderPanel();
  }

  close(): void {
    this.openState = false;
    this.panel?.remove();
    this.panel = null;
  }

  /** Refreshes HUD badge and re-renders an open Ultimate Autoplay panel immediately after a live policy change. */
  refreshPolicy(): void {
    const showHud = this.opts.extras.policy.enabled && this.opts.extras.settings.showUltimateHud;
    if (this.hud) {
      this.hud.style.display = showHud ? 'flex' : 'none';
      if (showHud) this.updateHudContent();
    }
    if (this.openState) {
      if (this.panel) this.captureDraftInputs(this.panel);
      this.renderPanel();
    }
  }

  /** Refreshes the compact floating HUD pill (throttled to ~2 Hz) and Stats tab when open. */
  refresh(now = performance.now()): void {
    if (now - this.lastHudTick < 500) return;
    this.lastHudTick = now;
    const showHud = this.opts.extras.policy.enabled && this.opts.extras.settings.showUltimateHud;
    if (this.hud) {
      this.hud.style.display = showHud ? 'flex' : 'none';
      if (showHud) this.updateHudContent();
    }
    if (this.openState && this.tab === 'stats' && this.panel) {
      const body = this.panel.querySelector<HTMLElement>('#uap-stats-body');
      if (body) body.innerHTML = this.renderStatsHtml();
    }
  }

  private mountHud(): void {
    if (this.hud) return;
    const hud = document.createElement('div');
    hud.id = 'hnmo-ultimate-hud';
    Object.assign(hud.style, {
      position: 'fixed', left: '12px', bottom: '68px', zIndex: '60',
      display: this.opts.extras.settings.showUltimateHud ? 'flex' : 'none',
      alignItems: 'center', gap: '8px', padding: '6px 10px',
      borderRadius: '8px', border: '1px solid rgba(212,175,55,0.45)',
      background: 'rgba(14,16,22,0.88)', color: '#f3e9d2',
      fontFamily: 'system-ui, -apple-system, sans-serif', fontSize: '12px',
      boxShadow: '0 4px 16px rgba(0,0,0,0.45)', pointerEvents: 'auto',
    });
    hud.addEventListener('pointerdown', (e) => e.stopPropagation());
    hud.innerHTML = `
      <span style="display:inline-flex;align-items:center;gap:5px;font-weight:600;color:#e7c873">
        <span data-uap-ref="dot" style="width:8px;height:8px;border-radius:50%;background:#94a3b8;display:inline-block"></span>
        <span data-uap-ref="status"></span>
        <span data-uap-ref="vip" style="display:none;padding:1px 5px;border-radius:4px;background:#78350f;color:#fde68a;font-size:10px;font-weight:700">VIP</span>
      </span>
      <span data-uap-hud="body" style="display:inline-flex;align-items:center;gap:8px">
        <span style="color:#cbd5e1"><span data-uap-ref="killsLabel"></span>: <b data-uap-ref="kills">0</b></span>
        <span style="color:#cbd5e1">EXP/h: <b data-uap-ref="exp">0</b></span>
        <button type="button" data-uap-hud="toggle" style="${btnStyle('#166534')}"></button>
        <button type="button" data-uap-hud="town" style="${btnStyle('#334155')}"></button>
        <button type="button" data-uap-hud="open" style="${btnStyle('#78350f')}"></button>
      </span>
      <button type="button" data-uap-hud="minimize" style="${btnStyle('#1e293b')}" title="Minimize / Restore">-</button>
    `;
    const dot = hud.querySelector<HTMLElement>('[data-uap-ref="dot"]')!;
    const statusHost = hud.querySelector<HTMLElement>('[data-uap-ref="status"]')!;
    const statusText = document.createTextNode('');
    statusHost.appendChild(statusText);
    const vipTag = hud.querySelector<HTMLElement>('[data-uap-ref="vip"]')!;
    const body = hud.querySelector<HTMLElement>('[data-uap-hud="body"]')!;
    const killsHost = hud.querySelector<HTMLElement>('[data-uap-ref="killsLabel"]')!;
    const killsLabel = document.createTextNode('');
    killsHost.appendChild(killsLabel);
    const killsVal = hud.querySelector<HTMLElement>('[data-uap-ref="kills"]')!;
    const expVal = hud.querySelector<HTMLElement>('[data-uap-ref="exp"]')!;
    const toggleBtn = hud.querySelector<HTMLButtonElement>('[data-uap-hud="toggle"]')!;
    const townBtn = hud.querySelector<HTMLButtonElement>('[data-uap-hud="town"]')!;
    const openBtn = hud.querySelector<HTMLButtonElement>('[data-uap-hud="open"]')!;
    const minimizeBtn = hud.querySelector<HTMLButtonElement>('[data-uap-hud="minimize"]')!;

    toggleBtn.addEventListener('click', () => this.opts.toggleActive());
    townBtn.addEventListener('click', () => {
      if (this.opts.extras.onTrip) {
        this.opts.message(tx('Ultimate Auto play: already heading to town.', 'Autoplay: đang trên đường về thành.'));
        return;
      }
      this.opts.extras.goToTown();
      this.opts.message(tx('Ultimate Auto play: returning to Lorencia.', 'Autoplay: đang quay về Lorencia.'));
    });
    openBtn.addEventListener('click', () => this.toggle());
    minimizeBtn.addEventListener('click', () => {
      this.hudMinimized = !this.hudMinimized;
      this.updateHudContent();
    });

    document.body.appendChild(hud);
    this.hud = hud;
    this.hudRefs = { dot, statusText, vipTag, body, killsLabel, killsVal, expVal, toggleBtn, townBtn, openBtn, minimizeBtn };
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
    const bodyDisplay = this.hudMinimized ? 'none' : 'inline-flex';
    if (refs.body.style.display !== bodyDisplay) refs.body.style.display = bodyDisplay;
    const minLabel = this.hudMinimized ? '+' : '-';
    if (refs.minimizeBtn.textContent !== minLabel) refs.minimizeBtn.textContent = minLabel;
  }

  private badge(key: UltimateFeatureKey): { disabled: boolean; tag: string } {
    if (!this.opts.extras.policy.enabled) {
      return { disabled: true, tag: ` <span style="font-size:10px;padding:1px 5px;border-radius:4px;background:#334155;color:#cbd5e1">${tx('OFF', 'TẮT')}</span>` };
    }
    if (this.opts.extras.isFeatureAllowed(key)) return { disabled: false, tag: '' };
    if (this.opts.extras.isFeatureVipLocked(key)) {
      return { disabled: true, tag: ` <span style="font-size:10px;padding:1px 5px;border-radius:4px;background:#78350f;color:#fde68a;font-weight:700">${tx('VIP ONLY', 'CHỈ VIP')}</span>` };
    }
    return { disabled: true, tag: ` <span style="font-size:10px;padding:1px 5px;border-radius:4px;background:#334155;color:#cbd5e1">${tx('ADMIN OFF', 'ĐÃ TẮT')}</span>` };
  }

  private renderPanel(): void {
    this.panel?.remove();
    const wrap = document.createElement('div');
    wrap.id = 'hnmo-ultimate-autoplay-panel';
    wrap.setAttribute('role', 'dialog');
    wrap.setAttribute('aria-label', 'Ultimate Autoplay');
    Object.assign(wrap.style, {
      position: 'fixed', top: '50%', left: '50%', transform: 'translate(-50%, -50%)',
      width: 'min(560px, 94vw)', maxHeight: '86vh', overflowY: 'auto', zIndex: '90',
      borderRadius: '10px', border: '1px solid #b8923a',
      background: 'linear-gradient(180deg, #171a22 0%, #101218 100%)', color: '#f3e9d2',
      fontFamily: 'system-ui, -apple-system, sans-serif', fontSize: '13px',
      boxShadow: '0 16px 40px rgba(0,0,0,0.72)', padding: '14px 16px', pointerEvents: 'auto',
    });
    wrap.addEventListener('pointerdown', (e) => e.stopPropagation());
    wrap.addEventListener('keydown', (e) => { if (e.key !== 'Escape') e.stopPropagation(); });

    const tabBtn = (id: Tab, label: string) =>
      `<button type="button" data-uap-tab="${id}" style="padding:6px 12px;border-radius:6px;border:1px solid ${this.tab === id ? '#d4af37' : '#334155'};background:${this.tab === id ? '#423212' : '#1e293b'};color:${this.tab === id ? '#fde68a' : '#cbd5e1'};cursor:pointer;font-weight:600">${label}</button>`;

    const tierLabel = this.opts.extras.policy.enabled
      ? (this.opts.extras.policy.isVip ? tx('Tier: VIP Active', 'Hạng: VIP Đang bật') : tx('Tier: Free (upgrade VIP for locked features)', 'Hạng: Miễn phí (nâng VIP để mở khóa)'))
      : tx('Disabled by server administrator', 'Đã tắt bởi quản trị viên máy chủ');

    wrap.innerHTML = `
      <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:10px;border-bottom:1px solid rgba(212,175,55,0.28);padding-bottom:8px">
        <div>
          <div style="font-size:16px;font-weight:700;color:#f4d06f">${tx('Ultimate Autoplay', 'Autoplay Toàn Diện')}</div>
          <div style="font-size:11px;color:#94a3b8">${tierLabel} • ${tx('Shortcut Alt+Z', 'Phím tắt Alt+Z')}</div>
        </div>
        <button type="button" id="uap-close" style="background:transparent;border:1px solid #475569;color:#cbd5e1;border-radius:6px;padding:3px 9px;cursor:pointer">×</button>
      </div>
      <div style="display:flex;gap:8px;margin-bottom:12px;flex-wrap:wrap">
        ${tabBtn('survival', tx('Presets & Survival', 'Chế độ & Sinh tồn'))}
        ${tabBtn('combat', tx('Combat, Sparkles & Patrol', 'Chiến đấu, Vệt sáng & Tuần tra'))}
        ${tabBtn('stats', tx('Live Stats', 'Thống kê trực tiếp'))}
      </div>
      <div id="uap-tab-body"></div>
      <div style="display:flex;justify-content:space-between;align-items:center;gap:8px;margin-top:14px;border-top:1px solid rgba(255,255,255,0.08);padding-top:10px;flex-wrap:wrap">
        <button type="button" id="uap-autopick-skills" style="${btnStyle('#1d4ed8')}">${tx('Auto-Pick Best Skills', 'Tự chọn Skill tối ưu')}</button>
        <div style="display:flex;gap:8px">
          <button type="button" id="uap-go-town" style="${btnStyle('#334155')}">${tx('Trip to Town Now', 'Về thành ngay')}</button>
          <button type="button" id="uap-save" style="${btnStyle('#926213')}">${tx('Save & Apply', 'Lưu & Áp dụng')}</button>
        </div>
      </div>
    `;

    document.body.appendChild(wrap);
    this.panel = wrap;

    const body = wrap.querySelector<HTMLElement>('#uap-tab-body')!;
    if (this.tab === 'survival') body.innerHTML = this.renderSurvivalHtml();
    else if (this.tab === 'combat') body.innerHTML = this.renderCombatHtml();
    else body.innerHTML = `<div id="uap-stats-body">${this.renderStatsHtml()}</div><div style="margin-top:10px"><button type="button" id="uap-reset-stats" style="${btnStyle('#475569')}">${tx('Reset Stats', 'Đặt lại thống kê')}</button></div>`;

    this.bindPanelEvents(wrap);
  }

  private renderSurvivalHtml(): string {
    const d = this.draft;
    const pot = this.badge('manaSdPotions');
    const surv = this.badge('survival');
    const pres = this.badge('presetsStats');

    const presetCard = (id: Exclude<UltimatePreset, 'custom'>, title: string, desc: string, locked: boolean) => {
      const active = d.preset === id;
      return `<button type="button" data-uap-preset="${id}"${locked ? ' disabled' : ''} style="flex:1;min-width:140px;text-align:left;padding:8px 10px;border-radius:7px;border:1px solid ${active ? '#f4d06f' : '#334155'};background:${active ? 'rgba(212,175,55,0.16)' : '#161b26'};color:#f8fafc;cursor:${locked ? 'not-allowed' : 'pointer'};opacity:${locked ? '0.55' : '1'}">
        <div style="font-weight:700;color:${active ? '#fde68a' : '#e2e8f0'}">${title}</div>
        <div style="font-size:11px;color:#94a3b8;margin-top:2px">${desc}</div>
      </button>`;
    };

    return `
      <div style="margin-bottom:10px">
        <div style="font-weight:600;color:#e7c873;margin-bottom:6px">${tx('1-Click Presets', 'Cấu hình nhanh')}${pres.tag}</div>
        <div style="display:flex;gap:8px;flex-wrap:wrap">
          ${presetCard('balanced', tx('Balanced', 'Cân bằng'), tx('Auto Mana 30%, Jewel Bank, town restock', 'Tự uống Mana 30%, gửi ngọc, về thành'), pres.disabled)}
          ${presetCard('afkFarm', tx('AFK Farm', 'Cày AFK'), tx('Mana+SD, Patrol, Auto-return on death', 'Mana+SD, Tuần tra, Tự quay lại khi chết'), pres.disabled)}
          ${presetCard('bossHunt', tx('Boss Hunt', 'Săn Boss'), tx('Aggro-first, Boss Trail, Rare drops', 'Ưu tiên quái đánh mình, Vệt Boss, Nhặt đồ hiếm'), pres.disabled)}
        </div>
      </div>
      <div style="display:grid;gap:7px">
        ${checkboxRow('useManaPotion', tx('Auto-drink Mana Potion', 'Tự uống bình Mana') + pot.tag, d.useManaPotion, pot.disabled)}
        ${numberRow('manaThreshold', tx('Drink Mana Potion when MP <= (%)', 'Uống Mana khi MP <= (%)'), d.manaThreshold, 1, 99, pot.disabled)}
        ${numberRow('manaPotionsToBuy', tx('Mana Potions to buy in town (0 = skip)', 'Số bình Mana mua ở thành (0 = bỏ qua)'), d.manaPotionsToBuy, 0, 255, pot.disabled)}
        ${checkboxRow('useSdPotion', tx('Auto-drink SD Potion', 'Tự uống bình SD') + pot.tag, d.useSdPotion, pot.disabled)}
        ${numberRow('sdThreshold', tx('Drink SD Potion when SD <= (%)', 'Uống SD khi SD <= (%)'), d.sdThreshold, 1, 99, pot.disabled)}
        ${numberRow('retreatHpPercent', tx('Emergency town retreat HP <= % (0 HP pots, 0 = off)', 'Chạy về thành khẩn cấp HP <= % (hết máu, 0 = tắt)') + surv.tag, d.retreatHpPercent, 0, 90, surv.disabled)}
        ${checkboxRow('autoReturnAfterDeath', tx('Auto-return to spot & resume after death', 'Tự quay lại bãi và bật Auto sau khi chết') + surv.tag, d.autoReturnAfterDeath, surv.disabled)}
        ${numberRow('stopAfterMinutes', tx('Stop Autoplay after (minutes, 0 = never)', 'Tự dừng sau (phút, 0 = không dừng)'), d.stopAfterMinutes, 0, 1440, false)}
        ${checkboxRow('alertOnDeath', tx('Alert on death', 'Cảnh báo khi chết'), d.alertOnDeath, false)}
        ${checkboxRow('alertOnBagFull', tx('Alert when bag is full', 'Cảnh báo khi đầy túi'), d.alertOnBagFull, false)}
        ${checkboxRow('showUltimateHud', tx('Show floating Ultimate HUD bar', 'Hiện thanh trạng thái Ultimate nổi'), d.showUltimateHud, false)}
      </div>
    `;
  }

  private renderCombatHtml(): string {
    const d = this.draft;
    const spark = this.badge('sparkleGuide');
    const boss = this.badge('bossFocus');
    const targ = this.badge('targetPriority');
    const jwl = this.badge('jewelDeposit');
    const pat = this.badge('patrol');

    const spotsHtml = d.patrolSpots.length === 0
      ? `<span style="color:#94a3b8">${tx('No patrol spots recorded yet.', 'Chưa lưu điểm tuần tra nào.')}</span>`
      : d.patrolSpots.map((s, idx) => `<span style="display:inline-block;padding:2px 7px;margin:2px;border-radius:4px;background:#1e293b;border:1px solid #334155">#${idx + 1} ${this.opts.mapName(s.map)} (${s.x}, ${s.y})</span>`).join('');

    return `
      <div style="display:grid;gap:7px">
        ${checkboxRow('sparkleGuide', tx('3D Ground Sparkle Trail & Hunting Ring', 'Vệt sáng dẫn đường 3D & Vòng phạm vi') + spark.tag, d.sparkleGuide, spark.disabled)}
        ${checkboxRow('bossFocus', tx('World Boss Sparkle Guide & Spawn Alert', 'Dẫn đường Boss thế giới & Cảnh báo Boss') + boss.tag, d.bossFocus, boss.disabled)}
        <label style="display:flex;align-items:center;justify-content:space-between;gap:10px">
          <span>${tx('Target priority', 'Ưu tiên mục tiêu')}${targ.tag}</span>
          <select id="uap-target-priority"${targ.disabled ? ' disabled' : ''} style="background:#0f172a;color:#f8fafc;border:1px solid #475569;border-radius:5px;padding:4px 8px">
            <option value="nearest"${d.targetPriority === 'nearest' ? ' selected' : ''}>${tx('Nearest monster', 'Quái gần nhất')}</option>
            <option value="aggroFirst"${d.targetPriority === 'aggroFirst' ? ' selected' : ''}>${tx('Aggro / attacking me first', 'Quái đang đánh mình trước')}</option>
            <option value="lowestHp"${d.targetPriority === 'lowestHp' ? ' selected' : ''}>${tx('Lowest HP first', 'Quái ít máu nhất trước')}</option>
          </select>
        </label>
        ${checkboxRow('prioritizeRareDrops', tx('Prioritize Jewels, Ancient & Excellent drops', 'Ưu tiên nhặt Ngọc, Đồ Thần & Đồ Hoàn Hảo') + targ.tag, d.prioritizeRareDrops, targ.disabled)}
        ${checkboxRow('autoDepositJewels', tx('Auto-deposit jewels to Jewel Bank (F7) when bag is full', 'Tự gửi ngọc vào Ngân hàng ngọc (F7) khi đầy túi') + jwl.tag, d.autoDepositJewels, jwl.disabled)}
        ${checkboxRow('returnWhenBagFull', tx('Go to town when bag is still full', 'Về thành khi túi vẫn đầy'), d.returnWhenBagFull, false)}
        ${checkboxRow('returnWhenOutOfPotions', tx('Go to town when healing potions run out', 'Về thành khi hết bình máu'), d.returnWhenOutOfPotions, false)}
        ${numberRow('potionsToBuy', tx('Healing Potions to buy in town', 'Số bình máu mua ở thành'), d.potionsToBuy, 0, 255, false)}
        ${checkboxRow('sellJunk', tx('Sell junk items (plain weapons/armour) in town', 'Bán đồ rác (vũ khí/giáp thường) ở thành'), d.sellJunk, false)}
        <div style="margin-top:6px;padding-top:8px;border-top:1px solid rgba(255,255,255,0.08)">
          ${checkboxRow('patrolEnabled', tx('Enable multi-spot patrol when spawn is empty', 'Bật tuần tra nhiều điểm khi hết quái') + pat.tag, d.patrolEnabled, pat.disabled)}
          ${numberRow('patrolIdleSeconds', tx('Move to next spot after idle (seconds)', 'Chuyển điểm nếu không có quái sau (giây)'), d.patrolIdleSeconds, 5, 120, pat.disabled)}
          <div style="margin-top:6px;font-size:12px">
            <div style="margin-bottom:4px;color:#cbd5e1">${tx('Recorded patrol spots (same map, add 2+ spots):', 'Điểm tuần tra đã lưu (cùng bản đồ, thêm 2+ điểm):')}</div>
            <div id="uap-spots-list" style="margin-bottom:6px">${spotsHtml}</div>
            <div style="display:flex;gap:8px">
              <button type="button" id="uap-add-spot"${pat.disabled ? ' disabled' : ''} style="${btnStyle('#1e40af')}">${tx('+ Add Current Spot', '+ Lưu vị trí hiện tại')}</button>
              <button type="button" id="uap-clear-spots"${pat.disabled ? ' disabled' : ''} style="${btnStyle('#475569')}">${tx('Clear Spots', 'Xóa điểm')}</button>
            </div>
          </div>
        </div>
      </div>
    `;
  }

  private renderStatsHtml(): string {
    const s = this.opts.extras.stats;
    const row = (label: string, val: string) =>
      `<div style="display:flex;justify-content:space-between;padding:5px 8px;background:#161b26;border-radius:5px"><span style="color:#94a3b8">${label}</span><b>${val}</b></div>`;
    return `
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px">
        ${row(tx('Session time', 'Thời gian chạy'), formatDuration(s.elapsedMs))}
        ${row(tx('Kills (Kills/h)', 'Quái hạ (Quái/giờ)'), `${s.kills.toLocaleString()} (${perHour(s.kills, s.elapsedMs).toLocaleString()}/h)`)}
        ${row(tx('EXP gained', 'EXP nhận được'), s.expGained.toLocaleString())}
        ${row('EXP / hour', `${perHour(s.expGained, s.elapsedMs).toLocaleString()}/h`)}
        ${row(tx('Zen gained', 'Zen nhận được'), s.zenGained.toLocaleString())}
        ${row(tx('Rare drops picked', 'Đồ hiếm đã nhặt'), s.rarePickups.toLocaleString())}
        ${row(tx('Potions drunk (MP/SD)', 'Bình đã uống (MP/SD)'), s.potionsDrunk.toLocaleString())}
        ${row(tx('Jewel Bank deposits', 'Lần gửi ngọc tự động'), s.jewelDeposits.toLocaleString())}
        ${row(tx('Town trips', 'Số lần về thành'), s.townTrips.toLocaleString())}
      </div>
    `;
  }

  private captureDraftInputs(wrap: HTMLElement): void {
    const d = this.draft as unknown as Record<string, unknown>;
    for (const input of wrap.querySelectorAll<HTMLInputElement>('input[data-uap-key]')) {
      if (input.disabled) continue;
      const key = input.dataset.uapKey!;
      if (input.type === 'checkbox') d[key] = input.checked;
      else {
        const min = Number(input.min || 0);
        const max = Number(input.max || 9999);
        d[key] = Math.min(max, Math.max(min, Math.floor(Number(input.value) || 0)));
      }
    }
    const sel = wrap.querySelector<HTMLSelectElement>('#uap-target-priority');
    if (sel && !sel.disabled) this.draft.targetPriority = sel.value as TargetPriority;
  }

  private bindPanelEvents(wrap: HTMLElement): void {
    wrap.querySelector('#uap-close')?.addEventListener('click', () => this.close());

    for (const btn of wrap.querySelectorAll<HTMLButtonElement>('[data-uap-tab]')) {
      btn.addEventListener('click', () => {
        this.captureDraftInputs(wrap);
        this.tab = btn.dataset.uapTab as Tab;
        this.renderPanel();
      });
    }

    for (const btn of wrap.querySelectorAll<HTMLButtonElement>('[data-uap-preset]')) {
      btn.addEventListener('click', () => {
        const preset = btn.dataset.uapPreset as Exclude<UltimatePreset, 'custom'>;
        const applied = this.opts.extras.applyPreset(preset);
        this.draft = { ...applied, patrolSpots: [...applied.patrolSpots] };
        this.renderPanel();
        this.opts.message(tx(`Ultimate Autoplay preset applied: ${preset}.`, `Đã áp dụng chế độ Autoplay: ${preset}.`));
      });
    }

    for (const input of wrap.querySelectorAll<HTMLInputElement>('input[data-uap-key]')) {
      input.addEventListener('change', () => { this.draft.preset = 'custom'; });
    }

    wrap.querySelector('#uap-autopick-skills')?.addEventListener('click', () => {
      this.opts.onAutoPickSkills?.();
    });

    wrap.querySelector('#uap-add-spot')?.addEventListener('click', () => {
      this.captureDraftInputs(wrap);
      this.opts.extras.save({ ...this.draft });
      const spot = this.opts.extras.addPatrolSpot();
      this.draft.patrolSpots = [...this.opts.extras.settings.patrolSpots];
      if (spot) this.opts.message(tx(`Added patrol spot (${spot.x}, ${spot.y}).`, `Đã lưu điểm tuần tra (${spot.x}, ${spot.y}).`));
      this.renderPanel();
    });

    wrap.querySelector('#uap-clear-spots')?.addEventListener('click', () => {
      this.captureDraftInputs(wrap);
      this.opts.extras.clearPatrolSpots();
      this.draft.patrolSpots = [];
      this.renderPanel();
    });

    wrap.querySelector('#uap-reset-stats')?.addEventListener('click', () => {
      this.opts.extras.resetStats();
      this.renderPanel();
    });

    wrap.querySelector('#uap-go-town')?.addEventListener('click', () => {
      this.opts.extras.goToTown();
      this.close();
    });

    wrap.querySelector('#uap-save')?.addEventListener('click', () => {
      this.captureDraftInputs(wrap);
      this.opts.extras.save({ ...this.draft });
      this.updateHudContent();
      this.close();
      this.opts.message(tx('Ultimate Autoplay settings saved.', 'Đã lưu cài đặt Autoplay Toàn Diện.'));
    });
  }
}

function checkboxRow(key: string, label: string, checked: boolean, disabled: boolean): string {
  return `<label style="display:flex;align-items:center;justify-content:space-between;gap:10px;cursor:${disabled ? 'not-allowed' : 'pointer'};opacity:${disabled ? '0.6' : '1'}">
    <span>${label}</span>
    <input type="checkbox" data-uap-key="${key}"${checked ? ' checked' : ''}${disabled ? ' disabled' : ''} style="width:16px;height:16px;accent-color:#d4af37;cursor:${disabled ? 'not-allowed' : 'pointer'}"/>
  </label>`;
}

function numberRow(key: string, label: string, value: number, min: number, max: number, disabled: boolean): string {
  return `<label style="display:flex;align-items:center;justify-content:space-between;gap:10px;opacity:${disabled ? '0.6' : '1'}">
    <span>${label}</span>
    <input type="number" data-uap-key="${key}" value="${value}" min="${min}" max="${max}"${disabled ? ' disabled' : ''} style="width:76px;background:#0f172a;color:#f8fafc;border:1px solid #475569;border-radius:5px;padding:3px 6px;text-align:right"/>
  </label>`;
}

function btnStyle(bg: string): string {
  return `background:${bg};color:#f8fafc;border:1px solid rgba(255,255,255,0.2);border-radius:5px;padding:4px 9px;font-size:12px;cursor:pointer;font-weight:600`;
}
