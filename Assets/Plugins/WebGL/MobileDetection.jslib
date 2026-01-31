mergeInto(LibraryManager.library, {
  
  // Detect if running on iOS (iPhone/iPad) via Safari or WebView
  IsiOSMobile: function() {
    var userAgent = navigator.userAgent || navigator.vendor || window.opera;
    
    // Check for iOS
    var isiOS = /iPad|iPhone|iPod/.test(userAgent) && !window.MSStream;
    
    // Also check for iPad on iOS 13+ which reports as Mac
    var isMacWithTouch = (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    
    // Additional check for iOS WebView
    var isiOSWebView = /(iPhone|iPod|iPad).*AppleWebKit(?!.*Safari)/i.test(userAgent);
    
    var result = isiOS || isMacWithTouch || isiOSWebView;
    
    console.log('[MobileDetection] IsiOSMobile check - userAgent: ' + userAgent);
    console.log('[MobileDetection] isiOS: ' + isiOS + ', isMacWithTouch: ' + isMacWithTouch + ', isiOSWebView: ' + isiOSWebView);
    console.log('[MobileDetection] IsiOSMobile result: ' + result);
    
    return result ? 1 : 0;
  },
  
  // Detect if running on Android mobile browser
  IsAndroidMobile: function() {
    var userAgent = navigator.userAgent || navigator.vendor || window.opera;
    var result = /android/i.test(userAgent);
    
    console.log('[MobileDetection] IsAndroidMobile check - result: ' + result);
    
    return result ? 1 : 0;
  },
  
  // Combined check for any mobile browser
  IsMobileBrowser: function() {
    var userAgent = navigator.userAgent || navigator.vendor || window.opera;
    
    // iOS detection
    var isiOS = /iPad|iPhone|iPod/.test(userAgent) && !window.MSStream;
    var isMacWithTouch = (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    var isiOSWebView = /(iPhone|iPod|iPad).*AppleWebKit(?!.*Safari)/i.test(userAgent);
    
    // Android detection  
    var isAndroid = /android/i.test(userAgent);
    
    // General mobile detection
    var isMobile = /Mobile|webOS|BlackBerry|IEMobile|Opera Mini/i.test(userAgent);
    
    // Touch capability as additional signal
    var hasTouch = ('ontouchstart' in window) || (navigator.maxTouchPoints > 0);
    
    // Mobile viewport detection
    var isSmallScreen = (window.innerWidth <= 1024 && hasTouch);
    
    var result = isiOS || isMacWithTouch || isiOSWebView || isAndroid || isMobile || isSmallScreen;
    
    console.log('[MobileDetection] IsMobileBrowser comprehensive check:');
    console.log('  userAgent: ' + userAgent);
    console.log('  isiOS: ' + isiOS + ', isMacWithTouch: ' + isMacWithTouch);
    console.log('  isAndroid: ' + isAndroid + ', isMobile: ' + isMobile);
    console.log('  hasTouch: ' + hasTouch + ', isSmallScreen: ' + isSmallScreen);
    console.log('  RESULT: ' + result);
    
    return result ? 1 : 0;
  },
  
  // Check if running in Safari browser
  IsSafariBrowser: function() {
    var userAgent = navigator.userAgent;
    var isSafari = /^((?!chrome|android).)*safari/i.test(userAgent);
    
    console.log('[MobileDetection] IsSafariBrowser: ' + isSafari);
    
    return isSafari ? 1 : 0;
  },
  
  // Force enable touch events for iOS Safari PWA mode
  EnableTouchEvents: function() {
    console.log('[MobileDetection] EnableTouchEvents called');
    
    // Ensure touch events work in fullscreen/PWA mode on iOS Safari
    document.addEventListener('touchstart', function(e) {}, { passive: true });
    document.addEventListener('touchmove', function(e) {}, { passive: true });
    document.addEventListener('touchend', function(e) {}, { passive: true });
    
    // Prevent default touch behaviors that might interfere
    var canvas = document.querySelector('#unity-canvas');
    if (canvas) {
      canvas.style.touchAction = 'none';
      canvas.addEventListener('touchstart', function(e) { e.preventDefault(); }, { passive: false });
      canvas.addEventListener('touchmove', function(e) { e.preventDefault(); }, { passive: false });
      
      console.log('[MobileDetection] Canvas touch events configured');
    }
  },
  
  // Get detailed device info for debugging
  GetMobileDeviceInfo: function() {
    var info = {
      userAgent: navigator.userAgent,
      platform: navigator.platform,
      maxTouchPoints: navigator.maxTouchPoints,
      innerWidth: window.innerWidth,
      innerHeight: window.innerHeight,
      devicePixelRatio: window.devicePixelRatio,
      touchSupported: 'ontouchstart' in window
    };
    
    var infoStr = JSON.stringify(info);
    console.log('[MobileDetection] Device info: ' + infoStr);
    
    var bufferSize = lengthBytesUTF8(infoStr) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(infoStr, buffer, bufferSize);
    return buffer;
  },
  
  // ============ PWA Functions ============
  
  // Check if running as installed PWA (standalone mode)
  IsPWAInstalled: function() {
    var isStandalone = window.matchMedia('(display-mode: standalone)').matches ||
                       window.matchMedia('(display-mode: fullscreen)').matches ||
                       window.navigator.standalone === true ||
                       document.referrer.includes('android-app://');
    
    console.log('[PWA] IsPWAInstalled: ' + isStandalone);
    return isStandalone ? 1 : 0;
  },
  
  // Show the PWA install prompt/wizard
  ShowPWAInstallPrompt: function() {
    console.log('[PWA] ShowPWAInstallPrompt called from Unity');
    if (window.PWAInstall) {
      window.PWAInstall.show();
    } else {
      console.warn('[PWA] PWAInstall not available');
    }
  },
  
  // Request fullscreen mode
  RequestFullscreen: function() {
    console.log('[PWA] RequestFullscreen called from Unity');
    var elem = document.documentElement;
    if (elem.requestFullscreen) {
      elem.requestFullscreen().catch(function(err) {
        console.warn('[PWA] Fullscreen request failed:', err);
      });
    } else if (elem.webkitRequestFullscreen) {
      elem.webkitRequestFullscreen();
    } else if (elem.msRequestFullscreen) {
      elem.msRequestFullscreen();
    }
  },
  
  // Exit fullscreen mode
  ExitFullscreen: function() {
    console.log('[PWA] ExitFullscreen called from Unity');
    if (document.exitFullscreen) {
      document.exitFullscreen();
    } else if (document.webkitExitFullscreen) {
      document.webkitExitFullscreen();
    } else if (document.msExitFullscreen) {
      document.msExitFullscreen();
    }
  },
  
  // Check if currently in fullscreen
  IsFullscreen: function() {
    var isFullscreen = document.fullscreenElement != null ||
                       document.webkitFullscreenElement != null ||
                       document.msFullscreenElement != null;
    return isFullscreen ? 1 : 0;
  },
  
  // Get PWA display mode
  GetPWADisplayMode: function() {
    var mode = 'browser';
    if (window.matchMedia('(display-mode: fullscreen)').matches) {
      mode = 'fullscreen';
    } else if (window.matchMedia('(display-mode: standalone)').matches) {
      mode = 'standalone';
    } else if (window.matchMedia('(display-mode: minimal-ui)').matches) {
      mode = 'minimal-ui';
    } else if (window.navigator.standalone === true) {
      mode = 'standalone-ios';
    }
    
    console.log('[PWA] Display mode: ' + mode);
    
    var bufferSize = lengthBytesUTF8(mode) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(mode, buffer, bufferSize);
    return buffer;
  }
  
});
