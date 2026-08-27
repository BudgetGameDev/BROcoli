'use strict';

const assert = require('node:assert/strict');
const path = require('node:path');

const platform = require(path.join(
  __dirname,
  '..',
  'Assets',
  'WebGLTemplates',
  'Custom',
  'platform-detection.js'
));

function environment({
  userAgent,
  navigatorPlatform,
  maxTouchPoints = 0,
  innerWidth = 1440,
  touch = false
}) {
  const browserWindow = {
    innerWidth,
    matchMedia: () => ({ matches: false })
  };
  if (touch) browserWindow.ontouchstart = null;

  return {
    navigator: {
      userAgent,
      platform: navigatorPlatform,
      maxTouchPoints
    },
    window: browserWindow,
    document: { referrer: '' }
  };
}

const macSafari =
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) ' +
  'AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.6 Safari/605.1.15';
const ipadSafari =
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15) ' +
  'AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1';

for (const maxTouchPoints of [0, 5]) {
  const result = platform.detect(environment({
    userAgent: macSafari,
    navigatorPlatform: 'MacIntel',
    maxTouchPoints,
    innerWidth: 900,
    touch: maxTouchPoints > 0
  }));

  assert.equal(result.isSafari, true, 'macOS Safari should remain Safari');
  assert.equal(result.isAppleMobile, false, 'macOS Safari must not be detected as iOS');
  assert.equal(result.isMobile, false, 'macOS Safari must retain desktop controls');
}

const ipad = platform.detect(environment({
  userAgent: ipadSafari,
  navigatorPlatform: 'MacIntel',
  maxTouchPoints: 5,
  innerWidth: 1024,
  touch: true
}));
assert.equal(ipad.isIPadOS, true);
assert.equal(ipad.isAppleMobile, true);
assert.equal(ipad.isMobile, true);

const iphone = platform.detect(environment({
  userAgent:
    'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) ' +
    'AppleWebKit/605.1.15 Mobile/15E148 Safari/604.1',
  navigatorPlatform: 'iPhone',
  maxTouchPoints: 5,
  innerWidth: 390,
  touch: true
}));
assert.equal(iphone.isIOS, true);
assert.equal(iphone.isAppleMobile, true);

const android = platform.detect(environment({
  userAgent:
    'Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 ' +
    'Chrome/140.0 Mobile Safari/537.36',
  navigatorPlatform: 'Linux armv8l',
  maxTouchPoints: 5,
  innerWidth: 412,
  touch: true
}));
assert.equal(android.isAndroid, true);
assert.equal(android.isMobile, true);
assert.equal(android.isSafari, false);

console.log('webgl platform detection: all cases passed');
