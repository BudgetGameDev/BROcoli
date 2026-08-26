using System.Collections;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the GitHub call-to-action alongside the in-scene game-over overlay.
/// WebGL uses the HTML template; other platforms get a compact Unity fallback.
/// </summary>
public sealed class GameOverCTAManager : MonoBehaviour
{
    private const string GitHubRepoUrl = "https://github.com/BudgetGameDev/BROcoli";
    private const int MinScoreToShow = 0;
    private static GameOverCTAManager instance;
    private GameObject editorFallbackUI;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ShowGameOverCTA(int score, int minScore);

    [DllImport("__Internal")]
    private static extern void HideGameOverCTA();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    public static void ShowForGameOverOverlay()
    {
        if (instance == null)
        {
            GameObject managerObject = new GameObject("GameOverCTAManager");
            instance = managerObject.AddComponent<GameOverCTAManager>();
            DontDestroyOnLoad(managerObject);
        }

        instance.StartCoroutine(instance.ShowAfterOverlayLayout());
    }

    public static void HideForGameOverOverlay()
    {
        if (instance != null)
            instance.HideCTA();
    }

    private IEnumerator ShowAfterOverlayLayout()
    {
        yield return null;
        int score = PlayerPrefs.GetInt("LastScore", 0);
        if (score < MinScoreToShow)
            yield break;

#if UNITY_WEBGL && !UNITY_EDITOR
        ShowGameOverCTA(score, MinScoreToShow);
#else
        CreateFallbackUI();
#endif
    }

    private void HideCTA()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        HideGameOverCTA();
#else
        if (editorFallbackUI != null)
            Destroy(editorFallbackUI);
        editorFallbackUI = null;
#endif
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    private void CreateFallbackUI()
    {
        if (editorFallbackUI != null)
            Destroy(editorFallbackUI);

        Canvas canvas = ScreenCanvasLocator.Find();
        if (canvas == null)
        {
            Debug.LogWarning("[GameOverCTA] No canvas found for fallback UI.");
            return;
        }

        editorFallbackUI = new GameObject(
            "GameOverCTAFallback",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(Outline)
        );
        editorFallbackUI.layer = canvas.gameObject.layer;
        editorFallbackUI.transform.SetParent(canvas.transform, false);

        RectTransform rect = editorFallbackUI.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.85f);
        rect.anchorMax = new Vector2(0.5f, 0.85f);
        rect.sizeDelta = new Vector2(250f, 48f);

        Image background = editorFallbackUI.GetComponent<Image>();
        background.color = new Color32(33, 38, 45, 255);
        Button button = editorFallbackUI.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(OpenGitHub);

        Outline outline = editorFallbackUI.GetComponent<Outline>();
        outline.effectColor = new Color32(240, 246, 252, 45);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        labelObject.layer = editorFallbackUI.layer;
        labelObject.transform.SetParent(editorFallbackUI.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "★  STAR ON GITHUB";
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color32(201, 209, 217, 255);
        label.raycastTarget = false;
    }

    private void OpenGitHub()
    {
        Application.OpenURL(GitHubRepoUrl);
    }
#endif
}
