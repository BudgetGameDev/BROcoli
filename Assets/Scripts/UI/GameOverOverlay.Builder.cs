using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class GameOverOverlay
{
    private static GameOverOverlay CreateOverlay()
    {
        Canvas canvas = ScreenCanvasLocator.Find();
        if (canvas == null)
            canvas = CreateCanvas();

        GameObject overlayObject = new GameObject(
            "GameOverOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        overlayObject.layer = canvas.gameObject.layer;
        overlayObject.transform.SetParent(canvas.transform, false);

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image backdrop = overlayObject.GetComponent<Image>();
        backdrop.color = new Color(0.015f, 0.02f, 0.035f, 0.94f);
        backdrop.raycastTarget = true;

        GameOverOverlay overlay = overlayObject.AddComponent<GameOverOverlay>();
        overlay.BuildInterface();
        return overlay;
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "GameOverCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private void BuildInterface()
    {
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.layer = gameObject.layer;
        content.transform.SetParent(transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(760f, 560f);

        CreateText(
            content.transform,
            "GameOverTitle",
            "GAME OVER",
            new Vector2(0f, 175f),
            new Vector2(720f, 110f),
            76f,
            new Color(1f, 0.24f, 0.28f),
            FontStyles.Bold);

        statsText = CreateText(
            content.transform,
            "RunStats",
            string.Empty,
            new Vector2(0f, 55f),
            new Vector2(700f, 120f),
            34f,
            Color.white,
            FontStyles.Bold);

        CreateText(
            content.transform,
            "RestartHint",
            "The outbreak continues. Ready for another run?",
            new Vector2(0f, -35f),
            new Vector2(700f, 70f),
            24f,
            new Color(0.76f, 0.8f, 0.88f),
            FontStyles.Normal);

        GameObject buttons = new GameObject(
            "Buttons",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup));
        buttons.layer = gameObject.layer;
        buttons.transform.SetParent(content.transform, false);
        RectTransform buttonsRect = buttons.GetComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonsRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonsRect.anchoredPosition = new Vector2(0f, -145f);
        buttonsRect.sizeDelta = new Vector2(650f, 76f);

        HorizontalLayoutGroup layout = buttons.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 34f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        restartButton = CreateButton(
            buttons.transform,
            "RestartButton",
            "RESTART",
            new Color(0.12f, 0.55f, 0.25f));
        mainMenuButton = CreateButton(
            buttons.transform,
            "MainMenuButton",
            "MAIN MENU",
            new Color(0.24f, 0.3f, 0.42f));

        restartButton.onClick.AddListener(RestartGame);
        mainMenuButton.onClick.AddListener(GoToMainMenu);
        menuButtons = new[] { restartButton, mainMenuButton };
        ConfigureNavigation();
    }

    private TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        FontStyles style)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(18f, fontSize * 0.62f);
        text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.fontStyle = style;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Color backgroundColor)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(Outline));
        buttonObject.layer = gameObject.layer;
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<RectTransform>().sizeDelta = new Vector2(285f, 72f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.22f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(backgroundColor, Color.black, 0.25f);
        button.colors = colors;

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 0.9f, 0.28f, 0.95f);
        outline.effectDistance = new Vector2(5f, 5f);
        outline.enabled = false;

        CreateText(
            buttonObject.transform,
            "Label",
            label,
            Vector2.zero,
            new Vector2(265f, 62f),
            28f,
            Color.white,
            FontStyles.Bold);
        return button;
    }

    private void ConfigureNavigation()
    {
        buttonOutlines = new Outline[menuButtons.Length];
        originalButtonScales = new Vector3[menuButtons.Length];
        for (int i = 0; i < menuButtons.Length; i++)
        {
            Button button = menuButtons[i];
            buttonOutlines[i] = button.GetComponent<Outline>();
            originalButtonScales[i] = button.transform.localScale;

            int index = i;
            EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry enter = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            enter.callback.AddListener(_ => SelectButton(index));
            trigger.triggers.Add(enter);
        }

        Navigation restartNavigation = restartButton.navigation;
        restartNavigation.mode = Navigation.Mode.Explicit;
        restartNavigation.selectOnRight = mainMenuButton;
        restartNavigation.selectOnDown = mainMenuButton;
        restartButton.navigation = restartNavigation;

        Navigation menuNavigation = mainMenuButton.navigation;
        menuNavigation.mode = Navigation.Mode.Explicit;
        menuNavigation.selectOnLeft = restartButton;
        menuNavigation.selectOnUp = restartButton;
        mainMenuButton.navigation = menuNavigation;
    }
}
