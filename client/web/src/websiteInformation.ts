const labels = {
  roadmap: ['Roadmap', 'Lộ trình'],
  about: ['About', 'Giới thiệu'],
  terms: ['Terms of use', 'Điều khoản sử dụng'],
  privacy: ['Privacy policy', 'Chính sách quyền riêng tư'],
  news: ['News', 'Tin tức'],
  shop: ['Shop catalogue', 'Danh mục cửa hàng'],
  topup: ['Top up coins', 'Nạp xu'],
} as const;
const pages = ['roadmap', 'about', 'terms', 'privacy'] as const;
type InformationPage = typeof pages[number];
type Content = Record<InformationPage, { en: string; vi: string }>;

const root = document.querySelector<HTMLElement>('#website-information');
const requested = new URLSearchParams(location.search).get('page');
const current = pages.find(page => page === requested) ?? 'roadmap';
let content: Content | undefined;
let unavailable = false;

function render(): void {
  const vi = document.documentElement.lang === 'vi';
  for (const link of document.querySelectorAll<HTMLAnchorElement>('[data-information-label]')) {
    const key = link.dataset.informationLabel as keyof typeof labels;
    const caption = labels[key];
    if (caption) link.textContent = caption[vi ? 1 : 0];
    if (root && link.closest('[data-information-nav]') && key === current) link.setAttribute('aria-current', 'page');
  }

  if (!root) return;
  root.querySelector('[data-information-nav]')?.setAttribute('aria-label', vi ? 'Thông tin website' : 'Website information');
  root.querySelector('[data-information-resources]')?.setAttribute('aria-label', vi ? 'Tin tức và cửa hàng' : 'News and shop');
  root.querySelector('#information-heading')!.textContent = labels[current][vi ? 1 : 0];
  const status = root.querySelector('#information-status')!;
  const body = root.querySelector('#information-body')!;
  const value = content?.[current]?.[vi ? 'vi' : 'en'];
  if (typeof value === 'string' && value.trim()) {
    status.textContent = '';
    body.replaceChildren(...value.trim().split(/\r?\n[ \t]*\r?\n/).map(paragraph => {
      const node = document.createElement('p');
      node.textContent = paragraph;
      return node;
    }));
    return;
  }

  body.replaceChildren();
  status.textContent = unavailable
    ? (vi ? 'Chưa thể tải nội dung. Hãy thử lại sau.' : 'Content is temporarily unavailable. Please try again later.')
    : content
      ? (vi ? 'Chủ máy chủ chưa công bố nội dung bằng ngôn ngữ này.' : 'The host has not published content in this language.')
      : (vi ? 'Đang tải nội dung…' : 'Loading content…');
}

new MutationObserver(render).observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
render();

if (root) {
  void fetch('/website/content', { cache: 'no-store' }).then(async response => {
    if (!response.ok) throw new Error('Content request failed');
    content = await response.json() as Content;
  }).catch(() => { unavailable = true; }).finally(render);
}
