'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const templatePath = path.join(
  __dirname,
  '..',
  'Assets',
  'WebGLTemplates',
  'Custom',
  'index.html'
);
const template = fs.readFileSync(templatePath, 'utf8');
const projectSettings = fs.readFileSync(
  path.join(__dirname, '..', 'ProjectSettings', 'ProjectSettings.asset'),
  'utf8'
);
const releaseProfile = fs.readFileSync(
  path.join(__dirname, '..', 'Assets', 'Settings', 'Build Profiles', 'Web - Desktop - Release.asset'),
  'utf8'
);
const serviceWorker = fs.readFileSync(
  path.join(__dirname, '..', 'Assets', 'WebGLTemplates', 'Custom', 'sw.js'),
  'utf8'
);
const versionChecker = fs.readFileSync(
  path.join(__dirname, '..', 'Assets', 'WebGLTemplates', 'Custom', 'version-check.js'),
  'utf8'
);
const iosOptimizer = fs.readFileSync(
  path.join(__dirname, '..', 'Assets', 'Scripts', 'iOSSafariWebGLOptimizer.cs'),
  'utf8'
);
const resizeFunction = template.match(
  /function updateCanvasSize\(\)\s*\{([\s\S]*?)\n\s*\}\n\s*\n\s*\/\/ Full-screen canvas setup/
);

assert.ok(resizeFunction, 'updateCanvasSize must remain present in the WebGL template');
assert.match(template, /matchWebGLToCanvasSize:\s*true/);
assert.match(
  template,
  /devicePixelRatio:\s*unityDevicePixelRatio/,
  'Unity must use the platform policy covered by the browser matrix'
);
assert.match(
  template,
  /unityDevicePixelRatio\s*=\s*platformInfo\.unityDevicePixelRatio/,
  'The tested platform render scale must reach the compiled Unity configuration'
);
assert.doesNotMatch(
  resizeFunction[1],
  /canvas\.(?:width|height)\s*=/,
  'Unity owns the drawing-buffer size when matchWebGLToCanvasSize is enabled'
);
assert.doesNotMatch(
  template,
  /powerPreference:\s*['"]high-performance['"]/,
  'Let the browser choose a compatible GPU adapter'
);
assert.doesNotMatch(
  template,
  /webglContextAttributes\s*:/,
  'Configure the Unity WebGL context through PlayerSettings, not template overrides'
);
assert.match(projectSettings, /^\s*webGLPowerPreference:\s*0$/m);
assert.match(releaseProfile, /webGLPowerPreference:\s*0/);
assert.match(projectSettings, /^\s*webGLInitialMemorySize:\s*256$/m);
assert.match(releaseProfile, /webGLInitialMemorySize:\s*256/);
assert.doesNotMatch(
  serviceWorker,
  /setTimeout\(\(\) => controller\.abort\(\),\s*30000\)/,
  'Large Unity build downloads must not be aborted on slow mobile connections'
);
assert.match(
  template,
  /navigator\.serviceWorker\.controller\s*&&\s*isMobile\s*&&\s*!isiOSDevice/,
  'iOS must not re-fetch the full Unity payload immediately after startup'
);
assert.match(serviceWorker, /const CACHE_VERSION = 'v5'/);
assert.match(versionChecker, /cacheVersion:\s*'v5'/);
assert.match(versionChecker, /unity-game-cache-v5-/);
assert.doesNotMatch(
  iosOptimizer,
  /Shader\.WarmupAllShaders\(\)/,
  'iOS startup must not prewarm every shader variant'
);

console.log('webgl template contract: all checks passed');
