using System.Runtime.InteropServices;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages CTA (Call-to-Action) UI on the EndGame scene using HTML/CSS overlay.
/// Uses JavaScript bridge to communicate with the HTML UI layer in the WebGL template.
/// This approach gives us proper web styling (GitHub button with SVG, etc) that Unity UI cannot achieve.
/// Falls back to simple Unity UI in Editor for testing.
/// </summary>
public class EndGameCTAManager : MonoBehaviour
{
    private static EndGameCTAManager instance;
    
    [Header("Score Settings")]
    private int minScoreToShowCTA = 0; // Score threshold to show CTA
    
    private const string GITHUB_REPO_URL = "https://github.com/BudgetGameDev/BROcoli";
    private const string GITHUB_API_URL = "https://api.github.com/repos/BudgetGameDev/BROcoli";
    
    // Cached star count
    private static int? cachedStarCount = null;
    private static bool isFetchingStars = false;
    private TextMeshProUGUI starCountTextRef;

#if UNITY_WEBGL && !UNITY_EDITOR
    // JavaScript bridge functions - these call into the HTML/JS layer
    [DllImport("__Internal")]
    private static extern void ShowEndGameCTA(int score, int minScore);
    
    [DllImport("__Internal")]
    private static extern void HideEndGameCTA();
    
    [DllImport("__Internal")]
    private static extern void ShowSteamButton(bool show);
#else
    // Stub implementations for Editor and non-WebGL builds
    private static void ShowEndGameCTA(int score, int minScore)
    {
        Debug.Log($"[EndGameCTA] Would show CTA - Score: {score}, MinScore: {minScore}");
    }
    
    private static void HideEndGameCTA()
    {
        Debug.Log("[EndGameCTA] Would hide CTA");
    }
    
    private static void ShowSteamButton(bool show)
    {
        Debug.Log($"[EndGameCTA] Would set Steam button visible: {show}");
    }
#endif

    // Editor fallback UI
    private GameObject editorFallbackUI;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Show CTA on EndGame scene
        if (scene.name == "EndGame")
        {
            if (instance == null)
            {
                GameObject managerObj = new GameObject("EndGameCTAManager");
                instance = managerObj.AddComponent<EndGameCTAManager>();
                DontDestroyOnLoad(managerObj);
            }
            
            instance.ShowCTA();
        }
        else
        {
            // Hide CTA when leaving EndGame scene
            if (instance != null)
            {
                instance.HideCTA();
            }
        }
    }

    void ShowCTA()
    {
        int playerScore = PlayerPrefs.GetInt("LastScore", 0);
        Debug.Log($"[EndGameCTA] Showing CTA - Score: {playerScore}, Min: {minScoreToShowCTA}");
        
        if (playerScore < minScoreToShowCTA)
        {
            Debug.Log("[EndGameCTA] Score below minimum, not showing CTA");
            return;
        }
        
#if UNITY_WEBGL && !UNITY_EDITOR
        // In WebGL build, use HTML overlay
        ShowEndGameCTA(playerScore, minScoreToShowCTA);
#else
        // In Editor or non-WebGL, show Unity UI fallback
        CreateEditorFallbackUI();
#endif
    }

    void HideCTA()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        HideEndGameCTA();
#else
        if (editorFallbackUI != null)
        {
            Destroy(editorFallbackUI);
            editorFallbackUI = null;
        }
