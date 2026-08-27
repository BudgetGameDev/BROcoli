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
const resizeFunction = template.match(
  /function updateCanvasSize\(\)\s*\{([\s\S]*?)\n\s*\}\n\s*\n\s*\/\/ Full-screen canvas setup/
);

assert.ok(resizeFunction, 'updateCanvasSize must remain present in the WebGL template');
assert.match(template, /matchWebGLToCanvasSize:\s*true/);
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

console.log('webgl template contract: all checks passed');
