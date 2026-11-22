import { defaultCache } from '@serwist/next/worker';
import type { PrecacheEntry, SerwistGlobalConfig } from 'serwist';
import { NetworkOnly, Serwist } from 'serwist';

declare global {
  interface WorkerGlobalScope extends SerwistGlobalConfig {
    __SW_MANIFEST: (PrecacheEntry | string)[] | undefined;
  }
}

declare const self: ServiceWorkerGlobalScope;

// Backend endpoints carry per-user data authorized by a Bearer header, which the
// Cache API ignores; caching them could leak one user's data to the next login.
const BACKEND_PREFIXES = ['/api/', '/v1/', '/swagger/'];

// Runtime caches left behind by the previous next-pwa/workbox service worker.
const LEGACY_CACHE_NAMES = ['apis', 'start-url', 'next-data'];

const serwist = new Serwist({
  precacheEntries: self.__SW_MANIFEST,
  // A new version waits until the user accepts the update prompt (see usePwaUpdatePrompt),
  // so already-open pages keep loading their own chunks from the old precache.
  skipWaiting: false,
  clientsClaim: true,
  navigationPreload: true,
  runtimeCaching: [
    {
      matcher: ({ sameOrigin, url: { pathname } }) =>
        sameOrigin && BACKEND_PREFIXES.some((p) => pathname.startsWith(p)),
      handler: new NetworkOnly(),
    },
    ...defaultCache,
  ],
  fallbacks: {
    entries: [
      {
        url: '/offline',
        matcher: ({ request }) => request.destination === 'document',
      },
    ],
  },
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(
        keys
          .filter(
            (key) =>
              LEGACY_CACHE_NAMES.includes(key) ||
              key.startsWith('workbox-precache'),
          )
          .map((key) => caches.delete(key)),
      ),
    ),
  );
});

serwist.addEventListeners();
