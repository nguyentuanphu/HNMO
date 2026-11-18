// Adaptive Mobile & Tablet Landscape HUD (ROADMAP 17.1-17.8).
// Provides a modern RPG touch layout (hero badge, collapsible top ribbon, interactive minimap with
// tap-to-walk & pinch zoom, virtual analog thumbstick, HP/MP orbs, swipeable tabbed chat + 5 editable
// chat macros, target lock, and bottom-sheet QoL actions) while preserving #touchBar and #touchFight.

import type { Chat, ChatKind } from '../game/chat.js';
import { currentLanguage, onLanguageChange } from '../i18n.js';
import { loadMiniMapData, type MiniMapMarker } from '../game/miniMapData.js';
import { isWakeLockActive, type HudMode } from '../render/quality.js';

const MACROS_KEY = 'hnmo.chatMacros';
const LOOT_FILTER_KEY = 'hnmo.autoLootFilter';

export type LootFilterMode = 'all' | 'jewels' | 'zen';
const LOOT_FILTERS: readonly LootFilterMode[] = ['all', 'jewels', 'zen'];

export interface ChatMacro {
  label: string;
  text: string;
}

const DEFAULT_MACROS_EN: ChatMacro[] = [
  { label: 'Hi', text: 'Hello!' },
  { label: 'PT', text: 'Looking for party!' },
  { label: 'Buff', text: 'Buff please!' },
  { label: 'Reset', text: '/reset' },
  { label: 'Town', text: '/move lorencia' },
];

const DEFAULT_MACROS_VI: ChatMacro[] = [
  { label: 'Chào', text: 'Xin chào mọi người!' },
  { label: 'Xin PT', text: 'Cho mình xin slot party!' },
  { label: 'Xin Buff', text: 'Cho mình xin buff nhé!' },
  { label: 'Reset', text: '/reset' },
  { label: 'Về thành', text: '/move lorencia' },
];

export interface MobilePartyDot {
  name: string;
  cell: { x: number; y: number };
}

export interface MobileBuffBadge {
  label: string;
  secondsLeft: number;
}

export interface MobileHudSnapshot {
  heroName: string;
  className: string;
  classIndex: number;
  level: number;
  resets: number;
  safeZone: boolean;
  fps: number;
  batterySaver: boolean;
  wakeLock: boolean;
  hideOtherPlayers: boolean;
  autoPlayActive: boolean;
  hp: number;
  maxHp: number;
  sd: number;
  maxSd: number;
  mp: number;
  maxMp: number;
  ag: number;
  maxAg: number;
  expFraction: number;
  expPercentText: string;
  mapNumber: number | null;
  mapName: string;
  cell: { x: number; y: number };
  buffs: MobileBuffBadge[];
  partyDots: MobilePartyDot[];
  targetName: string | null;
  skillHotkeys: { hotkey: string; selected: boolean; cooldown: number; usable: boolean }[];
  potionCounts: number[];
}

export interface MobileHudActions {
  chat: Chat;
  stickMove: (nx: number, ny: number) => void;
  walkToCell: (cell: { x: number; y: number }) => void;
  fight: () => void;
  cycleTarget: () => string | null;
  pickNearest: () => boolean;
  toggleAutoPlay: () => void;
  openAutoSettings: () => void;
  openBag: () => void;
  openCharacter: () => void;
  openParty: () => void;
  openWarp: () => void;
  openFullMap: () => void;
  openRanking: () => void;
  openGuide: () => void;
  openTimetable: () => void;
  openCashShop: () => void;
  openSettings: () => void;
  sendCommand: (command: string) => void;
  triggerAutoBuff: () => void;
  toggleHidePlayers: () => boolean;
  toggleBatterySaver: () => boolean;
  toggleWakeLock: () => boolean;
  sellJunk: () => void;
  repairAll: () => void;
  setLootFilter?: (mode: LootFilterMode) => void;
}

interface Labels {
  safeZone: string;
  combatZone: string;
  lv: string;
  rs: string;
  ranking: string;
  guide: string;
  reset: string;
  events: string;
  shop: string;
  settings: string;
  autoPlay: string;
  autoBuff: string;
  warp: string;
  party: string;
  pick: string;
  bag: string;
  menu: string;
  macros: string;
  editMacros: string;
  saveMacros: string;
  chatPlaceholder: string;
  send: string;
  tabs: string[];
  targetNone: string;
  qolTitle: string;
  sellJunk: string;
  repairAll: string;
  hidePlayers: string;
  batterySaver: string;
  wakeLock: string;
  lootFilter: string;
  lootNames: Record<LootFilterMode, string>;
  touchBarToggle: string;
  close: string;
  wakeActive: string;
  pinchHint: string;
}

function labels(): Labels {
  const vi = currentLanguage() === 'vi';
  return {
    safeZone: vi ? 'An Toàn' : 'Safe Zone',
    combatZone: vi ? 'Chiến Đấu' : 'Combat',
    lv: vi ? 'Cấp' : 'Lv',
    rs: 'RS',
    ranking: vi ? 'BXH' : 'Rank',
    guide: vi ? 'Cẩm Nang' : 'Guide',
    reset: 'Reset',
    events: vi ? 'Hoạt Động' : 'Events',
    shop: 'Cash Shop',
    settings: vi ? 'Cài Đặt' : 'Settings',
    autoPlay: vi ? 'Treo Máy' : 'Auto',
    autoBuff: 'Auto Buff',
    warp: vi ? 'Dịch Chuyển' : 'Warp',
    party: vi ? 'Tổ Đội' : 'Party',
    pick: vi ? 'Nhặt' : 'Pick',
    bag: vi ? 'Túi Đồ' : 'Bag',
    menu: 'Menu',
    macros: vi ? 'Mẫu Chat' : 'Macros',
    editMacros: vi ? 'Sửa câu mẫu chat' : 'Edit Chat Macros',
    saveMacros: vi ? 'Lưu' : 'Save',
    chatPlaceholder: vi ? 'Nhập tin nhắn...' : 'Type a message...',
    send: vi ? 'Gửi' : 'Send',
    tabs: vi ? ['Thế giới', 'Mật', 'Đội', 'Hội', 'Hệ thống'] : ['All', 'Whisper', 'Party', 'Guild', 'System'],
    targetNone: vi ? 'Mục tiêu' : 'Target',
    qolTitle: vi ? 'Tiện ích & Cài đặt nhanh' : 'Quick Menu & Mobile QoL',
    sellJunk: vi ? 'Bán đồ rác' : 'Sell Junk',
    repairAll: vi ? 'Sửa tất cả' : 'Repair All',
    hidePlayers: vi ? 'Ẩn người chơi' : 'Hide Players',
    batterySaver: vi ? 'Tiết kiệm pin (30 FPS)' : 'Battery Saver (30 FPS)',
    wakeLock: vi ? 'Giữ sáng màn hình' : 'Keep Screen Awake',
    lootFilter: vi ? 'Lọc tự nhặt' : 'Auto-Loot Filter',
    lootNames: {
      all: vi ? 'Tất cả' : 'All items',
      jewels: vi ? 'Ngọc & Hoàn Hảo' : 'Jewels + Exc',
      zen: vi ? 'Chỉ Zen' : 'Zen only',
    },
    touchBarToggle: vi ? 'Phím Tắt' : 'Keys',
    close: vi ? 'Đóng' : 'Close',
    wakeActive: vi ? 'Sáng MH' : 'Awake',
    pinchHint: vi ? 'Chạm để đi' : 'Tap map to walk',
  };
}

