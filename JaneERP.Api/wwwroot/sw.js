// ⚠️ IMPORTANT: Bump this version on every deployment to invalidate cached pages.
// Format: janeerp-vYYYYMMDD (or add a letter suffix for same-day deploys: v20260513b)
const CACHE = 'janeerp-v20260515';
const STATIC = [
  '/',
  '/index.html',
  '/css/app.css',
  '/js/app.js',
  '/js/api.js',
  '/js/pages/login.js',
  '/js/pages/dashboard.js',
  '/js/pages/cyclecount.js',
  '/js/pages/inventory.js',
  '/js/pages/orders.js',
  '/js/pages/purchaseorders.js',
  '/js/pages/customers.js',
  '/js/pages/workorders.js',
  '/js/pages/cooking.js',
  '/js/pages/accounting.js',
  '/js/pages/tasks.js',
];

self.addEventListener('install', e => {
  e.waitUntil(
    caches.open(CACHE).then(c => c.addAll(STATIC)).then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', e => {
  e.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', e => {
  const url = new URL(e.request.url);

  // API calls: network first, no caching
  if (url.pathname.startsWith('/api/')) {
    e.respondWith(fetch(e.request).catch(() =>
      new Response(JSON.stringify({ error: 'Offline' }), {
        status: 503,
        headers: { 'Content-Type': 'application/json' }
      })
    ));
    return;
  }

  // Static assets: cache first
  e.respondWith(
    caches.match(e.request).then(cached => cached || fetch(e.request).then(res => {
      const clone = res.clone();
      caches.open(CACHE).then(c => c.put(e.request, clone));
      return res;
    }))
  );
});
