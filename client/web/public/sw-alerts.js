// Service Worker for HNMO Web Push event alerts (/sw-alerts.js).
self.addEventListener('push', (event) => {
  let data = { title: 'HNMO Event Alert', body: '', tag: 'hnmo-event', url: '/home.html#schedule' };
  if (event.data) {
    try {
      const parsed = event.data.json();
      if (parsed && typeof parsed === 'object') {
        data = { ...data, ...parsed };
      }
    } catch {
      const text = event.data.text();
      if (text) data.body = text;
    }
  }
  event.waitUntil(
    self.registration.showNotification(data.title || 'HNMO Event Alert', {
      body: data.body || '',
      tag: data.tag || 'hnmo-event',
      renotify: true,
      data: { url: data.url || '/home.html#schedule' },
    }),
  );
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const targetUrl = (event.notification.data && event.notification.data.url) || '/home.html#schedule';
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clients) => {
      for (const client of clients) {
        if ('focus' in client) return client.focus();
      }
      if (self.clients.openWindow) return self.clients.openWindow(targetUrl);
      return undefined;
    }),
  );
});
