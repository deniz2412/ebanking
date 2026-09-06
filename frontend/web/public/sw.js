// E-Banking Service Worker
// Handles push notifications, caching, and offline functionality

// Bump this on every deploy that changes app code — a new value forces the browser to
// install a fresh service worker and drop the old cache (see 'activate' below). Without
// this, "cache-first" would serve a stale app shell forever, invisible to server-side fixes.
const CACHE_NAME = 'ebanking-v2';
const OFFLINE_URL = '/offline.html';
// Paths that must always be fetched fresh (app shell + code) — cached only as an
// offline fallback, never served ahead of a live network response.
const NETWORK_FIRST_PATHS = ['/', '/index.html', '/config.json'];
const isAppShellOrCode = (url) =>
  NETWORK_FIRST_PATHS.includes(url.pathname) ||
  /\.(?:js|css)$/.test(url.pathname);

// URLs to cache for offline functionality
const urlsToCache = [
  '/',
  '/dashboard',
  '/transactions',
  '/transfer',
  '/offline.html',
  '/styles.css',
  '/main.js'
];

// Install event - cache essential resources
self.addEventListener('install', (event) => {
  console.log('Service Worker installing');
  
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => {
        console.log('Caching essential resources');
        return cache.addAll(urlsToCache);
      })
      .then(() => {
        console.log('Service Worker installed successfully');
        return self.skipWaiting();
      })
      .catch((error) => {
        console.error('Service Worker installation failed:', error);
      })
  );
});

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
  console.log('Service Worker activating');
  
  event.waitUntil(
    caches.keys().then((cacheNames) => {
      return Promise.all(
        cacheNames.map((cacheName) => {
          if (cacheName !== CACHE_NAME) {
            console.log('Deleting old cache:', cacheName);
            return caches.delete(cacheName);
          }
        })
      );
    }).then(() => {
      console.log('Service Worker activated');
      return self.clients.claim();
    })
  );
});

// Fetch event.
// App shell + JS/CSS: network-first, falling back to cache only when offline — so a
// server-side change is visible on the very next load, not hidden behind a stale cache.
// Everything else: cache-first (unchanged), for genuinely static assets (icons, fonts).
self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') {
    return;
  }
  if (event.request.url.startsWith('chrome-extension://')) {
    return;
  }

  const url = new URL(event.request.url);

  if (isAppShellOrCode(url)) {
    event.respondWith(
      fetch(event.request)
        .then((response) => {
          if (response && response.status === 200 && response.type === 'basic') {
            const toCache = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(event.request, toCache));
          }
          return response;
        })
        .catch(() =>
          caches.match(event.request).then((cached) => {
            if (cached) return cached;
            if (event.request.mode === 'navigate') return caches.match(OFFLINE_URL);
          })
        )
    );
    return;
  }

  event.respondWith(
    caches.match(event.request)
      .then((response) => {
        if (response) {
          return response;
        }
        return fetch(event.request)
          .then((response) => {
            if (!response || response.status !== 200 || response.type !== 'basic') {
              return response;
            }
            const responseToCache = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(event.request, responseToCache));
            return response;
          })
          .catch(() => {
            if (event.request.mode === 'navigate') {
              return caches.match(OFFLINE_URL);
            }
          });
      })
  );
});

