// Web-side Extras and Ultimate Autoplay tabs mounted inside Window 1 (mu_helper_config)
// without modifying shared desktop RML/RCSS files in client/src/bin/Data/Interface/RmlUi/.

import { currentLanguage } from '../i18n.js';
import type { AutoPlayExtras, AutoPlayExtrasSettings, UltimateFeatureKey, UltimatePreset } from '../game/autoPlayExtras.js';
import type { TargetPriority } from '../game/autoPlay.js';

export type UltimateSection = 'survival' | 'combat' | 'stats';

export interface AutoPlayHnmoTabsHost {
  extras: AutoPlayExtras;
  mapName: (map: number) => string;
  onSelectTab: (tab: number) => void;
  onAutoPickSkills?: () => boolean;
  onGoToTown?: () => void;
  onMessage?: (text: string) => void;
  onDraftChange?: () => void;
}

const tx = (en: string, vi: string): string => (currentLanguage() === 'vi' ? vi : en);

export function formatDuration(ms: number): string {
  const totalSec = Math.floor(ms / 1000);
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

export function perHour(value: number, elapsedMs: number): number {
  if (elapsedMs < 5000) return 0;
  return Math.round((value * 3_600_000) / elapsedMs);
}

const HNMO_TABS_CSS = `
#panel.hnmo-hnmo-tab #box_extension,
#panel.hnmo-hnmo-tab #extension_used,
#panel.hnmo-hnmo-tab #extension_none { display: none !important; }
#panel:not(.hnmo-modern-helper) .hnmo-tab-btn {
  position: absolute; top: 28px; height: 18px;
  display: flex; align-items: center; justify-content: center;
  font-size: 9.5px; line-height: 1; font-weight: 600;
  border: 1px solid #6b5630; border-radius: 3px 3px 0 0;
  background: linear-gradient(180deg, #272219 0%, #141210 100%);
  color: #b8ad98; cursor: pointer; pointer-events: auto;
  box-sizing: border-box; z-index: 2; padding: 0 3px;
}
#panel:not(.hnmo-modern-helper) #tab_3 { left: 10px; width: 83px; }
#panel:not(.hnmo-modern-helper) #tab_4 { left: 96px; width: 82px; }
#panel:not(.hnmo-modern-helper) .hnmo-tab-btn.active {
  border-color: #d4af37;
  background: linear-gradient(180deg, #4b3917 0%, #241d0f 100%);
  color: #fde68a;
}
#panel:not(.hnmo-modern-helper) .hnmo-tab-label {
  position: static; display: block; width: auto; height: auto;
  pointer-events: none; white-space: nowrap; color: inherit; font-size: inherit;
}
#panel.hnmo-modern-helper #mhc_tabs { flex-wrap: wrap; gap: 2px; }
#panel.hnmo-modern-helper #mhc_tabs .mhc-tab-cell { flex: 1 0 30%; min-width: 58px; }
#panel:not(.hnmo-modern-helper) .hnmo-page {
  position: absolute; left: 12px; top: 73px; width: 166px; height: 309px;
  overflow-y: auto; overflow-x: hidden; box-sizing: border-box;
  padding: 5px 6px; background: rgba(0, 0, 0, 0.58);
  border: 1px solid #5a4b32; border-radius: 2px;
  color: #ece4d0; font-size: 9.5px; line-height: 1.25; pointer-events: auto;
}
#panel.hnmo-modern-helper .hnmo-page {
  position: relative; width: 100%; box-sizing: border-box;
  padding: 5px 6px; background: rgba(0, 0, 0, 0.36);
  border: 1px solid #5a4b32; border-radius: 3px;
  color: #ece4d0; font-size: 10px; line-height: 1.3; pointer-events: auto;
}
.hnmo-page.hidden { display: none !important; }
.hnmo-col { display: flex; flex-direction: column; gap: 5px; width: 100%; box-sizing: border-box; }
.hnmo-row {
  display: flex; align-items: center; justify-content: space-between;
  gap: 4px; width: 100%; min-width: 0; pointer-events: auto;
}
.hnmo-row.clickable { cursor: pointer; }
.hnmo-row.disabled { opacity: 0.78; cursor: not-allowed; }
.hnmo-label { flex: 1 1 auto; min-width: 0; white-space: normal; word-break: break-word; color: #ece4d0; }
.hnmo-page .checkbox-box { width: 13px; height: 13px; flex: 0 0 auto; pointer-events: auto; cursor: pointer; }
.hnmo-num {
  width: 40px; flex: 0 0 auto; box-sizing: border-box;
  background: #0f172a; color: #f8fafc; border: 1px solid #5a4b32;
  border-radius: 2px; padding: 1px 3px; font-size: 9.5px; text-align: right; pointer-events: auto;
}
.hnmo-sel {
  width: 100%; box-sizing: border-box; background: #0f172a; color: #f8fafc;
  border: 1px solid #5a4b32; border-radius: 2px; padding: 2px 4px;
  font-size: 9.5px; pointer-events: auto;
}
.hnmo-hint {
  margin-top: 2px; padding-top: 4px; border-top: 1px solid rgba(255, 255, 255, 0.1);
  color: #a89f8c; font-size: 8.8px; line-height: 1.25; white-space: normal;
}
.hnmo-tier { font-size: 9px; color: #f4d06f; font-weight: 600; white-space: normal; }
.hnmo-switcher { display: flex; gap: 3px; width: 100%; }
.hnmo-sw-btn {
  flex: 1 1 0; min-width: 0; padding: 3px 2px; font-size: 9px; font-weight: 600;
  border: 1px solid #475569; border-radius: 3px; background: #1e293b; color: #cbd5e1;
  cursor: pointer; pointer-events: auto; text-align: center;
}
.hnmo-sw-btn.active { border-color: #d4af37; background: #423212; color: #fde68a; }
.hnmo-preset {
  width: 100%; text-align: left; padding: 4px 6px; border-radius: 3px;
  border: 1px solid #475569; background: #161b26; color: #f8fafc;
  cursor: pointer; pointer-events: auto; font-size: 9.2px;
}
.hnmo-preset.active { border-color: #f4d06f; background: rgba(212, 175, 55, 0.18); }
.hnmo-preset:disabled { opacity: 0.55; cursor: not-allowed; }
.hnmo-badge {
  display: inline-block; margin-left: 3px; padding: 0 3px; border-radius: 2px;
  font-size: 8px; font-weight: 700; vertical-align: middle;
}
.hnmo-badge-vip { background: #78350f; color: #fde68a; }
.hnmo-badge-hvip { background: #581c87; color: #f3e8ff; border: 1px solid rgba(212, 175, 55, 0.55); }
.hnmo-badge-off { background: #334155; color: #cbd5e1; }
.hnmo-btn {
  width: 100%; box-sizing: border-box; padding: 3px 6px; font-size: 9.5px; font-weight: 600;
  border: 1px solid rgba(255, 255, 255, 0.22); border-radius: 3px;
  color: #f8fafc; cursor: pointer; pointer-events: auto; text-align: center;
}
.hnmo-btn:disabled { opacity: 0.55; cursor: not-allowed; }
.hnmo-stat-row {
  display: flex; justify-content: space-between; align-items: center; gap: 4px;
  padding: 3px 5px; background: #161b26; border-radius: 3px; font-size: 9.2px;
}
.hnmo-stat-row span { color: #94a3b8; }
.hnmo-actions {
  display: flex; flex-direction: column; gap: 4px; margin-top: 3px;
  padding-top: 4px; border-top: 1px solid rgba(255, 255, 255, 0.1);
}
`;

export class AutoPlayHnmoTabs {
  private tab3Btn: HTMLElement | null = null;
  private tab4Btn: HTMLElement | null = null;
  private tab3Label: HTMLElement | null = null;
  private tab4Label: HTMLElement | null = null;
  private extrasPage: HTMLElement | null = null;
  private ultimatePage: HTMLElement | null = null;
  private panelEl: HTMLElement | null = null;
  private extensionTabEl: HTMLElement | null = null;

  private draft: AutoPlayExtrasSettings;
  private section: UltimateSection = 'survival';

  constructor(private readonly host: AutoPlayHnmoTabsHost) {
    this.draft = this.cloneSettings();
  }

  private cloneSettings(): AutoPlayExtrasSettings {
    return { ...this.host.extras.settings, patrolSpots: [...this.host.extras.settings.patrolSpots] };
  }

  resetDraft(): void {
    this.draft = this.cloneSettings();
  }

  currentDraft(): AutoPlayExtrasSettings {
    this.captureInputs();
    return { ...this.draft, patrolSpots: [...this.draft.patrolSpots] };
  }

  mount(configRoot: ShadowRoot | HTMLElement): void {
    const panel = configRoot.querySelector<HTMLElement>('#panel');
    if (!panel) return;
    this.panelEl = panel;
    const style = document.createElement('style');
    style.id = 'hnmo-helper-style';
    style.textContent = HNMO_TABS_CSS;
    configRoot.appendChild(style);

    const extBox = panel.querySelector<HTMLElement>('#box_extension');
    this.extensionTabEl = extBox?.parentElement ?? null;

    const tabsRow = panel.querySelector<HTMLElement>('#mhc_tabs');
    const bodyEl = panel.querySelector<HTMLElement>('#mhc_body');
    const modern = Boolean(tabsRow && bodyEl);
    panel.classList.toggle('hnmo-modern-helper', modern);

    if (modern && tabsRow && bodyEl) {
      const makeModernTab = (tabIndex: number, id: string) => {
        const cell = document.createElement('div');
        cell.className = 'mhc-tab-cell hnmo-tab-cell';
        const btn = document.createElement('div');
        btn.id = `tab_${tabIndex}`;
        btn.className = 'mh-tab hnmo-tab-btn';
        btn.addEventListener('click', () => this.host.onSelectTab(tabIndex));
        const label = document.createElement('span');
        label.id = `${id}_label`;
        label.className = 'mh-text mh-center mh-tab-label hnmo-tab-label';
        cell.append(btn, label);
        tabsRow.appendChild(cell);
        return { btn, label };
      };
      const t3 = makeModernTab(3, 'tab_3');
      const t4 = makeModernTab(4, 'tab_4');
      this.tab3Btn = t3.btn;
      this.tab3Label = t3.label;
      this.tab4Btn = t4.btn;
      this.tab4Label = t4.label;
    } else {
      const makeLegacyTab = (tabIndex: number, id: string) => {
        const btn = document.createElement('div');
        btn.id = id;
        btn.className = 'mh-tab hnmo-tab-btn';
        btn.addEventListener('click', () => this.host.onSelectTab(tabIndex));
        const label = document.createElement('span');
        label.id = `${id}_label`;
        label.className = 'hnmo-tab-label';
        btn.appendChild(label);
        return { btn, label };
      };
      const t3 = makeLegacyTab(3, 'tab_3');
      const t4 = makeLegacyTab(4, 'tab_4');
      this.tab3Btn = t3.btn;
      this.tab3Label = t3.label;
      this.tab4Btn = t4.btn;
      this.tab4Label = t4.label;
      panel.append(t3.btn, t4.btn);
    }

    const extrasPage = document.createElement('div');
    extrasPage.id = 'hnmo_page_extras';
    extrasPage.className = 'hnmo-page scroll-pane hidden';
    extrasPage.addEventListener('wheel', (e) => e.stopPropagation());

    const ultimatePage = document.createElement('div');
    ultimatePage.id = 'hnmo_page_ultimate';
    ultimatePage.className = 'hnmo-page scroll-pane hidden';
    ultimatePage.addEventListener('wheel', (e) => e.stopPropagation());

    if (modern && bodyEl) bodyEl.append(extrasPage, ultimatePage);
    else panel.append(extrasPage, ultimatePage);

    this.extrasPage = extrasPage;
    this.ultimatePage = ultimatePage;
    this.syncLabels();
    this.renderExtras();
    this.renderUltimate();
  }

  unmount(): void {
    this.tab3Btn = this.tab4Btn = null;
    this.tab3Label = this.tab4Label = null;
    this.extrasPage = this.ultimatePage = null;
    this.panelEl = this.extensionTabEl = null;
  }

  syncLabels(): void {
    if (this.tab3Label) this.tab3Label.textContent = tx('Extras', 'Tuỳ chọn thêm');
    if (this.tab4Label) this.tab4Label.textContent = 'Ultimate';
  }

  syncVisibility(activeTab: number): void {
    this.syncLabels();
    const isExtras = activeTab === 3;
    const isUltimate = activeTab === 4;
    this.tab3Btn?.classList.toggle('active', isExtras);
    this.tab3Label?.classList.toggle('active', isExtras);
    this.tab4Btn?.classList.toggle('active', isUltimate);
    this.tab4Label?.classList.toggle('active', isUltimate);
    this.extrasPage?.classList.toggle('hidden', !isExtras);
    this.ultimatePage?.classList.toggle('hidden', !isUltimate);
    this.panelEl?.classList.toggle('hnmo-hnmo-tab', activeTab > 2);
    if (this.extensionTabEl) {
      this.extensionTabEl.style.display = activeTab > 2 ? 'none' : '';
    }
  }

  refreshContent(activeTab: number): void {
    this.syncLabels();
    if (activeTab === 3) this.renderExtras();
    else if (activeTab === 4) this.renderUltimate();
  }

  refreshStatsOnly(): void {
    if (!this.ultimatePage || this.section !== 'stats') return;
    const body = this.ultimatePage.querySelector<HTMLElement>('#uap-stats-body');
    if (body) body.innerHTML = this.renderStatsHtml();
  }

  captureInputs(): void {
    const pages = [this.extrasPage, this.ultimatePage];
    const d = this.draft as unknown as Record<string, unknown>;
    for (const page of pages) {
      if (!page) continue;
      for (const input of page.querySelectorAll<HTMLInputElement>('input[data-extras-num]')) {
        if (input.disabled) continue;
        const key = input.dataset.extrasNum!;
        const min = Number(input.dataset.min ?? 0);
        const max = Number(input.dataset.max ?? 9999);
        d[key] = Math.min(max, Math.max(min, Math.floor(Number(input.value) || 0)));
      }
      for (const input of page.querySelectorAll<HTMLInputElement>('input[data-uap-key]')) {
        if (input.disabled) continue;
        const key = input.dataset.uapKey!;
        if (input.type === 'checkbox') d[key] = input.checked;
        else {
          const min = Number(input.min || 0);
          const max = Number(input.max || 9999);
          d[key] = Math.min(max, Math.max(min, Math.floor(Number(input.value) || 0)));
        }
      }
      const sel = page.querySelector<HTMLSelectElement>('#uap-target-priority');
      if (sel && !sel.disabled) this.draft.targetPriority = sel.value as TargetPriority;
    }
  }

  private badge(key: UltimateFeatureKey): { disabled: boolean; tag: string } {
    if (!this.host.extras.policy.enabled) {
      return { disabled: true, tag: `<span class="hnmo-badge hnmo-badge-off">${tx('OFF', 'TẮT')}</span>` };
    }
    if (this.host.extras.isFeatureAllowed(key)) return { disabled: false, tag: '' };
    if (this.host.extras.isFeatureVipLocked(key)) {
      const req = this.host.extras.featureRequiredVipLevel(key);
      if (req >= 2) {
        return { disabled: true, tag: `<span class="hnmo-badge hnmo-badge-hvip">${tx('VIP 2+', 'VIP CAO')}</span>` };
      }
      return { disabled: true, tag: `<span class="hnmo-badge hnmo-badge-vip">${tx('VIP ONLY', 'CHỈ VIP')}</span>` };
    }
    return { disabled: true, tag: `<span class="hnmo-badge hnmo-badge-off">${tx('ADMIN OFF', 'ĐÃ TẮT')}</span>` };
  }

  private renderExtras(): void {
    const root = this.extrasPage;
    if (!root) return;
    const s = this.draft;
    const town = this.badge('extrasTown');
    const boolRow = (key: keyof AutoPlayExtrasSettings, label: string, disabled: boolean) => {
      const checked = Boolean(s[key]);
      return `<div class="hnmo-row${disabled ? ' disabled' : ' clickable'}" data-extras-toggle="${key}">
        <span class="hnmo-label">${label}</span>
        <div class="checkbox-box${checked ? ' checked' : ''}"></div>
      </div>`;
    };
    const numRow = (key: keyof AutoPlayExtrasSettings, label: string, min: number, max: number, disabled: boolean) =>
      `<label class="hnmo-row${disabled ? ' disabled' : ''}">
        <span class="hnmo-label">${label}</span>
        <input type="text" class="hnmo-num" data-extras-num="${key}" data-min="${min}" data-max="${max}" value="${Number(s[key])}" maxlength="${String(max).length}"${disabled ? ' disabled' : ''}/>
      </label>`;

    root.innerHTML = `<div class="hnmo-col">
      ${boolRow('returnWhenBagFull', tx('Back to town when the bag is full', 'Về thành khi túi đồ đầy') + town.tag, town.disabled)}
      ${boolRow('returnWhenOutOfPotions', tx('Back to town when the healing potions run out', 'Về thành khi hết bình máu') + town.tag, town.disabled)}
      ${numRow('potionsToBuy', tx('Healing potions to buy', 'Số bình máu cần mua'), 0, 255, town.disabled)}
      ${boolRow('sellJunk', tx('Sell junk (plain weapons and armour) in town', 'Bán đồ rác (vũ khí/giáp thường) ở thành') + town.tag, town.disabled)}
      <button type="button" id="uap-extras-go-town" class="hnmo-btn" style="background:#334155"${town.disabled ? ' disabled' : ''}>${tx('Trip to Town Now', 'Về thành ngay')}${town.tag}</button>
      <div class="hnmo-col" style="gap:5px;padding-top:4px;border-top:1px solid rgba(255,255,255,0.1)">
        ${numRow('stopAfterMinutes', tx('Stop after (minutes, 0 = never)', 'Tự dừng sau (phút, 0 = tắt)'), 0, 1440, false)}
        ${boolRow('alertOnDeath', tx('Alert when the character dies', 'Cảnh báo khi nhân vật chết'), false)}
        ${boolRow('alertOnBagFull', tx('Alert when the bag is full', 'Cảnh báo khi túi đồ đầy'), false)}
      </div>
      <div class="hnmo-hint">${tx('Trips go to the potion seller in Lorencia and back to the spot where auto play was hunting.', 'Chuyến về thành sẽ gặp NPC bán máu ở Lorencia rồi tự quay lại bãi đánh.')}</div>
    </div>`;

    for (const row of root.querySelectorAll<HTMLElement>('[data-extras-toggle]')) {
      row.addEventListener('click', () => {
        if (row.classList.contains('disabled')) return;
        this.captureInputs();
        const key = row.dataset.extrasToggle as keyof AutoPlayExtrasSettings;
        (this.draft as unknown as Record<string, unknown>)[key] = !this.draft[key];
        this.draft.preset = 'custom';
        this.renderExtras();
        this.host.onDraftChange?.();
      });
    }
    root.querySelector('#uap-extras-go-town')?.addEventListener('click', () => {
      if (this.badge('extrasTown').disabled) return;
      this.host.extras.goToTown();
      this.host.onGoToTown?.();
    });
    for (const input of root.querySelectorAll<HTMLInputElement>('input[data-extras-num]')) {
      input.addEventListener('input', () => { input.value = input.value.replace(/\D/g, ''); });
      input.addEventListener('change', () => { this.captureInputs(); this.draft.preset = 'custom'; this.host.onDraftChange?.(); });
    }
  }

  private renderUltimate(): void {
    const root = this.ultimatePage;
    if (!root) return;
    const p = this.host.extras.policy;
    const vipLevel = p.vipLevel ?? (p.isVip ? 1 : 0);
    const town = this.badge('extrasTown');
    const tierLabel = !p.enabled
      ? tx('Disabled by server administrator', 'Đã tắt bởi quản trị viên máy chủ')
      : vipLevel >= 2
        ? tx(`Tier: Higher VIP (Lv ${vipLevel} — All Unlocked)`, `Hạng: VIP Cao cấp (Cấp ${vipLevel} — Mở toàn bộ)`)
        : vipLevel >= 1
          ? tx('Tier: VIP (Extras open · Ultimate needs VIP 2+)', 'Hạng: VIP (Mở Extras · Ultimate cần VIP 2+)')
          : tx('Tier: Free (Standard only · Extras=VIP, Ultimate=VIP 2+)', 'Hạng: Miễn phí (Cơ bản · Extras=VIP, Ultimate=VIP 2+)');

    const swBtn = (id: UltimateSection, label: string) =>
      `<button type="button" class="hnmo-sw-btn${this.section === id ? ' active' : ''}" data-uap-tab="${id}">${label}</button>`;

    const bodyHtml = this.section === 'survival'
      ? this.renderSurvivalHtml()
      : this.section === 'combat'
        ? this.renderCombatHtml()
        : `<div id="uap-stats-body" class="hnmo-col">${this.renderStatsHtml()}</div>
           <button type="button" id="uap-reset-stats" class="hnmo-btn" style="background:#475569">${tx('Reset Stats', 'Đặt lại thống kê')}</button>`;

    root.innerHTML = `<div class="hnmo-col">
      <div class="hnmo-tier">${tierLabel}</div>
      <div class="hnmo-switcher">
        ${swBtn('survival', tx('Survival', 'Sinh tồn'))}
        ${swBtn('combat', tx('Combat', 'Chiến đấu'))}
        ${swBtn('stats', tx('Stats', 'Thống kê'))}
      </div>
      <div id="hnmo-uap-section" class="hnmo-col">${bodyHtml}</div>
      <div class="hnmo-actions">
        <button type="button" id="uap-autopick-skills" class="hnmo-btn" style="background:#1d4ed8">${tx('Auto-Pick Best Skills', 'Tự chọn Skill tối ưu')}</button>
        <button type="button" id="uap-go-town" class="hnmo-btn" style="background:#334155"${town.disabled ? ' disabled' : ''}>${tx('Trip to Town Now', 'Về thành ngay')}${town.tag}</button>
      </div>
    </div>`;

    this.bindUltimateEvents(root);
  }

  private renderSurvivalHtml(): string {
    const d = this.draft;
    const pot = this.badge('manaSdPotions');
    const surv = this.badge('survival');
    const pres = this.badge('presetsStats');

    const presetCard = (id: Exclude<UltimatePreset, 'custom'>, title: string, desc: string, locked: boolean) =>
      `<button type="button" class="hnmo-preset${d.preset === id ? ' active' : ''}" data-uap-preset="${id}"${locked ? ' disabled' : ''}>
        <div style="font-weight:700;color:${d.preset === id ? '#fde68a' : '#e2e8f0'}">${title}</div>
        <div style="font-size:8.5px;color:#94a3b8">${desc}</div>
      </button>`;

    return `
      <div class="hnmo-col" style="gap:3px">
        <div style="font-weight:600;color:#e7c873">${tx('1-Click Presets', 'Cấu hình nhanh')}${pres.tag}</div>
        ${presetCard('balanced', tx('Balanced', 'Cân bằng'), tx('Mana 30%, Jewel Bank, town restock', 'Mana 30%, gửi ngọc, về thành'), pres.disabled)}
        ${presetCard('afkFarm', tx('AFK Farm', 'Cày AFK'), tx('Mana+SD, Patrol, Auto-return on death', 'Mana+SD, Tuần tra, Tự quay lại'), pres.disabled)}
        ${presetCard('bossHunt', tx('Boss Hunt', 'Săn Boss'), tx('Aggro-first, Boss Trail, Rare drops', 'Ưu tiên đánh lại, Vệt Boss, Đồ hiếm'), pres.disabled)}
      </div>
      ${uapCheckboxRow('useManaPotion', tx('Auto-drink Mana Potion', 'Tự uống bình Mana') + pot.tag, d.useManaPotion, pot.disabled)}
      ${uapNumberRow('manaThreshold', tx('Mana threshold MP <= (%)', 'Uống Mana khi MP <= (%)'), d.manaThreshold, 1, 99, pot.disabled)}
      ${uapNumberRow('manaPotionsToBuy', tx('Mana Potions to buy', 'Số bình Mana mua'), d.manaPotionsToBuy, 0, 255, pot.disabled)}
      ${uapCheckboxRow('useSdPotion', tx('Auto-drink SD Potion', 'Tự uống bình SD') + pot.tag, d.useSdPotion, pot.disabled)}
      ${uapNumberRow('sdThreshold', tx('SD threshold SD <= (%)', 'Uống SD khi SD <= (%)'), d.sdThreshold, 1, 99, pot.disabled)}
      ${uapNumberRow('retreatHpPercent', tx('Emergency retreat HP <= %', 'Chạy về thành HP <= %') + surv.tag, d.retreatHpPercent, 0, 90, surv.disabled)}
      ${uapCheckboxRow('autoReturnAfterDeath', tx('Auto-return after death', 'Tự quay lại sau khi chết') + surv.tag, d.autoReturnAfterDeath, surv.disabled)}
      ${uapCheckboxRow('showUltimateHud', tx('Show floating Ultimate HUD bar', 'Hiện thanh Ultimate nổi'), d.showUltimateHud, false)}
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
      ? `<span style="color:#94a3b8">${tx('No patrol spots saved.', 'Chưa lưu điểm tuần tra.')}</span>`
      : d.patrolSpots.map((s, idx) => `<div style="padding:1px 4px;margin-top:2px;background:#1e293b;border:1px solid #334155;border-radius:2px">#${idx + 1} ${this.host.mapName(s.map)} (${s.x}, ${s.y})</div>`).join('');

    return `
      ${uapCheckboxRow('sparkleGuide', tx('3D Sparkle Trail & Ring', 'Vệt sáng 3D & Vòng phạm vi') + spark.tag, d.sparkleGuide, spark.disabled)}
      ${uapCheckboxRow('bossFocus', tx('World Boss Sparkle Guide', 'Dẫn đường Boss thế giới') + boss.tag, d.bossFocus, boss.disabled)}
      <div class="hnmo-col" style="gap:2px">
        <span class="hnmo-label">${tx('Target priority', 'Ưu tiên mục tiêu')}${targ.tag}</span>
        <select id="uap-target-priority" class="hnmo-sel"${targ.disabled ? ' disabled' : ''}>
          <option value="nearest"${d.targetPriority === 'nearest' ? ' selected' : ''}>${tx('Nearest monster', 'Quái gần nhất')}</option>
          <option value="aggroFirst"${d.targetPriority === 'aggroFirst' ? ' selected' : ''}>${tx('Aggro / attacking me first', 'Quái đang đánh mình trước')}</option>
          <option value="lowestHp"${d.targetPriority === 'lowestHp' ? ' selected' : ''}>${tx('Lowest HP first', 'Quái ít máu nhất trước')}</option>
        </select>
      </div>
      ${uapCheckboxRow('prioritizeRareDrops', tx('Prioritize Rare Drops', 'Ưu tiên nhặt đồ hiếm') + targ.tag, d.prioritizeRareDrops, targ.disabled)}
      ${uapCheckboxRow('autoDepositJewels', tx('Auto-deposit Jewels (F7)', 'Tự gửi Ngọc (F7)') + jwl.tag, d.autoDepositJewels, jwl.disabled)}
      <div class="hnmo-col" style="gap:4px;padding-top:4px;border-top:1px solid rgba(255,255,255,0.1)">
        ${uapCheckboxRow('patrolEnabled', tx('Multi-spot patrol', 'Tuần tra nhiều điểm') + pat.tag, d.patrolEnabled, pat.disabled)}
        ${uapNumberRow('patrolIdleSeconds', tx('Switch spot idle (s)', 'Đổi điểm sau (giây)'), d.patrolIdleSeconds, 5, 120, pat.disabled)}
        <div id="uap-spots-list">${spotsHtml}</div>
        <button type="button" id="uap-add-spot" class="hnmo-btn" style="background:#1e40af"${pat.disabled ? ' disabled' : ''}>${tx('+ Add Current Spot', '+ Lưu vị trí hiện tại')}</button>
        <button type="button" id="uap-clear-spots" class="hnmo-btn" style="background:#475569"${pat.disabled ? ' disabled' : ''}>${tx('Clear Spots', 'Xóa điểm')}</button>
      </div>
    `;
  }

  private renderStatsHtml(): string {
    const s = this.host.extras.stats;
    const row = (label: string, val: string) =>
      `<div class="hnmo-stat-row"><span>${label}</span><b>${val}</b></div>`;
    return `
      ${row(tx('Session time', 'Thời gian chạy'), formatDuration(s.elapsedMs))}
      ${row(tx('Kills (Kills/h)', 'Quái hạ (Quái/giờ)'), `${s.kills.toLocaleString()} (${perHour(s.kills, s.elapsedMs).toLocaleString()}/h)`)}
      ${row(tx('EXP gained', 'EXP nhận được'), s.expGained.toLocaleString())}
      ${row('EXP / hour', `${perHour(s.expGained, s.elapsedMs).toLocaleString()}/h`)}
      ${row(tx('Zen gained', 'Zen nhận được'), s.zenGained.toLocaleString())}
      ${row(tx('Rare drops', 'Đồ hiếm nhặt'), s.rarePickups.toLocaleString())}
      ${row(tx('Potions (MP/SD)', 'Bình uống (MP/SD)'), s.potionsDrunk.toLocaleString())}
      ${row(tx('Jewel deposits', 'Lần gửi ngọc'), s.jewelDeposits.toLocaleString())}
      ${row(tx('Town trips', 'Số lần về thành'), s.townTrips.toLocaleString())}
    `;
  }

  private bindUltimateEvents(root: HTMLElement): void {
    for (const btn of root.querySelectorAll<HTMLButtonElement>('[data-uap-tab]')) {
      btn.addEventListener('click', () => {
        this.captureInputs();
        this.section = btn.dataset.uapTab as UltimateSection;
        this.renderUltimate();
      });
    }
    for (const btn of root.querySelectorAll<HTMLButtonElement>('[data-uap-preset]')) {
      btn.addEventListener('click', () => {
        const preset = btn.dataset.uapPreset as Exclude<UltimatePreset, 'custom'>;
        const applied = this.host.extras.applyPreset(preset);
        this.draft = { ...applied, patrolSpots: [...applied.patrolSpots] };
        this.renderExtras();
        this.renderUltimate();
        this.host.onMessage?.(tx(`Ultimate Autoplay preset applied: ${preset}.`, `Đã áp dụng chế độ Autoplay: ${preset}.`));
      });
    }
    for (const input of root.querySelectorAll<HTMLInputElement>('input[data-uap-key]')) {
      input.addEventListener('input', () => {
        if (input.type !== 'checkbox') input.value = input.value.replace(/\D/g, '');
      });
      input.addEventListener('change', () => {
        this.captureInputs();
        this.draft.preset = 'custom';
      });
    }
    root.querySelector<HTMLSelectElement>('#uap-target-priority')?.addEventListener('change', () => {
      this.captureInputs();
      this.draft.preset = 'custom';
    });
    root.querySelector('#uap-autopick-skills')?.addEventListener('click', () => {
      this.host.onAutoPickSkills?.();
    });
    root.querySelector('#uap-add-spot')?.addEventListener('click', () => {
      this.captureInputs();
      this.host.extras.save({ ...this.draft, patrolSpots: [...this.draft.patrolSpots] });
      const spot = this.host.extras.addPatrolSpot();
      this.draft.patrolSpots = [...this.host.extras.settings.patrolSpots];
      if (spot) this.host.onMessage?.(tx(`Added patrol spot (${spot.x}, ${spot.y}).`, `Đã lưu điểm tuần tra (${spot.x}, ${spot.y}).`));
      this.renderUltimate();
    });
    root.querySelector('#uap-clear-spots')?.addEventListener('click', () => {
      this.captureInputs();
      this.host.extras.clearPatrolSpots();
      this.draft.patrolSpots = [];
      this.renderUltimate();
    });
    root.querySelector('#uap-reset-stats')?.addEventListener('click', () => {
      this.host.extras.resetStats();
      this.renderUltimate();
    });
    root.querySelector('#uap-go-town')?.addEventListener('click', () => {
      if (this.badge('extrasTown').disabled) return;
      this.host.extras.goToTown();
      this.host.onGoToTown?.();
    });
  }
}

function uapCheckboxRow(key: string, label: string, checked: boolean, disabled: boolean): string {
  return `<label class="hnmo-row${disabled ? ' disabled' : ' clickable'}">
    <span class="hnmo-label">${label}</span>
    <input type="checkbox" data-uap-key="${key}"${checked ? ' checked' : ''}${disabled ? ' disabled' : ''} style="width:14px;height:14px;flex:0 0 auto;accent-color:#d4af37;cursor:${disabled ? 'not-allowed' : 'pointer'}"/>
  </label>`;
}

function uapNumberRow(key: string, label: string, value: number, min: number, max: number, disabled: boolean): string {
  return `<label class="hnmo-row${disabled ? ' disabled' : ''}">
    <span class="hnmo-label">${label}</span>
    <input type="number" class="hnmo-num" data-uap-key="${key}" value="${value}" min="${min}" max="${max}"${disabled ? ' disabled' : ''}/>
  </label>`;
}