const CSS = `
body.hnmo-modern-hud #touchBar {
  top: max(4px, env(safe-area-inset-top, 0px)) !important;
  right: auto !important;
  bottom: auto !important;
  left: 50% !important;
  transform: translateX(-50%) !important;
  flex-direction: row !important;
  gap: 4px !important;
  max-width: min(560px, calc(100vw - 340px));
  flex-wrap: nowrap;
  overflow-x: auto;
  scrollbar-width: none;
  padding: 2px 6px;
  border-radius: 999px;
  background: rgba(10, 13, 20, 0.72);
  border: 1px solid rgba(201, 162, 74, 0.35);
  z-index: 30;
}
body.hnmo-modern-hud #touchBar button {
  min-width: 30px;
  min-height: 30px;
  padding: 0 6px;
  font-size: 12px;
  border-radius: 999px;
  flex-shrink: 0;
}
body.hnmo-modern-hud.hnmo-ribbon-collapsed #touchBar {
  opacity: 0.3;
  transform: translateX(-50%) scale(0.9) !important;
}
body.hnmo-modern-hud #touchMore {
  top: max(44px, calc(env(safe-area-inset-top, 0px) + 42px));
  right: auto;
  left: 50%;
  transform: translateX(-50%);
  flex-direction: row;
  flex-wrap: wrap;
  justify-content: center;
  max-width: min(560px, 92vw);
  padding: 6px;
  border-radius: 12px;
  background: rgba(10, 13, 20, 0.9);
  border: 1px solid rgba(201, 162, 74, 0.45);
  z-index: 41;
}
body.hnmo-modern-hud #touchFight {
  right: max(12px, env(safe-area-inset-right, 0px)) !important;
  bottom: max(18px, env(safe-area-inset-bottom, 0px)) !important;
  width: 196px;
  height: 174px;
  display: block !important;
  pointer-events: none;
  z-index: 27;
}
body.hnmo-modern-hud #touchFight button {
  position: absolute;
  pointer-events: auto;
  display: grid;
  place-items: center;
  border-radius: 50%;
  background: radial-gradient(circle at 32% 30%, #2a3145, #101420);
  border: 1.5px solid rgba(201, 162, 74, 0.78);
  box-shadow: 0 4px 14px rgba(0, 0, 0, 0.58);
  color: #f6e7c1;
  font-weight: 700;
  touch-action: manipulation;
}
body.hnmo-modern-hud #touchFight button.potion {
  width: 34px;
  height: 34px;
  top: 0;
  font-size: 11px;
  border-color: rgba(140, 175, 235, 0.68);
}
body.hnmo-modern-hud #touchFight button.potion:nth-of-type(6) { right: 126px; }
body.hnmo-modern-hud #touchFight button.potion:nth-of-type(7) { right: 86px; }
body.hnmo-modern-hud #touchFight button.potion:nth-of-type(8) { right: 46px; }
body.hnmo-modern-hud #touchFight button.potion:nth-of-type(9) { right: 6px; }
body.hnmo-modern-hud #touchFight button.potion[data-count]:not([data-count=""])::after {
  content: attr(data-count);
  position: absolute;
  bottom: -4px;
  right: -4px;
  min-width: 16px;
  padding: 0 3px;
  border-radius: 999px;
  background: #0a0d14;
  border: 1px solid rgba(255, 255, 255, 0.25);
  color: #ffe9a8;
  font-size: 9px;
  line-height: 13px;
  text-align: center;
}
/* Curved 5-skill arc around the primary Fight button */
body.hnmo-modern-hud #touchFight button:not(.potion):not(.fight) {
  width: 44px;
  height: 44px;
  font-size: 13px;
}
body.hnmo-modern-hud #touchFight button:nth-of-type(1) { right: 148px; bottom: 8px; }
body.hnmo-modern-hud #touchFight button:nth-of-type(2) { right: 134px; bottom: 58px; }
body.hnmo-modern-hud #touchFight button:nth-of-type(3) { right: 98px; bottom: 96px; }
body.hnmo-modern-hud #touchFight button:nth-of-type(4) { right: 50px; bottom: 110px; }
body.hnmo-modern-hud #touchFight button:nth-of-type(5) { right: 2px; bottom: 96px; }
body.hnmo-modern-hud #touchFight button.hnmo-skill-selected {
  border-color: #ffd666;
  box-shadow: 0 0 12px rgba(255, 206, 86, 0.65);
}
body.hnmo-modern-hud #touchFight button.fight {
  width: 72px;
  height: 72px;
  right: 12px;
  bottom: 10px;
  font-size: 28px;
  background: radial-gradient(circle at 35% 30%, #61491f, #221909 74%);
  border: 2.5px solid #f0c96d;
  color: #fff2c4;
}

#mobileHud {
  position: fixed;
  inset: 0;
  pointer-events: none;
  z-index: 26;
  font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
  color: #e6e8ef;
  user-select: none;
}
#mobileHud[hidden] { display: none !important; }
#mobileHud button, #mobileHud input, #mobileHud .mh-interactive { pointer-events: auto; }

/* Top-left Hero Portrait Card */
.mh-hero {
  position: fixed;
  top: max(8px, env(safe-area-inset-top, 0px));
  left: max(8px, env(safe-area-inset-left, 0px));
  display: grid;
  grid-template-columns: 44px auto;
  gap: 8px;
  align-items: center;
  padding: 5px 10px 5px 6px;
  border-radius: 14px;
  background: linear-gradient(135deg, rgba(16, 20, 32, 0.88), rgba(9, 11, 18, 0.82));
  border: 1px solid rgba(201, 162, 74, 0.55);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.45);
}
.mh-avatar {
  width: 44px;
  height: 44px;
  border-radius: 50%;
  background: radial-gradient(circle at 35% 30%, #584321, #191d2b);
  border: 2px solid #d9b155;
  display: grid;
  place-items: center;
  font-weight: 800;
  font-size: 13px;
  color: #ffe8ad;
}
.mh-hero-meta { display: grid; gap: 2px; line-height: 1.2; }
.mh-hero-top { display: flex; gap: 6px; align-items: center; font-size: 12px; font-weight: 700; color: #f6e7c1; }
.mh-hero-sub { display: flex; gap: 6px; align-items: center; font-size: 11px; color: #bac1d4; }
.mh-zone { padding: 1px 6px; border-radius: 999px; font-size: 10px; font-weight: 700; background: rgba(72, 185, 110, 0.22); color: #84e5a4; border: 1px solid rgba(110, 210, 145, 0.45); }
.mh-zone.combat { background: rgba(235, 136, 72, 0.2); color: #ffb988; border-color: rgba(235, 140, 72, 0.45); }
.mh-telemetry { font-size: 10px; color: #9aa0b2; display: flex; gap: 6px; align-items: center; }
.mh-buffs {
  position: fixed;
  top: max(64px, calc(env(safe-area-inset-top, 0px) + 56px));
  left: max(8px, env(safe-area-inset-left, 0px));
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
  max-width: 200px;
}
.mh-buff {
  padding: 2px 5px;
  border-radius: 6px;
  background: rgba(14, 18, 28, 0.85);
  border: 1px solid rgba(201, 162, 74, 0.45);
  font-size: 10px;
  color: #ffe8ad;
}

/* Top-center Collapsible Quick Ribbon */
.mh-ribbon-wrap {
  position: fixed;
  top: max(44px, calc(env(safe-area-inset-top, 0px) + 42px));
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: 5px;
  max-width: calc(100vw - 340px);
  z-index: 29;
}
.mh-ribbon {
  display: flex;
  gap: 5px;
  align-items: center;
  padding: 4px 7px;
  border-radius: 999px;
  background: rgba(11, 14, 23, 0.82);
  border: 1px solid rgba(201, 162, 74, 0.42);
  overflow-x: auto;
  scrollbar-width: none;
}
.mh-ribbon[hidden] { display: none; }
.mh-circle-btn {
  all: unset;
  box-sizing: border-box;
  cursor: pointer;
  min-width: 34px;
  height: 30px;
  padding: 0 7px;
  border-radius: 999px;
  background: rgba(25, 31, 46, 0.92);
  border: 1px solid rgba(255, 255, 255, 0.16);
  color: #e9ecf4;
  font-size: 11px;
  font-weight: 600;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 3px;
  white-space: nowrap;
}
.mh-circle-btn.on { background: rgba(201, 162, 74, 0.25); border-color: #d9b155; color: #ffe8ad; }
.mh-ribbon-toggle {
  all: unset;
  cursor: pointer;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  display: grid;
  place-items: center;
  background: rgba(11, 14, 23, 0.86);
  border: 1px solid rgba(201, 162, 74, 0.5);
  color: #ffe8ad;
  font-size: 12px;
}

/* Top-right Interactive Minimap */
.mh-minimap {
  position: fixed;
  top: max(8px, env(safe-area-inset-top, 0px));
  right: max(8px, env(safe-area-inset-right, 0px));
  width: 152px;
  border-radius: 10px;
  background: rgba(9, 12, 20, 0.86);
  border: 1px solid rgba(201, 162, 74, 0.5);
  overflow: hidden;
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.5);
}
.mh-minimap-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 3px 6px;
  background: rgba(22, 27, 40, 0.9);
  font-size: 10.5px;
  font-weight: 600;
  color: #ffe8ad;
  gap: 4px;
}
.mh-minimap-head span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.mh-minimap canvas {
  display: block;
  width: 152px;
  height: 98px;
  touch-action: none;
  cursor: crosshair;
}
.mh-minimap-tools {
  display: flex;
  justify-content: space-between;
  padding: 3px 5px;
  gap: 3px;
  background: rgba(12, 15, 24, 0.92);
}
.mh-mini-btn {
  all: unset;
  cursor: pointer;
  flex: 1;
  text-align: center;
  font-size: 11px;
  padding: 2px 3px;
  border-radius: 5px;
  background: rgba(255, 255, 255, 0.06);
  color: #d7dced;
}
.mh-mini-btn.on { background: rgba(201, 162, 74, 0.25); color: #ffe8ad; }

/* Bottom-left Virtual Analog Thumbstick & Edge Quick Actions */
.mh-left-cluster {
  position: fixed;
  left: max(12px, env(safe-area-inset-left, 0px));
  bottom: max(20px, env(safe-area-inset-bottom, 0px));
  display: flex;
  align-items: flex-end;
  gap: 10px;
}
.mh-stick {
  position: relative;
  width: 108px;
  height: 108px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.06) 0%, rgba(12, 16, 26, 0.65) 72%);
  border: 2px solid rgba(255, 255, 255, 0.22);
  box-shadow: inset 0 0 18px rgba(0, 0, 0, 0.5);
  touch-action: none;
}
.mh-stick-knob {
  position: absolute;
  left: 50%;
  top: 50%;
  width: 44px;
  height: 44px;
  margin-left: -22px;
  margin-top: -22px;
  border-radius: 50%;
  background: radial-gradient(circle at 35% 30%, #f1cf7a, #7d6028);
  border: 1.5px solid #fff0c2;
  box-shadow: 0 3px 10px rgba(0, 0, 0, 0.6);
  pointer-events: none;
}
.mh-left-quick { display: grid; gap: 6px; }
.mh-pill-btn {
  all: unset;
  cursor: pointer;
  padding: 5px 9px;
  border-radius: 999px;
  background: rgba(11, 14, 23, 0.85);
  border: 1px solid rgba(201, 162, 74, 0.45);
  color: #f3e4bc;
  font-size: 11px;
  font-weight: 600;
  text-align: center;
  white-space: nowrap;
}
.mh-pill-btn.on { background: rgba(74, 172, 108, 0.28); border-color: #6fe39c; color: #cbffdc; }

/* Bottom-center HP/SD + Swipeable Chat + Macros + MP/AG + EXP Bar */
.mh-center-dock {
  position: fixed;
  left: 50%;
  bottom: max(10px, env(safe-area-inset-bottom, 0px));
  transform: translateX(-50%);
  width: min(430px, calc(100vw - 410px));
  min-width: 250px;
  display: grid;
  gap: 4px;
}
.mh-center-actions {
  display: flex;
  justify-content: center;
  gap: 8px;
}
.mh-core-row {
  display: grid;
  grid-template-columns: 56px 1fr 56px;
  gap: 6px;
  align-items: end;
}
.mh-orb {
  position: relative;
  width: 56px;
  height: 56px;
  border-radius: 50%;
  border: 2px solid #9e8145;
  background: #0d1018;
  overflow: hidden;
  display: grid;
  place-items: center;
  text-align: center;
}
.mh-orb-fill {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  height: 100%;
  transition: height 0.12s;
}
.mh-orb.hp .mh-orb-fill { background: linear-gradient(180deg, #ef5244, #8d1310); }
.mh-orb.mp .mh-orb-fill { background: linear-gradient(180deg, #4b94f7, #113a88); }
.mh-orb-text { position: relative; z-index: 1; font-size: 9.5px; font-weight: 700; line-height: 1.15; text-shadow: 0 1px 2px #000; }

.mh-chat {
  background: rgba(9, 12, 20, 0.84);
  border: 1px solid rgba(255, 255, 255, 0.16);
  border-radius: 8px;
  padding: 4px 6px;
  display: grid;
  gap: 3px;
}
.mh-chat-tabs { display: flex; gap: 3px; align-items: center; }
.mh-chat-tab {
  all: unset;
  cursor: pointer;
  flex: 1;
  text-align: center;
  font-size: 10px;
  padding: 2px 3px;
  border-radius: 4px;
  color: #a6aec2;
}
.mh-chat-tab.active { background: rgba(201, 162, 74, 0.25); color: #ffe8ad; font-weight: 700; }
.mh-chat-lines {
  height: 44px;
  overflow-y: auto;
  font-size: 11px;
  line-height: 1.3;
  color: #dfe4f0;
  touch-action: pan-y;
}
.mh-chat-line.system { color: #9de2a8; }
.mh-chat-line.error { color: #ff8e80; }
.mh-chat-line.whisper { color: #f6b2ef; }
.mh-chat-line.party { color: #8ad2ff; }
.mh-chat-line.guild { color: #ffd484; }
.mh-chat-input-row { display: flex; gap: 4px; }
.mh-chat-input-row input {
  flex: 1;
  min-width: 0;
  font-size: 11px;
  padding: 3px 6px;
  border-radius: 5px;
  border: 1px solid rgba(255, 255, 255, 0.18);
  background: rgba(16, 20, 32, 0.92);
  color: #fff;
}
.mh-macros { display: flex; gap: 3px; align-items: center; overflow-x: auto; scrollbar-width: none; }
.mh-macro-btn {
  all: unset;
  cursor: pointer;
  padding: 2px 6px;
  border-radius: 999px;
  background: rgba(34, 41, 60, 0.9);
  border: 1px solid rgba(255, 255, 255, 0.14);
  font-size: 10px;
  color: #d6dcf0;
  white-space: nowrap;
}
.mh-exp {
  position: relative;
  height: 11px;
  border-radius: 999px;
  background: rgba(10, 12, 18, 0.9);
  border: 1px solid rgba(201, 162, 74, 0.45);
  overflow: hidden;
}
.mh-exp-fill { height: 100%; width: 0%; background: linear-gradient(90deg, #9f7929, #f5cf70); }
.mh-exp-label {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  font-size: 9px;
  font-weight: 700;
  color: #fff;
  text-shadow: 0 1px 2px #000;
}

/* Bottom-right Extras above Skill Arc */
.mh-right-tools {
  position: fixed;
  right: max(12px, env(safe-area-inset-right, 0px));
  bottom: max(198px, calc(env(safe-area-inset-bottom, 0px) + 190px));
  display: flex;
  gap: 6px;
  align-items: center;
}

/* Bottom Sheet Dialog (Macros editor / Quick QoL menu) */
#mobileHudSheet {
  position: fixed;
  left: 50%;
  bottom: env(safe-area-inset-bottom, 0px);
  transform: translateX(-50%);
  width: min(560px, 100vw);
  max-height: min(76vh, 480px);
  overflow-y: auto;
  background: rgba(11, 14, 23, 0.97);
  border: 1px solid #c9a24a;
  border-bottom: 0;
  border-radius: 16px 16px 0 0;
  padding: 14px 16px 18px;
  z-index: 75;
  color: #e6e8ef;
  display: grid;
  gap: 10px;
  box-shadow: 0 -10px 36px rgba(0, 0, 0, 0.72);
  pointer-events: auto;
}
#mobileHudSheet[hidden] { display: none !important; }
.mh-sheet-head { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(201, 162, 74, 0.3); padding-bottom: 8px; }
.mh-sheet-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(145px, 1fr)); gap: 8px; }
.mh-sheet-btn {
  border: 1px solid rgba(201, 162, 74, 0.45);
  background: rgba(25, 31, 46, 0.92);
  color: #ffe8ad;
  border-radius: 8px;
  padding: 8px 10px;
  font: 600 12px system-ui, sans-serif;
  cursor: pointer;
  min-height: 38px;
}
.mh-macro-row { display: grid; grid-template-columns: 90px 1fr; gap: 6px; }
.mh-macro-row input { padding: 6px 8px; border-radius: 6px; border: 1px solid #323c54; background: #141926; color: #fff; }

@media (max-width: 780px) {
  .mh-center-dock { width: min(300px, calc(100vw - 260px)); min-width: 190px; }
  .mh-minimap { width: 126px; }
  .mh-minimap canvas { width: 126px; height: 84px; }
  .mh-stick { width: 92px; height: 92px; }
}
`;

