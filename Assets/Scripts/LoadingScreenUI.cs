using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates and manages a loading screen overlay with progress bar.
/// Used by GamePreloader to hide game graphics during warmup.
/// </summary>
public class LoadingScreenUI
{
    private Canvas _canvas;
    private Image _progressBarFill;
    private TextMeshProUGUI _loadingLabel;
    private float _progress = 0f;
    
    public LoadingScreenUI(Transform parent, Color backgroundColor, Color barBgColor, Color barFillColor, string initialText)
    {
        CreateUI(parent, backgroundColor, barBgColor, barFillColor, initialText);
    }
    
    private void CreateUI(Transform parent, Color bgColor, Color barBgColor, Color barFillColor, string text)
    {
        // Canvas that covers everything
        GameObject canvasGO = new GameObject("LoadingScreen");
        canvasGO.transform.SetParent(parent);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bg = bgGO.AddComponent<Image>();
        bg.color = bgColor;
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        // Loading text
        GameObject textGO = new GameObject("LoadingText");
        textGO.transform.SetParent(canvasGO.transform, false);
        _loadingLabel = textGO.AddComponent<TextMeshProUGUI>();
        _loadingLabel.text = text;
        _loadingLabel.fontSize = 48;
        _loadingLabel.alignment = TextAlignmentOptions.Center;
        _loadingLabel.color = Color.white;
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0, 60);
        textRect.sizeDelta = new Vector2(600, 80);
        
        // Progress bar background
        GameObject barBgGO = new GameObject("ProgressBarBg");
        barBgGO.transform.SetParent(canvasGO.transform, false);
        Image barBg = barBgGO.AddComponent<Image>();
        barBg.color = barBgColor;
        RectTransform barBgRect = barBgGO.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0.5f, 0.5f);
        barBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        barBgRect.anchoredPosition = new Vector2(0, -20);
        barBgRect.sizeDelta = new Vector2(400, 24);
        
        // Progress bar fill
        GameObject barFillGO = new GameObject("ProgressBarFill");
        barFillGO.transform.SetParent(barBgGO.transform, false);
        _progressBarFill = barFillGO.AddComponent<Image>();
        _progressBarFill.color = barFillColor;
        RectTransform fillRect = barFillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.offsetMin = new Vector2(4, 4);
        fillRect.offsetMax = new Vector2(0, -4);
        
        SetProgress(0f);
    }
    
    public void SetProgress(float progress)
    {
        _progress = Mathf.Clamp01(progress);
        if (_progressBarFill != null)
        {
            _progressBarFill.rectTransform.anchorMax = new Vector2(_progress, 1);
        }
    }
    
    public void SetText(string text)
    {
        if (_loadingLabel != null)
            _loadingLabel.text = text;
    }
    
    public void Destroy()
    {
        if (_canvas != null)
            Object.Destroy(_canvas.gameObject);
    }
}
