/** Fictional preview fixtures. None of these identifiers belongs to a live account. */
export type HubLanguage = 'en' | 'vi';
export type Localized = readonly [string, string];
export interface HubItem { id: string; name: Localized; character: string; storage: Localized; world: string; attack: number; defense: number; speed: number; expires: string | null; locked: boolean }
export interface HubEvent { id: string; traceId: string; kind: 'drop' | 'pickup' | 'trade' | 'upgrade' | 'storage' | 'craft' | 'reward' | 'boss'; at: string; character: string; location: string; slot: string; description: Localized; before?: string; after?: string; parents?: readonly string[] }
export const hubItems: readonly HubItem[] = [
  { id: 'DEMO-ITEM-0001', name: ['Example sword +9', 'Kiếm mẫu +9'], character: 'Explorer', storage: ['Explorer · bag', 'Explorer · túi đồ'], world: 'S6.3 · Atlas', attack: 148, defense: 0, speed: 25, expires: null, locked: true },
  { id: 'DEMO-ITEM-0002', name: ['Example jewel', 'Ngọc mẫu'], character: 'Explorer', storage: ['Vault 2', 'Rương 2'], world: 'S6.3 · Atlas', attack: 0, defense: 0, speed: 0, expires: null, locked: false },
  { id: 'DEMO-ITEM-0003', name: ['Example ring', 'Nhẫn mẫu'], character: 'Explorer', storage: ['Explorer · equipped', 'Explorer · đang mặc'], world: 'S6.3 · Atlas', attack: 0, defense: 18, speed: 0, expires: null, locked: true },
  { id: 'DEMO-ITEM-0004', name: ['Example blade +7', 'Lưỡi kiếm mẫu +7'], character: 'Explorer', storage: ['Vault 1', 'Rương 1'], world: 'S6.3 · Atlas', attack: 126, defense: 8, speed: 30, expires: null, locked: false },
  { id: 'DEMO-ITEM-0005', name: ['Example timed cloak', 'Áo choàng mẫu có thời hạn'], character: 'Starlight', storage: ['Starlight · bag', 'Starlight · túi đồ'], world: 'S9 · Aurora', attack: 0, defense: 35, speed: 5, expires: '2026-11-23T09:00:00Z', locked: false },
];
export const hubEvents: readonly HubEvent[] = [
  { id: 'EVT-001', traceId: 'DEMO-ITEM-0001', kind: 'drop', at: '2026-11-20T08:00:00Z', character: 'Explorer', location: 'Dungeon (51, 82)', slot: 'ground', description: ['A monster dropped the sword.', 'Quái vật làm rơi thanh kiếm.'] },
  { id: 'EVT-002', traceId: 'DEMO-ITEM-0001', kind: 'pickup', at: '2026-11-20T08:01:00Z', character: 'Explorer', location: 'Dungeon (51, 82)', slot: 'bag:12', description: ['Picked up into the bag.', 'Nhặt vào túi đồ.'] },
  { id: 'EVT-003', traceId: 'DEMO-ITEM-0001', kind: 'upgrade', at: '2026-11-20T09:00:00Z', character: 'Explorer', location: 'Town', slot: 'bag:12', description: ['Upgrade succeeded; identity retained.', 'Nâng cấp thành công; giữ nguyên mã vật phẩm.'], before: '+8', after: '+9' },
  { id: 'EVT-004', traceId: 'DEMO-ITEM-0001', kind: 'storage', at: '2026-11-21T08:00:00Z', character: 'Explorer', location: 'Town', slot: 'vault:1:5 → bag:12', description: ['Moved from vault page 1 into the bag.', 'Chuyển từ trang rương 1 vào túi đồ.'] },
  { id: 'EVT-005', traceId: 'DEMO-ITEM-0002', kind: 'trade', at: '2026-11-21T09:30:00Z', character: 'Explorer', location: 'Town', slot: 'bag:16', description: ['Received in a completed player trade.', 'Nhận qua giao dịch đã hoàn tất.'] },
  { id: 'EVT-006', traceId: 'DEMO-ITEM-0002', kind: 'storage', at: '2026-11-21T09:35:00Z', character: 'Explorer', location: 'Town', slot: 'bag:16 → vault:2:8', description: ['Stored on vault page 2.', 'Cất vào trang rương 2.'] },
  { id: 'EVT-007', traceId: 'DEMO-ITEM-0003', kind: 'craft', at: '2026-11-21T10:00:00Z', character: 'Explorer', location: 'Town', slot: 'craft → equipped:10', description: ['Crafted from two example ingredients; inputs consumed.', 'Tạo từ hai nguyên liệu mẫu; nguyên liệu đã tiêu hao.'], parents: ['DEMO-INPUT-0101', 'DEMO-INPUT-0102'], before: '2', after: '1' },
  { id: 'EVT-008', traceId: 'DEMO-ITEM-0005', kind: 'reward', at: '2026-11-21T11:00:00Z', character: 'Starlight', location: 'Aurora', slot: 'mail → bag:14', description: ['Timed reward collected from the gift box.', 'Nhận thưởng có thời hạn từ hộp quà.'] },
  { id: 'EVT-009', traceId: '', kind: 'boss', at: '2026-11-21T12:00:00Z', character: 'Explorer', location: 'Dungeon', slot: '—', description: ['Example boss defeated with the party.', 'Hạ gục trùm mẫu cùng tổ đội.'] },
];
export function localized(value: Localized, language: HubLanguage): string { return value[language === 'vi' ? 1 : 0]; }
export function hubTime(value: string, language: HubLanguage): string { return new Intl.DateTimeFormat(language === 'vi' ? 'vi-VN' : 'en-AU', { timeZone: 'Australia/Sydney', dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)); }
