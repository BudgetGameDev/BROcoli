// Service Worker for PWA functionality
// IMPORTANT: Update CACHE_VERSION when you want to force a cache refresh
const CACHE_VERSION = 'v1';
const CACHE_NAME = `unity-game-cache-${CACHE_VERSION}`;
const OFFLINE_URL = 'offline.html';

// Get the base path dynamically (works with GitHub Pages subdirectories)
const BASE_PATH = self.registration.scope;

// Files to NEVER cache (always fetch fresh) - version checking files
const NO_CACHE_FILES = [
  'version.json',
  'version-check.js'
];

// Files to cache immediately on install (relative to scope)
const PRECACHE_ASSETS = [
  './',
  './index.html',
  './manifest.json',
  './icons/icon-192x192.png',
  './icons/icon-512x512.png'
];

// Check if a URL should never be cached
function shouldNeverCache(url) {
  return NO_CACHE_FILES.some(file => url.pathname.endsWith(file));
}

// Install event - cache essential files
self.addEventListener('install', (event) => {
  console.log('[ServiceWorker] Installing version:', CACHE_VERSION);
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => {
        console.log('[ServiceWorker] Pre-caching essential assets');
        return cache.addAll(PRECACHE_ASSETS).catch(err => {
          console.warn('[ServiceWorker] Some assets failed to cache:', err);
        });
      })
      .then(() => {
        console.log('[ServiceWorker] Installed successfully');
        return self.skipWaiting();
      })
  );
});

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
  console.log('[ServiceWorker] Activating...');
  event.waitUntil(
    caches.keys().then((cacheNames) => {
      return Promise.all(
        cacheNames.map((cacheName) => {
          if (cacheName !== CACHE_NAME) {
            console.log('[ServiceWorker] Deleting old cache:', cacheName);
            return caches.delete(cacheName);
          }
        })
      );
    }).then(() => {
      console.log('[ServiceWorker] Activated successfully');
      return self.clients.claim();
    })
  );
});

// Fetch event - network first, then cache for game assets
self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);
  
  // Skip non-GET requests
  if (event.request.method !== 'GET') {
    return;
  }
  
  // Skip chrome-extension and other non-http(s) requests
  if (!url.protocol.startsWith('http')) {
    return;
  }
  
  // NEVER cache version files - always fetch fresh
  if (shouldNeverCache(url)) {
    console.log('[ServiceWorker] Bypassing cache for:', url.pathname);
    event.respondWith(
      fetch(event.request, { 
        cache: 'no-store',
        headers: {
          'Cache-Control': 'no-cache, no-store, must-revalidate',
          'Pragma': 'no-cache'
        }
      }).catch(() => {
        // If network fails, we have no fallback for version files
        return new Response(JSON.stringify({ error: 'offline' }), {
          status: 503,
          headers: { 'Content-Type': 'application/json' }
        });
      })
    );
    return;
  }
  
  // For Unity build files, use cache-first strategy (they're versioned by build ID now)
  if (url.pathname.includes('/Build/')) {
    event.respondWith(
      caches.match(event.request)
        .then((cachedResponse) => {
          if (cachedResponse) {
            return cachedResponse;
          }
          return fetch(event.request).then((response) => {
            // Don't cache if not a valid response
            if (!response || response.status !== 200) {
              return response;
            }
            // Cache the Unity build files
            const responseToCache = response.clone();
            caches.open(CACHE_NAME).then((cache) => {
              cache.put(event.request, responseToCache);
            });
            return response;
          });
        })
    );
    return;
  }
  
  // For other requests, use network-first strategy
  event.respondWith(
    fetch(event.request)
      .then((response) => {
        // Cache successful responses
        if (response && response.status === 200) {
          const responseToCache = response.clone();
          caches.open(CACHE_NAME).then((cache) => {
            cache.put(event.request, responseToCache);
          });
        }
        return response;
      })
      .catch(() => {
        // If network fails, try cache
        return caches.match(event.request);
      })
  );
});

// Handle messages from the main thread
self.addEventListener('message', (event) => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    console.log('[ServiceWorker] Received SKIP_WAITING message');
    self.skipWaiting();
  }
  
  // Handle force cache clear request
  if (event.data && event.data.type === 'CLEAR_ALL_CACHES') {
    console.log('[ServiceWorker] Received CLEAR_ALL_CACHES message');
    event.waitUntil(
      caches.keys().then((cacheNames) => {
        return Promise.all(
          cacheNames.map((cacheName) => {
            console.log('[ServiceWorker] Deleting cache:', cacheName);
            return caches.delete(cacheName);
          })
        );
      }).then(() => {
        console.log('[ServiceWorker] All caches cleared via message');
        // Notify the client that caches are cleared
        if (event.source) {
          event.source.postMessage({ type: 'CACHES_CLEARED' });
        }
      })
    );
  }
  
  // Handle cache status request
  if (event.data && event.data.type === 'GET_CACHE_STATUS') {
    caches.keys().then((cacheNames) => {
      if (event.source) {
        event.source.postMessage({ 
          type: 'CACHE_STATUS', 
          caches: cacheNames,
          currentCache: CACHE_NAME
        });
      }
    });
  }
});
