(function(root) {
  'use strict';

  function getEnvironment() {
    return {
      navigator: root.navigator || {},
      window: root,
      document: root.document || {}
    };
  }

  function detect(environment) {
    var env = environment || getEnvironment();
    var nav = env.navigator || {};
    var win = env.window || {};
    var doc = env.document || {};
    var userAgent = nav.userAgent || nav.vendor || '';
    var platform = nav.platform || '';
    var maxTouchPoints = Number(nav.maxTouchPoints || 0);
    var hasTouch = 'ontouchstart' in win || maxTouchPoints > 0;
    var isIOSUserAgent = /iPad|iPhone|iPod/i.test(userAgent);

    // iPadOS can request desktop sites and report MacIntel. A real iPad still
    // includes Apple's Mobile token; desktop Safari does not. Requiring both
    // prevents a touch-capable Mac from being downgraded to the iOS profile.
    var isIPadOS =
      platform === 'MacIntel' &&
      maxTouchPoints > 1 &&
      /AppleWebKit/i.test(userAgent) &&
      /Mobile\//i.test(userAgent);
    var isAppleMobile = isIOSUserAgent || isIPadOS;
    var browserDevicePixelRatio = Number(win.devicePixelRatio || 1);
    if (!Number.isFinite(browserDevicePixelRatio) || browserDevicePixelRatio <= 0) {
      browserDevicePixelRatio = 1;
    }
    // iPhones and iPads have a tighter WebGL GPU-memory budget. Keeping this
    // policy beside platform detection makes it independently regression-testable
    // and prevents touch-capable Macs from accidentally receiving the iOS scale.
    var unityDevicePixelRatio = isAppleMobile ? 1 : browserDevicePixelRatio;
    var isAndroid = /Android/i.test(userAgent);
    var isOtherMobile = /webOS|BlackBerry|IEMobile|Opera Mini/i.test(userAgent);
    var isSmallTouchScreen =
      platform !== 'MacIntel' &&
      hasTouch &&
      Number(win.innerWidth || 0) > 0 &&
      Number(win.innerWidth || 0) <= 1024;
    var isMobile = isAppleMobile || isAndroid || isOtherMobile || isSmallTouchScreen;
    var isSafari =
      /Safari/i.test(userAgent) &&
      !/Chrome|Chromium|CriOS|Android|Edg|OPR|Firefox|FxiOS/i.test(userAgent);
    var displayModeStandalone = false;

    try {
      displayModeStandalone =
        (typeof win.matchMedia === 'function' &&
          (win.matchMedia('(display-mode: standalone)').matches ||
            win.matchMedia('(display-mode: fullscreen)').matches)) ||
        nav.standalone === true ||
        String(doc.referrer || '').indexOf('android-app://') === 0;
    } catch (error) {
      displayModeStandalone = false;
    }

    return {
      userAgent: userAgent,
      platform: platform,
      maxTouchPoints: maxTouchPoints,
      hasTouch: hasTouch,
      isIOS: isIOSUserAgent,
      isIPadOS: isIPadOS,
      isAppleMobile: isAppleMobile,
      unityDevicePixelRatio: unityDevicePixelRatio,
      isAndroid: isAndroid,
      isMobile: isMobile,
      isSafari: isSafari,
      isStandalone: displayModeStandalone
    };
  }

  function current() {
    return detect(getEnvironment());
  }

  var api = {
    detect: detect,
    current: current,
    isIOS: function() { return current().isIOS; },
    isIPadOS: function() { return current().isIPadOS; },
    isAppleMobile: function() { return current().isAppleMobile; },
    isAndroid: function() { return current().isAndroid; },
    isMobile: function() { return current().isMobile; },
    isStandalone: function() { return current().isStandalone; },
    isSafari: function() { return current().isSafari; },
    shouldUseIOSOptimizations: function() { return current().isAppleMobile; }
  };

  root.BroccoliPlatform = api;
  if (typeof module === 'object' && module.exports) {
    module.exports = api;
  }
})(typeof window !== 'undefined' ? window : globalThis);
