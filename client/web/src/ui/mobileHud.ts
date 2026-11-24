// Adaptive Mobile & Tablet Landscape HUD (ROADMAP 17.1-17.8).
// A compact touch layout that leaves the game in view: a small hero card (life / mana; a tap for AG, SD and the
// status line), one top-right row (bag, item names, auto play, menu, mini map), a joystick with a chat bubble,
// the attack button with five skills on an arc, and a menu sheet (QoL row, grouped menu, the key list) for the
// rest. It keeps #touchBar and #touchFight (restyled while it shows).

import type { Chat, ChatKind } from '../game/chat.js';
import { currentLanguage, onLanguageChange } from '../i18n.js';
import { isBlocked as chatIsBlocked, loadWindowSize, saveWindowSize, setBlocked as chatSetBlocked, type BoxWindowBounds } from '../game/chatExtras.js';
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
  /** The target's life as a share (0-1) from the last hit; null or -1 while unknown. */
  targetHealth?: number | null;
  /** Zen in the bag. */
  zen?: number;
  /** The round trip to the server in ms, when the game measures it. */
  pingMs?: number | null;
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
  /** Blocks or unblocks a player's chat (long press on their line). */
  setBlocked?: (name: string, blocked: boolean) => void;
  isBlocked?: (name: string) => boolean;
  /** Back to the character list. */
  switchCharacter?: () => void;
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
  autoShort: string;
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
  guild: string;
  on: string;
  off: string;
  hudSize: string;
  sizes: [string, string, string];
  groups: { character: string; social: string; trade: string; activities: string; settings: string; help: string; switchChar: string };
  block: string;
  unblock: string;
  report: string;
  reportHint: string;
  chatToggle: string;
  pickUp: string;
  skillPage: string;
  switchConfirm: string;
  battery: string;
  powerSave: string;
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
    autoShort: vi ? 'Treo' : 'Auto',
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
    guild: vi ? 'Bang' : 'Guild',
    on: vi ? 'BẬT' : 'ON',
    off: vi ? 'TẮT' : 'OFF',
    hudSize: vi ? 'Cỡ giao diện' : 'HUD size',
    sizes: vi ? ['Nhỏ', 'Vừa', 'Lớn'] : ['Small', 'Normal', 'Large'],
    groups: vi
      ? { character: 'Nhân vật', social: 'Xã hội', trade: 'Giao dịch', activities: 'Hoạt động', settings: 'Cài đặt', help: 'Trợ giúp', switchChar: 'Đổi nhân vật' }
      : { character: 'Character', social: 'Social', trade: 'Trade', activities: 'Activities', settings: 'Settings', help: 'Help', switchChar: 'Switch character' },
    block: vi ? 'Chặn' : 'Block',
    unblock: vi ? 'Bỏ chặn' : 'Unblock',
    report: vi ? 'Báo cáo' : 'Report',
    reportHint: vi
      ? 'Để báo cáo %s: chụp màn hình (📷) và gửi cho GM trong game hoặc qua trang hỗ trợ của website.'
      : 'To report %s: take a screenshot (📷) and send it to a GM in the game or through the website\'s support page.',
    chatToggle: vi ? 'Ẩn/hiện chat' : 'Show/hide chat',
    pickUp: vi ? 'Nhặt đồ' : 'Pick up',
    skillPage: vi ? 'Đổi trang kỹ năng (1-5 / 6-0)' : 'Switch skill page (1-5 / 6-0)',
    switchConfirm: vi ? 'Về màn chọn nhân vật? Trò chơi sẽ tải lại.' : 'Go back to the character list? The game reloads.',
    battery: vi ? 'Pin' : 'Battery',
    powerSave: vi ? 'Tiết kiệm pin' : 'Power save',
  };
}

const HUD_SCALE_KEY = 'hnmo.mobileHudScale';
const HUD_SCALES = [0.85, 1, 1.18] as const;
const SKILL_PAGE_KEY = 'hnmo.mobileSkillPage';
const MAP_OPEN_KEY = 'hnmo.mobileMiniMapOpen';

function readNumber(key: string, fallback: number): number {
  try { const v = Number(localStorage.getItem(key)); return Number.isFinite(v) && localStorage.getItem(key) !== null ? v : fallback; } catch { return fallback; }
}
function writeValue(key: string, value: string): void {
  try { localStorage.setItem(key, value); } catch { /* private window: not remembered */ }
}

/** The grouped menu (left: groups, right: buttons); keys and window events as the desktop's keys open them. */
interface MenuEntry { icon: string; en: string; vi: string; key?: string; event?: string; run?: (hud: MobileHud) => void }
const MENU_GROUPS: { id: keyof Labels['groups']; entries: MenuEntry[] }[] = [
  { id: 'character', entries: [
    { icon: '🎒', en: 'Bag', vi: 'Túi đồ', key: 'i' },
    { icon: '🧍', en: 'Character', vi: 'Nhân vật', key: 'c' },
    { icon: '➕', en: 'Add stats', vi: 'Cộng điểm', event: 'hnmo-add-stats-open' },
    { icon: '📜', en: 'Quests', vi: 'Nhiệm vụ', key: 't' },
    { icon: '🎯', en: 'Missions', vi: 'Nhiệm vụ ngày', key: 'j' },
    { icon: '🤖', en: 'Auto play', vi: 'Treo máy', key: 'z' },
    { icon: '🧭', en: 'Warp', vi: 'Dịch chuyển', key: 'm' },
    { icon: '✨', en: 'Auto buff', vi: 'Tự buff', run: (hud) => hud.autoBuff() },
    { icon: '🔄', en: 'Reset', vi: 'Reset', run: (hud) => hud.resetInfo() },
    { icon: '🗺️', en: 'Full map', vi: 'Bản đồ', key: 'Tab' },
    { icon: '🐴', en: 'Mount chain', vi: 'Bộ sưu tập thú cưỡi', event: 'hnmo-mount-chain-open' },
  ] },
  { id: 'social', entries: [
    { icon: '👥', en: 'Party', vi: 'Tổ đội', key: 'p' },
    { icon: '🤝', en: 'Friends', vi: 'Bạn bè', key: 'f' },
    { icon: '🛡️', en: 'Guild', vi: 'Bang hội', key: 'g' },
    { icon: '⌨️', en: 'Commands', vi: 'Lệnh', key: 'd' },
    { icon: '🌐', en: 'Community', vi: 'Cộng đồng', key: 'k' },
    { icon: '⚔️', en: 'Gens', vi: 'Gens', key: 'b' },
    { icon: '💍', en: 'Marriage', vi: 'Kết hôn', event: 'hnmo-marriage-open' },
    { icon: '📬', en: 'Mailbox', vi: 'Hộp thư', event: 'hnmo-giftbox-open' },
  ] },
  { id: 'trade', entries: [
    { icon: '🛒', en: 'Cash shop', vi: 'Cửa hàng', key: 'x' },
    { icon: '🏪', en: 'Market', vi: 'Chợ', event: 'hnmo-market-open' },
    { icon: '📜', en: 'Escrow', vi: 'Ký quỹ', event: 'hnmo-escrow-open' },
    { icon: '💎', en: 'Jewel bank', vi: 'Ngân hàng ngọc', event: 'hnmo-jewel-bank-open' },
    { icon: '♻️', en: 'Leftovers', vi: 'Đồ thừa', event: 'hnmo-leftovers-open' },
    { icon: '💳', en: 'Top-up', vi: 'Nạp', event: 'hnmo-topup-milestones-open' },
    { icon: '🎁', en: 'Gift code', vi: 'Mã quà', event: 'hnmo-gift-codes-open' },
    { icon: '👑', en: 'VIP', vi: 'VIP', event: 'hnmo-vip-open' },
  ] },
  { id: 'activities', entries: [
    { icon: '🕒', en: 'Events', vi: 'Sự kiện', key: 'h' },
    { icon: '🐉', en: 'World bosses', vi: 'Boss thế giới', key: 'n' },
    { icon: '🍀', en: 'Lucky number', vi: 'Số may mắn', key: 'l' },
    { icon: '🏆', en: 'Top races', vi: 'Đua top', event: 'hnmo-top-races-open' },
    { icon: '📊', en: 'Rankings', vi: 'Xếp hạng', event: 'hnmo-ranking-open' },
    { icon: '📅', en: 'Season plan', vi: 'Lộ trình mùa', event: 'hnmo-season-plan-open' },
    { icon: '🏟', en: 'Arena', vi: 'Đấu trường', event: 'hnmo-arena-open' },
  ] },
  { id: 'settings', entries: [
    { icon: '⚙️', en: 'Settings', vi: 'Cài đặt', key: 'o' },
    { icon: '🔍', en: 'HUD size', vi: 'Cỡ giao diện', run: (hud) => hud.cycleScale() },
    { icon: '🔧', en: 'Auto play settings', vi: 'Cài đặt treo máy', key: 'z' },
  ] },
  { id: 'help', entries: [
    { icon: 'ℹ️', en: 'Guide', vi: 'Cẩm nang', run: (hud) => hud.openGuideFromMenu() },
    { icon: '🧭', en: 'New-player guide', vi: 'Hướng dẫn tân thủ', key: 'y' },
    { icon: '📷', en: 'Screenshot', vi: 'Chụp màn hình', event: 'hnmo-share-open' },
    { icon: '🐞', en: 'Report a bug', vi: 'Báo lỗi', event: 'hnmo-bug-report-open' },
  ] },
  { id: 'switchChar', entries: [
    { icon: '🔁', en: 'Character list', vi: 'Danh sách nhân vật', run: (hud) => hud.switchCharacter() },
  ] },
];
const MENU_GROUP_KEY = 'hnmo.mobileMenuGroup';
function localStorageGroup(): keyof Labels['groups'] {
  try { return (localStorage.getItem(MENU_GROUP_KEY) ?? 'character') as keyof Labels['groups']; } catch { return 'character'; }
}

