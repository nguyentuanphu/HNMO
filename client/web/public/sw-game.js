// Service Worker for HNMO PWA Mobile & Tablet Web App (/sw-game.js).
// Caches the static game shell for fast repeat opens and offline notice without touching /relay or live API routes.

const CACHE_NAME = 'hnmo-shell-v1';
const SHELL_URLS = [
  '/index.html',
  '/home.html',
  '/home.css',
  '/images/icon-192.png',
  '/images/icon-512.png',
];

const NEVER_CACHE_PREFIXES = ['/relay', '/api/', '/website/', '/game-name.json', '/manifest.webmanifest', '/health'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(SHELL_URLS)).catch(() => undefined),
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key)))),
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.method !== 'GET') return;

  let url;
  try {
    url = new URL(request.url);
  } catch {
    return;
  }
  if (url.origin !== self.location.origin) return;
  if (NEVER_CACHE_PREFIXES.some((prefix) => url.pathname.startsWith(prefix))) return;

  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then((response) => {
          if (response.ok) {
            const copy = response.clone();
            void caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
          }
          return response;
        })
        .catch(async () => {
          const cached = await caches.match(request) || await caches.match('/index.html');
          if (cached) return cached;
          return new Response(
            '<!doctype html><html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">'
            + '<body style="background:#0a0d14;color:#e6e8ef;font-family:system-ui,sans-serif;padding:32px;text-align:center">'
            + '<h1 style="color:#c9a24a">HNMO Offline</h1>'
            + '<p>Check your connection and tap reload to reconnect to the server.</p>'
            + '<p>Kiểm tra kết nối mạng và tải lại trang để vào game.</p>'
            + '<button onclick="location.reload()" style="padding:10px 18px;border-radius:8px;border:0;background:#c9a24a;color:#181206;font-weight:700">Reload / Tải lại</button>'
            + '</body></html>',
            { status: 503, headers: { 'Content-Type': 'text/html; charset=utf-8' } },
          );
        }),
    );
    return;
  }

  const staticAsset = /\.(js|css|png|jpg|jpeg|webp|svg|woff2?|json)$/i.test(url.pathname);
  if (!staticAsset) return;

  event.respondWith(
    fetch(request)
      .then((response) => {
        if (response.ok) {
          const copy = response.clone();
          void caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
        }
        return response;
      })
      .catch(() => caches.match(request).then((cached) => cached || Response.error())),
  );
});
