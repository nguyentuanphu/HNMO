type Snapshot = { group: number; number: number; level: number; quantity: number; durability: number; sockets: number; hasSkill: boolean };
export type HistoryEvent = { kind: string; time: string; character: string | null; partner: string | null; before: Snapshot | null; after: Snapshot | null };
export type History = { enabled: true; retentionDays: number; events: HistoryEvent[] } | { enabled: false };

const kinds: Record<string, [string, string]> = {
  loot: ['Loot generated', 'Tạo đồ rơi'], pickup: ['Picked up', 'Nhặt lên'], drop: ['Dropped', 'Thả xuống'],
  trade: ['Trade', 'Giao dịch'], mix: ['Crafting', 'Chế tạo'], upgrade: ['Upgrade', 'Nâng cấp'],
  consume: ['Consumed', 'Đã dùng'], grant: ['Granted', 'Đã cấp'], 'npc-buy': ['NPC purchase', 'Mua từ NPC'],
  'npc-sell': ['NPC sale', 'Bán cho NPC'], 'personal-store': ['Personal store', 'Cửa hàng cá nhân'], 'item-market': ['Market', 'Chợ vật phẩm'],
};
export function itemHistoryKind(kind: string, language: 'en' | 'vi'): string {
  const vi = language === 'vi';
  return kinds[kind]?.[vi ? 1 : 0] ?? (vi ? 'Hoạt động vật phẩm' : 'Item activity');
}

export function renderItemHistoryEvents(
  results: HTMLOListElement,
  events: readonly HistoryEvent[],
  language: 'en' | 'vi',
  headingTag: 'h2' | 'h4',
): void {
  const vi = language === 'vi';
  const copy = (en: string, vietnamese: string) => vi ? vietnamese : en;
  const number = new Intl.NumberFormat(vi ? 'vi-VN' : 'en-AU', { maximumFractionDigits: 2 });
  const date = new Intl.DateTimeFormat(vi ? 'vi-VN' : 'en-AU', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Australia/Sydney' });
  results.replaceChildren();
  for (const entry of events) {
    const timestamp = new Date(entry.time);
    const row = document.createElement('li');
    const heading = document.createElement(headingTag);
    heading.textContent = itemHistoryKind(entry.kind, language);
    const time = document.createElement('time');
    time.dateTime = entry.time;
    time.textContent = Number.isNaN(timestamp.valueOf()) ? copy('Time unavailable', 'Không có thời gian') : `${date.format(timestamp)} (Sydney)`;
    row.append(heading, time);
    const actors = [entry.character, entry.partner].filter(Boolean);
    if (actors.length) {
      const participants = document.createElement('p');
      participants.textContent = actors.join(' · ');
      row.append(participants);
    }
    for (const [label, snapshot] of [[copy('Before', 'Trước'), entry.before], [copy('After', 'Sau'), entry.after]] as const) {
      if (!snapshot) continue;
      const state = document.createElement('p');
      state.textContent = copy(
        `${label} · Item ${snapshot.group}/${snapshot.number} · Level +${snapshot.level} · Quantity ${number.format(snapshot.quantity)} · Sockets ${number.format(snapshot.sockets)}`,
        `${label} · Vật phẩm ${snapshot.group}/${snapshot.number} · Cấp +${snapshot.level} · Số lượng ${number.format(snapshot.quantity)} · Ô socket ${number.format(snapshot.sockets)}`,
      );
      row.append(state);
    }
    results.append(row);
  }
}
