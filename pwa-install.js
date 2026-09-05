/**
 * PWA Install Wizard
 * Detects platform (iOS/Android/Desktop) and guides users through app installation
 */

(function() {
  'use strict';

  // Configuration
  const CONFIG = {
    showDelay: 2000,           // Delay before showing prompt (ms)
    dismissStorageKey: 'pwa_install_dismissed',
    installedStorageKey: 'pwa_installed',
    maxDismissals: 3,          // Show again after this many dismissals
    dismissalResetDays: 7      // Reset dismissal count after this many days
  };

  // Platform detection
  const Platform = {
    isIOS: function() {
      return /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;
    },
    isIPadOS: function() {
      return navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1;
    },
    isAndroid: function() {
      return /Android/.test(navigator.userAgent);
    },
    isMobile: function() {
      return this.isIOS() || this.isIPadOS() || this.isAndroid() || 
             /webOS|BlackBerry|Opera Mini|IEMobile/i.test(navigator.userAgent);
    },
    isStandalone: function() {
      // Check if running as installed PWA
      return window.matchMedia('(display-mode: standalone)').matches ||
             window.matchMedia('(display-mode: fullscreen)').matches ||
             window.navigator.standalone === true || // iOS Safari
             document.referrer.includes('android-app://');
    },
    isSafari: function() {
      return /^((?!chrome|android).)*safari/i.test(navigator.userAgent);
    },
    isChrome: function() {
      return /Chrome/.test(navigator.userAgent) && /Google Inc/.test(navigator.vendor);
    },
    isFirefox: function() {
      return /Firefox/.test(navigator.userAgent);
    },
    isSamsung: function() {
      return /SamsungBrowser/.test(navigator.userAgent);
    },
    getName: function() {
      if (this.isIOS() || this.isIPadOS()) return 'ios';
      if (this.isAndroid()) return 'android';
      return 'desktop';
    },
    getBrowserName: function() {
      if (this.isSafari()) return 'Safari';
      if (this.isChrome()) return 'Chrome';
      if (this.isFirefox()) return 'Firefox';
      if (this.isSamsung()) return 'Samsung Internet';
      return 'your browser';
    }
  };

  // Storage helpers
  const Storage = {
    get: function(key) {
      try {
        const item = localStorage.getItem(key);
        return item ? JSON.parse(item) : null;
      } catch (e) {
        return null;
      }
    },
    set: function(key, value) {
      try {
        localStorage.setItem(key, JSON.stringify(value));
      } catch (e) {
        console.warn('[PWA] Storage unavailable');
      }
    }
  };

  // PWA Install Manager
  const PWAInstall = {
    deferredPrompt: null,
    overlay: null,

    init: function() {
      console.log('[PWA] Initializing install wizard...');
      console.log('[PWA] Platform:', Platform.getName());
      console.log('[PWA] Standalone:', Platform.isStandalone());
      console.log('[PWA] Browser:', Platform.getBrowserName());

      // If already running as PWA, don't show anything
      if (Platform.isStandalone()) {
        console.log('[PWA] Running in standalone mode - skipping install prompt');
        Storage.set(CONFIG.installedStorageKey, true);
        return;
      }

      // Listen for the beforeinstallprompt event (Chrome/Edge/Samsung)
      window.addEventListener('beforeinstallprompt', (e) => {
        console.log('[PWA] beforeinstallprompt event fired');
        e.preventDefault();
        this.deferredPrompt = e;
        this.showInstallUI();
      });

      // Listen for successful installation
      window.addEventListener('appinstalled', () => {
        console.log('[PWA] App was installed');
        Storage.set(CONFIG.installedStorageKey, true);
        this.hideOverlay();
        this.deferredPrompt = null;
      });

      // For iOS and browsers without beforeinstallprompt, show manual instructions
      if (Platform.isMobile() && !this.deferredPrompt) {
        setTimeout(() => {
          if (!this.deferredPrompt && !Platform.isStandalone()) {
            this.showInstallUI();
          }
        }, CONFIG.showDelay);
      }
    },

    shouldShowPrompt: function() {
      // Already installed
      if (Storage.get(CONFIG.installedStorageKey)) {
        return false;
      }

      // Check dismissal data
      const dismissData = Storage.get(CONFIG.dismissStorageKey);
      if (dismissData) {
        const daysSinceDismissal = (Date.now() - dismissData.timestamp) / (1000 * 60 * 60 * 24);
        
        // Reset if enough time has passed
        if (daysSinceDismissal > CONFIG.dismissalResetDays) {
          Storage.set(CONFIG.dismissStorageKey, null);
          return true;
        }

        // Don't show if dismissed too many times recently
        if (dismissData.count >= CONFIG.maxDismissals) {
          return false;
        }
      }

      return true;
    },

    showInstallUI: function() {
      if (!this.shouldShowPrompt()) {
        console.log('[PWA] Prompt suppressed due to previous dismissals');
        return;
      }

      console.log('[PWA] Showing install UI');
      this.createOverlay();
      
      setTimeout(() => {
        if (this.overlay) {
          this.overlay.classList.add('active');
        }
      }, 100);
    },

    createOverlay: function() {
      if (this.overlay) {
        document.body.removeChild(this.overlay);
      }

      const platform = Platform.getName();
      const html = this.getDialogHTML(platform);

      this.overlay = document.createElement('div');
      this.overlay.className = 'pwa-overlay';
      this.overlay.innerHTML = html;
      document.body.appendChild(this.overlay);

      // Bind events
      this.bindEvents();
    },

    getDialogHTML: function(platform) {
      const appName = document.title.replace(' - Unity WebGL Player', '').trim() || 'Game';
      
      let instructionsHTML = '';
      let primaryButtonHTML = '';

      if (platform === 'ios') {
        instructionsHTML = this.getIOSInstructions();
        primaryButtonHTML = `
          <button class="pwa-btn pwa-btn-secondary" onclick="PWAInstall.dismiss()">
            Got it, I'll add it later
          </button>
        `;
      } else if (platform === 'android' && this.deferredPrompt) {
        instructionsHTML = this.getAndroidNativeInstructions();
        primaryButtonHTML = `
          <button class="pwa-btn pwa-btn-primary" onclick="PWAInstall.triggerInstall()">
            <span>📲</span> Install App
          </button>
          <button class="pwa-btn pwa-btn-secondary" onclick="PWAInstall.dismiss()">
            Maybe Later
          </button>
        `;
      } else if (platform === 'android') {
        instructionsHTML = this.getAndroidManualInstructions();
        primaryButtonHTML = `
          <button class="pwa-btn pwa-btn-secondary" onclick="PWAInstall.dismiss()">
            Got it, I'll add it later
          </button>
        `;
      } else if (this.deferredPrompt) {
        // Desktop with install prompt available
        instructionsHTML = this.getDesktopInstructions();
        primaryButtonHTML = `
          <button class="pwa-btn pwa-btn-primary" onclick="PWAInstall.triggerInstall()">
            <span>📲</span> Install App
          </button>
          <button class="pwa-btn pwa-btn-secondary" onclick="PWAInstall.dismiss()">
            Continue in Browser
          </button>
        `;
      } else {
        // Desktop without install prompt
        instructionsHTML = `
          <div class="pwa-fullscreen-hint">
            💡 Tip: Press <strong>F11</strong> for fullscreen mode
          </div>
        `;
        primaryButtonHTML = `
          <button class="pwa-btn pwa-btn-primary" onclick="PWAInstall.requestFullscreen()">
            <span>⛶</span> Enter Fullscreen
          </button>
          <button class="pwa-btn pwa-btn-secondary" onclick="PWAInstall.dismiss()">
            Continue
          </button>
        `;
      }

      return `
        <div class="pwa-dialog">
          <div class="pwa-header">
            <img src="icons/icon-192x192.png" alt="${appName}" class="pwa-icon" onerror="this.style.display='none'">
            <h2 class="pwa-title">${appName}</h2>
            <p class="pwa-subtitle">Install for the best experience</p>
          </div>

          <div class="pwa-benefits">
            <div class="pwa-benefit">
              <div class="pwa-benefit-icon">🖥️</div>
              <span>Full screen gameplay without browser UI</span>
            </div>
            <div class="pwa-benefit">
              <div class="pwa-benefit-icon">🚀</div>
              <span>Launch instantly from your home screen</span>
            </div>
            <div class="pwa-benefit">
              <div class="pwa-benefit-icon">📴</div>
              <span>Play offline after first load</span>
            </div>
          </div>

          ${instructionsHTML}

          <div class="pwa-buttons">
            ${primaryButtonHTML}
            <button class="pwa-btn pwa-btn-text" onclick="PWAInstall.dismissPermanently()">
              Don't show this again
            </button>
          </div>
        </div>
      `;
    },

    getIOSInstructions: function() {
      return `
        <div class="pwa-instructions">
          <h3 class="pwa-instructions-title">
            <span class="platform-icon">🍎</span> Add to Home Screen
          </h3>
          <div class="pwa-step">
            <div class="pwa-step-number">1</div>
            <div class="pwa-step-text">
              Tap the <span class="pwa-step-highlight">Share</span> button 
              <span class="ios-share-icon"></span> at the bottom of Safari
            </div>
          </div>
          <div class="pwa-step">
            <div class="pwa-step-number">2</div>
            <div class="pwa-step-text">
              Scroll down and tap <span class="pwa-step-highlight">Add to Home Screen</span>
            </div>
          </div>
          <div class="pwa-step">
            <div class="pwa-step-number">3</div>
            <div class="pwa-step-text">
              Tap <span class="pwa-step-highlight">Add</span> in the top right corner
            </div>
          </div>
        </div>
      `;
    },

    getAndroidNativeInstructions: function() {
      return `
        <div class="pwa-fullscreen-hint">
          ✨ Tap "Install App" to add this game to your home screen for a fullscreen experience!
        </div>
      `;
    },

    getAndroidManualInstructions: function() {
      const browser = Platform.getBrowserName();
      return `
        <div class="pwa-instructions">
          <h3 class="pwa-instructions-title">
            <span class="platform-icon">🤖</span> Add to Home Screen
          </h3>
          <div class="pwa-step">
            <div class="pwa-step-number">1</div>
            <div class="pwa-step-text">
              Tap the <span class="pwa-step-highlight">menu</span> button (⋮) in ${browser}
            </div>
          </div>
          <div class="pwa-step">
            <div class="pwa-step-number">2</div>
            <div class="pwa-step-text">
              Tap <span class="pwa-step-highlight">Add to Home screen</span> or <span class="pwa-step-highlight">Install app</span>
            </div>
          </div>
          <div class="pwa-step">
            <div class="pwa-step-number">3</div>
            <div class="pwa-step-text">
              Confirm by tapping <span class="pwa-step-highlight">Add</span>
            </div>
          </div>
        </div>
      `;
    },

    getDesktopInstructions: function() {
      return `
        <div class="pwa-fullscreen-hint">
          ✨ Install as an app for a native fullscreen gaming experience!
        </div>
      `;
    },

    triggerInstall: async function() {
      if (!this.deferredPrompt) {
        console.warn('[PWA] No install prompt available');
        return;
      }

      console.log('[PWA] Triggering install prompt');
      this.deferredPrompt.prompt();

      const { outcome } = await this.deferredPrompt.userChoice;
      console.log('[PWA] User choice:', outcome);

      if (outcome === 'accepted') {
        Storage.set(CONFIG.installedStorageKey, true);
      }

      this.deferredPrompt = null;
      this.hideOverlay();
    },

    requestFullscreen: function() {
      const elem = document.documentElement;
      if (elem.requestFullscreen) {
        elem.requestFullscreen();
      } else if (elem.webkitRequestFullscreen) {
        elem.webkitRequestFullscreen();
      } else if (elem.msRequestFullscreen) {
        elem.msRequestFullscreen();
      }
      this.hideOverlay();
    },

    dismiss: function() {
      console.log('[PWA] User dismissed prompt');
      
      const dismissData = Storage.get(CONFIG.dismissStorageKey) || { count: 0 };
      dismissData.count++;
      dismissData.timestamp = Date.now();
      Storage.set(CONFIG.dismissStorageKey, dismissData);

      this.hideOverlay();
    },

    dismissPermanently: function() {
      console.log('[PWA] User permanently dismissed prompt');
      Storage.set(CONFIG.dismissStorageKey, { count: CONFIG.maxDismissals + 1, timestamp: Date.now() });
      this.hideOverlay();
    },

    hideOverlay: function() {
      if (this.overlay) {
        this.overlay.classList.add('dismissing');
        setTimeout(() => {
          if (this.overlay && this.overlay.parentNode) {
            this.overlay.parentNode.removeChild(this.overlay);
            this.overlay = null;
          }
        }, 300);
      }
    },

    bindEvents: function() {
      // Close on backdrop click
      if (this.overlay) {
        this.overlay.addEventListener('click', (e) => {
          if (e.target === this.overlay) {
            this.dismiss();
          }
        });
      }

      // Close on escape key
      document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && this.overlay) {
          this.dismiss();
        }
      });
    },

    // Public method to manually show the install prompt (can be called from game)
    show: function() {
      // Reset dismissal to allow showing
      Storage.set(CONFIG.dismissStorageKey, null);
      this.showInstallUI();
    }
  };

  // Make PWAInstall globally accessible
  window.PWAInstall = PWAInstall;

  // Initialize when DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => PWAInstall.init());
  } else {
    PWAInstall.init();
  }
})();
