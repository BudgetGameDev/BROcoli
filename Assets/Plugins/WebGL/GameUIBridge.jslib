// GameUIBridge.jslib
// JavaScript plugin for Unity WebGL to communicate with HTML UI overlay

mergeInto(LibraryManager.library, {
    ShowEndGameCTA: function(score, minScore) {
        if (window.GameUI && window.GameUI.showEndGameCTA) {
            window.GameUI.showEndGameCTA(score, minScore);
        } else {
            console.warn('[GameUIBridge] GameUI.showEndGameCTA not available');
        }
    },

    HideEndGameCTA: function() {
        if (window.GameUI && window.GameUI.hideEndGameCTA) {
            window.GameUI.hideEndGameCTA();
        } else {
            console.warn('[GameUIBridge] GameUI.hideEndGameCTA not available');
        }
    },

    ShowSteamButton: function(show) {
        if (window.GameUI && window.GameUI.showSteamButton) {
            window.GameUI.showSteamButton(show);
        } else {
            console.warn('[GameUIBridge] GameUI.showSteamButton not available');
        }
    },
    
    RegisterVisibilityChangeCallback: function() {
        // This works on all browsers including iOS Safari
        document.addEventListener('visibilitychange', function() {
            if (document.hidden) {
                // Page is hidden (tab switched, minimized, etc.)
                console.log('[GameUIBridge] Page hidden - triggering focus lost');
                if (window.unityInstance) {
                    window.unityInstance.SendMessage('[ForceLandscapeAspect]', 'OnVisibilityLost');
                }
            } else {
                // Page is visible again
                console.log('[GameUIBridge] Page visible - triggering focus regained');
                if (window.unityInstance) {
                    window.unityInstance.SendMessage('[ForceLandscapeAspect]', 'OnVisibilityRegained');
                }
            }
        });
        
        // Also handle page blur/focus for additional coverage
        window.addEventListener('blur', function() {
            console.log('[GameUIBridge] Window blur - triggering focus lost');
            if (window.unityInstance) {
                window.unityInstance.SendMessage('[ForceLandscapeAspect]', 'OnVisibilityLost');
            }
        });
        
        window.addEventListener('focus', function() {
            console.log('[GameUIBridge] Window focus - triggering focus regained');
            if (window.unityInstance) {
                window.unityInstance.SendMessage('[ForceLandscapeAspect]', 'OnVisibilityRegained');
            }
        });
        
        // iOS Safari specific: handle page show/hide events
        window.addEventListener('pagehide', function() {
            console.log('[GameUIBridge] Page hide (iOS) - triggering focus lost');
            if (window.unityInstance) {
                window.unityInstance.SendMessage('[ForceLandscapeAspect]', 'OnVisibilityLost');
            }
        });
        
        window.addEventListener('pageshow', function(event) {
            // Only trigger on back-forward cache restore
            if (event.persisted) {
                console.log('[GameUIBridge] Page show from bfcache (iOS) - triggering focus regained');
                if (window.unityInstance) {
                    window.unityInstance.SendMessage('[ForceLandscapeAspect]', 'OnVisibilityRegained');
                }
            }
        });
        
        console.log('[GameUIBridge] Visibility change callbacks registered');
    }
});