const CLASS_BADGES = ['DW', 'DK', 'ELF', 'MG', 'DL', 'SUM', 'RF'];

function loadMacros(): ChatMacro[] {
  const fallback = currentLanguage() === 'vi' ? DEFAULT_MACROS_VI : DEFAULT_MACROS_EN;
  try {
    const raw = localStorage.getItem(MACROS_KEY);
    if (!raw) return fallback.map((m) => ({ ...m }));
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return fallback.map((m) => ({ ...m }));
    const cleaned: ChatMacro[] = [];
    for (let i = 0; i < 5; i++) {
      const entry = parsed[i] as Partial<ChatMacro> | undefined;
      cleaned.push({
        label: typeof entry?.label === 'string' && entry.label.trim() ? entry.label.trim().slice(0, 14) : fallback[i].label,
        text: typeof entry?.text === 'string' && entry.text.trim() ? entry.text.trim().slice(0, 80) : fallback[i].text,
      });
    }
    return cleaned;
  } catch {
    return fallback.map((m) => ({ ...m }));
  }
}

function saveMacros(macros: ChatMacro[]): void {
  try { localStorage.setItem(MACROS_KEY, JSON.stringify(macros)); } catch { /* ignore */ }
}

export function loadAutoLootFilter(): LootFilterMode {
  try {
    const raw = localStorage.getItem(LOOT_FILTER_KEY);
    return LOOT_FILTERS.includes(raw as LootFilterMode) ? raw as LootFilterMode : 'all';
  } catch {
    return 'all';
  }
}

