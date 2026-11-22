// Owner-private design controls. The real admin saves WebsiteConfiguration and Brand settings instead.
import { gameName, loadGameName } from './gameName.js';

const setting = 'hnmo.preview.owner.websiteTheme';
const labels = [
  ['classic', 'Dark classic', 'Cổ điển tối'], ['ember', 'Armored fortress', 'Thành trì chiến binh'],
  ['azure', 'Ice citadel', 'Thành băng'], ['jade', 'Elven sanctuary', 'Thánh địa tinh linh'],
  ['violet', 'Sky fortress', 'Pháo đài thiên không'], ['parchment', 'Continent chronicle', 'Biên niên sử lục địa'],
] as const;
let language: 'en' | 'vi' = 'vi';
try { language = localStorage.getItem('hnmo.language') === 'en' ? 'en' : 'vi'; } catch { /* Optional example preference. */ }
const root = document.getElementById('hostPreview');
if (!(root instanceof HTMLElement)) throw new Error('Missing owner preview root');
const host = root;
function node(tag: string, text = ''): HTMLElement { const el = document.createElement(tag); el.textContent = text; return el; }
function render(): void {
  document.documentElement.lang = language;
  const vi = language === 'vi';
  let selected = 'classic';
  try { selected = localStorage.getItem(setting) ?? 'classic'; } catch { /* Review storage is optional. */ }
  const title = node('h1', vi ? 'Một giao diện cho toàn website' : 'One theme for the entire website');
  const brand = node('p', gameName()); brand.dataset.gameName = '{game}';
  const note = node('p', vi ? 'Đây là bảng xem giao diện dành riêng cho chủ website. Lựa chọn chỉ lưu trong trình duyệt này, không sửa cài đặt máy chủ. Ở bản chạy thật, host chọn một theme trong admin và mọi người đều thấy cùng theme đó. Giữ nguyên tính năng trên mọi trang.' : 'Private visual review for the website owner. This choice is saved only in this browser and does not configure a live server. In production, the host saves one theme in the admin panel and everyone sees that theme. Every page keeps its features.');
  const languageButton = document.createElement('button'); languageButton.type = 'button'; languageButton.textContent = vi ? 'English' : 'Tiếng Việt'; languageButton.title = vi ? 'Đọc bản xem bằng tiếng Anh.' : 'Read this preview in Vietnamese.';
  languageButton.onclick = () => { language = vi ? 'en' : 'vi'; localStorage.setItem('hnmo.language', language); render(); };
  const form = document.createElement('form'); const grid = node('div'); grid.className = 'owner-themes';
  for (const [id, en, viet] of labels) {
    const label = document.createElement('label'); const input = document.createElement('input'); input.type = 'radio'; input.name = 'theme'; input.value = id; input.checked = id === selected;
    const art = document.createElement('img'); art.src = `/images/portal-themes/${id === 'classic' ? 'ember' : id}.webp`; art.alt = ''; art.width = 600; art.height = 260;
    label.append(input, node('strong', vi ? viet : en), art); grid.append(label);
  }
  const save = document.createElement('button'); save.type = 'submit'; save.textContent = vi ? 'Xem website với giao diện này' : 'Preview the website with this theme'; save.title = vi ? 'Tất cả trang sẽ dùng cùng lựa chọn của host.' : 'All pages will use this single host choice.';
  form.append(grid, save); form.onsubmit = event => { event.preventDefault(); const value = new FormData(form).get('theme'); if (typeof value === 'string' && labels.some(row => row[0] === value)) { localStorage.setItem(setting, value); location.reload(); } };
  const links = node('nav');
  for (const [path, en, viet] of [['/home.html', 'Website with existing features', 'Website giữ các tính năng hiện có'], ['/account.html', 'Working account components', 'Các mục tài khoản hiện có'], ['/account-profile-preview.html?view=profile', 'Approved profile design (sample)', 'Thiết kế profile đã duyệt (mẫu)'], ['/game.html', 'Game login appearance', 'Giao diện đăng nhập game'], ['/topup.html', 'Existing top-up page', 'Trang nạp hiện có']] as const) {
    const link = document.createElement('a'); link.href = path; link.textContent = vi ? viet : en; link.title = link.textContent; links.append(link);
  }
  host.replaceChildren(brand, title, note, languageButton, form, links);
}
render(); void loadGameName();
