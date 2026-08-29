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
const mobileDetection = fs.readFileSync(
  path.join(__dirname, '..', 'Assets', 'Plugins', 'WebGL', 'MobileDetection.jslib'),
  'utf8'
);
const qualitySettings = fs.readFileSync(
  path.join(__dirname, '..', 'ProjectSettings', 'QualitySettings.asset'),
  'utf8'
);
const graphicsSettings = fs.readFileSync(
  path.join(__dirname, '..', 'ProjectSettings', 'GraphicsSettings.asset'),
  'utf8'
);

// Resolve the pipeline asset through GraphicsSettings rather than naming a path.
// This contract used to read Assets/Settings/UniversalRP.asset directly, and went
// on passing after the project switched pipelines, vouching for a light budget the
// game had stopped using. Following the GUID means it can only check the live one.
function findAssetByGuid(guid) {
  const pending = [path.join(__dirname, '..', 'Assets')];
  while (pending.length > 0) {
    const dir = pending.pop();
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        pending.push(full);
      } else if (entry.name.endsWith('.asset.meta')) {
        if (fs.readFileSync(full, 'utf8').includes(`guid: ${guid}`)) {
          return full.slice(0, -'.meta'.length);
        }
      }
    }
  }
  return null;
}

const pipelineGuid = graphicsSettings.match(
  /m_CustomRenderPipeline:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})/
);
assert.ok(pipelineGuid, 'GraphicsSettings must name the active render pipeline asset');
const pipelinePath = findAssetByGuid(pipelineGuid[1]);
assert.ok(pipelinePath, `No asset matches the active render pipeline GUID ${pipelineGuid[1]}`);
const urpSettings = fs.readFileSync(pipelinePath, 'utf8');
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
assert.match(serviceWorker, /const CACHE_VERSION = 'v6'/);
assert.match(versionChecker, /cacheVersion:\s*'v6'/);
assert.match(versionChecker, /unity-game-cache-v6-/);
assert.doesNotMatch(
  iosOptimizer,
  /Shader\.WarmupAllShaders\(\)/,
  'iOS startup must not prewarm every shader variant'
);
assert.doesNotMatch(
  iosOptimizer,
  /QualitySettings\.SetQualityLevel\(/,
  'iOS must retain the WebGL quality profile that supplies scene lighting'
);
assert.doesNotMatch(
  iosOptimizer,
  /QualitySettings\.(?:shadows|shadowResolution|shadowDistance)\s*=(?!=)/,
  'iOS optimizations must not override the project shadow policy'
);
assert.doesNotMatch(
  iosOptimizer,
  /urpAsset\.(?:shadowDistance|maxAdditionalLightsCount)\s*=(?!=)/,
  'iOS optimizations must preserve the URP scene-light budget'
);

const webglQuality = qualitySettings.match(/^\s*WebGL:\s*(\d+)$/m);
const additionalLights = urpSettings.match(/^\s*m_AdditionalLightsPerObjectLimit:\s*(\d+)$/m);
assert.ok(webglQuality, 'WebGL must declare a default quality profile');
assert.ok(Number(webglQuality[1]) >= 3, 'WebGL must retain the High-or-better lighting profile');
assert.ok(additionalLights, 'URP must declare its additional-light budget');
assert.ok(
  Number(additionalLights[1]) >= 2,
  'URP must support the Dungeon world and player-proximity lights together'
);
assert.match(iosOptimizer, /ReportIOSLightingSettings\(/);
assert.match(mobileDetection, /data-ios-lighting/);

console.log('webgl template contract: all checks passed');
