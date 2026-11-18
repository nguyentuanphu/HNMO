// News & Announcements helpers for:
// 1. Login & character selection screen collapsible news panel (#loginNewsPanel) fed by GET /website/news
// 2. In-game chat notices pushed by ServerToClient HnmoNewsNotice (C2 F5 2A)

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read } from '../net/packets.js';

export interface PublicNewsPost {
  id: string;
  titleEn: string;
  titleVi: string;
  bodyEn: string;
  bodyVi: string;
  imageUrl?: string;
  publishAtUtc: number;
  publishSydney?: string;
  pinned?: boolean;
  author?: string;
}

export interface PublicNewsFeed {
  v: number;
  enabled: boolean;
  allowWebsite?: boolean;
  showLoginPanel?: boolean;
  posts?: PublicNewsPost[];
}

export interface NewsNoticePayload {
  v: number;
  fresh?: boolean;
  latest?: PublicNewsPost;
}

const NEWS_SUBCODE = 0x2a;
const SEEN_SESSION_KEY = 'hnmo.seenNewsNoticeIds';

export function sanitizeNewsImageUrl(raw: string | undefined): string {
  if (!raw) return '';
  const trimmed = raw.trim();
  if (trimmed.startsWith('/') && !trimmed.startsWith('//')) return trimmed;
  if (/^https?:\/\//i.test(trimmed)) return trimmed;
  return '';
}

function isVi(): boolean {
  return currentLanguage() === 'vi';
}

function pickLocalized(en: string | undefined, vi: string | undefined): string {
  return (isVi() ? vi || en : en || vi) ?? '';
}

function renderNewsPanel(container: HTMLElement, posts: PublicNewsPost[]): void {
  const vi = isVi();
  const visible = posts.slice(0, 5);
  if (!visible.length) {
    container.hidden = true;
    container.replaceChildren();
    return;
  }

  container.hidden = false;
  container.replaceChildren();

  const details = document.createElement('details');
  details.open = true;

  const summary = document.createElement('summary');
  summary.textContent = vi ? `Tin tức & Thông báo (${visible.length})` : `Server news & announcements (${visible.length})`;
  details.append(summary);

  const list = document.createElement('div');
  list.className = 'login-news-list';

  for (const post of visible) {
    const card = document.createElement('article');
    card.className = 'login-news-item';

    const meta = document.createElement('div');
    meta.className = 'login-news-meta';
    if (post.pinned) {
      const badge = document.createElement('span');
      badge.className = 'login-news-pin';
      badge.textContent = vi ? 'Ghim' : 'Pinned';
      meta.append(badge);
    }
    const time = document.createElement('span');
    time.textContent = post.publishSydney ? `${post.publishSydney} Sydney` : '';
    meta.append(time);

    const title = document.createElement('strong');
    title.className = 'login-news-title';
    title.textContent = pickLocalized(post.titleEn, post.titleVi);

    const body = document.createElement('p');
    body.className = 'login-news-body';
    body.textContent = pickLocalized(post.bodyEn, post.bodyVi);

    card.append(meta, title, body);

    const safeImg = sanitizeNewsImageUrl(post.imageUrl);
    if (safeImg) {
      const img = document.createElement('img');
      img.className = 'login-news-img';
      img.src = safeImg;
      img.alt = title.textContent ?? '';
      img.loading = 'lazy';
      card.append(img);
    }

    list.append(card);
  }

  details.append(list);
  container.append(details);
}

export function mountLoginNewsPanel(container: HTMLElement | null): () => void {
  if (!container) return () => {};
  let latestPosts: PublicNewsPost[] = [];
  let active = true;

  const rerender = () => {
    if (!active) return;
    renderNewsPanel(container, latestPosts);
  };

  const unsubLang = onLanguageChange(rerender);

  void fetch('/website/news', { cache: 'no-store', signal: AbortSignal.timeout(5000) })
    .then((r) => (r.ok ? (r.json() as Promise<PublicNewsFeed>) : null))
    .then((data) => {
      if (!active || !data || !data.enabled || data.showLoginPanel === false || !Array.isArray(data.posts)) {
        return;
      }
      latestPosts = data.posts;
      rerender();
    })
    .catch(() => {
      /* Login screen works normally if offline or API disabled. */
    });

  return () => {
    active = false;
    unsubLang();
  };
}

export class NewsChatHandler {
  private readonly message: (text: string) => void;
  private readonly seenIds = new Set<string>();

  constructor(message: (text: string) => void) {
    this.message = message;
    try {
      const saved = sessionStorage.getItem(SEEN_SESSION_KEY);
      if (saved) {
        for (const id of JSON.parse(saved) as string[]) {
          if (typeof id === 'string') this.seenIds.add(id);
        }
      }
    } catch {
      /* Ignore storage restrictions in headless/incognito environments. */
    }
  }

  handle(bytes: Uint8Array): boolean {
    if (bytes.length < 5 || bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== NEWS_SUBCODE) {
      return false;
    }

    const packet = read('ServerToClient', 'HnmoNewsNotice', bytes);
    let payload: NewsNoticePayload;
    try {
      payload = JSON.parse(String(packet.Json ?? '{}')) as NewsNoticePayload;
    } catch {
      return false;
    }

    const post = payload.latest;
    if (!post || typeof post.id !== 'string' || !post.id) {
      return true;
    }

    if (!payload.fresh && this.seenIds.has(post.id)) {
      return true;
    }

    this.seenIds.add(post.id);
    try {
      sessionStorage.setItem(SEEN_SESSION_KEY, JSON.stringify([...this.seenIds].slice(-30)));
    } catch {
      /* Ignore storage failure. */
    }

    const prefix = isVi() ? '[Tin tức]' : '[News]';
    const headline = pickLocalized(post.titleEn, post.titleVi);
    const body = pickLocalized(post.bodyEn, post.bodyVi).replace(/\s+/g, ' ').trim();
    const snippet = body.length > 120 ? `${body.slice(0, 117)}...` : body;
    this.message(snippet ? `${prefix} ${headline} — ${snippet}` : `${prefix} ${headline}`);
    return true;
  }
}