// Push event - handle push notifications
self.addEventListener('push', (event) => {
  console.log('Push notification received:', event);

  let notificationData = {
    title: 'E-Banking Notification',
    body: 'You have a new notification',
    icon: '/favicon.ico',
    badge: '/favicon.ico',
    tag: 'ebanking-notification',
    requireInteraction: false,
    silent: false
  };

  try {
    if (event.data) {
      const data = event.data.json();
      notificationData = {
        ...notificationData,
        ...data,
        icon: data.icon || '/favicon.ico',
        badge: data.badge || '/favicon.ico'
      };
    }
  } catch (error) {
    console.error('Failed to parse push notification data:', error);
  }

  // Add action buttons based on notification type
  if (notificationData.type === 'transaction') {
    notificationData.actions = [
      {
        action: 'view',
        title: 'View Transaction',
        icon: '/icons/view.png'
      },
      {
        action: 'dismiss',
        title: 'Dismiss',
        icon: '/icons/dismiss.png'
      }
    ];
  } else if (notificationData.type === 'security') {
    notificationData.actions = [
      {
        action: 'secure',
        title: 'Secure Account',
        icon: '/icons/security.png'
      },
      {
        action: 'view',
        title: 'View Details',
        icon: '/icons/view.png'
      }
    ];
    notificationData.requireInteraction = true;
  }

  // Add timestamp and unique data
  notificationData.data = {
    ...notificationData.data,
    timestamp: Date.now(),
    url: notificationData.url || '/dashboard'
  };

  event.waitUntil(
    self.registration.showNotification(notificationData.title, notificationData)
      .then(() => {
        console.log('Notification displayed successfully');
      })
      .catch((error) => {
        console.error('Failed to display notification:', error);
      })
  );
});

// Notification click event
self.addEventListener('notificationclick', (event) => {
  console.log('Notification clicked:', event);

  event.notification.close();

  const action = event.action;
  const notificationData = event.notification.data || {};
  
  let targetUrl = '/dashboard';

  if (action === 'view') {
    targetUrl = notificationData.url || '/dashboard';
  } else if (action === 'secure') {
    targetUrl = '/security';
  } else if (action === 'dismiss') {
    // Just close the notification
    return;
  } else {
    // Default click action
    targetUrl = notificationData.url || '/dashboard';
  }

  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true })
      .then((clientList) => {
        // Check if app is already open
        for (const client of clientList) {
          if (client.url.includes(targetUrl) && 'focus' in client) {
            return client.focus();
          }
        }
        
        // Open new window/tab
        if (clients.openWindow) {
          return clients.openWindow(targetUrl);
        }
      })
      .catch((error) => {
        console.error('Failed to handle notification click:', error);
      })
  );
});

// Notification close event
self.addEventListener('notificationclose', (event) => {
  console.log('Notification closed:', event);
  
  // Track notification dismissal
  const notificationData = event.notification.data || {};
  
  if (notificationData.trackDismissal) {
    // Send analytics event to server
    fetch('/api/analytics/notification-dismissed', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        notificationId: notificationData.id,
        timestamp: Date.now()
      })
    }).catch(error => {
      console.error('Failed to track notification dismissal:', error);
    });
  }
});

// Background sync event (for future implementation)
self.addEventListener('sync', (event) => {
  console.log('Background sync triggered:', event.tag);
  
  if (event.tag === 'background-sync-transactions') {
    event.waitUntil(
      // Sync pending transactions when online
      syncPendingTransactions()
    );
  }
});

// Sync pending transactions (placeholder)
async function syncPendingTransactions() {
  try {
    // This would sync any pending transactions stored in IndexedDB
    console.log('Syncing pending transactions...');
    
    // Implementation would:
    // 1. Get pending transactions from IndexedDB
    // 2. Send them to server
    // 3. Remove from IndexedDB on success
    
  } catch (error) {
    console.error('Failed to sync pending transactions:', error);
  }
}

// Message event - handle messages from the main thread
self.addEventListener('message', (event) => {
  console.log('Service Worker received message:', event.data);
  
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  } else if (event.data && event.data.type === 'GET_VERSION') {
    event.ports[0].postMessage({ version: CACHE_NAME });
  }
});

// Error event
self.addEventListener('error', (event) => {
  console.error('Service Worker error:', event);
});

// Unhandled rejection
self.addEventListener('unhandledrejection', (event) => {
  console.error('Service Worker unhandled rejection:', event);
});

console.log('Service Worker script loaded successfully');