#endif
    }

    void CreateEditorFallbackUI()
    {
        // Clean up any existing fallback UI
        if (editorFallbackUI != null)
        {
            Destroy(editorFallbackUI);
        }
        
        // Find the canvas
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[EndGameCTA] No Canvas found!");
            return;
        }
        
        // Create container
        editorFallbackUI = new GameObject("CTAFallbackUI");
        editorFallbackUI.transform.SetParent(canvas.transform, false);
        
        RectTransform rect = editorFallbackUI.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.85f);
        rect.anchorMax = new Vector2(0.5f, 0.85f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(260, 50);
        
        // Horizontal layout for button group (like GitHub's Star + Count)
        HorizontalLayoutGroup layout = editorFallbackUI.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 0;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        
        // Colors matching GitHub dark theme exactly
        Color32 btnBg = new Color32(33, 38, 45, 255);       // #21262d
        Color32 btnHover = new Color32(48, 54, 61, 255);    // #30363d
        Color32 btnPressed = new Color32(40, 46, 52, 255); // #282e33
        Color32 textColor = new Color32(201, 209, 217, 255); // #c9d1d9
        Color32 borderColor = new Color32(240, 246, 252, 26); // rgba(240,246,252,0.1)
        
        // === Star Button (left side) ===
        GameObject starBtnObj = new GameObject("StarButton");
        starBtnObj.transform.SetParent(editorFallbackUI.transform, false);
        
        RectTransform starRect = starBtnObj.AddComponent<RectTransform>();
        starRect.sizeDelta = new Vector2(115, 42);
        
        Image starBg = starBtnObj.AddComponent<Image>();
        starBg.color = btnBg;
        
        // Add outline for border effect
        Outline starOutline = starBtnObj.AddComponent<Outline>();
        starOutline.effectColor = borderColor;
        starOutline.effectDistance = new Vector2(1, -1);
        
        Button starBtn = starBtnObj.AddComponent<Button>();
        ColorBlock starColors = starBtn.colors;
        starColors.normalColor = btnBg;
        starColors.highlightedColor = btnHover;
        starColors.pressedColor = btnPressed;
        starColors.selectedColor = btnBg;
        starColors.fadeDuration = 0.1f;
        starBtn.colors = starColors;
        starBtn.targetGraphic = starBg;
        starBtn.onClick.AddListener(OnGitHubClicked);
        
        // Star button content container
        GameObject starContentObj = new GameObject("Content");
        starContentObj.transform.SetParent(starBtnObj.transform, false);
        RectTransform starContentRect = starContentObj.AddComponent<RectTransform>();
        starContentRect.anchorMin = Vector2.zero;
        starContentRect.anchorMax = Vector2.one;
        starContentRect.offsetMin = Vector2.zero;
        starContentRect.offsetMax = Vector2.zero;
        
        HorizontalLayoutGroup contentLayout = starContentObj.AddComponent<HorizontalLayoutGroup>();
        contentLayout.spacing = 6;
        contentLayout.childAlignment = TextAnchor.MiddleCenter;
        contentLayout.childControlWidth = false;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = false;
        contentLayout.childForceExpandHeight = false;
        contentLayout.padding = new RectOffset(12, 12, 0, 0);
        
        // Star icon
        GameObject starIconObj = new GameObject("StarIcon");
        starIconObj.transform.SetParent(starContentObj.transform, false);
        RectTransform starIconRect = starIconObj.AddComponent<RectTransform>();
        starIconRect.sizeDelta = new Vector2(20, 20);
        
        Image starIcon = starIconObj.AddComponent<Image>();
        starIcon.sprite = CreateStarSprite(20, new Color32(139, 148, 158, 255)); // #8b949e - GitHub icon gray
        
        // Star button text
        GameObject starTextObj = new GameObject("Label");
        starTextObj.transform.SetParent(starContentObj.transform, false);
        RectTransform starTextRect = starTextObj.AddComponent<RectTransform>();
        starTextRect.sizeDelta = new Vector2(45, 42);
        
        TextMeshProUGUI starText = starTextObj.AddComponent<TextMeshProUGUI>();
        starText.text = "Star";
        starText.fontSize = 18;
        starText.alignment = TextAlignmentOptions.MidlineLeft;
        starText.color = textColor;
        
        // === Count Badge (right side) ===
        GameObject countObj = new GameObject("CountBadge");
        countObj.transform.SetParent(editorFallbackUI.transform, false);
        
        RectTransform countRect = countObj.AddComponent<RectTransform>();
        countRect.sizeDelta = new Vector2(55, 42);
        
        Image countBg = countObj.AddComponent<Image>();
        countBg.color = btnBg;
        
        Outline countOutline = countObj.AddComponent<Outline>();
        countOutline.effectColor = borderColor;
        countOutline.effectDistance = new Vector2(1, -1);
        
        Button countBtn = countObj.AddComponent<Button>();
        ColorBlock countColors = countBtn.colors;
        countColors.normalColor = btnBg;
        countColors.highlightedColor = btnHover;
        countColors.pressedColor = btnPressed;
        countColors.selectedColor = btnBg;
        countColors.fadeDuration = 0.1f;
        countBtn.colors = countColors;
        countBtn.targetGraphic = countBg;
        countBtn.onClick.AddListener(OnGitHubClicked);
        
        // Count text
        GameObject countTextObj = new GameObject("Count");
        countTextObj.transform.SetParent(countObj.transform, false);
        RectTransform countTextRect = countTextObj.AddComponent<RectTransform>();
        countTextRect.anchorMin = Vector2.zero;
        countTextRect.anchorMax = Vector2.one;
        countTextRect.offsetMin = Vector2.zero;
        countTextRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI countText = countTextObj.AddComponent<TextMeshProUGUI>();
        countText.text = cachedStarCount.HasValue ? cachedStarCount.Value.ToString() : "--";
        countText.fontSize = 18;
        countText.fontStyle = FontStyles.Bold;
        countText.alignment = TextAlignmentOptions.Center;
        countText.color = textColor;
        
        // Store reference for updating after fetch
        starCountTextRef = countText;
        
        // Fetch star count if not cached
        if (!cachedStarCount.HasValue && !isFetchingStars)
        {
            StartCoroutine(FetchGitHubStarCount());
        }
        
        Debug.Log("[EndGameCTA] Editor fallback UI created");
    }
    
    IEnumerator FetchGitHubStarCount()
    {
        isFetchingStars = true;
        Debug.Log("[EndGameCTA] Fetching GitHub star count...");
        
        using (UnityWebRequest request = UnityWebRequest.Get(GITHUB_API_URL))
        {
            // GitHub API requires a User-Agent header
            request.SetRequestHeader("User-Agent", "Unity-BROcoli-Game");
            request.timeout = 10;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    // Parse JSON response to get stargazers_count
                    string json = request.downloadHandler.text;
                    // Simple parsing - look for "stargazers_count":NUMBER
                    int startIndex = json.IndexOf("\"stargazers_count\":");
                    if (startIndex >= 0)
                    {
                        startIndex += "\"stargazers_count\":".Length;
                        int endIndex = json.IndexOfAny(new char[] { ',', '}' }, startIndex);
                        if (endIndex > startIndex)
                        {
                            string countStr = json.Substring(startIndex, endIndex - startIndex).Trim();
                            if (int.TryParse(countStr, out int starCount))
                            {
                                cachedStarCount = starCount;
                                Debug.Log($"[EndGameCTA] GitHub stars: {starCount}");
                                
                                // Update the text if it still exists
                                if (starCountTextRef != null)
                                {
                                    starCountTextRef.text = starCount.ToString();
                                }
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[EndGameCTA] Failed to parse GitHub API response: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[EndGameCTA] Failed to fetch GitHub stars: {request.error}");
            }
        }
        
        isFetchingStars = false;
    }

    void OnGitHubClicked()
    {
        Debug.Log($"[EndGameCTA] Opening GitHub: {GITHUB_REPO_URL}");
        Application.OpenURL(GITHUB_REPO_URL);
    }

    Sprite CreateStarSprite(int size, Color32 color)
    {
        // Create a texture with a 5-pointed star
        int texSize = size * 4; // Higher res for quality
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        
        Color32 transparent = new Color32(0, 0, 0, 0);
        Color32[] pixels = new Color32[texSize * texSize];
        
        // Fill with transparent
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = transparent;
        
        // Draw 5-pointed star
        float cx = texSize / 2f;
        float cy = texSize / 2f;
        float outerRadius = texSize * 0.45f;
        float innerRadius = texSize * 0.18f;
        
        // Calculate star points (5 outer, 5 inner)
        Vector2[] points = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            // Start from top, go clockwise
            float angle = -Mathf.PI / 2f + i * Mathf.PI / 5f;
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            points[i] = new Vector2(
                cx + Mathf.Cos(angle) * radius,
                cy - Mathf.Sin(angle) * radius  // Flip Y for texture coords
            );
        }
        
        // Fill the star polygon
        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                if (IsPointInStar(x, y, points, cx, cy))
                {
                    pixels[y * texSize + x] = color;
                }
            }
        }
        
        tex.SetPixels32(pixels);
        tex.Apply();
        
        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), texSize);
    }
    
    bool IsPointInStar(float px, float py, Vector2[] points, float cx, float cy)
    {
        // Check if point is inside the star polygon using ray casting
        int n = points.Length;
        bool inside = false;
        
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = points[i].x, yi = points[i].y;
            float xj = points[j].x, yj = points[j].y;
            
            if (((yi > py) != (yj > py)) &&
                (px < (xj - xi) * (py - yi) / (yj - yi) + xi))
            {
                inside = !inside;
            }
        }
        
        return inside;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
