// Development service worker — intentionally a no-op so live changes aren't cached.
// The real offline/caching logic lives in service-worker.published.js (used on publish).
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', () => self.clients.claim());
self.addEventListener('fetch', () => { /* pass-through in dev */ });
