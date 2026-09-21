/**
 * Service Worker：应用外壳缓存 + Web Push 接收。
 *
 * 只做两件事，刻意保持精简：
 * 1. **导航请求**走「网络优先、失败回落到缓存的 index.html」——保证离线时至少能打开应用外壳；
 * 2. **静态资源**（带 hash 的 JS/CSS、图标）走「缓存优先」——它们的内容不会变。
 *
 * 不做的事：不缓存 API 响应。行情与财务数据必须是最新的，缓存它们会让用户看到
 * 过期数字而不自知——离线时的正确表现是「明确提示无数据」，而不是显示旧数据。
 *
 * 版本号变更会触发 activate 时清理旧缓存；改缓存策略时务必同时改它。
 */
const CACHE_VERSION = 'sa-shell-v1';

/** 预缓存的最小集合：应用外壳本身。 */
const PRECACHE_URLS = ['/', '/index.html', '/manifest.webmanifest', '/icon-192.png', '/icon-512.png'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_VERSION).then((cache) => cache.addAll(PRECACHE_URLS)).then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== CACHE_VERSION).map((key) => caches.delete(key))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const request = event.request;

  // 只处理同源 GET；POST/PUT（含全部业务接口）一律直连网络
  if (request.method !== 'GET' || new URL(request.url).origin !== self.location.origin) {
    return;
  }

  const url = new URL(request.url);

  // 接口与实时通道绝不缓存：过期数据比没有数据更危险
  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
    return;
  }

  // 导航请求：网络优先，离线时回落缓存的外壳
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(() =>
        caches.match('/index.html').then((cached) => cached ?? new Response('离线且无缓存', { status: 503 }))
      )
    );
    return;
  }

  // 静态资源：缓存优先（文件名带 hash 或为固定图标）
  event.respondWith(
    caches.match(request).then((cached) => {
      if (cached) {
        return cached;
      }

      return fetch(request).then((response) => {
        if (response.ok && (url.pathname.startsWith('/assets/') || url.pathname.startsWith('/icon-'))) {
          const clone = response.clone();
          caches.open(CACHE_VERSION).then((cache) => cache.put(request, clone));
        }

        return response;
      });
    })
  );
});

/**
 * 接收 Web Push 并弹出系统通知。
 *
 * 载荷格式由服务端定义（title / body / level / code / url），这里做防御性解析：
 * 拿不到 title 时给一个通用标题，避免弹出空白通知。
 */
self.addEventListener('push', (event) => {
  let payload = {};

  try {
    payload = event.data ? event.data.json() : {};
  } catch {
    payload = {};
  }

  const title = payload.title || '股析 SA';
  const options = {
    body: payload.body || '有新的提醒',
    icon: '/icon-192.png',
    badge: '/icon-192.png',
    tag: payload.notificationId ? `sa-${payload.notificationId}` : 'sa-alert',
    // 同一 tag 的新通知替换旧的，避免同类提醒堆满通知栏
    renotify: false,
    data: { url: payload.url || '/notifications', code: payload.code ?? null }
  };

  event.waitUntil(self.registration.showNotification(title, options));
});

/** 点击通知：已打开该页面则聚焦，否则新开。 */
self.addEventListener('notificationclick', (event) => {
  event.notification.close();

  const target = (event.notification.data && event.notification.data.url) || '/notifications';

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clients) => {
      for (const client of clients) {
        if (new URL(client.url).pathname === target && 'focus' in client) {
          return client.focus();
        }
      }

      return self.clients.openWindow(target);
    })
  );
});
