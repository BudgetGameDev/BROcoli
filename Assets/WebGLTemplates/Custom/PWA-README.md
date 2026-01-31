# PWA (Progressive Web App) Setup Guide

This WebGL template includes full Progressive Web App support, enabling your game to be installed as a native-like app on mobile devices and desktops.

## Features

✅ **Automatic Install Prompt** - Detects platform and shows appropriate installation instructions  
✅ **iOS Safari Support** - Step-by-step guide for "Add to Home Screen"  
✅ **Android Chrome/Samsung** - Native install prompt via `beforeinstallprompt` API  
✅ **Desktop Support** - Install as standalone app on Chrome/Edge  
✅ **True Fullscreen** - When installed, runs without any browser UI  
✅ **Offline Support** - Service worker caches game files for offline play  
✅ **Smart Dismissal** - Remembers user preference, doesn't annoy repeat visitors  

## Files Included

```
WebGLTemplates/Custom/
├── index.html          # Main template with PWA integration
├── manifest.json       # PWA manifest (app name, icons, display mode)
├── sw.js              # Service worker for caching/offline support
├── pwa-install.css    # Install wizard styling
├── pwa-install.js     # Install wizard logic & platform detection
├── generate-icons.html # Tool to generate PWA icons
└── icons/             # PWA icon directory
    └── icon.svg       # Source icon (vector)
```

## Setup Instructions

### 1. Generate PWA Icons

Before building, you need PNG icons in various sizes. Two options:

**Option A: Use the Icon Generator**
1. Open `generate-icons.html` in a browser
2. Click "Generate & Download All Icons"
3. Move downloaded PNGs to the `icons/` folder

**Option B: Create Custom Icons**
Create PNG files in these sizes and place in `icons/`:
- icon-72x72.png
- icon-96x96.png
- icon-128x128.png
- icon-144x144.png
- icon-152x152.png
- icon-192x192.png
- icon-384x384.png
- icon-512x512.png

### 2. Build Your WebGL Project

1. In Unity, go to **File > Build Settings**
2. Select **WebGL** platform
3. Click **Player Settings**
4. Under **Resolution and Presentation**, select your **Custom** WebGL template
5. Build your project

### 3. Deploy to HTTPS

PWAs require HTTPS to work. Options:
- **GitHub Pages** - Free, easy setup
- **Netlify** - Free tier available
- **Vercel** - Free tier available
- **Any web server with SSL**

## How It Works

### Install Flow

1. **First Visit** - After a 2-second delay, the install wizard appears
2. **Platform Detection** - Automatically detects iOS, Android, or Desktop
3. **Appropriate Instructions** - Shows platform-specific installation steps
4. **User Choice** - User can install, dismiss, or permanently hide the prompt

### iOS Safari
Since iOS doesn't support the standard PWA install prompt, users see step-by-step instructions:
1. Tap the Share button
2. Scroll to "Add to Home Screen"
3. Tap "Add"

### Android Chrome/Samsung Internet
These browsers support the native `beforeinstallprompt` API:
- A single "Install App" button triggers the native install dialog

### Desktop (Chrome/Edge)
- Similar to Android, shows a native install prompt
- Can also be installed via browser's "Install" button in address bar

## Customization

### Change Install Prompt Timing

In `pwa-install.js`, modify:
```javascript
const CONFIG = {
  showDelay: 2000,           // Delay before showing prompt (ms)
  maxDismissals: 3,          // Show again after this many dismissals
  dismissalResetDays: 7      // Reset dismissal count after this many days
};
```

### Customize Appearance

Edit `pwa-install.css` to match your game's theme:
- Colors are defined using CSS custom properties
- The gradient uses `#4facfe` → `#00f2fe` (cyan/blue)
- Background uses `#1a1a2e` → `#16213e` (dark blue)

### Modify App Metadata

Edit `manifest.json`:
```json
{
  "name": "Your Game Name",
  "short_name": "Game",
  "description": "Your game description",
  "theme_color": "#000000",
  "background_color": "#000000"
}
```

Note: `{{{ PRODUCT_NAME }}}` is automatically replaced by Unity during build.

## Calling Install Prompt from Game

You can trigger the install wizard from your Unity game via JavaScript:

```csharp
// In Unity C#
using System.Runtime.InteropServices;

public class PWAHelper : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void ShowPWAInstallPrompt();
    
    public void ShowInstallPrompt()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        ShowPWAInstallPrompt();
        #endif
    }
}
```

Add this to your `index.html` (already included):
```javascript
// This is exposed globally
window.PWAInstall.show();
```

Create a `.jslib` plugin file:
```javascript
mergeInto(LibraryManager.library, {
    ShowPWAInstallPrompt: function() {
        if (window.PWAInstall) {
            window.PWAInstall.show();
        }
    }
});
```

## Troubleshooting

### Install Prompt Not Showing

1. **Check HTTPS** - PWAs only work on HTTPS (or localhost)
2. **Check manifest.json** - Must be valid JSON, linked in HTML
3. **Check icons** - All referenced icons must exist
4. **Check service worker** - Must register successfully (check console)

### iOS Safari Issues

- iOS only supports PWA via Safari (not Chrome/Firefox on iOS)
- Must use "Add to Home Screen" - no automatic prompt available
- Some features (like push notifications) aren't supported on iOS

### Service Worker Not Caching

- Clear browser cache and reload
- Check DevTools > Application > Service Workers
- Ensure sw.js is in the root of your build output

## Testing PWA

### Chrome DevTools
1. Open DevTools (F12)
2. Go to **Application** tab
3. Check **Manifest** section - should show your app info
4. Check **Service Workers** - should show registered
5. Use **Lighthouse** audit to test PWA compliance

### Mobile Testing
1. Deploy to HTTPS
2. Open on mobile device
3. Install prompt should appear after 2 seconds
4. For iOS, test in Safari specifically

## Browser Support

| Browser | Install Prompt | Fullscreen PWA |
|---------|---------------|----------------|
| Chrome (Android) | ✅ Native | ✅ |
| Samsung Internet | ✅ Native | ✅ |
| Firefox (Android) | ❌ Manual | ✅ |
| Safari (iOS) | ❌ Manual | ✅ |
| Chrome (Desktop) | ✅ Native | ✅ |
| Edge (Desktop) | ✅ Native | ✅ |
| Firefox (Desktop) | ❌ | ❌ |
| Safari (macOS) | ❌ | ❌ |
