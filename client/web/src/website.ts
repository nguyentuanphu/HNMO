import { loadGameName } from "./gameName.js";

type Language = "en" | "vi";
const copy = {
  en: {
    realm: "The realm",
    events: "Events",
    world: "World",
    start: "Get started",
    account: "Create account",
    eyebrow: "A world worth returning to",
    hero: "Your legend.\nYour next chapter.",
    intro:
      "Gather your party, explore the world, and begin a new adventure. Your journey starts here.",
    play: "Enter the game →",
    explore: "Explore the realm",
    browser: "Play directly in your browser with the game launcher.",
    live: "LIVE REALM",
    refresh: "Refresh ↻",
    source: "Direct from the game server",
    realmTitle: "Know your realm.",
    server: "Server",
    truth:
      "Rates and rules reflect the running server. No estimated statistics.",
    eventsTitle: "Events",
    eventTitle: "Answer the call.",
    eventHint: "Configured event rules · times are not estimated",
    worldTitle: "Find your place in the world.",
    classes: "Character classes",
    maps: "Explore the maps",
    search: "Search maps…",
    adventure: "The journey begins with you",
    startTitle: "Step into your next adventure.",
    create: "Create your account",
    createText: "Register through the game launcher’s account portal.",
    choose: "Choose your path",
    chooseText:
      "Open the browser client, select your server, and create your character.",
    download: "Take the realm with you",
    downloadText:
      "Download the latest Windows launcher. Extract it and run Start HNMO.bat.",
    downloadLink: "Download Windows launcher →",
    footer: "Live game data. English & Tiếng Việt.",
    top: "Back to top ↑",
    unavailable: "Game data is unavailable. Start the game server and refresh.",
    loading: "Connecting to the realm…",
    online: "Connected",
    offline: "Unavailable",
    players: "Players online / capacity",
    experience: "Experience rate",
    maxLevel: "Maximum level",
    maxMaster: "Maximum master level",
    characters: "Characters per account",
    party: "Maximum party size",
    master: "Master experience",
    level: "Level",
    duration: "Duration (minutes)",
    capacity: "Player limit",
    fee: "Entrance fee",
    required: "Required level",
    empty: "No entries supplied by this server.",
    updated: "Updated",
    all: "Server",
    mapsRate: "Map experience",
    nameFallback: "Unnamed",
    noSchedule:
      "Event schedules and global rankings are not supplied by this feed.",
  },
  vi: {
    realm: "Máy chủ",
    events: "Sự kiện",
    world: "Thế giới",
    start: "Bắt đầu",
    account: "Tạo tài khoản",
    eyebrow: "Thế giới luôn chờ bạn trở lại",
    hero: "Huyền thoại của bạn.\nHành trình mới.",
    intro:
      "Lập tổ đội, khám phá thế giới và bắt đầu cuộc phiêu lưu mới. Hành trình của bạn bắt đầu từ đây.",
    play: "Vào trò chơi →",
    explore: "Khám phá máy chủ",
    browser: "Chơi ngay trên trình duyệt thông qua trình khởi chạy.",
    live: "MÁY CHỦ TRỰC TIẾP",
    refresh: "Làm mới ↻",
    source: "Dữ liệu trực tiếp từ máy chủ trò chơi",
    realmTitle: "Tìm hiểu máy chủ.",
    server: "Máy chủ",
    truth:
      "Tỷ lệ và quy tắc lấy từ máy chủ đang chạy. Không dùng số liệu ước tính.",
    eventsTitle: "Sự kiện",
    eventTitle: "Sẵn sàng thử thách.",
    eventHint: "Quy tắc sự kiện từ máy chủ · không ước tính giờ mở",
    worldTitle: "Tìm vị trí của bạn trong thế giới.",
    classes: "Lớp nhân vật",
    maps: "Khám phá bản đồ",
    search: "Tìm bản đồ…",
    adventure: "Hành trình bắt đầu cùng bạn",
    startTitle: "Bước vào cuộc phiêu lưu mới.",
    create: "Tạo tài khoản",
    createText: "Đăng ký tại trang tài khoản của trình khởi chạy trò chơi.",
    choose: "Chọn con đường của bạn",
    chooseText: "Mở trò chơi trên trình duyệt, chọn máy chủ và tạo nhân vật.",
    download: "Mang theo thế giới của bạn",
    downloadText:
      "Tải trình khởi chạy Windows mới nhất. Giải nén và chạy Start HNMO.bat.",
    downloadLink: "Tải trình khởi chạy Windows →",
    footer: "Dữ liệu trò chơi trực tiếp. English & Tiếng Việt.",
    top: "Về đầu trang ↑",
    unavailable:
      "Chưa lấy được dữ liệu. Hãy khởi động máy chủ trò chơi rồi làm mới.",
    loading: "Đang kết nối máy chủ…",
    online: "Đã kết nối",
    offline: "Chưa kết nối",
    players: "Người chơi trực tuyến / sức chứa",
    experience: "Tỷ lệ kinh nghiệm",
    maxLevel: "Cấp độ tối đa",
    maxMaster: "Cấp master tối đa",
    characters: "Số nhân vật mỗi tài khoản",
    party: "Số thành viên tổ đội tối đa",
    master: "Kinh nghiệm master",
    level: "Cấp độ",
    duration: "Thời lượng (phút)",
    capacity: "Số người tối đa",
    fee: "Phí tham gia",
    required: "Cấp độ yêu cầu",
    empty: "Máy chủ chưa cung cấp mục nào.",
    updated: "Cập nhật",
    all: "Máy chủ",
    mapsRate: "Kinh nghiệm bản đồ",
    nameFallback: "Chưa có tên",
    noSchedule:
      "Nguồn dữ liệu này chưa cung cấp lịch mở sự kiện và bảng xếp hạng toàn máy chủ.",
  },
};
type CopyKey = keyof typeof copy.en;
interface Named {
  name: string | null;
}
interface GameClass extends Named {
  id: number;
  requiredLevel: number;
}
interface GameMap extends Named {
  id: number;
  expMultiplier: number;
}
interface GameEvent extends Named {
  description: string | null;
  level: number;
  minimumLevel: number;
  maximumLevel: number;
  durationSeconds: number;
  capacity: number;
  entranceFee: number;
}
interface Realm {
  id: number;
  players: number;
  capacity: number;
  experienceRate: number;
  masterExperienceRate: number;
  maximumLevel: number;
  maximumMasterLevel: number;
  maximumCharactersPerAccount: number;
  maximumPartySize: number;
  classes: GameClass[];
  maps: GameMap[];
  events: GameEvent[];
}
interface Snapshot {
  updatedAt: string;
  servers: Realm[];
}
let language: Language = "en";
try {
  language = localStorage.getItem("website-language") === "vi" ? "vi" : "en";
} catch {
  /* Storage is optional. */
}
let snapshot: Snapshot | null = null;
let selected = "";
const REFRESH_MS = 30000;
function t(key: CopyKey): string {
  return copy[language][key];
}
function element(id: string): HTMLElement {
  return document.getElementById(id)!;
}
function node(tag: string, text: string, className = ""): HTMLElement {
  const item = document.createElement(tag);
  item.textContent = text;
  item.className = className;
  return item;
}
function localized(value: string | null): string {
  const parts = (value ?? "").split("||");
  const translated = parts
    .slice(1)
    .find(
      (part) =>
        part.startsWith(`${language}=`) ||
        part.startsWith(`${language}-${language === "vi" ? "VN" : "US"}=`),
    );
  return (
    translated?.slice(translated.indexOf("=") + 1) ||
    parts[0] ||
    t("nameFallback")
  );
}
function clear(id: string): HTMLElement {
  const target = element(id);
  target.replaceChildren();
  return target;
}
function current(): Realm | undefined {
  return snapshot?.servers.find((server) => String(server.id) === selected);
}
function empty(id: string): void {
  clear(id).append(node("p", t("empty"), "muted"));
}
function renderStats(realm: Realm): void {
  const stats = clear("stats");
  const fields: [string, string][] = [
    [`${realm.players} / ${realm.capacity}`, t("players")],
    [`${realm.experienceRate}×`, t("experience")],
    [String(realm.maximumLevel), t("maxLevel")],
    [String(realm.maximumPartySize), t("party")],
    [`${realm.masterExperienceRate}×`, t("master")],
    [String(realm.maximumMasterLevel), t("maxMaster")],
    [String(realm.maximumCharactersPerAccount), t("characters")],
    [String(realm.maps.length), t("maps")],
  ];
  for (const [value, label] of fields) {
    const item = node("div", "", "stat");
    item.append(node("strong", value), node("span", label));
    stats.append(item);
  }
}
function renderEvents(realm: Realm): void {
  const target = clear("event-list");
  for (const event of realm.events) {
    const card = node("article", "", "event");
    card.append(
      node("span", `${t("level")} ${event.level}`, "number"),
      node("h3", localized(event.name)),
      node("p", localized(event.description)),
    );
    const details = node("dl", "");
    const rows: [string, string][] = [
      [t("level"), `${event.minimumLevel}–${event.maximumLevel}`],
      [t("duration"), String(event.durationSeconds / 60)],
      [t("capacity"), String(event.capacity)],
      [t("fee"), String(event.entranceFee)],
    ];
    for (const [label, value] of rows) {
      const row = node("div", "");
      row.append(node("dt", label), node("dd", value));
      details.append(row);
    }
    card.append(details);
    target.append(card);
  }
  if (!realm.events.length) empty("event-list");
}
function renderMaps(): void {
  const realm = current();
  if (!realm) return;
  const query = (element("search") as HTMLInputElement).value.toLocaleLowerCase(
    language,
  );
  const maps = realm.maps.filter((map) =>
    localized(map.name).toLocaleLowerCase(language).includes(query),
  );
  const target = clear("maps");
  for (const map of maps) {
    const row = node("div", "", "map-row");
    row.append(
      node("span", localized(map.name)),
      node("small", `${map.expMultiplier}×`),
    );
    target.append(row);
  }
  if (!maps.length) empty("maps");
}
function render(): void {
  document.documentElement.lang = language;
  for (const item of document.querySelectorAll<HTMLElement>("[data-t]"))
    item.textContent = t(item.dataset.t as CopyKey);
  (element("search") as HTMLInputElement).placeholder = t("search");
  for (const lang of ["en", "vi"])
    element(lang).setAttribute("aria-pressed", String(language === lang));
  element("connection").textContent = t(snapshot ? "online" : "offline");
  element("notice").textContent = snapshot ? "" : t("unavailable");
  const select = element("server") as HTMLSelectElement;
  select.replaceChildren();
  for (const realm of snapshot?.servers ?? []) {
    const option = new Option(`${t("all")} ${realm.id}`, String(realm.id));
    select.add(option);
  }
  select.value = selected;
  const realm = current();
  if (!realm) {
    for (const id of ["stats", "event-list", "classes", "maps"]) empty(id);
    element("updated").textContent = "";
    return;
  }
  renderStats(realm);
  renderEvents(realm);
  renderMaps();
  const classes = clear("classes");
  for (const entry of realm.classes) {
    const row = node("div", "");
    row.append(
      node("span", localized(entry.name)),
      node("small", `${t("required")} ${entry.requiredLevel}`),
    );
    classes.append(row);
  }
  if (!realm.classes.length) empty("classes");
  element("updated").textContent =
    `${t("updated")}: ${new Date(snapshot!.updatedAt).toLocaleString(language, { timeZone: "Australia/Sydney" })} · Sydney`;
}
async function refresh(): Promise<void> {
  (element("refresh") as HTMLButtonElement).disabled = true;
  element("connection").textContent = t("loading");
  try {
    const response = await fetch("/website/data", {
      cache: "no-store",
      signal: AbortSignal.timeout(8000),
    });
    if (!response.ok) throw new Error("Server unavailable");
    const data: unknown = await response.json();
    if (
      typeof data !== "object" ||
      data === null ||
      !("servers" in data) ||
      !Array.isArray(data.servers) ||
      !("updatedAt" in data) ||
      typeof data.updatedAt !== "string"
    )
      throw new Error("Invalid snapshot");
    snapshot = data as Snapshot;
    if (!snapshot.servers.some((server) => String(server.id) === selected))
      selected = String(snapshot.servers[0]?.id ?? "");
  } catch {
    snapshot = null;
  } // Clear old numbers rather than present them as live.
  render();
  (element("refresh") as HTMLButtonElement).disabled = false;
}
for (const lang of ["en", "vi"] as const)
  element(lang).addEventListener("click", () => {
    language = lang;
    try {
      localStorage.setItem("website-language", lang);
    } catch {
      /* Optional storage. */
    }
    render();
  });
element("server").addEventListener("change", (event) => {
  selected = (event.target as HTMLSelectElement).value;
  render();
});
element("search").addEventListener("input", renderMaps);
element("refresh").addEventListener("click", () => void refresh());
render();
void loadGameName();
void refresh();
setInterval(() => {
  if (!document.hidden) void refresh();
}, REFRESH_MS);
