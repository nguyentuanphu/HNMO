export interface GiftBoxLetterSummary {
  id: string;
  title: string;
  sentAtUtc: string;
  expiresAtUtc: string | null;
  collected: boolean;
  expired: boolean;
  pieces: number;
  coins: number;
  zen: number;
  coinType: string;
}

export interface GiftBoxItemView {
  group: number;
  number: number;
  name: string;
  level: number;
  count: number;
  durability: number;
  skill: boolean;
  luck: boolean;
  optionLevel: number;
  excellentOptions: number;
}

export interface GiftBoxLetterDetail {
  id: string;
  title: string;
  message: string;
  sentAtUtc: string;
  expiresAtUtc: string | null;
  collected: boolean;
  expired: boolean;
  collectedBy: string | null;
  coins: number;
  zen: number;
  coinType: string;
  items: GiftBoxItemView[];
}

export interface GiftBoxCharacterView {
  id: string;
  name: string;
}

export interface GiftBoxStateView {
  accountKey: string;
  offset: number;
  hasMore: boolean;
  letters: GiftBoxLetterSummary[];
  detail: GiftBoxLetterDetail | null;
  characters: GiftBoxCharacterView[];
  status: string | null;
}

const SYDNEY_FORMAT: Intl.DateTimeFormatOptions = {
  timeZone: 'Australia/Sydney',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
};

export function formatSydneyTime(isoUtc: string | null | undefined, language: 'en' | 'vi'): string {
  if (!isoUtc) return language === 'vi' ? 'Không giới hạn' : 'No expiry';
  const dt = new Date(isoUtc);
  if (Number.isNaN(dt.getTime())) return isoUtc;
  try {
    return `${dt.toLocaleString(language === 'vi' ? 'vi-VN' : 'en-AU', SYDNEY_FORMAT)} (Sydney)`;
  } catch {
    return isoUtc;
  }
}

const TEXTS = {
  en: {
    heading: 'Mailbox / Gift Box',
    gameTitle: 'Mailbox / Gift Box (Alt+M)',
    subtitle: 'View gifts sent by admins and claim them safely to your character.',
    helpHint: 'Collect rewards to your current character (if offline on website, choose any character; if online, press Alt+M in game). Full bags leave every reward owed.',
    empty: 'No gift letters yet.',
    prev: 'Previous',
    next: 'Next',
    reload: 'Refresh',
    collected: 'Collected',
    expired: 'Expired',
    ready: 'Ready to claim',
    character: 'Deliver to character',
    collect: 'Collect Reward',
    collecting: 'Collecting...',
    sentAt: 'Sent',
    expiresAt: 'Expires',
    claimedBy: 'Collected by',
    coins: 'Coins',
    zen: 'Zen',
    items: 'Items',
    selectPrompt: 'Select a letter from the list to read its message and claim rewards.',
    close: 'Close',
    closeHint: 'Close mailbox (Alt+M / Esc)',
    connectionError: 'Could not reach your mailbox right now.',
    statusApplied: 'Reward collected and delivered cleanly.',
    statusAlreadyCollected: 'This gift was already collected earlier.',
    statusOnline: 'Your account is currently in game. Press Alt+M inside the game to claim while playing, or log out first.',
    statusInventoryFull: 'Inventory bag does not have enough free space. Clear room and try again — nothing was lost.',
    statusBalanceFull: 'Character Zen or account coin limit reached.',
    statusExpired: 'This gift letter has expired.',
    statusPending: 'Delivery is confirming in background recovery. Refresh in a moment before retrying.',
    statusFailed: 'Collection could not be completed right now.',
  },
  vi: {
    heading: 'Hộp thư / Quà tặng',
    gameTitle: 'Hộp thư quà tặng (Alt+M)',
    subtitle: 'Xem thư quà do quản trị viên gửi và nhận an toàn vào nhân vật của bạn.',
    helpHint: 'Nhận quà về nhân vật đã chọn (khi đang chơi trong game, hãy nhấn Alt+M để nhận trực tiếp). Túi đồ đầy sẽ giữ nguyên mọi phần quà.',
    empty: 'Chưa có thư quà nào.',
    prev: 'Trang trước',
    next: 'Trang sau',
    reload: 'Làm mới',
    collected: 'Đã nhận',
    expired: 'Hết hạn',
    ready: 'Chờ nhận',
    character: 'Nhân vật nhận',
    collect: 'Nhận quà',
    collecting: 'Đang nhận...',
    sentAt: 'Ngày gửi',
    expiresAt: 'Hết hạn',
    claimedBy: 'Đã nhận bởi',
    coins: 'Xu',
    zen: 'Zen',
    items: 'Vật phẩm',
    selectPrompt: 'Chọn một thư ở danh sách để xem nội dung và nhận quà.',
    close: 'Đóng',
    closeHint: 'Đóng hộp thư (Alt+M / Esc)',
    connectionError: 'Không thể kết nối tới hộp thư lúc này.',
    statusApplied: 'Đã nhận quà thành công vào tài khoản / hành trang.',
    statusAlreadyCollected: 'Thư quà này đã được nhận trước đó.',
    statusOnline: 'Tài khoản đang trong game. Hãy nhấn Alt+M ngay trong game để nhận, hoặc thoát game rồi thử lại.',
    statusInventoryFull: 'Hành trang không đủ ô trống. Hãy dọn chỗ trống và thử lại — phần quà vẫn còn nguyên.',
    statusBalanceFull: 'Số Zen hoặc Xu đã chạm mức tối đa.',
    statusExpired: 'Thư quà này đã hết hạn nhận.',
    statusPending: 'Giao dịch đang chờ xác nhận phục hồi. Vui lòng làm mới sau ít giây.',
    statusFailed: 'Không thể nhận quà lúc này.',
  },
} as const;

export type GiftBoxTextKey = keyof typeof TEXTS.en;

export function giftBoxText(language: 'en' | 'vi', key: GiftBoxTextKey): string {
  return TEXTS[language][key];
}

export function statusMessage(language: 'en' | 'vi', status: string | null | undefined): string {
  switch (status) {
    case null:
    case undefined:
    case '':
      return '';
    case 'Applied':
      return giftBoxText(language, 'statusApplied');
    case 'AlreadyCollected':
      return giftBoxText(language, 'statusAlreadyCollected');
    case 'Online':
      return giftBoxText(language, 'statusOnline');
    case 'InventoryFull':
    case 'Full':
      return giftBoxText(language, 'statusInventoryFull');
    case 'BalanceFull':
      return giftBoxText(language, 'statusBalanceFull');
    case 'Expired':
      return giftBoxText(language, 'statusExpired');
    case 'Pending':
      return giftBoxText(language, 'statusPending');
    default:
      return `${giftBoxText(language, 'statusFailed')} (${status})`;
  }
}