function saveAutoLootFilter(mode: LootFilterMode): void {
  try { localStorage.setItem(LOOT_FILTER_KEY, mode); } catch { /* ignore */ }
}

export class MobileHud {
  private readonly root: HTMLDivElement;
  private readonly sheet: HTMLDivElement;
  private mode: HudMode = 'auto';
  private inWorld = false;
  private touchActive = typeof window !== 'undefined' && (window.matchMedia?.('(pointer: coarse)').matches ?? false);
  private ribbonExpanded = true;
  private activeChatTab = 0;
  private macros: ChatMacro[] = loadMacros();
  private lootFilter: LootFilterMode = loadAutoLootFilter();
  private mapZoom = 1;
  private mapMarkers: MiniMapMarker[] = [];
  private loadedMarkerMap: number | null = null;
  private walkPing: { x: number; y: number; until: number } | null = null;
  private latest: MobileHudSnapshot | null = null;

  // Dynamic elements kept directly for zero-rebuild frame updates
  private avatarEl!: HTMLDivElement;
  private nameEl!: HTMLSpanElement;
  private lvEl!: HTMLSpanElement;
  private zoneEl!: HTMLSpanElement;
  private telemetryEl!: HTMLDivElement;
  private buffsEl!: HTMLDivElement;
  private ribbonEl!: HTMLDivElement;
  private ribbonToggleBtn!: HTMLButtonElement;
  private mapTitleEl!: HTMLSpanElement;
  private mapCanvas!: HTMLCanvasElement;
  private hidePlayersBtn!: HTMLButtonElement;
  private stickEl!: HTMLDivElement;
  private knobEl!: HTMLDivElement;
  private autoPlayPill!: HTMLButtonElement;
  private targetPill!: HTMLButtonElement;
  private hpFillEl!: HTMLDivElement;
  private hpTextEl!: HTMLDivElement;
  private mpFillEl!: HTMLDivElement;
  private mpTextEl!: HTMLDivElement;
  private expFillEl!: HTMLDivElement;
  private expLabelEl!: HTMLDivElement;
  private chatTabsWrap!: HTMLDivElement;
  private chatLinesEl!: HTMLDivElement;
  private chatInputEl!: HTMLInputElement;
  private macroBarEl!: HTMLDivElement;
  private actionLabels: { btn: HTMLButtonElement; read: (l: Labels) => string }[] = [];

  constructor(private readonly actions: MobileHudActions) {
    const style = document.createElement('style');
    style.id = 'mobileHudStyles';
    style.textContent = CSS;
    document.head.append(style);

    this.root = document.createElement('div');
    this.root.id = 'mobileHud';
    this.root.hidden = true;

    this.sheet = document.createElement('div');
    this.sheet.id = 'mobileHudSheet';
    this.sheet.hidden = true;

    this.buildDom();
    document.body.append(this.root, this.sheet);

    this.actions.chat.onChange(() => this.renderChat());
    onLanguageChange(() => {
      this.macros = loadMacros();
      this.refreshLabels();
      this.renderChat();
      if (this.latest) this.update(this.latest);
    });
  }

  get active(): boolean {
    return this.inWorld && (this.mode === 'modern' || (this.mode === 'auto' && this.touchActive));
  }

  setMode(mode: HudMode): void {
    this.mode = mode;
    this.syncVisibility();
  }

  setTouchDetected(touch: boolean): void {
    if (this.touchActive === touch) return;
    this.touchActive = touch;
    this.syncVisibility();
  }

  setInWorld(inWorld: boolean): void {
    this.inWorld = inWorld;
    this.syncVisibility();
  }

  private syncVisibility(): void {
    const show = this.active;
    this.root.hidden = !show;
    document.body.classList.toggle('hnmo-modern-hud', show);
    if (!show) this.sheet.hidden = true;
  }

