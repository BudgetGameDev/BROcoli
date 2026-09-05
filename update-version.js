#!/usr/bin/env node
/**
 * Build Version Updater
 * 
 * Run this script as part of your build/deploy process to update version.json
 * with a new build ID before deploying to GitHub Pages.
 * 
 * Usage:
 *   node update-version.js [description]
 *   
 * Example:
 *   node update-version.js "Added new level and fixed bugs"
 * 
 * This will:
 *   1. Generate a new unique build ID (timestamp + random)
 *   2. Increment the build number
 *   3. Update the timestamp
 *   4. Save to version.json
 */

const fs = require('fs');
const path = require('path');

const VERSION_FILE = path.join(__dirname, 'version.json');

function generateBuildId() {
  const timestamp = Date.now();
  const random = Math.random().toString(36).substring(2, 8);
  return `build-${timestamp}-${random}`;
}

function updateVersion(description) {
  let currentVersion = {
    buildId: 'initial',
    buildTimestamp: new Date().toISOString(),
    buildNumber: 0,
    description: 'Initial build'
  };
  
  // Try to read existing version file
  try {
    if (fs.existsSync(VERSION_FILE)) {
      const content = fs.readFileSync(VERSION_FILE, 'utf8');
      currentVersion = JSON.parse(content);
    }
  } catch (err) {
    console.warn('Could not read existing version.json, creating new one');
  }
  
  // Create new version
  const newVersion = {
    buildId: generateBuildId(),
    buildTimestamp: new Date().toISOString(),
    buildNumber: (currentVersion.buildNumber || 0) + 1,
    description: description || `Build #${(currentVersion.buildNumber || 0) + 1}`
  };
  
  // Write new version
  fs.writeFileSync(VERSION_FILE, JSON.stringify(newVersion, null, 2) + '\n');
  
  console.log('Updated version.json:');
  console.log(JSON.stringify(newVersion, null, 2));
  console.log('');
  console.log('Previous build ID:', currentVersion.buildId);
  console.log('New build ID:', newVersion.buildId);
  
  return newVersion;
}

// Run if called directly
if (require.main === module) {
  const description = process.argv.slice(2).join(' ') || undefined;
  updateVersion(description);
}

module.exports = { updateVersion, generateBuildId };