function pressKey(name: string): void {
  window.dispatchEvent(new KeyboardEvent('keydown', { key: name, code: /^[0-9]$/.test(name) ? `Digit${name}` : name.length === 1 ? `Key${name.toUpperCase()}` : name, bubbles: true }));
}

const CSS = `
/* ---- Compact touch HUD: the game first. Everything sits in the corners; the centre stays clear. ---- */

/* The modern HUD stands in for the classic bottom bar, chat log, system log and auto play bar. */
body.hnmo-modern-hud .rml-main_frame,
body.hnmo-modern-hud .rml-chat_log,
body.hnmo-modern-hud .rml-system_log,
body.hnmo-modern-hud .rml-mu_helper_bar { display: none !important; }

/* The status line ("entered the world", …) as a one-line toast, top centre under the top row. */
body.hnmo-modern-hud #loading {
  top: max(52px, calc(env(safe-area-inset-top, 0px) + 50px));
  bottom: auto;
  width: auto;
  max-width: min(360px, 46vw);
  padding: 3px 10px;
  pointer-events: none;
}
body.hnmo-modern-hud #loading #status { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-size: 11px; }
body.hnmo-modern-hud #loading #track { display: none; }

/* HUD size S / M / L (remembered): the HUD's parts and the fight buttons. */
#mobileHud > *, body.hnmo-modern-hud #touchFight, body.hnmo-modern-hud #touchBar { zoom: var(--mh-scale, 1); }

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

/* Round buttons, at least 36 px */
.mh-top-btn, .mh-round-btn {
  all: unset;
  box-sizing: border-box;
  cursor: pointer;
  min-width: 40px;
  height: 40px;
  border-radius: 999px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: rgba(10, 13, 20, 0.62);
  border: 1px solid rgba(201, 162, 74, 0.55);
  color: #f3e4bc;
  font-size: 17px;
  font-weight: 700;
  line-height: 1;
  touch-action: manipulation;
}
.mh-round-btn { min-width: 36px; height: 36px; font-size: 15px; }
.mh-top-btn.on, .mh-round-btn.on { background: rgba(201, 162, 74, 0.35); border-color: #ffd666; }

/* 1. Hero card, top left (about 160 px); a tap opens AG / SD and the status line */
.mh-hero {
  position: fixed;
  top: max(6px, env(safe-area-inset-top, 0px));
  left: max(8px, env(safe-area-inset-left, 0px));
  width: 160px;
  box-sizing: border-box;
  display: grid;
  grid-template-columns: 30px 1fr;
  gap: 6px;
  align-items: start;
  padding: 4px 6px 5px 4px;
  border-radius: 10px;
  background: rgba(10, 13, 20, 0.55);
  border: 1px solid rgba(201, 162, 74, 0.35);
}
.mh-avatar {
  width: 30px;
  height: 30px;
  border-radius: 50%;
  background: radial-gradient(circle at 35% 30%, #584321, #191d2b);
  border: 1.5px solid #d9b155;
  display: grid;
  place-items: center;
  font-weight: 800;
  font-size: 10px;
  color: #ffe8ad;
}
.mh-hero-meta { display: grid; gap: 2px; min-width: 0; }
.mh-hero-top { display: flex; gap: 4px; align-items: baseline; font-size: 11px; line-height: 1.15; white-space: nowrap; overflow: hidden; }
.mh-hero-name { font-weight: 700; color: #f6e7c1; overflow: hidden; text-overflow: ellipsis; }
.mh-hero-lv { font-size: 9.5px; color: #bac1d4; flex: 0 0 auto; }
.mh-bars { display: grid; gap: 2px; }
.mh-bar { position: relative; height: 8px; border-radius: 3px; background: rgba(0, 0, 0, 0.55); overflow: hidden; }
.mh-bar.ag, .mh-bar.sd { display: none; }
.mh-hero.mh-expanded .mh-bar.ag, .mh-hero.mh-expanded .mh-bar.sd:not([hidden]) { display: block; }
.mh-bar-fill { position: absolute; inset: 0 auto 0 0; width: 0%; transition: width 0.15s; }
.mh-bar.hp .mh-bar-fill { background: linear-gradient(90deg, #8d1310, #ef5244); }
.mh-bar.mp .mh-bar-fill { background: linear-gradient(90deg, #113a88, #4b94f7); }
.mh-bar.ag .mh-bar-fill { background: linear-gradient(90deg, #6b5a12, #e8c547); }
.mh-bar.sd .mh-bar-fill { background: linear-gradient(90deg, #4a4f5e, #c9d2e6); }
.mh-bar-text { position: absolute; inset: 0; display: grid; place-items: center; font-size: 7.5px; font-weight: 700; color: #fff; text-shadow: 0 1px 1px #000; line-height: 1; }
.mh-hero-more { display: none; gap: 3px; font-size: 9.5px; color: #b8bfd2; }
.mh-hero.mh-expanded .mh-hero-more { display: grid; }
.mh-hero.mh-expanded { width: 200px; }
.mh-zone { justify-self: start; padding: 0 5px; border-radius: 999px; font-size: 9px; font-weight: 700; background: rgba(72, 185, 110, 0.22); color: #84e5a4; }
.mh-zone.combat { background: rgba(235, 136, 72, 0.2); color: #ffb988; }
.mh-telemetry { font-size: 9.5px; color: #9aa0b2; }
.mh-status { display: flex; align-items: center; gap: 4px; white-space: normal; }
.mh-power {
  all: unset;
  cursor: pointer;
  flex: 0 0 auto;
  width: 36px;
  height: 36px;
  border-radius: 8px;
  display: grid;
  place-items: center;
  font-size: 15px;
  background: rgba(25, 31, 46, 0.9);
  border: 1px solid rgba(255, 255, 255, 0.16);
}
.mh-power.on { background: rgba(201, 162, 74, 0.35); border-color: #d9b155; }
.mh-buffs {
  position: fixed;
  top: max(52px, calc(env(safe-area-inset-top, 0px) + 50px));
  left: max(8px, env(safe-area-inset-left, 0px));
  display: flex;
  gap: 3px;
  flex-wrap: wrap;
  max-width: 160px;
}
.mh-hero.mh-expanded ~ .mh-buffs { display: none; }
.mh-buff { padding: 1px 4px; border-radius: 5px; background: rgba(14, 18, 28, 0.6); font-size: 9px; color: #ffe8ad; }

/* 2. Top right, one row: [key bar 🎒 🏷 ⋯] [Auto] [☰ without the key bar] [🗺] */
.mh-topright {
  position: fixed;
  top: max(6px, env(safe-area-inset-top, 0px));
  right: calc(max(8px, env(safe-area-inset-right, 0px)) + 44px);
  display: flex;
  gap: 4px;
}
.mh-auto-toggle { width: 72px; font-size: 11px; }
body.hnmo-modern-hud:has(#touchBar.shown) #mobileMenuBtn { display: none; }
body.hnmo-modern-hud #touchBar {
  top: max(6px, env(safe-area-inset-top, 0px)) !important;
  right: calc(max(8px, env(safe-area-inset-right, 0px)) + 124px) !important;
  left: auto !important;
  bottom: auto !important;
  transform: none !important;
  flex-direction: row !important;
  gap: 4px !important;
  padding: 0;
  background: none;
  border: 0;
  z-index: 30;
}
body.hnmo-modern-hud #touchBar button:not([data-key="i"]):not(.names):not(.more) { display: none; }
body.hnmo-modern-hud #touchBar button {
  min-width: 40px;
  min-height: 40px;
  width: 40px;
  height: 40px;
  padding: 0;
  border-radius: 999px;
  font-size: 0;
  background: rgba(10, 13, 20, 0.62);
  border-color: rgba(201, 162, 74, 0.55);
}
body.hnmo-modern-hud #touchBar button::before { font-size: 17px; line-height: 1; }
body.hnmo-modern-hud #touchBar button[data-key="i"]::before { content: '🎒'; }
body.hnmo-modern-hud #touchBar button.names::before { content: '🏷'; }
body.hnmo-modern-hud #touchBar button.more::before { content: '☰'; color: #f3e4bc; }
body.hnmo-modern-hud #touchBar button.on { background: rgba(201, 162, 74, 0.35); border-color: #ffd666; }
/* The ⋯ list lives in the menu sheet while the modern HUD shows. */
body.hnmo-modern-hud > #touchMore { display: none !important; }
#mobileHudSheet #touchMore {
  position: static;
  transform: none;
  display: flex;
  flex-direction: row;
  flex-wrap: wrap;
  gap: 4px;
  max-width: none;
  padding: 0;
  background: none;
  border: 0;
}
#mobileHudSheet #touchMore button { min-width: 36px; min-height: 36px; padding: 0 6px; font-size: 12px; }

/* Target bar, top centre */
.mh-targetbar {
  position: fixed;
  top: max(6px, env(safe-area-inset-top, 0px));
  left: 50%;
  transform: translateX(-50%);
  width: min(200px, 28vw);
  display: grid;
  gap: 2px;
  padding: 2px 8px 3px;
  border-radius: 8px;
  background: rgba(9, 12, 20, 0.6);
  border: 1px solid rgba(235, 120, 90, 0.45);
  text-align: center;
}
.mh-targetbar[hidden] { display: none; }
.mh-targetbar-name { font-size: 10.5px; font-weight: 700; color: #ffd2c4; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.mh-targetbar-track { height: 5px; border-radius: 3px; background: rgba(0, 0, 0, 0.6); overflow: hidden; }
.mh-targetbar-fill { height: 100%; width: 100%; background: linear-gradient(90deg, #8d1310, #ef5244); transition: width 0.15s; }
.mh-targetbar.unknown .mh-targetbar-fill { opacity: 0.45; }

/* 3. Mini map: a round button; open, a 110 px card under the top row */
.mh-minimap {
  position: fixed;
  top: max(6px, env(safe-area-inset-top, 0px));
  right: max(8px, env(safe-area-inset-right, 0px));
}
.mh-minimap-body {
  position: absolute;
  top: 44px;
  right: 0;
  width: 110px;
  border-radius: 8px;
  background: rgba(9, 12, 20, 0.7);
  border: 1px solid rgba(201, 162, 74, 0.45);
  overflow: hidden;
}
.mh-minimap.mh-map-closed .mh-minimap-body { display: none; }
.mh-minimap-head { padding: 2px 5px; font-size: 9.5px; font-weight: 600; color: #ffe8ad; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.mh-minimap canvas { display: block; width: 110px; height: 74px; touch-action: none; cursor: crosshair; }
.mh-mini-btn { all: unset; cursor: pointer; }

/* 4. Bottom left: chat bubble over the joystick */
.mh-left-cluster {
  position: fixed;
  left: max(12px, env(safe-area-inset-left, 0px));
  bottom: max(12px, env(safe-area-inset-bottom, 0px));
  display: grid;
  justify-items: start;
  gap: 6px;
}
.mh-stick {
  position: relative;
  width: 96px;
  height: 96px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.05) 0%, rgba(12, 16, 26, 0.45) 72%);
  border: 1.5px solid rgba(255, 255, 255, 0.2);
  touch-action: none;
}
.mh-stick-knob {
  position: absolute;
  left: 50%;
  top: 50%;
  width: 40px;
  height: 40px;
  margin-left: -20px;
  margin-top: -20px;
  border-radius: 50%;
  background: radial-gradient(circle at 35% 30%, #f1cf7a, #7d6028);
  border: 1.5px solid #fff0c2;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.5);
  pointer-events: none;
}
.mh-stick-mark { position: absolute; font-size: 8px; color: rgba(255, 240, 200, 0.5); pointer-events: none; line-height: 1; }
.mh-stick-mark.up { top: 5px; left: 50%; transform: translateX(-50%); }
.mh-stick-mark.down { bottom: 5px; left: 50%; transform: translateX(-50%); }
.mh-stick-mark.left { left: 6px; top: 50%; transform: translateY(-50%); }
.mh-stick-mark.right { right: 6px; top: 50%; transform: translateY(-50%); }

/* 5. Chat: closed until 💬; open, a small see-through panel low on the left, clear of the centre */
.mh-chat {
  position: fixed;
  left: max(12px, env(safe-area-inset-left, 0px));
  bottom: max(160px, calc(env(safe-area-inset-bottom, 0px) + 156px));
  width: min(210px, 28vw);
  min-width: 170px;
  box-sizing: border-box;
  background: rgba(9, 12, 20, 0.45);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  padding: 4px 16px 4px 6px;
  display: grid;
  gap: 3px;
}
.mh-chat-resize {
  position: absolute;
  top: 2px;
  right: 2px;
  width: 14px;
  height: 14px;
  cursor: nesw-resize;
  touch-action: none;
  border-radius: 3px;
  background: linear-gradient(225deg, rgba(255, 232, 173, 0.7) 0 5px, transparent 5px);
}
.mh-chat.mh-collapsed { display: none; }
.mh-chat-tabs { display: flex; gap: 2px; align-items: center; overflow-x: auto; scrollbar-width: none; }
.mh-chat-tab { all: unset; cursor: pointer; flex: 1; text-align: center; font-size: 9.5px; padding: 2px 3px; border-radius: 4px; color: #a6aec2; white-space: nowrap; }
.mh-chat-tab.active { background: rgba(201, 162, 74, 0.25); color: #ffe8ad; font-weight: 700; }
.mh-chat-lines { height: 46px; overflow-y: auto; font-size: 10.5px; line-height: 1.3; color: #dfe4f0; touch-action: pan-y; text-shadow: 0 1px 1px #000; }
.mh-chat-line.system { color: #9de2a8; }
.mh-chat-line.error { color: #ff8e80; }
.mh-chat-line.whisper { color: #f6b2ef; }
.mh-chat-line.party { color: #8ad2ff; }
.mh-chat-line.guild { color: #ffd484; }
.mh-chat-line[data-sender] { cursor: context-menu; }
.mh-chat-input-row { display: flex; gap: 4px; }
.mh-chat-input-row input { flex: 1; min-width: 0; font-size: 11px; padding: 3px 6px; border-radius: 5px; border: 1px solid rgba(255, 255, 255, 0.18); background: rgba(16, 20, 32, 0.8); color: #fff; }
.mh-circle-btn {
  all: unset;
  box-sizing: border-box;
  cursor: pointer;
  min-width: 36px;
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
}
.mh-macros { display: flex; gap: 3px; align-items: center; overflow-x: auto; scrollbar-width: none; }
.mh-macro-btn { all: unset; cursor: pointer; padding: 2px 6px; border-radius: 999px; background: rgba(34, 41, 60, 0.8); font-size: 9.5px; color: #d6dcf0; white-space: nowrap; }

/* 6. Exp: a thin line along the very bottom */
.mh-exp { position: fixed; left: 0; right: 0; bottom: 0; height: 3px; background: rgba(0, 0, 0, 0.45); }
.mh-exp-fill { height: 100%; width: 0%; background: linear-gradient(90deg, #9f7929, #f5cf70); }
.mh-exp-label { display: none; }

/* 7. Bottom right: the attack button, five skills on a tight arc, and on the outer ring the two first potions,
   pick up, target lock and the skill page. #touchFight keeps its 10 buttons (E and R stay in the bar). */
body.hnmo-modern-hud #touchFight, .mh-right-tools {
  position: fixed;
  right: max(8px, env(safe-area-inset-right, 0px)) !important;
  bottom: max(8px, env(safe-area-inset-bottom, 0px)) !important;
  width: 200px;
  height: 200px;
  pointer-events: none;
}
body.hnmo-modern-hud #touchFight { display: block !important; z-index: 27; }
.mh-right-tools { z-index: 28; }
body.hnmo-modern-hud #touchFight button, .mh-right-tools .mh-arc {
  position: absolute;
  pointer-events: auto;
  box-sizing: border-box;
  display: grid;
  place-items: center;
  padding: 0;
  border-radius: 50%;
  background: rgba(16, 20, 32, 0.6);
  border: 1.5px solid rgba(201, 162, 74, 0.7);
  color: #f6e7c1;
  font-weight: 700;
  touch-action: manipulation;
}
body.hnmo-modern-hud #touchFight button:not(.potion):not(.fight) { width: 40px; height: 40px; min-width: 40px; min-height: 40px; font-size: 13px; }
body.hnmo-modern-hud #touchFight button:nth-of-type(1) { right: 111px; bottom: 6px; }
body.hnmo-modern-hud #touchFight button:nth-of-type(2) { right: 108px; bottom: 49px; }
body.hnmo-modern-hud #touchFight button:nth-of-type(3) { right: 86px; bottom: 86px; }
body.hnmo-modern-hud #touchFight button:nth-of-type(4) { right: 49px; bottom: 108px; }
body.hnmo-modern-hud #touchFight button:nth-of-type(5) { right: 6px; bottom: 111px; }
body.hnmo-modern-hud #touchFight button.hnmo-skill-selected { border-color: #ffd666; box-shadow: 0 0 10px rgba(255, 206, 86, 0.6); }
body.hnmo-modern-hud #touchFight button.potion { width: 36px; height: 36px; min-width: 36px; min-height: 36px; font-size: 11px; border-color: rgba(140, 175, 235, 0.65); }
body.hnmo-modern-hud #touchFight button.potion:nth-of-type(6) { right: 164px; bottom: 12px; }
body.hnmo-modern-hud #touchFight button.potion:nth-of-type(7) { right: 156px; bottom: 72px; }
body.hnmo-modern-hud #touchFight button.potion:nth-of-type(8),
body.hnmo-modern-hud #touchFight button.potion:nth-of-type(9) { display: none; }
body.hnmo-modern-hud #touchFight button.potion[data-count]:not([data-count=""])::after {
  content: attr(data-count);
  position: absolute;
  bottom: -4px;
  right: -5px;
  min-width: 14px;
  padding: 0 3px;
  border-radius: 999px;
  background: #0a0d14;
  color: #ffe9a8;
  font-size: 8.5px;
  line-height: 12px;
  text-align: center;
}
.mh-right-tools .mh-arc { width: 36px; height: 36px; min-width: 36px; font-size: 14px; }
.mh-right-tools .mh-arc.pick { right: 123px; bottom: 123px; }
.mh-right-tools .mh-arc.target { right: 72px; bottom: 156px; }
.mh-right-tools .mh-arc.page { right: 12px; bottom: 164px; font-size: 11px; }
body.hnmo-modern-hud #touchFight button.fight {
  width: 64px;
  height: 64px;
  min-width: 64px;
  right: 10px;
  bottom: 10px;
  font-size: 26px;
  background: radial-gradient(circle at 35% 30%, rgba(97, 73, 31, 0.85), rgba(34, 25, 9, 0.85) 74%);
  border: 2px solid #f0c96d;
  color: #fff2c4;
}

/* The menu sheet (QoL row, grouped menu, the key list) and the macro / player sheets */
#mobileHudSheet {
  position: fixed;
  left: 50%;
  bottom: env(safe-area-inset-bottom, 0px);
  transform: translateX(-50%);
  width: min(600px, 100vw);
  box-sizing: border-box;
  max-height: min(88vh, 520px);
  overflow-y: auto;
  background: rgba(11, 14, 23, 0.96);
  border: 1px solid #c9a24a;
  border-bottom: 0;
  border-radius: 14px 14px 0 0;
  padding: 10px 12px 14px;
  z-index: 75;
  color: #e6e8ef;
  display: grid;
  gap: 8px;
  pointer-events: auto;
  font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
}
#mobileHudSheet[hidden] { display: none !important; }
.mh-sheet-head { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(201, 162, 74, 0.3); padding-bottom: 6px; }
.mh-sheet-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(130px, 1fr)); gap: 6px; }
.mh-sheet-btn {
  border: 1px solid rgba(201, 162, 74, 0.45);
  background: rgba(25, 31, 46, 0.92);
  color: #ffe8ad;
  border-radius: 8px;
  padding: 6px 8px;
  font: 600 11.5px system-ui, sans-serif;
  cursor: pointer;
  min-height: 36px;
}
.mh-sheet-sub { font-size: 11px; color: #a6aec2; }
.mh-macro-row { display: grid; grid-template-columns: 90px 1fr; gap: 6px; }
.mh-macro-row input { padding: 6px 8px; border-radius: 6px; border: 1px solid #323c54; background: #141926; color: #fff; }
.mh-menu { display: grid; grid-template-columns: 112px 1fr; gap: 8px; border-top: 1px solid rgba(201, 162, 74, 0.3); padding-top: 8px; }
.mh-menu-side { display: grid; gap: 4px; align-content: start; }
.mh-menu-group { all: unset; cursor: pointer; min-height: 36px; box-sizing: border-box; padding: 9px 8px; border-radius: 7px; font-size: 12px; font-weight: 600; color: #c8cfdf; background: rgba(25, 31, 46, 0.7); }
.mh-menu-group.active { background: rgba(201, 162, 74, 0.3); color: #ffe8ad; }
.mh-menu-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(76px, 1fr)); gap: 6px; align-content: start; }
.mh-menu-item {
  all: unset;
  cursor: pointer;
  display: grid;
  justify-items: center;
  gap: 3px;
  padding: 7px 4px;
  border-radius: 9px;
  background: rgba(25, 31, 46, 0.92);
  border: 1px solid rgba(201, 162, 74, 0.35);
  color: #ffe8ad;
  font-size: 10.5px;
  font-weight: 600;
  text-align: center;
}
.mh-menu-icon { font-size: 19px; line-height: 1; }
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
  private touchMoreEl: HTMLElement | null = null;
  private mapTitleEl!: HTMLSpanElement;
  private mapCanvas!: HTMLCanvasElement;
  private hidePlayersBtn!: HTMLButtonElement;
  private stickEl!: HTMLDivElement;
  private knobEl!: HTMLDivElement;
  private targetPill!: HTMLButtonElement;
  private expFillEl!: HTMLDivElement;
  private expLabelEl!: HTMLDivElement;
  private chatTabsWrap!: HTMLDivElement;
  private chatLinesEl!: HTMLDivElement;
  private chatInputEl!: HTMLInputElement;
  private macroBarEl!: HTMLDivElement;
  private actionLabels: { btn: HTMLButtonElement; read: (l: Labels) => string }[] = [];
  // The bars in the hero card, the top-right row, the target bar, the status line, the combat extras.
  private bars: Record<'hp' | 'mp' | 'ag' | 'sd', { fill: HTMLDivElement; text: HTMLSpanElement; row: HTMLDivElement }> = {} as never;
  private autoToggleEl!: HTMLButtonElement;
  private mapToggleEl!: HTMLButtonElement;
  private mapCardEl!: HTMLDivElement;
  private targetBarEl!: HTMLDivElement;
  private targetNameEl!: HTMLSpanElement;
  private targetFillEl!: HTMLDivElement;
  private statusEl!: HTMLDivElement;
  private skillPageEl!: HTMLButtonElement;
  private chatCardEl!: HTMLDivElement;
  private batteryLevel: number | null = null;
  private scale = readNumber(HUD_SCALE_KEY, 1);
  private skillPage = readNumber(SKILL_PAGE_KEY, 0) === 1 ? 1 : 0;
  private mapOpen = readNumber(MAP_OPEN_KEY, 0) === 1;

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
    this.closeSheet();

    this.buildDom();
    document.body.append(this.root, this.sheet);
    this.applyScale();
    this.applySkillPage();
    // The battery's level for the status line, where the browser tells it.
    const nav = navigator as Navigator & { getBattery?: () => Promise<{ level: number; addEventListener(t: string, f: () => void): void }> };
    nav.getBattery?.().then((battery) => {
      const read = () => { this.batteryLevel = battery.level; };
      read();
      battery.addEventListener('levelchange', read);
    }).catch(() => undefined);

    this.actions.chat.onChange(() => this.renderChat());
    // The key bar's ⋯ (touchControls) opens the menu sheet while this HUD shows; a key in its list closes it.
    window.addEventListener('hnmo-touch-more', () => {
      // The sheet's own state decides (the list's open class can be stale after a QoL button).
      if (this.active) this.toggleMenu();
    });
    this.sheet.addEventListener('click', (e) => {
      if ((e.target as HTMLElement).closest('#touchMore button')) this.closeSheet();
    }, true);
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

  /** Which five skills the skill buttons hold: 0 = keys 1-5, 1 = keys 6-0. */
  get skillPageIndex(): number {
    return this.skillPage;
  }

  /** The HUD's size (S / M / L), on the HUD and the fight buttons. */
  private applyScale(): void {
    const scale = HUD_SCALES.includes(this.scale as typeof HUD_SCALES[number]) ? this.scale : 1;
    document.documentElement.style.setProperty('--mh-scale', String(scale));
  }

  /** The next size: S, M, L, S, … (remembered). */
  cycleScale(): void {
    const at = HUD_SCALES.indexOf(this.scale as typeof HUD_SCALES[number]);
    this.scale = HUD_SCALES[(at + 1) % HUD_SCALES.length];
    writeValue(HUD_SCALE_KEY, String(this.scale));
    this.applyScale();
  }

  private sizeName(): string {
    const at = Math.max(0, HUD_SCALES.indexOf(this.scale as typeof HUD_SCALES[number]));
    return labels().sizes[at];
  }

  /** The skill page on the 1/2 button, told to the fight buttons (touchControls) with an event. */
  private applySkillPage(): void {
    if (this.skillPageEl) {
      this.skillPageEl.textContent = this.skillPage === 0 ? '1/2' : '2/2';
      this.skillPageEl.classList.toggle('on', this.skillPage === 1);
    }
    window.dispatchEvent(new CustomEvent('hnmo-skill-page', { detail: { page: this.skillPage } }));
  }

  private applyMapOpen(): void {
    this.mapCardEl.classList.toggle('mh-map-closed', !this.mapOpen);
    this.mapToggleEl.textContent = this.mapOpen ? '▲' : '▼';
  }

  openGuideFromMenu(): void {
    this.closeSheet();
    this.actions.openGuide();
  }

  /** Back to the character list, after asking. */
  switchCharacter(): void {
    if (!window.confirm(labels().switchConfirm)) return;
    this.closeSheet();
    if (this.actions.switchCharacter) this.actions.switchCharacter();
    else location.reload();
  }

  /** A long press on a player's chat line: block / unblock, or how to report. */
  private openPlayerSheet(name: string): void {
    const l = labels();
    this.clearSheet();
    this.sheet.hidden = false;
    const head = document.createElement('div');
    head.className = 'mh-sheet-head';
    const title = document.createElement('strong');
    title.textContent = name;
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button'; closeBtn.className = 'mh-sheet-btn'; closeBtn.style.minHeight = '30px'; closeBtn.textContent = l.close;
    closeBtn.addEventListener('click', () => this.closeSheet());
    head.append(title, closeBtn);
    const grid = document.createElement('div');
    grid.className = 'mh-sheet-grid';
    const blocked = (this.actions.isBlocked ?? chatIsBlocked)(name);
    const blockBtn = document.createElement('button');
    blockBtn.type = 'button'; blockBtn.className = 'mh-sheet-btn'; blockBtn.id = 'mobileChatBlock';
    blockBtn.textContent = `🚫 ${blocked ? l.unblock : l.block}`;
    blockBtn.addEventListener('click', () => { (this.actions.setBlocked ?? chatSetBlocked)(name, !blocked); this.closeSheet(); this.renderChat(); });
    const reportBtn = document.createElement('button');
    reportBtn.type = 'button'; reportBtn.className = 'mh-sheet-btn'; reportBtn.id = 'mobileChatReport';
    reportBtn.textContent = `⚑ ${l.report}`;
    reportBtn.addEventListener('click', () => { this.actions.chat.add(l.reportHint.replace('%s', name), 'system'); this.closeSheet(); });
    grid.append(blockBtn, reportBtn);
    this.sheet.append(head, grid);
  }

  /** The status line: zen · FPS · ping · network · battery · level and exp. */
  private renderStatus(snap: MobileHudSnapshot): void {
    const l = labels();
    const parts: string[] = [];
    if (typeof snap.zen === 'number') parts.push(`💰 ${snap.zen.toLocaleString()}`);
    parts.push(`${snap.fps} FPS`);
    if (typeof snap.pingMs === 'number') parts.push(`${Math.round(snap.pingMs)} ms`);
    const conn = (navigator as Navigator & { connection?: { effectiveType?: string } }).connection;
    parts.push(navigator.onLine === false ? '📵' : `📶 ${conn?.effectiveType ?? ''}`.trim());
    if (this.batteryLevel !== null && Number.isFinite(this.batteryLevel)) parts.push(`🔋 ${Math.round(this.batteryLevel * 100)}%`);
    parts.push(`${l.lv}.${snap.level} · ${snap.expPercentText}`);
    const text = this.statusEl.querySelector<HTMLSpanElement>('.mh-status-text');
    if (text) text.textContent = parts.join(' · ');
    const save = this.statusEl.querySelector<HTMLButtonElement>('.mh-power');
    if (save) {
      save.classList.toggle('on', snap.batterySaver);
      save.title = `${l.powerSave}: ${snap.batterySaver ? l.on : l.off}`;
    }
  }

  private syncVisibility(): void {
    const show = this.active;
    this.root.hidden = !show;
    document.body.classList.toggle('hnmo-modern-hud', show);
    if (!show) {
      this.closeSheet();
      // The key list goes back to the page for the classic touch bar.
      if (this.touchMoreEl && this.touchMoreEl.parentElement !== document.body) document.body.append(this.touchMoreEl);
    }
  }

  /** Empties the sheet, keeping hold of the key list (#touchMore) if it was in it. */
  private clearSheet(): void {
    this.touchMoreEl ??= document.getElementById('touchMore');
    this.sheet.classList.remove('mh-menu-sheet');
    this.sheet.replaceChildren();
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
    const round = (id: string, text: string, cls: string, run: () => void) => {
      const b = document.createElement('button');
      b.type = 'button';
      b.id = id;
      b.className = cls;
      b.textContent = text;
      stop(b);
      b.addEventListener('click', (e) => { e.stopPropagation(); run(); });
      return b;
    };

    // 1. Top left: a small hero card (portrait, name, level, thin life and mana bars). A tap opens AG / SD and
    // the status line (zen · FPS · network · battery · exp) with the power-save switch.
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
    this.nameEl.className = 'mh-hero-name';
    this.nameEl.textContent = '-';
    this.lvEl = document.createElement('span');
    this.lvEl.className = 'mh-hero-lv';
    top.append(this.nameEl, this.lvEl);
    const barsEl = document.createElement('div');
    barsEl.className = 'mh-bars';
    for (const kind of ['hp', 'mp', 'ag', 'sd'] as const) {
      const row = document.createElement('div');
      row.className = `mh-bar ${kind}`;
      const fill = document.createElement('div');
      fill.className = 'mh-bar-fill';
      const text = document.createElement('span');
      text.className = 'mh-bar-text';
      row.append(fill, text);
      barsEl.append(row);
      this.bars[kind] = { fill, text, row };
    }
    this.zoneEl = document.createElement('span');
    this.zoneEl.className = 'mh-zone';
    this.telemetryEl = document.createElement('div');
    this.telemetryEl.className = 'mh-telemetry';
    this.statusEl = document.createElement('div');
    this.statusEl.id = 'mobileStatusLine';
    this.statusEl.className = 'mh-status';
    const statusText = document.createElement('span');
    statusText.className = 'mh-status-text';
    const powerBtn = round('mobilePowerSave', '⚡', 'mh-power', () => {
      const on = this.actions.toggleBatterySaver();
      powerBtn.classList.toggle('on', on);
    });
    this.statusEl.append(powerBtn, statusText);
    const more = document.createElement('div');
    more.className = 'mh-hero-more';
    more.append(this.zoneEl, this.telemetryEl, this.statusEl);
    meta.append(top, barsEl, more);
    heroCard.append(this.avatarEl, meta);
    heroCard.addEventListener('click', (e) => {
      if ((e.target as HTMLElement).closest('button')) return;
      heroCard.classList.toggle('mh-expanded');
    });

    this.buffsEl = document.createElement('div');
    this.buffsEl.id = 'mobileBuffRow';
    this.buffsEl.className = 'mh-buffs';

    // 2. Top right: one small row. In the game the key bar (#touchBar) adds the bag, item names and its ⋯
    // menu next to it (touchControls); here: auto play on / off, the menu (☰) and the mini map.
    const topRight = document.createElement('div');
    topRight.id = 'mobileTopRight';
    topRight.className = 'mh-topright mh-interactive';
    stop(topRight);
    this.autoToggleEl = round('mobileAutoToggle', '', 'mh-top-btn mh-auto-toggle', () => this.actions.toggleAutoPlay());
    const menuBtn = round('mobileMenuBtn', '☰', 'mh-top-btn', () => this.toggleMenu());
    menuBtn.setAttribute('aria-label', 'Menu');
    topRight.append(this.autoToggleEl, menuBtn);

    // The target's name and life, top centre, while there is one.
    this.targetBarEl = document.createElement('div');
    this.targetBarEl.id = 'mobileTargetBar';
    this.targetBarEl.className = 'mh-targetbar';
    this.targetBarEl.hidden = true;
    this.targetNameEl = document.createElement('span');
    this.targetNameEl.className = 'mh-targetbar-name';
    const targetTrack = document.createElement('div');
    targetTrack.className = 'mh-targetbar-track';
    this.targetFillEl = document.createElement('div');
    this.targetFillEl.className = 'mh-targetbar-fill';
    targetTrack.append(this.targetFillEl);
    this.targetBarEl.append(this.targetNameEl, targetTrack);

    // 3. The mini map: a round 🗺 button; open, a small card (about 110 px) under the top row (remembered).
    const mapCard = document.createElement('div');
    mapCard.id = 'mobileMiniMap';
    mapCard.className = 'mh-minimap mh-interactive';
    stop(mapCard);
    this.mapToggleEl = round('mobileMiniMapToggle', '🗺', 'mh-top-btn mh-map-btn', () => {
      this.mapOpen = !this.mapOpen;
      writeValue(MAP_OPEN_KEY, this.mapOpen ? '1' : '0');
      this.applyMapOpen();
    });
    const mapBody = document.createElement('div');
    mapBody.className = 'mh-minimap-body';
    const mapHead = document.createElement('div');
    mapHead.className = 'mh-minimap-head';
    this.mapTitleEl = document.createElement('span');
    this.mapTitleEl.textContent = '-';
    mapHead.append(this.mapTitleEl);
    this.mapCanvas = document.createElement('canvas');
    this.mapCanvas.id = 'mobileMiniMapCanvas';
    this.mapCanvas.width = 110;
    this.mapCanvas.height = 74;
    this.wireMiniMapGestures();
    this.hidePlayersBtn = document.createElement('button');
    this.hidePlayersBtn.type = 'button';
    this.hidePlayersBtn.id = 'mobileHidePlayersBtn';
    mapBody.append(mapHead, this.mapCanvas);
    mapCard.append(this.mapToggleEl, mapBody);
    this.mapCardEl = mapCard;

    // 4. Bottom left: the joystick (four direction marks) and the chat bubble above it.
    const leftCluster = document.createElement('div');
    leftCluster.className = 'mh-left-cluster';
    this.stickEl = document.createElement('div');
    this.stickEl.id = 'mobileThumbstick';
    this.stickEl.className = 'mh-stick mh-interactive';
    this.stickEl.dataset.nx = '0';
    this.stickEl.dataset.ny = '0';
    this.knobEl = document.createElement('div');
    this.knobEl.className = 'mh-stick-knob';
    for (const [dir, mark] of [['up', '▲'], ['down', '▼'], ['left', '◀'], ['right', '▶']] as const) {
      const arrow = document.createElement('span');
      arrow.className = `mh-stick-mark ${dir}`;
      arrow.textContent = mark;
      this.stickEl.append(arrow);
    }
    this.stickEl.append(this.knobEl);
    this.wireThumbstick();
    const chatToggle = round('mobileChatToggle', '💬', 'mh-round-btn mh-chat-bubble', () => {
      const closed = this.chatCardEl.classList.toggle('mh-collapsed');
      chatToggle.classList.toggle('on', !closed);
    });
    leftCluster.append(chatToggle, this.stickEl);

    // 5. Chat: closed until 💬; open, a small see-through panel low on the left (the macros inside it).
    const chatCard = document.createElement('div');
    chatCard.id = 'mobileChatBox';
    chatCard.className = 'mh-chat mh-interactive mh-collapsed';
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
    const drawerBounds: BoxWindowBounds = { minW: 170, maxW: 560, minH: 46, maxH: 360 };
    const savedDrawer = loadWindowSize('mobile_chat', drawerBounds);
    if (savedDrawer) {
      chatCard.style.width = `${savedDrawer.width}px`;
      this.chatLinesEl.style.height = `${savedDrawer.height}px`;
    }
    const resizeCorner = document.createElement('div');
    resizeCorner.id = 'mobileChatResize';
    resizeCorner.className = 'mh-chat-resize';
    resizeCorner.title = 'Drag to resize';
    resizeCorner.addEventListener('pointerdown', (e) => {
      e.preventDefault();
      e.stopPropagation();
      const startX = e.clientX;
      const startY = e.clientY;
      const startW = chatCard.getBoundingClientRect().width || savedDrawer?.width || 210;
      const startH = this.chatLinesEl.getBoundingClientRect().height || savedDrawer?.height || 46;
      let latest = { width: Math.round(startW), height: Math.round(startH) };
      const move = (ev: PointerEvent) => {
        const w = Math.max(drawerBounds.minW, Math.min(drawerBounds.maxW, Math.round(startW + (ev.clientX - startX))));
        const h = Math.max(drawerBounds.minH, Math.min(drawerBounds.maxH, Math.round(startH + (startY - ev.clientY))));
        latest = { width: w, height: h };
        chatCard.style.width = `${w}px`;
        this.chatLinesEl.style.height = `${h}px`;
      };
      const up = () => {
        window.removeEventListener('pointermove', move);
        window.removeEventListener('pointerup', up);
        saveWindowSize('mobile_chat', latest, drawerBounds);
      };
      window.addEventListener('pointermove', move);
      window.addEventListener('pointerup', up);
    });
    chatCard.append(resizeCorner, this.chatTabsWrap, this.chatLinesEl, inputRow, this.macroBarEl);
    this.chatCardEl = chatCard;

    // 6. Exp: a thin line along the very bottom.
    const expWrap = document.createElement('div');
    expWrap.id = 'mobileExpLine';
    expWrap.className = 'mh-exp';
    this.expFillEl = document.createElement('div'); this.expFillEl.className = 'mh-exp-fill';
    this.expLabelEl = document.createElement('div'); this.expLabelEl.className = 'mh-exp-label';
    expWrap.append(this.expFillEl, this.expLabelEl);

    // 7. Bottom right, on the outer ring of the fight buttons (#touchFight keeps its 10 buttons):
    // the skill page (1/2), target lock and pick up.
    const rightTools = document.createElement('div');
    rightTools.id = 'mobileRightTools';
    rightTools.className = 'mh-right-tools';
    this.skillPageEl = round('mobileSkillPage', '1/2', 'mh-round-btn mh-arc page', () => {
      this.skillPage = this.skillPage === 0 ? 1 : 0;
      writeValue(SKILL_PAGE_KEY, String(this.skillPage));
      this.applySkillPage();
    });
    this.targetPill = round('mobileTargetLock', '🎯', 'mh-round-btn mh-arc target', () => {
      const locked = this.actions.cycleTarget();
      this.showTargetLock(locked);
    });
    const pickBtn = round('mobilePickBtn', '✋', 'mh-round-btn mh-arc pick', () => { this.actions.pickNearest(); });
    rightTools.append(this.skillPageEl, this.targetPill, pickBtn);

    this.root.append(heroCard, this.buffsEl, topRight, this.targetBarEl, mapCard, leftCluster, chatCard, expWrap, rightTools);
    this.applyMapOpen();
    this.refreshLabels();
    this.renderChat();
  }

  /** The target lock button: its title says the locked monster ("" = none). */
  private showTargetLock(name: string | null | undefined): void {
    const l = labels();
    this.targetPill.title = name ? `🎯 ${name}` : `🎯 ${l.targetNone}`;
    this.targetPill.dataset.target = name ?? '';
    this.targetPill.classList.toggle('on', Boolean(name));
  }

  /** ☰ (and the key bar's ⋯ in the game): the menu sheet opens or closes. */
  toggleMenu(open = this.sheet.hidden || !this.sheet.classList.contains('mh-menu-sheet')): void {
    if (open) this.openQuickSheet();
    else this.closeSheet();
  }

  private closeSheet(): void {
    this.sheet.hidden = true;
    this.sheet.classList.remove('mh-menu-sheet');
    document.getElementById('touchMore')?.classList.remove('open');
  }

  autoBuff(): void {
    this.closeSheet();
    this.actions.triggerAutoBuff();
  }

  resetInfo(): void {
    this.closeSheet();
    this.actions.sendCommand('/reset info');
  }

  private refreshLabels(): void {
    const l = labels();
    for (const entry of this.actionLabels) entry.btn.textContent = entry.read(l);
    this.chatInputEl.placeholder = l.chatPlaceholder;
    if (!this.latest?.targetName) this.showTargetLock(null);
    if (!this.latest) this.autoToggleEl.textContent = `${l.autoShort} ${l.off}`;
    this.mapToggleEl.title = this.mapOpen ? '▲' : '▼';

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
      if (msg.id && msg.id !== this.latest?.heroName) this.wireLongPress(line, msg.id);
      this.chatLinesEl.append(line);
    }
    this.chatLinesEl.scrollTop = this.chatLinesEl.scrollHeight;
  }

  /** Holding a player's line for half a second opens Block / Report for that player. */
  private wireLongPress(line: HTMLElement, name: string): void {
    let timer = 0;
    let at: { x: number; y: number } | null = null;
    const cancel = () => { window.clearTimeout(timer); at = null; };
    line.dataset.sender = name;
    line.addEventListener('pointerdown', (e) => {
      at = { x: e.clientX, y: e.clientY };
      timer = window.setTimeout(() => { at = null; this.openPlayerSheet(name); }, 520);
    });
    line.addEventListener('pointermove', (e) => { if (at && Math.hypot(e.clientX - at.x, e.clientY - at.y) > 10) cancel(); });
    line.addEventListener('pointerup', cancel);
    line.addEventListener('pointercancel', cancel);
    line.addEventListener('contextmenu', (e) => { e.preventDefault(); cancel(); this.openPlayerSheet(name); });
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
    this.clearSheet();
    this.sheet.hidden = false;
    const head = document.createElement('div');
    head.className = 'mh-sheet-head';
    const title = document.createElement('strong');
    title.textContent = l.editMacros;
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button'; closeBtn.className = 'mh-sheet-btn'; closeBtn.style.minHeight = '30px'; closeBtn.textContent = l.close;
    closeBtn.addEventListener('click', () => this.closeSheet());
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
      this.closeSheet();
    });
    this.sheet.append(saveBtn);
  }

  private openQuickSheet(): void {
    const l = labels();
    const snap = this.latest;
    this.clearSheet();
    this.sheet.hidden = false;

    const head = document.createElement('div');
    head.className = 'mh-sheet-head';
    const title = document.createElement('strong');
    title.textContent = l.qolTitle;
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button'; closeBtn.className = 'mh-sheet-btn'; closeBtn.style.minHeight = '30px'; closeBtn.textContent = l.close;
    closeBtn.addEventListener('click', () => this.closeSheet());
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

    addAction(`🧹 ${l.sellJunk}`, () => { this.actions.sellJunk(); this.closeSheet(); }, 'mobileQolSellJunk');
    addAction(`🔧 ${l.repairAll}`, () => { this.actions.repairAll(); this.closeSheet(); }, 'mobileQolRepairAll');
    const lootBtn = addAction(`💎 ${l.lootFilter}: ${l.lootNames[this.lootFilter]}`, () => {
      const next = LOOT_FILTERS[(LOOT_FILTERS.indexOf(this.lootFilter) + 1) % LOOT_FILTERS.length];
      this.lootFilter = next;
      saveAutoLootFilter(next);
      this.actions.setLootFilter?.(next);
      lootBtn.textContent = `💎 ${l.lootFilter}: ${l.lootNames[next]}`;
    }, 'mobileQolLootFilter');
    addAction(`${l.autoPlay} ⚙`, () => { this.closeSheet(); this.actions.openAutoSettings(); });
    addAction(`${l.batterySaver}: ${snap?.batterySaver ? 'ON' : 'OFF'}`, () => {
      const on = this.actions.toggleBatterySaver();
      this.closeSheet();
      void on;
    }, 'mobileQolBattery');
    addAction(`${l.wakeLock}: ${snap?.wakeLock ? 'ON' : 'OFF'}`, () => {
      this.actions.toggleWakeLock();
      this.closeSheet();
    }, 'mobileQolWakeLock');
    addAction(`${l.hidePlayers}: ${snap?.hideOtherPlayers ? 'ON' : 'OFF'}`, () => {
      this.actions.toggleHidePlayers();
      this.closeSheet();
    });

    const sizeBtn = addAction(`🔍 ${l.hudSize}: ${this.sizeName()}`, () => {
      this.cycleScale();
      sizeBtn.textContent = `🔍 ${l.hudSize}: ${this.sizeName()}`;
    }, 'mobileQolHudSize');

    this.sheet.append(head, grid, this.buildGroupedMenu());
    // The key bar's list of windows, at the bottom of the menu.
    if (this.touchMoreEl) {
      this.touchMoreEl.classList.add('open');
      this.sheet.append(this.touchMoreEl);
    }
    this.sheet.classList.add('mh-menu-sheet');
  }

  /** The grouped menu: groups on the left, a grid of icon + label buttons on the right. */
  private buildGroupedMenu(): HTMLDivElement {
    const l = labels();
    const vi = currentLanguage() === 'vi';
    const wrap = document.createElement('div');
    wrap.id = 'mobileMenuGroups';
    wrap.className = 'mh-menu';
    const side = document.createElement('div');
    side.className = 'mh-menu-side';
    const body = document.createElement('div');
    body.className = 'mh-menu-grid';
    let current = MENU_GROUPS.some((g) => g.id === localStorageGroup()) ? localStorageGroup() : 'character';
    const show = () => {
      side.querySelectorAll<HTMLButtonElement>('button').forEach((b) => b.classList.toggle('active', b.dataset.group === current));
      body.replaceChildren();
      const group = MENU_GROUPS.find((g) => g.id === current) ?? MENU_GROUPS[0];
      for (const entry of group.entries) {
        const b = document.createElement('button');
        b.type = 'button';
        b.className = 'mh-menu-item';
        const icon = document.createElement('span');
        icon.className = 'mh-menu-icon';
        icon.textContent = entry.icon;
        const name = document.createElement('span');
        name.textContent = vi ? entry.vi : entry.en;
        b.append(icon, name);
        b.addEventListener('click', () => {
          if (entry.run) { entry.run(this); return; }
          this.closeSheet();
          if (entry.event) window.dispatchEvent(new Event(entry.event));
          else if (entry.key) pressKey(entry.key);
        });
        body.append(b);
      }
    };
    for (const group of MENU_GROUPS) {
      const b = document.createElement('button');
      b.type = 'button';
      b.className = 'mh-menu-group';
      b.dataset.group = group.id;
      b.textContent = l.groups[group.id];
      b.addEventListener('click', () => { current = group.id; writeValue(MENU_GROUP_KEY, group.id); show(); });
      side.append(b);
    }
    show();
    wrap.append(side, body);
    return wrap;
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
    this.telemetryEl.textContent = `${snap.className || '-'}${snap.batterySaver ? ' · ⚡' : ''}${isWakeLockActive() || snap.wakeLock ? ` · ${l.wakeActive}` : ''}`;

    this.buffsEl.replaceChildren();
    for (const b of snap.buffs.slice(0, 6)) {
      const chip = document.createElement('span');
      chip.className = 'mh-buff';
      chip.textContent = b.secondsLeft > 0 ? `${b.label} ${Math.ceil(b.secondsLeft)}s` : b.label;
      this.buffsEl.append(chip);
    }

    this.mapTitleEl.textContent = `${snap.mapName} ${snap.cell.x},${snap.cell.y}`;
    this.hidePlayersBtn.classList.toggle('on', snap.hideOtherPlayers);
    this.autoToggleEl.classList.toggle('on', snap.autoPlayActive);
    this.autoToggleEl.textContent = `${l.autoShort} ${snap.autoPlayActive ? l.on : l.off}`;
    this.showTargetLock(snap.targetName);
    // The target bar: name and life fraction, hidden with no target.
    this.targetBarEl.hidden = !snap.targetName;
    if (snap.targetName) {
      const health = typeof snap.targetHealth === 'number' && snap.targetHealth >= 0 ? Math.min(1, snap.targetHealth) : null;
      this.targetNameEl.textContent = health === null ? snap.targetName : `${snap.targetName} · ${Math.round(health * 100)}%`;
      this.targetFillEl.style.width = `${((health ?? 1) * 100).toFixed(1)}%`;
      this.targetBarEl.classList.toggle('unknown', health === null);
    }

    // Life, mana, AG and SD bars on the hero card (SD only where the hero has it).
    const setBar = (kind: 'hp' | 'mp' | 'ag' | 'sd', cur: number, max: number) => {
      const bar = this.bars[kind];
      const pct = max > 0 ? Math.max(0, Math.min(100, (cur / max) * 100)) : 0;
      bar.fill.style.width = `${pct.toFixed(1)}%`;
      bar.text.textContent = `${kind.toUpperCase()} ${Math.max(0, Math.round(cur))} / ${Math.round(max)}`;
    };
    setBar('hp', snap.hp, snap.maxHp);
    setBar('mp', snap.mp, snap.maxMp);
    setBar('ag', snap.ag, snap.maxAg);
    setBar('sd', snap.sd, snap.maxSd);
    this.bars.sd.row.hidden = !(snap.maxSd > 1);
    this.renderStatus(snap);

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
        if (b) b.dataset.count = count > 0 ? String(count) : '+';
      }
    }

    this.drawMiniMap();
  }
}