  private btn(label: (l: Labels) => string, onClick: () => void, cls = 'mh-circle-btn'): HTMLButtonElement {
    const el = document.createElement('button');
    el.type = 'button';
    el.className = cls;
    el.textContent = label(labels());
    el.addEventListener('pointerdown', (e) => e.stopPropagation());
    el.addEventListener('click', (e) => { e.stopPropagation(); onClick(); });
    this.actionLabels.push({ btn: el, read: label });
    return el;
  }

  private buildDom(): void {
    const stop = (el: HTMLElement) => el.addEventListener('pointerdown', (e) => e.stopPropagation());

    // 1. Top-left hero portrait card
    const heroCard = document.createElement('div');
    heroCard.id = 'mobileHeroCard';
    heroCard.className = 'mh-hero mh-interactive';
    stop(heroCard);
    this.avatarEl = document.createElement('div');
    this.avatarEl.className = 'mh-avatar';
    this.avatarEl.textContent = 'DK';
    const meta = document.createElement('div');
    meta.className = 'mh-hero-meta';
    const top = document.createElement('div');
    top.className = 'mh-hero-top';
    this.nameEl = document.createElement('span');
    this.nameEl.textContent = '-';
    this.zoneEl = document.createElement('span');
    this.zoneEl.className = 'mh-zone';
    top.append(this.nameEl, this.zoneEl);
    const sub = document.createElement('div');
    sub.className = 'mh-hero-sub';
    this.lvEl = document.createElement('span');
    sub.append(this.lvEl);
    this.telemetryEl = document.createElement('div');
    this.telemetryEl.className = 'mh-telemetry';
    meta.append(top, sub, this.telemetryEl);
    heroCard.append(this.avatarEl, meta);
    heroCard.addEventListener('click', () => this.actions.openCharacter());

    this.buffsEl = document.createElement('div');
    this.buffsEl.id = 'mobileBuffRow';
    this.buffsEl.className = 'mh-buffs';

    // 2. Top-center collapsible ribbon
    const ribbonWrap = document.createElement('div');
    ribbonWrap.className = 'mh-ribbon-wrap';
    this.ribbonEl = document.createElement('div');
    this.ribbonEl.id = 'mobileTopRibbon';
    this.ribbonEl.className = 'mh-ribbon mh-interactive';
    stop(this.ribbonEl);
    this.ribbonEl.append(
      this.btn((l) => l.ranking, () => this.actions.openRanking()),
      this.btn((l) => l.guide, () => this.actions.openGuide()),
      this.btn((l) => l.reset, () => this.actions.sendCommand('/reset info')),
      this.btn((l) => l.events, () => this.actions.openTimetable()),
      this.btn((l) => l.shop, () => this.actions.openCashShop()),
      this.btn((l) => l.settings, () => this.actions.openSettings()),
    );
    this.ribbonToggleBtn = document.createElement('button');
    this.ribbonToggleBtn.type = 'button';
    this.ribbonToggleBtn.id = 'mobileRibbonToggle';
    this.ribbonToggleBtn.className = 'mh-ribbon-toggle';
    this.ribbonToggleBtn.textContent = '▲';
    stop(this.ribbonToggleBtn);
    this.ribbonToggleBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      this.ribbonExpanded = !this.ribbonExpanded;
      this.ribbonEl.hidden = !this.ribbonExpanded;
      this.ribbonToggleBtn.textContent = this.ribbonExpanded ? '▲' : '▼';
      document.body.classList.toggle('hnmo-ribbon-collapsed', !this.ribbonExpanded);
    });
    ribbonWrap.append(this.ribbonEl, this.ribbonToggleBtn);

    // 3. Top-right minimap widget
    const mapCard = document.createElement('div');
    mapCard.id = 'mobileMiniMap';
    mapCard.className = 'mh-minimap mh-interactive';
    stop(mapCard);
    const mapHead = document.createElement('div');
    mapHead.className = 'mh-minimap-head';
    this.mapTitleEl = document.createElement('span');
    this.mapTitleEl.textContent = '-';
    const zoomOut = document.createElement('button');
    zoomOut.type = 'button'; zoomOut.className = 'mh-mini-btn'; zoomOut.textContent = '−'; zoomOut.style.flex = '0 0 20px';
    const zoomIn = document.createElement('button');
    zoomIn.type = 'button'; zoomIn.className = 'mh-mini-btn'; zoomIn.textContent = '+'; zoomIn.style.flex = '0 0 20px';
    zoomIn.addEventListener('click', () => { this.mapZoom = Math.min(2.6, +(this.mapZoom + 0.3).toFixed(2)); this.drawMiniMap(); });
    zoomOut.addEventListener('click', () => { this.mapZoom = Math.max(0.6, +(this.mapZoom - 0.3).toFixed(2)); this.drawMiniMap(); });
    mapHead.append(this.mapTitleEl, zoomOut, zoomIn);

    this.mapCanvas = document.createElement('canvas');
    this.mapCanvas.id = 'mobileMiniMapCanvas';
    this.mapCanvas.width = 152;
    this.mapCanvas.height = 98;
    this.wireMiniMapGestures();

    const mapTools = document.createElement('div');
    mapTools.className = 'mh-minimap-tools';
    const fullMapBtn = document.createElement('button');
    fullMapBtn.type = 'button'; fullMapBtn.className = 'mh-mini-btn'; fullMapBtn.textContent = 'Map';
    fullMapBtn.addEventListener('click', () => this.actions.openFullMap());
    this.hidePlayersBtn = document.createElement('button');
    this.hidePlayersBtn.type = 'button'; this.hidePlayersBtn.id = 'mobileHidePlayersBtn'; this.hidePlayersBtn.className = 'mh-mini-btn'; this.hidePlayersBtn.textContent = '👁';
    this.hidePlayersBtn.addEventListener('click', () => {
      const hidden = this.actions.toggleHidePlayers();
      this.hidePlayersBtn.classList.toggle('on', hidden);
    });
    const focusChatBtn = document.createElement('button');
    focusChatBtn.type = 'button'; focusChatBtn.className = 'mh-mini-btn'; focusChatBtn.textContent = '💬';
    focusChatBtn.addEventListener('click', () => this.chatInputEl.focus());
    mapTools.append(fullMapBtn, this.hidePlayersBtn, focusChatBtn);
    mapCard.append(mapHead, this.mapCanvas, mapTools);

    // 4. Bottom-left virtual thumbstick & quick buttons
    const leftCluster = document.createElement('div');
    leftCluster.className = 'mh-left-cluster';
    this.stickEl = document.createElement('div');
    this.stickEl.id = 'mobileThumbstick';
    this.stickEl.className = 'mh-stick mh-interactive';
    this.stickEl.dataset.nx = '0';
    this.stickEl.dataset.ny = '0';
    this.knobEl = document.createElement('div');
    this.knobEl.className = 'mh-stick-knob';
    this.stickEl.append(this.knobEl);
    this.wireThumbstick();

    const leftQuick = document.createElement('div');
    leftQuick.className = 'mh-left-quick';
    leftQuick.append(
      this.btn((l) => l.warp, () => this.actions.openWarp(), 'mh-pill-btn'),
      this.btn((l) => l.autoBuff, () => this.actions.triggerAutoBuff(), 'mh-pill-btn'),
      this.btn((l) => l.party, () => this.actions.openParty(), 'mh-pill-btn'),
    );
    leftCluster.append(this.stickEl, leftQuick);

    // 5. Bottom-center orbs, swipeable chat + macros, EXP bar
    const centerDock = document.createElement('div');
    centerDock.className = 'mh-center-dock';
    const centerActions = document.createElement('div');
    centerActions.className = 'mh-center-actions';
    const pickPill = this.btn((l) => l.pick, () => { this.actions.pickNearest(); }, 'mh-pill-btn');
    const bagPill = this.btn((l) => l.bag, () => this.actions.openBag(), 'mh-pill-btn');
    this.autoPlayPill = this.btn((l) => l.autoPlay, () => this.actions.toggleAutoPlay(), 'mh-pill-btn');
    this.autoPlayPill.id = 'mobileAutoPill';
    centerActions.append(pickPill, bagPill, this.autoPlayPill);

    const coreRow = document.createElement('div');
    coreRow.className = 'mh-core-row';
    const hpOrb = document.createElement('div');
    hpOrb.className = 'mh-orb hp';
    this.hpFillEl = document.createElement('div'); this.hpFillEl.className = 'mh-orb-fill';
    this.hpTextEl = document.createElement('div'); this.hpTextEl.className = 'mh-orb-text';
    hpOrb.append(this.hpFillEl, this.hpTextEl);

    const mpOrb = document.createElement('div');
    mpOrb.className = 'mh-orb mp';
    this.mpFillEl = document.createElement('div'); this.mpFillEl.className = 'mh-orb-fill';
    this.mpTextEl = document.createElement('div'); this.mpTextEl.className = 'mh-orb-text';
    mpOrb.append(this.mpFillEl, this.mpTextEl);

    const chatCard = document.createElement('div');
    chatCard.id = 'mobileChatBox';
    chatCard.className = 'mh-chat mh-interactive';
    stop(chatCard);
    this.chatTabsWrap = document.createElement('div');
    this.chatTabsWrap.className = 'mh-chat-tabs';
    this.chatLinesEl = document.createElement('div');
    this.chatLinesEl.id = 'mobileChatLines';
    this.chatLinesEl.className = 'mh-chat-lines';
    this.wireChatSwipe();

    const inputRow = document.createElement('form');
    inputRow.className = 'mh-chat-input-row';
    this.chatInputEl = document.createElement('input');
    this.chatInputEl.id = 'mobileChatInput';
    this.chatInputEl.maxLength = 60;
    const sendBtn = this.btn((l) => l.send, () => this.submitChat(), 'mh-circle-btn');
    sendBtn.type = 'submit';
    inputRow.addEventListener('submit', (e) => { e.preventDefault(); this.submitChat(); });
    inputRow.append(this.chatInputEl, sendBtn);

    this.macroBarEl = document.createElement('div');
    this.macroBarEl.id = 'mobileMacroBar';
    this.macroBarEl.className = 'mh-macros';

    chatCard.append(this.chatTabsWrap, this.chatLinesEl, inputRow, this.macroBarEl);
    coreRow.append(hpOrb, chatCard, mpOrb);

    const expWrap = document.createElement('div');
    expWrap.className = 'mh-exp';
    this.expFillEl = document.createElement('div'); this.expFillEl.className = 'mh-exp-fill';
    this.expLabelEl = document.createElement('div'); this.expLabelEl.className = 'mh-exp-label';
    expWrap.append(this.expFillEl, this.expLabelEl);

    centerDock.append(centerActions, coreRow, expWrap);

    // 6. Bottom-right target lock & menu trigger (separate from #touchFight's 10 buttons!)
    const rightTools = document.createElement('div');
    rightTools.id = 'mobileRightTools';
    rightTools.className = 'mh-right-tools';
    this.targetPill = document.createElement('button');
    this.targetPill.type = 'button';
    this.targetPill.id = 'mobileTargetLock';
    this.targetPill.className = 'mh-pill-btn';
    stop(this.targetPill);
    this.targetPill.addEventListener('click', (e) => {
      e.stopPropagation();
      const locked = this.actions.cycleTarget();
      this.targetPill.textContent = locked ? `🎯 ${locked}` : `🎯 ${labels().targetNone}`;
      this.targetPill.classList.toggle('on', Boolean(locked));
    });
    const menuBtn = this.btn((l) => `⊞ ${l.menu}`, () => this.openQuickSheet(), 'mh-pill-btn');
    menuBtn.id = 'mobileMenuBtn';
    rightTools.append(this.targetPill, menuBtn);

    this.root.append(heroCard, this.buffsEl, ribbonWrap, mapCard, leftCluster, centerDock, rightTools);
    this.refreshLabels();
    this.renderChat();
  }

  private refreshLabels(): void {
    const l = labels();
    for (const entry of this.actionLabels) entry.btn.textContent = entry.read(l);
    this.chatInputEl.placeholder = l.chatPlaceholder;
    if (!this.latest?.targetName) this.targetPill.textContent = `🎯 ${l.targetNone}`;

    this.chatTabsWrap.replaceChildren();
    l.tabs.forEach((name, idx) => {
      const b = document.createElement('button');
      b.type = 'button';
      b.className = `mh-chat-tab${idx === this.activeChatTab ? ' active' : ''}`;
      b.textContent = name;
      b.addEventListener('click', () => {
        this.activeChatTab = idx;
        this.actions.chat.setTab(idx);
        this.renderChat();
      });
      this.chatTabsWrap.append(b);
    });

    this.macroBarEl.replaceChildren();
    this.macros.forEach((macro, index) => {
      const mBtn = document.createElement('button');
      mBtn.type = 'button';
      mBtn.className = 'mh-macro-btn';
      mBtn.dataset.macro = String(index);
      mBtn.textContent = macro.label;
      mBtn.title = macro.text;
      mBtn.addEventListener('click', () => {
        this.actions.chat.submitText(macro.text, this.activeChatTab === 2 ? 1 : this.activeChatTab === 3 ? 2 : 0);
      });
      this.macroBarEl.append(mBtn);
    });
    const editBtn = document.createElement('button');
    editBtn.type = 'button';
    editBtn.id = 'mobileMacroEditBtn';
    editBtn.className = 'mh-macro-btn';
    editBtn.textContent = '✎';
    editBtn.title = l.editMacros;
    editBtn.addEventListener('click', () => this.openMacroEditor());
    this.macroBarEl.append(editBtn);
  }

  private submitChat(): void {
    const text = this.chatInputEl.value.trim();
    if (!text) return;
    const channel = this.activeChatTab === 2 ? 1 : this.activeChatTab === 3 ? 2 : 0;
    if (this.actions.chat.submitText(text, channel)) {
      this.chatInputEl.value = '';
    }
  }

  private renderChat(): void {
    const tabs = this.chatTabsWrap.querySelectorAll<HTMLButtonElement>('.mh-chat-tab');
    tabs.forEach((btn, idx) => btn.classList.toggle('active', idx === this.activeChatTab));
    const lines = this.actions.chat.getMessages(this.activeChatTab).slice(-8);
    this.chatLinesEl.replaceChildren();
    for (const msg of lines) {
      const line = document.createElement('div');
      line.className = `mh-chat-line ${msg.kind as ChatKind}`;
      line.textContent = `${msg.id ? `${msg.id}: ` : ''}${msg.text}${msg.count > 1 ? ` (x${msg.count})` : ''}`;
      this.chatLinesEl.append(line);
    }
    this.chatLinesEl.scrollTop = this.chatLinesEl.scrollHeight;
  }

  private wireChatSwipe(): void {
    let startX: number | null = null;
    this.chatLinesEl.addEventListener('pointerdown', (e) => { startX = e.clientX; });
    this.chatLinesEl.addEventListener('pointerup', (e) => {
      if (startX === null) return;
      const dx = e.clientX - startX;
      startX = null;
      if (Math.abs(dx) < 38) return;
      const next = (this.activeChatTab + (dx < 0 ? 1 : 4)) % 5;
      this.activeChatTab = next;
      this.actions.chat.setTab(next);
      this.renderChat();
    });
  }

  private wireThumbstick(): void {
    const MAX_R = 34;
    let activeId: number | null = null;
    const updateStick = (clientX: number, clientY: number) => {
      const rect = this.stickEl.getBoundingClientRect();
      const cx = rect.left + rect.width / 2;
      const cy = rect.top + rect.height / 2;
      const dx = clientX - cx;
      const dy = clientY - cy;
      const dist = Math.hypot(dx, dy);
      const clamped = Math.min(MAX_R, dist);
      const angle = Math.atan2(dy, dx);
      const kx = dist > 0 ? Math.cos(angle) * clamped : 0;
      const ky = dist > 0 ? Math.sin(angle) * clamped : 0;
      this.knobEl.style.transform = `translate(${kx.toFixed(1)}px, ${ky.toFixed(1)}px)`;
      const nx = Math.round((kx / MAX_R) * 100) / 100;
      const ny = Math.round((ky / MAX_R) * 100) / 100;
      this.stickEl.dataset.nx = String(nx);
      this.stickEl.dataset.ny = String(ny);
      this.actions.stickMove(nx, ny);
    };
    const release = (pointerId: number) => {
      if (activeId !== pointerId) return;
      activeId = null;
      this.knobEl.style.transform = 'translate(0px, 0px)';
      this.stickEl.dataset.nx = '0';
      this.stickEl.dataset.ny = '0';
      this.actions.stickMove(0, 0);
    };
    this.stickEl.addEventListener('pointerdown', (e) => {
      e.preventDefault();
      e.stopPropagation();
      activeId = e.pointerId;
      this.stickEl.setPointerCapture?.(e.pointerId);
      updateStick(e.clientX, e.clientY);
    });
    this.stickEl.addEventListener('pointermove', (e) => {
      if (activeId === e.pointerId) updateStick(e.clientX, e.clientY);
    });
    this.stickEl.addEventListener('pointerup', (e) => release(e.pointerId));
    this.stickEl.addEventListener('pointercancel', (e) => release(e.pointerId));
  }

  private wireMiniMapGestures(): void {
    const pointers = new Map<number, { x: number; y: number }>();
    let downPos: { x: number; y: number } | null = null;
    let pinchDist = 0;

    this.mapCanvas.addEventListener('pointerdown', (e) => {
      e.stopPropagation();
      pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (pointers.size === 1) downPos = { x: e.clientX, y: e.clientY };
      else if (pointers.size === 2) {
        downPos = null;
        const [a, b] = [...pointers.values()];
        pinchDist = Math.hypot(a.x - b.x, a.y - b.y);
      }
    });
    this.mapCanvas.addEventListener('pointermove', (e) => {
      if (!pointers.has(e.pointerId)) return;
      pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (pointers.size === 2 && pinchDist > 0) {
        const [a, b] = [...pointers.values()];
        const next = Math.hypot(a.x - b.x, a.y - b.y);
        if (next > 5) {
          this.mapZoom = Math.max(0.6, Math.min(2.6, +(this.mapZoom * (next / pinchDist)).toFixed(2)));
          pinchDist = next;
          this.drawMiniMap();
        }
      }
    });
    const lift = (e: PointerEvent) => {
      const wasTap = pointers.size === 1 && downPos && Math.hypot(e.clientX - downPos.x, e.clientY - downPos.y) < 10;
      pointers.delete(e.pointerId);
      if (pointers.size < 2) pinchDist = 0;
      if (wasTap && this.latest) {
        const rect = this.mapCanvas.getBoundingClientRect();
        const scalePx = 1.6 * this.mapZoom;
        const dx = (e.clientX - (rect.left + rect.width / 2)) / scalePx;
        const dy = (e.clientY - (rect.top + rect.height / 2)) / scalePx;
        // Inverse 45 deg rotation to MU cell coordinates
        const cos = Math.SQRT1_2;
        const cellDx = Math.round(dx * cos + dy * cos);
        const cellDy = Math.round(dy * cos - dx * cos);
        const tx = Math.max(0, Math.min(255, this.latest.cell.x + cellDx));
        const ty = Math.max(0, Math.min(255, this.latest.cell.y - cellDy));
        this.walkPing = { x: tx, y: ty, until: Date.now() + 3500 };
        this.actions.walkToCell({ x: tx, y: ty });
        this.drawMiniMap();
      }
      downPos = null;
    };
    this.mapCanvas.addEventListener('pointerup', lift);
    this.mapCanvas.addEventListener('pointercancel', lift);
  }

  private drawMiniMap(): void {
    const snap = this.latest;
    if (!snap) return;
    if (snap.mapNumber !== null && snap.mapNumber !== this.loadedMarkerMap) {
      this.loadedMarkerMap = snap.mapNumber;
      void loadMiniMapData(snap.mapNumber).then((d) => { if (d) this.mapMarkers = d.markers; }).catch(() => undefined);
    }
    const ctx = this.mapCanvas.getContext('2d');
    if (!ctx) return;
    const w = this.mapCanvas.width, h = this.mapCanvas.height;
    ctx.clearRect(0, 0, w, h);
    ctx.fillStyle = '#0c111c';
    ctx.fillRect(0, 0, w, h);

    // Subtle radar grid
    ctx.strokeStyle = 'rgba(201, 162, 74, 0.12)';
    ctx.lineWidth = 1;
    for (let x = 16; x < w; x += 24) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke(); }
    for (let y = 16; y < h; y += 24) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(w, y); ctx.stroke(); }

    const cos = Math.SQRT1_2;
    const scalePx = 1.6 * this.mapZoom;
    const project = (cx: number, cy: number) => {
      const dx = cx - snap.cell.x;
      const dy = snap.cell.y - cy;
      return {
        x: w / 2 + (dx - dy) * cos * scalePx,
        y: h / 2 + (dx + dy) * cos * scalePx,
      };
    };

    for (const marker of this.mapMarkers) {
      if (marker.kind !== 1 && marker.kind !== 2) continue;
      const p = project(marker.location[0], marker.location[1]);
      if (p.x < 0 || p.x > w || p.y < 0 || p.y > h) continue;
      ctx.fillStyle = marker.kind === 2 ? '#58a6ff' : '#63d488';
      ctx.beginPath();
      ctx.arc(p.x, p.y, marker.kind === 2 ? 3 : 2, 0, Math.PI * 2);
      ctx.fill();
    }

    for (const mate of snap.partyDots) {
      const p = project(mate.cell.x, mate.cell.y);
      ctx.fillStyle = '#4be3ff';
      ctx.beginPath();
      ctx.arc(p.x, p.y, 3.2, 0, Math.PI * 2);
      ctx.fill();
    }

    if (this.walkPing && Date.now() < this.walkPing.until) {
      const p = project(this.walkPing.x, this.walkPing.y);
      ctx.strokeStyle = '#ffd666';
      ctx.beginPath();
      ctx.arc(p.x, p.y, 5, 0, Math.PI * 2);
      ctx.stroke();
    }

    // Hero centered arrow
    ctx.fillStyle = '#ffd666';
    ctx.beginPath();
    ctx.moveTo(w / 2, h / 2 - 5);
    ctx.lineTo(w / 2 + 4, h / 2 + 4);
    ctx.lineTo(w / 2 - 4, h / 2 + 4);
    ctx.closePath();
    ctx.fill();
  }

  private openMacroEditor(): void {
    const l = labels();
    this.sheet.replaceChildren();
    this.sheet.hidden = false;
    const head = document.createElement('div');
    head.className = 'mh-sheet-head';
    const title = document.createElement('strong');
    title.textContent = l.editMacros;
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button'; closeBtn.className = 'mh-sheet-btn'; closeBtn.style.minHeight = '30px'; closeBtn.textContent = l.close;
    closeBtn.addEventListener('click', () => { this.sheet.hidden = true; });
    head.append(title, closeBtn);
    this.sheet.append(head);

    const inputs: { label: HTMLInputElement; text: HTMLInputElement }[] = [];
    for (const macro of this.macros) {
      const row = document.createElement('div');
      row.className = 'mh-macro-row';
      const lbl = document.createElement('input');
      lbl.value = macro.label; lbl.maxLength = 14;
      const txt = document.createElement('input');
      txt.value = macro.text; txt.maxLength = 80;
      inputs.push({ label: lbl, text: txt });
      row.append(lbl, txt);
      this.sheet.append(row);
    }

    const saveBtn = document.createElement('button');
    saveBtn.type = 'button';
    saveBtn.id = 'mobileMacroSaveBtn';
    saveBtn.className = 'mh-sheet-btn';
    saveBtn.textContent = l.saveMacros;
    saveBtn.addEventListener('click', () => {
      this.macros = inputs.map((pair, idx) => ({
        label: pair.label.value.trim() || `M${idx + 1}`,
        text: pair.text.value.trim() || this.macros[idx].text,
      }));
      saveMacros(this.macros);
      this.refreshLabels();
      this.sheet.hidden = true;
    });
    this.sheet.append(saveBtn);
  }

  private openQuickSheet(): void {
    const l = labels();
    const snap = this.latest;
    this.sheet.replaceChildren();
    this.sheet.hidden = false;

    const head = document.createElement('div');
    head.className = 'mh-sheet-head';
    const title = document.createElement('strong');
    title.textContent = l.qolTitle;
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button'; closeBtn.className = 'mh-sheet-btn'; closeBtn.style.minHeight = '30px'; closeBtn.textContent = l.close;
    closeBtn.addEventListener('click', () => { this.sheet.hidden = true; });
    head.append(title, closeBtn);

    const grid = document.createElement('div');
    grid.className = 'mh-sheet-grid';
    const addAction = (text: string, onClick: () => void, id?: string) => {
      const b = document.createElement('button');
      b.type = 'button'; b.className = 'mh-sheet-btn';
      if (id) b.id = id;
      b.textContent = text;
      b.addEventListener('click', onClick);
      grid.append(b);
      return b;
    };

    addAction(`🧹 ${l.sellJunk}`, () => { this.actions.sellJunk(); this.sheet.hidden = true; }, 'mobileQolSellJunk');
    addAction(`🔧 ${l.repairAll}`, () => { this.actions.repairAll(); this.sheet.hidden = true; }, 'mobileQolRepairAll');
    const lootBtn = addAction(`💎 ${l.lootFilter}: ${l.lootNames[this.lootFilter]}`, () => {
      const next = LOOT_FILTERS[(LOOT_FILTERS.indexOf(this.lootFilter) + 1) % LOOT_FILTERS.length];
      this.lootFilter = next;
      saveAutoLootFilter(next);
      this.actions.setLootFilter?.(next);
      lootBtn.textContent = `💎 ${l.lootFilter}: ${l.lootNames[next]}`;
    }, 'mobileQolLootFilter');
    addAction(`${l.autoPlay} ⚙`, () => { this.sheet.hidden = true; this.actions.openAutoSettings(); });
    addAction(`${l.batterySaver}: ${snap?.batterySaver ? 'ON' : 'OFF'}`, () => {
      const on = this.actions.toggleBatterySaver();
      this.sheet.hidden = true;
      void on;
    }, 'mobileQolBattery');
    addAction(`${l.wakeLock}: ${snap?.wakeLock ? 'ON' : 'OFF'}`, () => {
      this.actions.toggleWakeLock();
      this.sheet.hidden = true;
    }, 'mobileQolWakeLock');
    addAction(`${l.hidePlayers}: ${snap?.hideOtherPlayers ? 'ON' : 'OFF'}`, () => {
      this.actions.toggleHidePlayers();
      this.sheet.hidden = true;
    });
    addAction(l.touchBarToggle, () => {
      document.getElementById('touchBar')?.classList.toggle('shown');
      this.sheet.hidden = true;
    });

    this.sheet.append(head, grid);
  }

  update(snap: MobileHudSnapshot): void {
    this.latest = snap;
    if (!this.active) return;
    const l = labels();

    this.avatarEl.textContent = CLASS_BADGES[snap.classIndex] ?? 'MU';
    this.nameEl.textContent = snap.heroName || '-';
    this.lvEl.textContent = `${l.lv}.${snap.level} · ${l.rs}:${snap.resets}`;
    this.zoneEl.textContent = snap.safeZone ? l.safeZone : l.combatZone;
    this.zoneEl.classList.toggle('combat', !snap.safeZone);
    this.telemetryEl.textContent = `${snap.fps} FPS${snap.batterySaver ? ' · ⚡' : ''}${isWakeLockActive() || snap.wakeLock ? ` · ${l.wakeActive}` : ''}`;

    this.buffsEl.replaceChildren();
    for (const b of snap.buffs.slice(0, 6)) {
      const chip = document.createElement('span');
      chip.className = 'mh-buff';
      chip.textContent = b.secondsLeft > 0 ? `${b.label} ${Math.ceil(b.secondsLeft)}s` : b.label;
      this.buffsEl.append(chip);
    }

    this.mapTitleEl.textContent = `${snap.mapName} ${snap.cell.x},${snap.cell.y}`;
    this.hidePlayersBtn.classList.toggle('on', snap.hideOtherPlayers);
    this.autoPlayPill.classList.toggle('on', snap.autoPlayActive);
    if (snap.targetName) {
      this.targetPill.textContent = `🎯 ${snap.targetName}`;
      this.targetPill.classList.add('on');
    } else {
      this.targetPill.textContent = `🎯 ${l.targetNone}`;
      this.targetPill.classList.remove('on');
    }

    const hpPct = snap.maxHp > 0 ? Math.max(0, Math.min(100, (snap.hp / snap.maxHp) * 100)) : 0;
    const mpPct = snap.maxMp > 0 ? Math.max(0, Math.min(100, (snap.mp / snap.maxMp) * 100)) : 0;
    this.hpFillEl.style.height = `${hpPct.toFixed(1)}%`;
    this.hpTextEl.textContent = `${snap.hp}\n/${snap.maxHp}`;
    this.mpFillEl.style.height = `${mpPct.toFixed(1)}%`;
    this.mpTextEl.textContent = `${snap.mp}\n/${snap.maxMp}`;

    const expPct = Math.max(0, Math.min(100, snap.expFraction * 100));
    this.expFillEl.style.width = `${expPct.toFixed(2)}%`;
    this.expLabelEl.textContent = snap.expPercentText;

    // Sync live potion counts & skill selection onto #touchFight buttons
    const fightEl = document.getElementById('touchFight');
    if (fightEl) {
      const btns = fightEl.querySelectorAll<HTMLButtonElement>('button');
      for (let i = 0; i < 5; i++) {
        const b = btns[i];
        const info = snap.skillHotkeys[i];
        if (b && info) {
          b.classList.toggle('hnmo-skill-selected', info.selected);
          b.style.opacity = info.cooldown > 0 ? '0.62' : '1';
        }
      }
      for (let p = 0; p < 4; p++) {
        const b = btns[5 + p];
        const count = snap.potionCounts[p] ?? 0;
        if (b) b.dataset.count = count > 0 ? String(count) : '';
      }
    }

    this.drawMiniMap();
  }
}
